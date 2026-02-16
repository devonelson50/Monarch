using Microsoft.Data.SqlClient;
using Monarch.Models;
using Monarch.Jira;
using System.Text;
using System.Text.Json;

// App Admin Service for Monarch
// Manages CRUD operations for Monarch-managed applications and their
// integration mappings (New Relic, Nagios, Slack channels, Jira workspaces).
// Reads discovered monitoring apps from the monapi database (read-only)
// and reads/writes app configuration to the monarch database.

namespace Monarch.Services
{
    /// <summary>
    /// Service for managing application configurations from the Admin panel.
    /// Uses the monarch database user which has r/w on monarch and read-only on monapi.
    /// </summary>
    public class AppAdminService
    {
        private readonly string _monarchConnectionString;
        private readonly string _monapiConnectionString;
        private readonly HttpClient _httpClient;

        public AppAdminService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            var password = File.ReadAllText("/run/secrets/monarch_sql_monarch_password").Trim();
            _monarchConnectionString = $"Server=sqlserver,1433;Database=monarch;User Id=monarch;Password={password};TrustServerCertificate=True;";
            _monapiConnectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monarch;Password={password};TrustServerCertificate=True;";
        }

        // ===== Monarch App CRUD =====

        /// <summary>
        /// Retrieves all Monarch-managed applications from monarch.apps
        /// </summary>
        public async Task<List<MonarchApp>> GetAllAppsAsync()
        {
            var apps = new List<MonarchApp>();

            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT appId, appName, status, newRelicId, nagiosId, slackAlert, jiraAlert, smtpAlert FROM apps ORDER BY appName";
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        apps.Add(new MonarchApp
                        {
                            AppId = reader.GetInt32(0),
                            AppName = reader.GetString(1),
                            Status = reader.GetString(2),
                            NewRelicId = reader.IsDBNull(3) ? null : reader.GetString(3),
                            NagiosId = reader.IsDBNull(4) ? null : reader.GetString(4),
                            SlackAlert = reader.IsDBNull(5) ? false : reader.GetBoolean(5),
                            JiraAlert = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                            SmtpAlert = reader.IsDBNull(7) ? false : reader.GetBoolean(7)
                        });
                    }
                }
            }

            // Load slack channel and jira workspace selections for each app
            foreach (var app in apps)
            {
                app.SelectedSlackChannels = await GetAppSlackChannelsAsync(app.AppId);
                app.SelectedJiraWorkspaces = await GetAppJiraWorkspacesAsync(app.AppId);
            }

            return apps;
        }

        /// <summary>
        /// Retrieves a single Monarch app by its ID
        /// </summary>
        public async Task<MonarchApp?> GetAppByIdAsync(int appId)
        {
            MonarchApp? app = null;

            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT appId, appName, status, newRelicId, nagiosId, slackAlert, jiraAlert, smtpAlert FROM apps WHERE appId = @appId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appId", appId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            app = new MonarchApp
                            {
                                AppId = reader.GetInt32(0),
                                AppName = reader.GetString(1),
                                Status = reader.GetString(2),
                                NewRelicId = reader.IsDBNull(3) ? null : reader.GetString(3),
                                NagiosId = reader.IsDBNull(4) ? null : reader.GetString(4),
                                SlackAlert = reader.IsDBNull(5) ? false : reader.GetBoolean(5),
                                JiraAlert = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                                SmtpAlert = reader.IsDBNull(7) ? false : reader.GetBoolean(7)
                            };
                        }
                    }
                }
            }

            if (app != null)
            {
                app.SelectedSlackChannels = await GetAppSlackChannelsAsync(app.AppId);
                app.SelectedJiraWorkspaces = await GetAppJiraWorkspacesAsync(app.AppId);
            }

            return app;
        }

        /// <summary>
        /// Creates a new Monarch application and returns its generated ID
        /// </summary>
        public async Task<int> CreateAppAsync(string appName)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();
                var query = "INSERT INTO apps (appName, status) OUTPUT INSERTED.appId VALUES (@appName, @status)";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appName", appName);
                    command.Parameters.AddWithValue("@status", "Unknown");
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        /// <summary>
        /// Updates an existing app's integration configuration
        /// </summary>
        public async Task UpdateAppAsync(MonarchApp app)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();
                var query = @"UPDATE apps SET 
                    appName = @appName,
                    newRelicId = @newRelicId, 
                    nagiosId = @nagiosId, 
                    slackAlert = @slackAlert, 
                    jiraAlert = @jiraAlert, 
                    smtpAlert = @smtpAlert 
                    WHERE appId = @appId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appId", app.AppId);
                    command.Parameters.AddWithValue("@appName", app.AppName);
                    command.Parameters.AddWithValue("@newRelicId", (object?)app.NewRelicId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@nagiosId", (object?)app.NagiosId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@slackAlert", app.SlackAlert);
                    command.Parameters.AddWithValue("@jiraAlert", app.JiraAlert);
                    command.Parameters.AddWithValue("@smtpAlert", app.SmtpAlert);
                    await command.ExecuteNonQueryAsync();
                }
            }

            // Update many-to-many relationships
            await SaveAppSlackChannelsAsync(app.AppId, app.SelectedSlackChannels);
            await SaveAppJiraWorkspacesAsync(app.AppId, app.SelectedJiraWorkspaces);
        }

        /// <summary>
        /// Deletes a Monarch application by ID
        /// </summary>
        public async Task DeleteAppAsync(int appId)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();

                // Remove related mappings first
                using (var cmd = new SqlCommand("DELETE FROM appSlackChannels WHERE appId = @appId", connection))
                {
                    cmd.Parameters.AddWithValue("@appId", appId);
                    await cmd.ExecuteNonQueryAsync();
                }
                using (var cmd = new SqlCommand("DELETE FROM appJiraWorkspaces WHERE appId = @appId", connection))
                {
                    cmd.Parameters.AddWithValue("@appId", appId);
                    await cmd.ExecuteNonQueryAsync();
                }
                using (var cmd = new SqlCommand("DELETE FROM apps WHERE appId = @appId", connection))
                {
                    cmd.Parameters.AddWithValue("@appId", appId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ===== Discovered Monitoring Apps (read-only from monapi) =====

        /// <summary>
        /// Syncs discovered New Relic apps into monarch.apps so dashboard apps
        /// appear in the admin dropdown. Only creates entries for apps not yet linked.
        /// </summary>
        public async Task SyncDiscoveredAppsAsync()
        {
            try
            {
                // Get discovered New Relic app IDs and names from monapi
                var discoveredApps = new List<(string Id, string Name)>();
                using (var monapiConn = new SqlConnection(_monapiConnectionString))
                {
                    await monapiConn.OpenAsync();
                    using (var command = new SqlCommand("SELECT appId, appName FROM newRelicApps", monapiConn))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            discoveredApps.Add((reader.GetValue(0).ToString()!, reader.GetString(1)));
                        }
                    }
                }

                if (!discoveredApps.Any()) return;

                // Get existing monarch apps that are already linked to a New Relic app
                var linkedNewRelicIds = new HashSet<string>();
                using (var monarchConn = new SqlConnection(_monarchConnectionString))
                {
                    await monarchConn.OpenAsync();
                    using (var command = new SqlCommand("SELECT newRelicId FROM apps WHERE newRelicId IS NOT NULL AND newRelicId != ''", monarchConn))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            linkedNewRelicIds.Add(reader.GetString(0));
                        }
                    }
                }

                // Create monarch.apps entries for any unlinked discovered apps
                foreach (var (id, name) in discoveredApps)
                {
                    if (!linkedNewRelicIds.Contains(id))
                    {
                        using (var conn = new SqlConnection(_monarchConnectionString))
                        {
                            await conn.OpenAsync();
                            var insertQuery = "INSERT INTO apps (appName, status, newRelicId) VALUES (@appName, @status, @newRelicId)";
                            using (var command = new SqlCommand(insertQuery, conn))
                            {
                                command.Parameters.AddWithValue("@appName", name);
                                command.Parameters.AddWithValue("@status", "Active");
                                command.Parameters.AddWithValue("@newRelicId", id);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing discovered apps: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all New Relic applications discovered by the monapi-worker
        /// </summary>
        public async Task<List<DiscoveredNewRelicApp>> GetDiscoveredNewRelicAppsAsync()
        {
            var apps = new List<DiscoveredNewRelicApp>();

            using (var connection = new SqlConnection(_monapiConnectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT appId, appName, status FROM newRelicApps ORDER BY appName";
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        apps.Add(new DiscoveredNewRelicApp
                        {
                            AppId = reader.GetValue(0).ToString() ?? string.Empty,
                            AppName = reader.GetString(1),
                            Status = reader.GetValue(2).ToString() ?? string.Empty
                        });
                    }
                }
            }

            return apps;
        }

        /// <summary>
        /// Retrieves all Nagios hosts discovered by the monapi-worker
        /// </summary>
        public async Task<List<DiscoveredNagiosApp>> GetDiscoveredNagiosAppsAsync()
        {
            var apps = new List<DiscoveredNagiosApp>();

            try
            {
                using (var connection = new SqlConnection(_monapiConnectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT appId, appName, status FROM nagiosApps ORDER BY appName";
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            apps.Add(new DiscoveredNagiosApp
                            {
                                AppId = reader.GetString(0),
                                AppName = reader.GetString(1),
                                Status = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (SqlException)
            {
                // Table may not exist yet (Nagios table creation is TBD in setup.sql)
                Console.WriteLine("Warning: nagiosApps table not found. Nagios discovery unavailable.");
            }

            return apps;
        }

        // ===== Many-to-Many: App <-> Slack Channels =====

        /// <summary>
        /// Gets Slack channel keys associated with an app
        /// </summary>
        private async Task<List<string>> GetAppSlackChannelsAsync(int appId)
        {
            var channels = new List<string>();

            try
            {
                using (var connection = new SqlConnection(_monarchConnectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT channelKey FROM appSlackChannels WHERE appId = @appId";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@appId", appId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                channels.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (SqlException)
            {
                // Table may not exist yet on first run
            }

            return channels;
        }

        /// <summary>
        /// Replaces all Slack channel mappings for an app
        /// </summary>
        private async Task SaveAppSlackChannelsAsync(int appId, List<string> channelKeys)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();

                // Clear existing
                using (var cmd = new SqlCommand("DELETE FROM appSlackChannels WHERE appId = @appId", connection))
                {
                    cmd.Parameters.AddWithValue("@appId", appId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert new
                foreach (var key in channelKeys)
                {
                    using (var cmd = new SqlCommand("INSERT INTO appSlackChannels (appId, channelKey) VALUES (@appId, @channelKey)", connection))
                    {
                        cmd.Parameters.AddWithValue("@appId", appId);
                        cmd.Parameters.AddWithValue("@channelKey", key);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // ===== Many-to-Many: App <-> Jira Workspaces =====

        /// <summary>
        /// Gets Jira workspace keys associated with an app
        /// </summary>
        private async Task<List<string>> GetAppJiraWorkspacesAsync(int appId)
        {
            var workspaces = new List<string>();

            try
            {
                using (var connection = new SqlConnection(_monarchConnectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT workspaceKey FROM appJiraWorkspaces WHERE appId = @appId";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@appId", appId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                workspaces.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (SqlException)
            {
                // Table may not exist yet on first run
            }

            return workspaces;
        }

        /// <summary>
        /// Replaces all Jira workspace mappings for an app
        /// </summary>
        private async Task SaveAppJiraWorkspacesAsync(int appId, List<string> workspaceKeys)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();

                // Clear existing
                using (var cmd = new SqlCommand("DELETE FROM appJiraWorkspaces WHERE appId = @appId", connection))
                {
                    cmd.Parameters.AddWithValue("@appId", appId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert new
                foreach (var key in workspaceKeys)
                {
                    using (var cmd = new SqlCommand("INSERT INTO appJiraWorkspaces (appId, workspaceKey) VALUES (@appId, @workspaceKey)", connection))
                    {
                        cmd.Parameters.AddWithValue("@appId", appId);
                        cmd.Parameters.AddWithValue("@workspaceKey", key);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // ===== Available Jira Workspaces =====

        /// <summary>
        /// Retrieves all configured Jira workspaces from monarch.jiraWorkspaces
        /// </summary>
        public async Task<List<JiraWorkspaceOption>> GetJiraWorkspacesAsync()
        {
            var workspaces = new List<JiraWorkspaceOption>();

            try
            {
                using (var connection = new SqlConnection(_monarchConnectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT workspaceKey, workspaceName, baseUrl FROM jiraWorkspaces ORDER BY workspaceName";
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            workspaces.Add(new JiraWorkspaceOption
                            {
                                WorkspaceKey = reader.GetString(0),
                                WorkspaceName = reader.GetString(1),
                                BaseUrl = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (SqlException)
            {
                // Table may not exist yet on first run
            }

            return workspaces;
        }

        /// <summary>
        /// Adds or updates a Jira workspace in the available workspaces list.
        /// Uses upsert pattern to prevent duplicate key errors.
        /// </summary>
        public async Task AddJiraWorkspaceAsync(JiraWorkspaceOption workspace)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();
                var query = @"
                    IF NOT EXISTS (SELECT 1 FROM jiraWorkspaces WHERE workspaceKey = @workspaceKey)
                        INSERT INTO jiraWorkspaces (workspaceKey, workspaceName, baseUrl) 
                        VALUES (@workspaceKey, @workspaceName, @baseUrl)
                    ELSE
                        UPDATE jiraWorkspaces 
                        SET workspaceName = @workspaceName, baseUrl = @baseUrl 
                        WHERE workspaceKey = @workspaceKey";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@workspaceKey", workspace.WorkspaceKey);
                    command.Parameters.AddWithValue("@workspaceName", workspace.WorkspaceName);
                    command.Parameters.AddWithValue("@baseUrl", workspace.BaseUrl);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Removes a Jira workspace from the available workspaces list
        /// </summary>
        public async Task RemoveJiraWorkspaceAsync(string workspaceKey)
        {
            using (var connection = new SqlConnection(_monarchConnectionString))
            {
                await connection.OpenAsync();

                // Remove app associations first
                using (var cmd = new SqlCommand("DELETE FROM appJiraWorkspaces WHERE workspaceKey = @workspaceKey", connection))
                {
                    cmd.Parameters.AddWithValue("@workspaceKey", workspaceKey);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Remove workspace
                using (var cmd = new SqlCommand("DELETE FROM jiraWorkspaces WHERE workspaceKey = @workspaceKey", connection))
                {
                    cmd.Parameters.AddWithValue("@workspaceKey", workspaceKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Sends a test Jira ticket to the specified workspace
        /// </summary>
        public async Task<(bool success, string message)> SendTestJiraTicketAsync(string workspaceKey)
        {
            try
            {
                // Get workspace details
                JiraWorkspaceOption? workspace = null;
                using (var connection = new SqlConnection(_monarchConnectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT workspaceKey, workspaceName, baseUrl FROM jiraWorkspaces WHERE workspaceKey = @workspaceKey";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@workspaceKey", workspaceKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                workspace = new JiraWorkspaceOption
                                {
                                    WorkspaceKey = reader.GetString(0),
                                    WorkspaceName = reader.GetString(1),
                                    BaseUrl = reader.GetString(2)
                                };
                            }
                        }
                    }
                }

                if (workspace == null)
                {
                    return (false, "Workspace not found");
                }

                // Read Jira API credentials
                var jiraApiKey = File.ReadAllText("/run/secrets/monarch_jira_api_key").Trim();
                var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(jiraApiKey));
                var authHeader = $"Basic {base64Credentials}";

                // Resolve available issue types for the project
                var issueTypeId = await GetFirstIssueTypeIdAsync(workspace.BaseUrl, workspace.WorkspaceKey, authHeader);
                if (issueTypeId == null)
                {
                    return (false, "Could not resolve issue types for this project. Verify the project key and API credentials.");
                }

                // Create test ticket using template with resolved issue type
                var url = $"{workspace.BaseUrl.TrimEnd('/')}/rest/api/3/issue";
                var payload = JiraTestTicket.Create(workspace.WorkspaceKey, workspace.WorkspaceName, issueTypeId);
                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", authHeader);
                request.Headers.Add("Accept", "application/json");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Failed to create ticket: {response.StatusCode} - {responseContent}");
                }

                using var jsonResponse = JsonDocument.Parse(responseContent);
                var issueKey = jsonResponse.RootElement.GetProperty("key").GetString();

                if (string.IsNullOrEmpty(issueKey))
                {
                    return (false, "Failed to parse issue key from response");
                }

                return (true, $"Successfully created test ticket: {issueKey}");
            }
            catch (FileNotFoundException)
            {
                return (false, "Jira API key not found. Ensure the secret 'monarch_jira_api_key' is configured.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches the first available issue type ID for a Jira project.
        /// Uses GET /rest/api/3/project/{key} which returns issue types in the response.
        /// </summary>
        private async Task<string?> GetFirstIssueTypeIdAsync(string baseUrl, string projectKey, string authHeader)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", authHeader);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/project/{projectKey}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("issueTypes", out var issueTypes))
                {
                    foreach (var issueType in issueTypes.EnumerateArray())
                    {
                        // Skip subtask types — they can't be created as top-level issues
                        if (issueType.TryGetProperty("subtask", out var subtask) && subtask.GetBoolean())
                            continue;

                        if (issueType.TryGetProperty("id", out var id))
                            return id.GetString();
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Represents an available Jira workspace that can be assigned to apps.
    /// Maps to the monarch.jiraWorkspaces table.
    /// </summary>
    public class JiraWorkspaceOption
    {
        public string WorkspaceKey { get; set; } = string.Empty;
        public string WorkspaceName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }
}
