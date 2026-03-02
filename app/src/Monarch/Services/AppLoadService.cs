using Monarch.Models;
using Microsoft.Data.SqlClient;
using Monarch.Components;
using Monarch.Services;
using System.Security.Cryptography.Xml;

/*  
  Brady Brown
  App Loading Service for Monarch
  Retrieves information from Monarch and Monapi Database, primarily for Dashboard View
  Also used for retrieving data for Admin Services/Configuration
  Loads pre-existing services, filters, and refreshes application statuses
*/

namespace Monarch.Services
{
  /*
    AppLoadService Class
    Class that contains all methods related to retrieving information from Monarch and Monapi Database
    Includes the following with more detail on each Method:
      - App Loading
        - GetMonarchAppsAsync()
        - GetAppByIdAsync(int)
        - RefreshAppStatusAsync(int)
        - GetDetailsAsync(AppModel)
      - Filter Loading
        - GetAppFiltersAsync(int)
        - GetAllFiltersAsync(int)
      - Notification Loading
        - GetAppSlackChannelsAsync(int)
        - GetAppJiraWorkspacesAsync(int)
  */

  public class AppLoadService(string monapiConn, string monarchConn)
  {

    //Private strings for database connections
    private readonly string monarchConn = monarchConn;
    private readonly string monapiConn = monapiConn;

