// Admin Panel data models for Monarch
// Used by AppAdminService and Admin.razor to manage app configurations,
// monitoring integrations (New Relic, Nagios), and notification routing (Slack, Jira)

namespace Monarch.Models
{
    /// <summary>
    /// Represents a Monarch-managed application with all integration mappings.
    /// Maps to the monarch.apps database table.
    /// </summary>
    public class MonarchApp
    {
        public int AppId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public string? NewRelicId { get; set; }
        public string? NagiosId { get; set; }
        public bool SlackAlert { get; set; }
        public bool JiraAlert { get; set; }
        public bool SmtpAlert { get; set; }

        // Slack channels selected for this app's notifications
        public List<string> SelectedSlackChannels { get; set; } = new();

        // Jira workspace keys selected for this app's tickets
        public List<string> SelectedJiraWorkspaces { get; set; } = new();
    }

    /// <summary>
    /// A New Relic application discovered by the monapi-worker connector.
    /// Read from the monapi.newRelicApps table.
    /// </summary>
    public class DiscoveredNewRelicApp
    {
        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// A Nagios host discovered by the monapi-worker connector.
    /// Read from the monapi.nagiosApps table.
    /// </summary>
    public class DiscoveredNagiosApp
    {
        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
