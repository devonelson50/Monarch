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