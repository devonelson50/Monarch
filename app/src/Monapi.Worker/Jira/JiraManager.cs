using Microsoft.Data.SqlClient;

namespace Monapi.Worker.Jira;

// Manages Jira ticket lifecycle based on application incidents
// Tracks status changes, creates tickets when needed, and updates existing tickets

public class JiraManager
{
    private readonly string _jiraApiKey;
    private readonly string _monarchConnStr;
    private readonly string _monapiConnStr;

    public JiraManager(string jiraApiKey, string monarchConnStr, string monapiConnStr)
    {
        _jiraApiKey = jiraApiKey;
        _monarchConnStr = monarchConnStr;
        _monapiConnStr = monapiConnStr;
    }


    // Creates or updates a Jira ticket when an app's status worsens.
    // The caller (Worker) is responsible for checking whether the status is actually worsening
    // before calling this method.

    public async Task HandleStatusChange(int appId, string appName, string newStatus, string metricDetails = "")
    {
        // Check if there's already an open incident for this app
        var existingIncidentId = await GetOpenIncidentId(appId);

        // Look up the Jira workspace configured for this app
        var workspace = await GetWorkspaceForApp(appId);
        if (workspace == null)
        {
            Console.WriteLine($"No Jira workspace configured for app {appName} (id: {appId}). Skipping ticket creation.");
            return;
        }

        var connector = new JiraConnector(workspace.Value.BaseUrl, workspace.Value.WorkspaceKey, _jiraApiKey);

        if (existingIncidentId.HasValue)
        {
            // Update existing ticket with new status
            await UpdateExistingIncident(existingIncidentId.Value, appName, newStatus, connector);
        }
        else
        {
            // Create new incident and Jira ticket
            await CreateNewIncident(appId, appName, newStatus, connector, metricDetails);
        }
    }

