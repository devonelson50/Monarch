using System.ComponentModel.DataAnnotations;

namespace Monarch.Models
{
        public enum StatusType
    {
        Operational = 0,
        DegradedPerformance = 1,
        Outage = 2,
        Unknown = 3
    }

    public class AppModel
    {
        public int AppId { get; set; }
        public int? NewRelicId { get; set; }
        public int? NagiosId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public StatusType Status { get; set; } = StatusType.Unknown;
        public string? MostRecentIncidentId { get; set; }
        public bool SlackAlert { get; set; }
        public bool JiraAlert { get; set; }
        public bool SmtpAlert{ get; set; }
        public List<FilterModel> Filters { get; set; } = new();
         // Slack channels selected for this app's notifications
        public List<string> SelectedSlackChannels { get; set; } = new();

        // Jira workspace keys selected for this app's tickets
        public List<string> SelectedJiraWorkspaces { get; set; } = new();
    }
}