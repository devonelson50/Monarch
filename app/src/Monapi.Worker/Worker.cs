using Monapi.Worker;
using System.Diagnostics;
using Monapi.Worker.Jira;
using Monapi.Worker.Slack;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Monapi.Worker.Kafka;

namespace Monapi.Worker;
/// <summary>
/// All team members
///  
/// Primary loop for the service worker. For prototyping purposes, it will
/// refresh simulated New Relic data from NewRelicSimulator every 30 seconds
/// </summary>

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private JiraManager? _jiraManager;
    private SlackWebhookService? _slackService;
    private Dictionary<string, string> _previousStatuses = new Dictionary<string, string>();
    private KafkaConnector? kfc;
    private string? _monarchConnStr;
    private string? _monapiConnStr;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize database connections
        var monapiPassword = File.ReadAllText("/run/secrets/monarch_sql_monapi_password").Trim();
        var monarchPassword = File.ReadAllText("/run/secrets/monarch_sql_monarch_password").Trim();
        _monarchConnStr = $"Server=sqlserver,1433;Database=monarch;User Id=monarch;Password={monarchPassword};TrustServerCertificate=False;";
        _monapiConnStr = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={monapiPassword};TrustServerCertificate=False;";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "./simulator.sh",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var simulatorProcess = Process.Start(psi);
        _logger.LogInformation(">>> Simulator started from C# with PID: {pid}", simulatorProcess?.Id);

        // Initialize integrations
        InitializeSlackIntegration();
        InitializeJiraIntegration();

        Nagios.NagiosConnector nc = new Nagios.NagiosConnector();
        NewRelic.NewRelicConnector nrc = new NewRelic.NewRelicConnector();
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("kafka_server")))
        {
            this.kfc = new Kafka.KafkaConnector();
        }
        
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

            // Check for status changes and trigger Jira tickets / Slack notifications
            await MonitorStatusChanges();

            await Task.Delay(30000, stoppingToken);
        }

        simulatorProcess?.Kill();
    }

    /// <summary>
    /// Initialize Slack webhook integration from Docker secret
    /// </summary>
    private void InitializeSlackIntegration()
    {
        try
        {
            _slackService = new SlackWebhookService("/run/secrets/monarch_slack_webhooks");
            Console.WriteLine("✅ Slack integration initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Slack integration disabled: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize Jira integration from Docker secret
    /// </summary>
    private void InitializeJiraIntegration()
    {
        try
        {
            var jiraApiKey = File.ReadAllText("/run/secrets/monarch_jira_api_key").Trim();
            _jiraManager = new JiraManager(jiraApiKey, _monarchConnStr!, _monapiConnStr!);
            Console.WriteLine("✅ Jira integration initialized (workspaces loaded from database)");
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
    /// Map numeric status code to display name
    /// </summary>
    private static string StatusName(int status) => status switch
    {
        0 => "Operational",
        1 => "Degraded",
        2 => "Down",
        _ => "Unknown"
    };

    /// <summary>
    /// Determines if a status transition represents worsening conditions
    /// </summary>
    private static bool IsStatusWorsening(string oldStatus, string newStatus)
    {
        var priority = new Dictionary<string, int>
        {
            { "Operational", 0 },
            { "Degraded", 1 },
            { "Down", 2 },
            { "Unknown", -1 }
        };

        int oldP = priority.GetValueOrDefault(oldStatus, -1);
        int newP = priority.GetValueOrDefault(newStatus, -1);

        // Any transition to Degraded or Down from a lower severity is worsening
        return newP > 0 && newP > oldP;
    }

    /// <summary>
    /// Map status name back to numeric string for Kafka
    /// </summary>
    private static string StatusToNumeric(string statusName) => statusName switch
    {
        "Operational" => "0",
        "Degraded" => "1",
        "Down" => "2",
        _ => "3"
    };

    /// <summary>
    /// Monitor Monarch-registered application status changes.
    /// Creates Jira tickets and sends Slack notifications when status worsens.
    /// </summary>
    private async Task MonitorStatusChanges()
    {
        if (_monarchConnStr == null || _monapiConnStr == null) return;

        try
        {
            // Step 1: Get all Monarch-registered apps with their alert flags
            var apps = new List<(int AppId, string AppName, int? NewRelicId, int? NagiosId, bool JiraAlert, bool SlackAlert)>();

            using (var conn = new SqlConnection(_monarchConnStr))
            {
                await conn.OpenAsync();
                var query = "SELECT appId, appName, newRelicId, nagiosId, jiraAlert, slackAlert FROM apps";
                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    apps.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2),
                        reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                        reader.IsDBNull(4) ? false : reader.GetBoolean(4),
                        reader.IsDBNull(5) ? false : reader.GetBoolean(5)
                    ));
                }
            }

            // Step 2: For each app, get current status and metrics from monapi
            foreach (var app in apps)
            {
                int nrStatus = -1, nStatus = -1;
                string metricDetails = "";

                using (var conn = new SqlConnection(_monapiConnStr))
                {
                    await conn.OpenAsync();

                    if (app.NewRelicId.HasValue)
                    {
                        var nrQuery = "SELECT status, cpuUsage, latency, throughput, output FROM newRelicApps WHERE appId = @id";
                        using var nrCmd = new SqlCommand(nrQuery, conn);
                        nrCmd.Parameters.AddWithValue("@id", app.NewRelicId.Value);
                        using var nrReader = await nrCmd.ExecuteReaderAsync();
                        if (await nrReader.ReadAsync())
                        {
                            nrStatus = nrReader.GetInt32(0);
                            var cpu = nrReader.IsDBNull(1) ? 0.0 : Convert.ToDouble(nrReader[1]);
                            var latency = nrReader.IsDBNull(2) ? 0 : nrReader.GetInt32(2);
                            var throughput = nrReader.IsDBNull(3) ? 0 : nrReader.GetInt32(3);
                            var output = nrReader.IsDBNull(4) ? "" : nrReader.GetString(4);

                            if (nrStatus > 0)
                                metricDetails += $"[New Relic] CPU: {cpu:F1}%, Latency: {latency}ms, Throughput: {throughput}, Message: {output}";
                        }
                    }

                    if (app.NagiosId.HasValue)
                    {
                        var nQuery = "SELECT currentState, output, perfData FROM nagiosApps WHERE hostObjectId = @id";
                        using var nCmd = new SqlCommand(nQuery, conn);
                        nCmd.Parameters.AddWithValue("@id", app.NagiosId.Value);
                        using var nReader = await nCmd.ExecuteReaderAsync();
                        if (await nReader.ReadAsync())
                        {
                            nStatus = nReader.GetInt32(0);
                            var output = nReader.IsDBNull(1) ? "" : nReader.GetString(1);

                            if (nStatus > 0)
                            {
                                if (metricDetails.Length > 0) metricDetails += " | ";
                                metricDetails += $"[Nagios] State: {nStatus}, Message: {output}";
                            }
                        }
                    }
                }

                // Calculate worst-case status
                int calcStatus;
                if (nrStatus >= 0 && nStatus >= 0)
                    calcStatus = Math.Max(nrStatus, nStatus);
                else if (nrStatus >= 0)
                    calcStatus = nrStatus;
                else if (nStatus >= 0)
                    calcStatus = nStatus;
                else
                    calcStatus = 3; // Unknown

                var currentStatusName = StatusName(calcStatus);
                var appKey = app.AppId.ToString();

                if (_previousStatuses.TryGetValue(appKey, out var prevStatusName))
                {
                    if (prevStatusName != currentStatusName)
                    {
                        var changeTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                        Console.WriteLine($"📊 Status change: {app.AppName} ({prevStatusName} → {currentStatusName})");

                        bool isWorsening = IsStatusWorsening(prevStatusName, currentStatusName);

                        if (isWorsening)
                        {
                            // Create Jira ticket if enabled
                            if (app.JiraAlert && _jiraManager != null)
                            {
                                await _jiraManager.HandleStatusChange(
                                    app.AppId, app.AppName, currentStatusName, metricDetails);
                            }

                            // Send Slack notifications if enabled
                            if (app.SlackAlert)
                            {
                                await SendSlackNotifications(app.AppId, app.AppName,
                                    prevStatusName, currentStatusName, changeTime, metricDetails);
                            }
                        }
                        else if (currentStatusName == "Operational")
                        {
                            // Handle recovery — close open incidents
                            if (_jiraManager != null)
                            {
                                await _jiraManager.HandleRecovery(app.AppId, app.AppName);
                            }
                        }

                        kfc?.WriteMessage(app.AppName, StatusToNumeric(currentStatusName), StatusToNumeric(prevStatusName));
                    }
                }

                _previousStatuses[appKey] = currentStatusName;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error monitoring status changes: {ex.Message}");
        }
    }

    /// <summary>
    /// Send Slack notifications to all channels associated with a Monarch app
    /// </summary>
    private async Task SendSlackNotifications(int appId, string appName, string oldStatus, string newStatus, string changeTime, string metricDetails)
    {
        if (_slackService == null || _monarchConnStr == null) return;

        try
        {
            // Look up associated Slack channels
            var channels = new List<string>();
            using (var conn = new SqlConnection(_monarchConnStr))
            {
                await conn.OpenAsync();
                var query = "SELECT channelKey FROM appSlackChannels WHERE appId = @appId";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@appId", appId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    channels.Add(reader.GetString(0));
                }
            }

            if (channels.Count == 0) return;

            var statusIcon = newStatus switch
            {
                "Down" => "🔴",
                "Degraded" => "🟡",
                _ => "⚪"
            };

            var message = $"{statusIcon} *{appName}* status changed: {oldStatus} → {newStatus}\n" +
                          $"*Time:* {changeTime} UTC\n" +
                          $"*Metrics:* {(string.IsNullOrEmpty(metricDetails) ? "N/A" : metricDetails)}";

            foreach (var channelKey in channels)
            {
                try
                {
                    await _slackService.SendMessageAsync(message, channelKey);
                    Console.WriteLine($"Sent Slack notification to channel '{channelKey}' for {appName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send Slack notification to '{channelKey}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending Slack notifications for {appName}: {ex.Message}");
        }
    }
}
