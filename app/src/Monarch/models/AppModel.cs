using System.ComponentModel.DataAnnotations;

namespace Monarch.Models
{
    public class AppModel
    {
        public int AppId { get; set; }
        public int? NewRelicId { get; set; }
        public int? NagiosId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public string? MostRecentIncidentId { get; set; }
        public bool SlackAlert { get; set; }
        public bool JiraAlert { get; set; }
        public bool SmtpAlert{ get; set; }
    }
}