    // Looks up the Jira workspace configured for an app
    private async Task<(string WorkspaceKey, string BaseUrl)?> GetWorkspaceForApp(int appId)
    {
        try
        {
            using (var connection = new SqlConnection(_monarchConnStr))
            {
                await connection.OpenAsync();
                
                var query = @"
                    SELECT TOP 1 jw.workspaceKey, jw.baseUrl 
                    FROM appJiraWorkspaces ajw 
                    INNER JOIN jiraWorkspaces jw ON ajw.workspaceKey = jw.workspaceKey 
                    WHERE ajw.appId = @appId";
                
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appId", appId);
                    
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return (reader.GetString(0), reader.GetString(1));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error looking up Jira workspace for app {appId}: {ex.Message}");
        }
        return null;
    }

    // Gets the open incident ID for an application, if one exists
    private async Task<int?> GetOpenIncidentId(int appId)
    {
        try
        {
            using (var connection = new SqlConnection(_monapiConnStr))
            {
                await connection.OpenAsync();
                
                // Find the most recent open incident for this app
                var query = @"
                    SELECT TOP 1 incidentId 
                    FROM incidents 
                    WHERE appId = @appId AND closeTime IS NULL 
                    ORDER BY openTime DESC";
                
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appId", appId);
                    
                    var result = await command.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for open incidents: {ex.Message}");
            return null;
        }
    }

    // Creates a new incident record and associated Jira ticket
    private async Task CreateNewIncident(int appId, string appName, string status, JiraConnector connector, string metricDetails = "")
    {
        try
        {
            // Resolve issue type ID dynamically from the Jira project
            var issueTypeId = await connector.ResolveIssueTypeId();
            if (issueTypeId == null)
            {
                Console.WriteLine($"Failed to resolve issue type for app {appName}. Cannot create Jira ticket.");
                return;
            }

            using (var connection = new SqlConnection(_monapiConnStr))
            {
                await connection.OpenAsync();
                
                // Create incident record
                var query = @"
                    INSERT INTO incidents (appId, openTime) 
                    OUTPUT INSERTED.incidentId
                    VALUES (@appId, @openTime)";
                
                int incidentId;
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appId", appId);
                    command.Parameters.AddWithValue("@openTime", DateTime.UtcNow);
                    
                    incidentId = (int)await command.ExecuteScalarAsync();
                }

                Console.WriteLine($"Created incident {incidentId} for app {appName}");

                // Create Jira ticket using template
                var priority = status == "Down" ? "High" : "Medium";
                var ticket = await connector.CreateIncidentIssue(appName, status, issueTypeId, priority, metricDetails);

                if (ticket != null)
                {
                    // Store Jira ticket reference in database
                    await StoreJiraTicket(ticket, incidentId);
                    
                    Console.WriteLine($"Created Jira ticket {ticket.IssueKey} for incident {incidentId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating new incident: {ex.Message}");
        }
    }

    // Updates an existing incident with additional information
    private async Task UpdateExistingIncident(int incidentId, string appName, string newStatus, JiraConnector connector)
    {
        try
        {
            // Get the Jira ticket associated with this incident
            var jiraKey = await GetJiraTicketForIncident(incidentId);
            
            if (jiraKey != null)
            {
                // Use template for update comment (pass empty string as oldStatus since we don't track it here)
                var comment = JiraIncidentTicket.CreateUpdateComment(appName, newStatus, "Previous");
                await connector.AddComment(jiraKey, comment);
                
                Console.WriteLine($"Updated Jira ticket {jiraKey} for incident {incidentId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating existing incident: {ex.Message}");
        }
    }

    // Handles application recovery by closing open incidents
    public async Task HandleRecovery(int appId, string appName)
    {
        try
        {
            var incidentId = await GetOpenIncidentId(appId);
            
            if (!incidentId.HasValue)
            {
                return; // No open incident to close
            }

            using (var connection = new SqlConnection(_monapiConnStr))
            {
                await connection.OpenAsync();
                
                // Close the incident
                var query = "UPDATE incidents SET closeTime = @closeTime WHERE incidentId = @incidentId";
                
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@closeTime", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@incidentId", incidentId.Value);
                    
                    await command.ExecuteNonQueryAsync();
                }

                Console.WriteLine($"Closed incident {incidentId} for app {appName}");

                // Add recovery comment to Jira ticket using template
                var jiraKey = await GetJiraTicketForIncident(incidentId.Value);
                
                if (jiraKey != null)
                {
                    var workspace = await GetWorkspaceForApp(appId);
                    if (workspace != null)
                    {
                        var connector = new JiraConnector(workspace.Value.BaseUrl, workspace.Value.WorkspaceKey, _jiraApiKey);
                        var comment = JiraIncidentTicket.CreateRecoveryComment(appName);
                        await connector.AddComment(jiraKey, comment);
                        
                        Console.WriteLine($"Added recovery comment to Jira ticket {jiraKey}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling recovery: {ex.Message}");
        }
    }

    // Stores Jira ticket information in the database
    private async Task StoreJiraTicket(JiraTicket ticket, int incidentId)
    {
        try
        {
            using (var connection = new SqlConnection(_monapiConnStr))
            {
                await connection.OpenAsync();
                
                var query = @"
                    INSERT INTO jira (ticketId, incidentId, issueKey, openTime, summary, description) 
                    VALUES (@ticketId, @incidentId, @issueKey, @openTime, @summary, @description)";
                
                using (var command = new SqlCommand(query, connection))
                {
                    // Use a hash of the issue key as the ticketId
                    command.Parameters.AddWithValue("@ticketId", Math.Abs(ticket.IssueKey.GetHashCode()));
                    command.Parameters.AddWithValue("@incidentId", incidentId);
                    command.Parameters.AddWithValue("@issueKey", ticket.IssueKey);
                    command.Parameters.AddWithValue("@openTime", ticket.CreatedAt);
                    command.Parameters.AddWithValue("@summary", ticket.Summary);
                    command.Parameters.AddWithValue("@description", ticket.Description);
                    
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing Jira ticket: {ex.Message}");
        }
    }

    // Gets the Jira ticket key for a specific incident
    private async Task<string?> GetJiraTicketForIncident(int incidentId)
    {
        try
        {
            using (var connection = new SqlConnection(_monapiConnStr))
            {
                await connection.OpenAsync();
                
                var query = "SELECT issueKey FROM jira WHERE incidentId = @incidentId";
                
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@incidentId", incidentId);
                    
                    var result = await command.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting Jira ticket for incident: {ex.Message}");
        }
        
        return null;
    }
}