    /*
      Brady Brown
      GetMonarchAppsAsync Method
      Retrieves all information necessary for the base app
      Includes all connections to slack, jira, and kafka
      Includes all filter mappings
      Does not include detailed view information (this is pulled in separate function)
    */
    public async Task<List<AppModel>> GetMonarchAppsAsync()
    {
      //List of all apps, will return when full
      var apps = new List<AppModel>(); 

      //Starts connection to monarch database
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();

        //Queries all data in apps table
        var sql = @"
          SELECT 
            appId, 
            appName, 
            status, 
            newRelicId, 
            nagiosId, 
            mostRecentIncidentId, 
            slackAlert, 
            jiraAlert, 
            smtpAlert,
          FROM apps";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while (await reader.ReadAsync())
          {

            //Get id before entering into app model
            var appId = reader.GetInt32(0);

            //Use id to get all app filters, slack connections, and jira connections to list in app model for dashboard
            var filters = await GetAppFiltersAsync(appId);
            var slackConns = await GetAppSlackChannelsAsync(appId);
            var jiraConns = await GetAppJiraWorkspacesAsync(appId);

            //Get status so we can convert to "StatusType" variable in app model
            int dbStatus = reader.GetInt32(2);

            //Enter all information into app model
            apps.Add(new AppModel
            {
              AppId = appId,
              AppName = reader.GetString(1),
              Status = (StatusType)dbStatus,

              //Handle nullables safely
              NewRelicId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
              NagiosId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
              MostRecentIncidentId = reader.IsDBNull(5) ? null : reader.GetString(5),

              //Handle booleans
              SlackAlert = reader.GetBoolean(6),
              JiraAlert = reader.GetBoolean(7),
              SmtpAlert = reader.GetBoolean(8),
              
              //Handle data retreived with other methods
              Filters = filters,
              SelectedSlackChannels = slackConns,
              SelectedJiraWorkspaces = jiraConns
            });
          }
        }
      }
      //Return filled list of apps
      return apps;
    }

    /// <summary>
    /// Conner Hammonds
    /// Retrieves a single Monarch app by its ID
    /// </summary>
    public async Task<AppModel?> GetAppByIdAsync(int appId)
    {
      AppModel? app = null;

      using (var connection = new SqlConnection(monarchConn))
      {
        await connection.OpenAsync();
        var query = "SELECT appId, appName, status, newRelicId, nagiosId, slackAlert, jiraAlert, smtpAlert FROM apps WHERE appId = @appId";
        using (var command = new SqlCommand(query, connection))
        {
          command.Parameters.AddWithValue("@appId", appId);
          using (var reader = await command.ExecuteReaderAsync())
          {
            if (await reader.ReadAsync())
            {
              
              var filters = await GetAppFiltersAsync(appId);
              var slackConns = await GetAppSlackChannelsAsync(appId);
              var jiraConns = await GetAppJiraWorkspacesAsync(appId);

              int dbStatus = reader.GetInt32(2);

              app = new AppModel
              {
                AppId = appId,
                AppName = reader.GetString(1),
                Status = (StatusType)dbStatus,
                NewRelicId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                NagiosId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                SlackAlert = reader.IsDBNull(5) ? false : reader.GetBoolean(5),
                JiraAlert = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                SmtpAlert = reader.IsDBNull(7) ? false : reader.GetBoolean(7),
                SelectedJiraWorkspaces = jiraConns,
                SelectedSlackChannels = slackConns,
                Filters = filters
              };
            }
          }
        }
      }
      return app;
    }


    /*
      Brady Brown
      RefreshAppStatusAsync Method
      Given an app id, retrieves updated status information from new relic & nagios
      This is done in the following 4 steps:
        1. New Relic & Nagios ids are retrieved from Monarch database
        2. Current status is retrieved from New Relic & Nagios
        3. New Relic and Nagios status is compared and worst case is chosen for Monarch display
        4. Monarch database is updated with new status
    */

        public async Task<StatusType> RefreshAppStatusAsync(int appId)
    {
      //variables created for future reference
      int nrId = -1;
      int nId = -1;

      //Step 1: ID retrieval
      //Connection to monarch database established
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();

        //Query to retrieve New Relic & Nagios ids 
        var sql = "SELECT newRelicId, nagiosId FROM apps WHERE appId = @id";
        
        using (var cmd = new SqlCommand(sql, conn))
        {
          //Adds app id as parameter for query
          cmd.Parameters.AddWithValue("@id", appId);
          using (var reader = await cmd.ExecuteReaderAsync())
          {
            if (await reader.ReadAsync())
            {
              //Checks if newRelicId column is empty
              if (!reader.IsDBNull(reader.GetOrdinal("newRelicId")))
                nrId = Convert.ToInt32(reader["newRelicId"]);

              //Checks if nagiosId column is empty
              if (!reader.IsDBNull(reader.GetOrdinal("nagiosId")))
                nId = Convert.ToInt32(reader["nagiosId"]);
            }
          }
        }
      }

      //Step 2: Current Status Retrieval
      //Initialized status as -1
      //This is so it will not affect calculations if it doesn't exist
      int nrStatus = -1;
      int nStatus = -1;

      //Open connection to monapi database
      using (var conn = new SqlConnection(monapiConn))
      {
        await conn.OpenAsync();

        //Gets New Relic data if it is not empty
        if (nrId != -1)
        {
          //Query for status where appId = nrId in newRelicApps
          var sqlNr = "SELECT status FROM newRelicApps WHERE appId = @nrId";

          using (var cmd = new SqlCommand(sqlNr, conn))
          {
            //Add nrId as parameter
            cmd.Parameters.AddWithValue("@nrId", nrId);

            //Set returned value as status
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
              nrStatus = Convert.ToInt32(result);
          }
        }

        //Gets Nagios data if it is not empty
        if (nId != -1)
        {
          //Query for status where appId = nId in nagiosApps
          var sqlN = "SELECT currentState FROM nagiosApps WHERE hostObjectId = @nId";
          using (var cmd = new SqlCommand(sqlN, conn))
          {
            //Add nrId as parameter
            cmd.Parameters.AddWithValue("@nId", nId);

            //Set returned value as status
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
              nStatus = Convert.ToInt32(result);
          }
        }
      }

      //Step 3: Calculate worst status
      //Initialize status as unknown(3)
      var intNewStatus = 3; 

      //Compare Status
      //If New Relic greater & under 3(unknown), set to New Relic status
      if ((nrStatus > nStatus) && (nrStatus < 3)) intNewStatus = nrStatus;

      //Otherwise we set status to Nagios
      //This works even if they're equal, because we would set it to the same status either way
      else intNewStatus = nStatus;

      //Convert int status to status type variable
      var newStatus = (StatusType)intNewStatus;

      //Step 4: Update Monarch Status
      //Connect to Monarch database again
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();
        // Update the status column with new info on specified appId
        var sqlUpdate = "UPDATE apps SET status = @s WHERE appId = @id";
        
        using (var cmd = new SqlCommand(sqlUpdate, conn))
        {
          //Enter appId and status parameters
          cmd.Parameters.AddWithValue("@s", intNewStatus);
          cmd.Parameters.AddWithValue("@id", appId);
          await cmd.ExecuteNonQueryAsync();
        }
      }

      //Returns status
      return newStatus;
    }

    /*
      Brady Brown
      GetDetailsAsync Method
      Given an AppModel, will return advanced details from New Relic and Nagios
      Currently returns all columns available and shows all in detailed view in Monarch app
    */
    public async Task<AppDetails> GetDetailsAsync(AppModel app)
    {
      //Instantiate empty details model
      var details = new AppDetails();

      //Connects to monapi database to get updated data
      using (var conn = new SqlConnection(monapiConn))
      {
        await conn.OpenAsync();

        //Query to take all New Relic data and Nagios data if it exists
        var sql = @"
          SELECT 
            nr.ipAddress as nr_ip,
            nr.status as nr_status, 
            nr.latency as nr_latency, 
            nr.cpuUsage as nr_cpu, 
            nr.throughput as nr_tput, 
            nr.statusUpdateTime as nr_time,
            nr.lastCheck as nr_check,
            
            n.ipAddress as n_ip,
            n.statusUpdateTime as n_time, 
            n.output as n_output, 
            n.perfData as n_perf, 
            n.currentState as n_state,
            n.lastCheck as n_check,
            n.latency as n_latency,
            n.lastStateChange as n_change,
            n.lastTimeUp as n_up,
            n.lastTimeDown as n_down,
            n.lastTimeUnreachable as n_unreach,
            n.lastNotification as n_notif
          FROM 
            (SELECT * FROM newRelicApps WHERE appId = @NrId) as nr
          FULL OUTER JOIN 
            (SELECT * FROM nagiosApps WHERE hostObjectId = @NId) as n
          ON 1=1";
          
        using (var cmd = new SqlCommand(sql, conn))
        {
          cmd.Parameters.AddWithValue("@NrId", app.NewRelicId == null ? DBNull.Value : app.NewRelicId);
          cmd.Parameters.AddWithValue("@NId", app.NagiosId == null ? DBNull.Value : app.NagiosId);
        
          using (var reader = await cmd.ExecuteReaderAsync())
          {
            if (await reader.ReadAsync())
            {
              //Checks if New Relic and Nagios IP address columns exist, and labels as null if not
              string? nrIp = !reader.IsDBNull(reader.GetOrdinal("nr_ip")) ? reader["nr_ip"].ToString() : null;
              string? nIp = !reader.IsDBNull(reader.GetOrdinal("n_ip")) ? reader["n_ip"].ToString() : null;
              
              //Uses New Relic if available, otherwise Nagios, otherwise default
              //This is done to ensure that the app returns a specific ip address, just in case the monitoring systems have a conflict
              details.IpAddress = nrIp ?? nIp ?? "0.0.0.0";

              //Maps New Relic columns to detail model
              //Checks that id is not null and that there is at least a status being shown
              if (app.NewRelicId != null && !reader.IsDBNull(reader.GetOrdinal("nr_status")))
              {
                details.nrStatus = Convert.ToInt32(reader["nr_status"]);
                details.nrLatency = Convert.ToInt32(reader["nr_latency"]);
                details.nrCpuUsage = Convert.ToSingle(reader["nr_cpu"]);
                details.nrThroughput = Convert.ToInt32(reader["nr_tput"]);
                details.nrStatusUpdateTime = Convert.ToDateTime(reader["nr_time"]);
                details.nrLastCheck = Convert.ToDateTime(reader["nr_check"]);
              }

              //Maps Nagios columns to detail model
              //Checks that id is not null and that there is at least a status being shown
              if (app.NagiosId != null && !reader.IsDBNull(reader.GetOrdinal("n_state")))
              {
                details.statusUpdateTime = Convert.ToDateTime(reader["n_time"]);
                details.output = reader["n_output"].ToString() ?? "";
                details.perfData = reader["n_perf"].ToString() ?? "";
                details.currentState = Convert.ToInt32(reader["n_state"]);
                details.lastCheck = Convert.ToDateTime(reader["n_check"]);
                details.lastStateChange = Convert.ToDateTime(reader["n_change"]);
                details.lastTimeUp = Convert.ToDateTime(reader["n_up"]);
                details.lastTimeDown = Convert.ToDateTime(reader["n_down"]);
                details.lastTimeUnreachable = Convert.ToDateTime(reader["n_unreach"]);
                details.lastNotification = Convert.ToDateTime(reader["n_notif"]);
                details.latency = reader["n_latency"].ToString() ?? "";
              }
            }
          }
        }
      }
      //Returns details model
      return details;
    }



    /*
      Brady Brown
      GetAppFiltersAsync Method
      Given an app id, returns a list of all filters associated with the app
    */
    public async Task<List<FilterModel>> GetAppFiltersAsync(int appId)
    {
      //Create empty list of filters
      var filters = new List<FilterModel>();

      //Starts connection to monarch database
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();

        //Queries all filters in filters table where given appId = appFilters appId in appFilter table
        var sql = @"
          SELECT 
            af.filterId,
            f.filterName
          FROM 
            appFilters af
          WHERE 
            af.appId = @appId
          LEFT JOIN
            Filters f ON af.filterId = f.filterId";

        using (var cmd = new SqlCommand(sql, conn))
        {
          //Add app id as parameter for query
          cmd.Parameters.AddWithValue("@appId", appId);

          using (var reader = await cmd.ExecuteReaderAsync())
          {
            while(await reader.ReadAsync())
            {
              //Add all filters to list of filter models
              filters.Add(new FilterModel
              {
                FilterId = reader.GetInt32(0),
                FilterName = reader.GetString(1)
              });
            }
          }
        }
      }
      //Return filled list of filters
      return filters;
    }

    /*
      Brady Brown
      GetAllFiltersAsync Method
      Returns a list of all currently available filters
      Used for filter selection dropdown for app config
    */

    public async Task<List<FilterModel>> GetAllFiltersAsync()
    {
      var filters = new List<FilterModel>();
      string sql = "SELECT filterId, filterName FROM Filters ORDER BY filterName";

      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();
        
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
          filters.Add(new FilterModel
          {
            FilterId = reader.GetInt32(0),
            FilterName = reader.GetString(1)
          });
        }
        return filters;
      }
    }


    /// Conner Hammonds
    /// <summary>
    /// Gets Slack channel keys associated with an app
    /// </summary>
    private async Task<List<string>> GetAppSlackChannelsAsync(int appId)
    {
      var channels = new List<string>();

      try
      {
        using (var connection = new SqlConnection(monarchConn))
        {
          await connection.OpenAsync();
          var query = "SELECT channelKey FROM appSlackChannels WHERE appId = @appId";
          using (var command = new SqlCommand(query, connection))
          {
            command.Parameters.AddWithValue("@appId", appId);
            using (var reader = await command.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                channels.Add(reader.GetString(0));
              }
            }
          }
        }
      }
      catch (SqlException)
      {
        // Table may not exist yet on first run
      }

      return channels;
    }

    /// Conner Hammonds
    /// <summary>
    /// Gets Jira workspace keys associated with an app
    /// </summary>
    private async Task<List<string>> GetAppJiraWorkspacesAsync(int appId)
    {
      var workspaces = new List<string>();

      try
      {
        using (var connection = new SqlConnection(monarchConn))
        {
          await connection.OpenAsync();
          var query = "SELECT workspaceKey FROM appJiraWorkspaces WHERE appId = @appId";
          using (var command = new SqlCommand(query, connection))
          {
            command.Parameters.AddWithValue("@appId", appId);
            using (var reader = await command.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                workspaces.Add(reader.GetString(0));
              }
            }
          }
        }
      }
      catch (SqlException)
      {
        // Table may not exist yet on first run
      }

      return workspaces;
    }
  }
}