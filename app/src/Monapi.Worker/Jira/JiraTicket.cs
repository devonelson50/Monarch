namespace Monapi.Worker.Jira;

// Data model representing a Jira ticket/issue
// Used to abstract Jira issue data for the Monarch monitoring system

public class JiraTicket
{
    // Jira issue key (e.g., "MON-123")
    public string IssueKey { get; set; } = string.Empty;

    // Brief summary of the incident
    public string Summary { get; set; } = string.Empty;

    // Detailed description of the incident
    public string Description { get; set; } = string.Empty;

    // Current status of the ticket (e.g., "Open", "In Progress", "Done")
    public string Status { get; set; } = string.Empty;

    // Priority level (e.g., "High", "Medium", "Low")
    public string Priority { get; set; } = "Medium";

    // Timestamp when the ticket was created
    public DateTime CreatedAt { get; set; }

    // Timestamp when the ticket was last updated
    public DateTime? UpdatedAt { get; set; }
}
