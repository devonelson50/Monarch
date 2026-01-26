using Monapi.Worker;
using Monapi.Worker.Jira;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

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

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize Jira integration
        await InitializeJiraIntegration();

        NewRelic.NewRelicSimulator nrs = new NewRelic.NewRelicSimulator();

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing NewRelic data");
            nrs.RunLoop();

            // Check for status changes and trigger Jira tickets
            await MonitorStatusChanges();

            await Task.Delay(30000, stoppingToken);
        }
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
        if (_jiraManager == null)
        {
            return; // Jira integration not available
        }

        try
        {
            var sqlPassword = File.ReadAllText("/run/secrets/monarch_sql_monapi_password").Trim();
            var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={sqlPassword};TrustServerCertificate=True;";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT appId, appName, status FROM newRelicApps";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var appId = reader.GetString(0);
                        var appName = reader.GetString(1);
                        var currentStatus = reader.GetString(2);

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
