namespace Monapi.Worker.Jira;

/// <summary>
/// Data model representing a Jira ticket/issue
/// Used to abstract Jira issue data for the Monarch monitoring system
/// </summary>
public class JiraTicket
{
    /// <summary>
    /// Jira issue key (e.g., "MON-123")
    /// </summary>
    public string IssueKey { get; set; } = string.Empty;

    /// <summary>
    /// Jira internal issue ID
    /// </summary>
    public string IssueId { get; set; } = string.Empty;

    /// <summary>
    /// Brief summary of the incident
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the incident
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the ticket (e.g., "Open", "In Progress", "Done")
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (e.g., "High", "Medium", "Low")
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Link to the Jira issue in browser
    /// </summary>
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the ticket was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the ticket was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
