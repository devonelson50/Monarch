using Monapi.Worker;
using System.Diagnostics;
using Monapi.Worker.Jira;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Monapi.Worker.Kafka;

namespace Monapi.Worker;
/// <summary>
/// Devon Nelson 
/// Primary loop for the service worker. For prototyping purposes, it will
/// refresh simulated New Relic data from NewRelicSimulator every 30 seconds
/// </summary>

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private JiraManager? _jiraManager;
    private Dictionary<string, string> _previousStatuses = new Dictionary<string, string>();
    private KafkaConnector kfc;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "./simulator.sh",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var simulatorProcess = Process.Start(psi);
        _logger.LogInformation(">>> Simulator started from C# with PID: {pid}", simulatorProcess?.Id);
        // Initialize Jira integration
        await InitializeJiraIntegration();

        Nagios.NagiosConnector nc = new Nagios.NagiosConnector();
        NewRelic.NewRelicConnector nrc = new NewRelic.NewRelicConnector();
        this.kfc = new Kafka.KafkaConnector();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("--- Starting Sync Cycle: {time} ---", DateTime.Now);

                Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing NewRelic data");
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing Nagios data");
                await nc.RunConnector();
                await nrc.RunConnector();
            }
            catch (Exception ex)
            {
                _logger.LogCritical("!!! WORKER CRASHED DURING SYNC: {type} - {msg}", ex.GetType().Name, ex.Message);
                _logger.LogDebug(ex.StackTrace);
            }
            
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing NewRelic data");

            // Check for status changes and trigger Jira tickets
            await MonitorStatusChanges();

            await Task.Delay(30000, stoppingToken);
        }

        simulatorProcess?.Kill();


    }

    /// <summary>
    /// Initialize Jira integration with configuration
    /// </summary>
    private async Task InitializeJiraIntegration()
    {
        try
        {
            var jiraConfig = _configuration.GetSection("Jira");
            var baseUrl = jiraConfig.GetValue<string>("BaseUrl");
            var projectKey = jiraConfig.GetValue<string>("ProjectKey");
            var issueType = jiraConfig.GetValue<string>("IssueType");

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(projectKey) || string.IsNullOrEmpty(issueType))
            {
                Console.WriteLine("⚠️  Jira configuration incomplete. Jira integration disabled.");
                Console.WriteLine("   To enable Jira: Configure Jira section in appsettings.json");
                return;
            }

            var jiraApiKey = File.ReadAllText("/run/secrets/monarch_jira_api_key").Trim();
            var sqlPassword = File.ReadAllText("/run/secrets/monarch_sql_monapi_password").Trim();

            var connector = new JiraConnector(baseUrl, projectKey, issueType, jiraApiKey);
            _jiraManager = new JiraManager(connector, sqlPassword);

            Console.WriteLine($"✅ Jira integration initialized: {baseUrl} (Project: {projectKey})");
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("⚠️  Jira API key not found. Jira integration disabled.");
            Console.WriteLine("   Create secret 'monarch_jira_api_key' with format: email:token");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to initialize Jira integration: {ex.Message}");
        }
    }

    /// <summary>
    /// Monitor application status changes and create Jira tickets when needed
    /// </summary>
    private async Task MonitorStatusChanges()
    {
        try
        {
            var sqlPassword = File.ReadAllText("/run/secrets/monarch_sql_monapi_password").Trim();
            var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={sqlPassword};TrustServerCertificate=False;";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT appId, appName, status FROM newRelicApps";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var appId = reader.GetInt32(0).ToString();
                        var appName = reader.GetString(1);
                        var currentStatus = reader.GetInt32(2).ToString();

                        // Check if status has changed
                        if (_previousStatuses.TryGetValue(appId, out var previousStatus))
                        {
                            if (previousStatus != currentStatus)
                            {
                                Console.WriteLine($"📊 Status change detected: {appName} ({previousStatus} → {currentStatus})");

                                // For now, always create Jira tickets (later we'll check jiraAlert flag)
                                await _jiraManager.HandleStatusChange(
                                    appId,
                                    appName,
                                    previousStatus,
                                    currentStatus,
                                    shouldCreateTicket: true
                                );


                                var currentStatusString = currentStatus switch
                                {
                                    "0" => "operational",
                                    "1" => "degraded",
                                    "2" => "down",
                                    _ => "unknown"
                                };
                                kfc.WriteMessage(appName,currentStatusString);
                            }
                        }

                        // Update previous status
                        _previousStatuses[appId] = currentStatus;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error monitoring status changes: {ex.Message}");
        }
    }
}
