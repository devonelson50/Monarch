using System.ComponentModel.DataAnnotations;

/*  
  Brady Brown
  Basic App Model for Monarch
  Stores basic data for applications, covers most use cases in dashboard
  Does not include advanced details for expanded view (this can be found in AppDetails.cs)
  Includes a StatusType variable, to turn integer data from database into better format for database
*/


namespace Monarch.Models
{

    //Custom variable type for better use in dashboard
        public enum StatusType
    {
        Operational = 0,
        DegradedPerformance = 1,
        Outage = 2,
        Unknown = 3
    }

    /*
    Overall model for basic application
    Includes the following:
        -int AppId
        -int NewRelicId (nullable)
        -int NagiosId (nullable)
        -string AppName
        -StatusType Status
        -string MostRecentIncidentId (nullable)
        -bool SlackAlert
        -bool JiraAlert
        -bool SmtpAlert
        -List<FilterModel> Filters
        -List<string> SelectedSlackChannels
        -List<string> SelectedJiraWorkspaces
    */
    public class AppModel
    {
        //Identification Variables

        //Auto-increment id pulled directly from Monarch database
        public int AppId { get; set; }
        //Id pulled from Monapi database NewRelic table and set by user during application config
        public int? NewRelicId { get; set; }
        //Id pulled from Monapi database Nagios table and set by user during application config
        public int? NagiosId { get; set; }
        //Name set by user during application config
        public string AppName { get; set; } = string.Empty;

        //Status Variables

        //Custom Status variable used, updated automatically by application after refresh timer
        public StatusType Status { get; set; } = StatusType.Unknown;
        //Id of most recent incident, nullable for if there are no incidents
        public string? MostRecentIncidentId { get; set; }
        //Boolean used for Slack notification purposes
        public bool SlackAlert { get; set; }
        //Boolean used for Jira notification purposes
        public bool JiraAlert { get; set; }
        //Boolean used for Smtp notification purposes
        public bool SmtpAlert{ get; set; }

        //Configuration Variables

        //List of filter models, used to determine what filters are applied to the application
        public List<FilterModel> Filters { get; set; } = new();
        
        // Slack channels selected for this app's notifications
        public List<string> SelectedSlackChannels { get; set; } = new();

        // Jira workspace keys selected for this app's tickets
        public List<string> SelectedJiraWorkspaces { get; set; } = new();
    }
}