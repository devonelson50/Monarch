using Microsoft.Data.SqlClient;

namespace Monapi.Worker.Jira;

// Manages Jira ticket lifecycle based on application incidents
// Tracks status changes, creates tickets when needed, and updates existing tickets

public class JiraManager
{
    private readonly JiraConnector _connector;
    private readonly string _sqlPassword;

    public JiraManager(JiraConnector connector, string sqlPassword)
    {
        _connector = connector;
        _sqlPassword = sqlPassword;
    }


    // Handles application status change and creates Jira ticket if needed

    public async Task HandleStatusChange(string appId, string appName, string oldStatus, string newStatus, bool shouldCreateTicket)
    {
        if (!shouldCreateTicket)
        {
            return;
        }

        // Only create tickets for degraded or down statuses
        if (newStatus == "Operational")
        {
            // If recovering, check if there's an open incident and close it
            await HandleRecovery(appId, appName);
            return;
        }

        // Check if status is worse (Operational → Degraded/Down or Degraded → Down)
        bool isWorsening = IsStatusWorsening(oldStatus, newStatus);
        
        if (!isWorsening)
        {
            return; // Don't create duplicate tickets for same or improving status
        }

        // Check if there's already an open incident for this app
        var existingIncidentId = await GetOpenIncidentId(appId);
        
        if (existingIncidentId.HasValue)
        {
            // Update existing ticket with new status
            await UpdateExistingIncident(existingIncidentId.Value, appName, newStatus);
        }
        else
        {
            // Create new incident and Jira ticket
            await CreateNewIncident(appId, appName, newStatus);
        }
    }

    // Determines if the status change represents worsening conditions
    private bool IsStatusWorsening(string oldStatus, string newStatus)
    {
        var statusPriority = new Dictionary<string, int>
        {
            { "Operational", 0 },
            { "Degraded", 1 },
            { "Down", 2 }
        };

        int oldPriority = statusPriority.GetValueOrDefault(oldStatus, 0);
        int newPriority = statusPriority.GetValueOrDefault(newStatus, 0);

        return newPriority > oldPriority;
    }

    // Gets the open incident ID for an application, if one exists
    private async Task<int?> GetOpenIncidentId(string appId)
    {
        try
        {
            var connectionString = $"Server=sqlserver,1433;Database=monarch;User Id=monapi;Password={_sqlPassword};TrustServerCertificate=True;";
            
            using (var connection = new SqlConnection(connectionString))
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
    private async Task CreateNewIncident(string appId, string appName, string status)
    {
        try
        {
            var connectionString = $"Server=sqlserver,1433;Database=monarch;User Id=monapi;Password={_sqlPassword};TrustServerCertificate=True;";
            
            using (var connection = new SqlConnection(connectionString))
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

                // Create Jira ticket
                var priority = status == "Down" ? "High" : "Medium";
                var summary = $"{appName} - {status}";
                var description = $"Application {appName} has changed status to {status}.\n\n" +
                                $"Incident ID: {incidentId}\n" +
                                $"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                                $"Status: {status}";

                var ticket = await _connector.CreateIssue(summary, description, priority);

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
    private async Task UpdateExistingIncident(int incidentId, string appName, string newStatus)
    {
        try
        {
            // Get the Jira ticket associated with this incident
            var jiraKey = await GetJiraTicketForIncident(incidentId);
            
            if (jiraKey != null)
            {
                var comment = $"Status update: Application {appName} is now {newStatus} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
                await _connector.AddComment(jiraKey, comment);
                
                Console.WriteLine($"Updated Jira ticket {jiraKey} for incident {incidentId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating existing incident: {ex.Message}");
        }
    }

    // Handles application recovery by closing open incidents
    private async Task HandleRecovery(string appId, string appName)
    {
        try
        {
            var incidentId = await GetOpenIncidentId(appId);
            
            if (!incidentId.HasValue)
            {
                return; // No open incident to close
            }

            var connectionString = $"Server=sqlserver,1433;Database=monarch;User Id=monapi;Password={_sqlPassword};TrustServerCertificate=True;";
            
            using (var connection = new SqlConnection(connectionString))
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

                // Add comment to Jira ticket
                var jiraKey = await GetJiraTicketForIncident(incidentId.Value);
                
                if (jiraKey != null)
                {
                    var comment = $"✅ RESOLVED: Application {appName} has recovered and is now Operational at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
                    await _connector.AddComment(jiraKey, comment);
                    
                    Console.WriteLine($"Added recovery comment to Jira ticket {jiraKey}");
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
            var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={_sqlPassword};TrustServerCertificate=True;";
            
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                var query = @"
                    INSERT INTO jira (ticketId, incidentId, teamId, issueKey, openTime, summary, description) 
                    VALUES (@ticketId, @incidentId, @teamId, @issueKey, @openTime, @summary, @description)";
                
                using (var command = new SqlCommand(query, connection))
                {
                    // Use a hash of the issue key as the ticketId
                    command.Parameters.AddWithValue("@ticketId", Math.Abs(ticket.IssueKey.GetHashCode()));
                    command.Parameters.AddWithValue("@incidentId", incidentId);
                    command.Parameters.AddWithValue("@teamId", 1); // Default team, can be enhanced later
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
            var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={_sqlPassword};TrustServerCertificate=True;";
            
            using (var connection = new SqlConnection(connectionString))
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
