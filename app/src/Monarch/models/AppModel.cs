using System.ComponentModel.DataAnnotations;

namespace Monarch.Models
{
        public enum StatusType
    {
        Unknown = 3,
        Operational = 2,
        DegradedPerformance = 1,
        Outage = 0
    }

    public class AppModel
    {
        public int AppId { get; set; }
        public string? NewRelicId { get; set; }
        public string? NagiosId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public StatusType Status { get; set; } = StatusType.Unknown;
        public string? MostRecentIncidentId { get; set; }
        public bool SlackAlert { get; set; }
        public bool JiraAlert { get; set; }
        public bool SmtpAlert{ get; set; }
    }
}