using Monarch.Models;
using Microsoft.Data.SqlClient;

namespace Monarch.Services
{
  public class AppCreationService(string monapiConn, string monarchConn)
  {
    private readonly string _monapiConnectionString = monapiConn;
    private readonly string _monarchConnectionString = monarchConn;

    public async Task<List<AppModel>> GetMonarchAppsAsync()
      {
          var apps = new List<AppModel>();

          using (var conn = new SqlConnection(_monarchConnectionString))
          {
              await conn.OpenAsync();

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
                      smtpAlert 
                  FROM apps";

              using (var cmd = new SqlCommand(sql, conn))
              using (var reader = await cmd.ExecuteReaderAsync())
              {
                  while (await reader.ReadAsync())
                  {
                      string dbStatusString = reader.GetString(2);
                      int dbStatusInt = Convert.ToInt32(dbStatusString);

                      apps.Add(new AppModel
                      {
                          AppId = reader.GetInt32(0),
                          AppName = reader.GetString(1),
                          Status = (StatusType)dbStatusInt,

                          // Handle Nullables safely
                          NewRelicId = reader.IsDBNull(3) ? null : reader.GetString(3),
                          NagiosId = reader.IsDBNull(4) ? null : reader.GetString(4),
                          MostRecentIncidentId = reader.IsDBNull(5) ? null : reader.GetString(5),

                          // Handle Booleans
                          SlackAlert = reader.GetBoolean(6),
                          JiraAlert = reader.GetBoolean(7),
                          SmtpAlert = reader.GetBoolean(8)
                      });
                  }
              }
          }
          return apps;
      }
    
    public async Task<List<NewRelicApp>> GetAvailableNewRelicAppsAsync()
    {
      var results = new List<NewRelicApp>();

      using (var conn = new SqlConnection(_monapiConnectionString))
      {
        await conn.OpenAsync();

        var sql = "SELECT appId, appName FROM newRelicApps ORDER BY appName";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while(await reader.ReadAsync())
          {
            results.Add(new NewRelicApp
            {
              AppId = reader.GetInt32(0).ToString(),
              AppName = reader.GetString(1)
            });
          }
        }
      }
      return results;
    }

    public async Task<List<NagiosApp>> GetAvailableNagiosAppsAsync()
    {
      var results = new List<NagiosApp>();

      using (var conn = new SqlConnection(_monapiConnectionString))
      {
        await conn.OpenAsync();

        var sql = "SELECT hostObjectId, hostName FROM nagiosApps ORDER BY hostName";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while(await reader.ReadAsync())
          {
            results.Add(new NagiosApp
            {
              AppId = reader.GetInt32(0).ToString(),
              AppName = reader.GetString(1)
            });
          }
        }
      }
      return results;
    }

    public async Task<List<string>> GetUsedIdsAsync()
    {
      var usedIds = new List<string>();
      using (var conn = new SqlConnection(_monarchConnectionString))
      {
        await conn.OpenAsync();
        var sql = "SELECT newRelicId FROM apps WHERE newRelicId IS NOT NULL " +
                  "UNION " +
                  "SELECT CAST(nagiosId AS VARCHAR) FROM apps WHERE nagiosId IS NOT NULL";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while (await reader.ReadAsync())
          {
            usedIds.Add(reader[0].ToString()!);
          }
        }
      }
      return usedIds;
    }

    public async Task CreateMonarchAppAsync(AppModel app)
    {
      using (var conn = new SqlConnection(_monarchConnectionString))
      {
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO apps (
                newRelicId,
                nagiosId,
                appName,
                status
                )
            VALUES (
                @nrId,
                @nId,
                @name,
                @stat
            )";
        
        using (var cmd = new SqlCommand(sql, conn))
        {
          cmd.Parameters.AddWithValue("@name", app.AppName);
          cmd.Parameters.AddWithValue("@stat", 0);
          
          cmd.Parameters.AddWithValue("@nrId",
            String.IsNullOrEmpty(app.NewRelicId) ? DBNull.Value : app.NewRelicId);
          cmd.Parameters.AddWithValue("@nId",
            String.IsNullOrEmpty(app.NagiosId) ? DBNull.Value : app.NagiosId);

          await cmd.ExecuteNonQueryAsync();
        }
      }
    }
    public async Task<AppDetails> GetDetailsAsync(AppModel app)
    {
        var details = new AppDetails();

        using (var conn = new SqlConnection(_monapiConnectionString))
        {
          await conn.OpenAsync();

          var sql = @"
            SELECT 
                -- New Relic Columns (Prefix with nr_)
                nr.ipAddress as nr_ip,
                nr.status as nr_status, 
                nr.latency as nr_latency, 
                nr.cpuUsage as nr_cpu, 
                nr.throughput as nr_tput, 
                nr.statusUpdateTime as nr_time,
                nr.lastCheck as nr_check,
                
                -- Nagios Columns (Prefix with n_)
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
            cmd.Parameters.AddWithValue("@NrId", String.IsNullOrEmpty(app.NewRelicId) ? DBNull.Value : app.NewRelicId);
            cmd.Parameters.AddWithValue("@NId", String.IsNullOrEmpty(app.NagiosId) ? DBNull.Value : app.NagiosId);
          
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    // --- 1. IP Address Fallback Logic ---
                    string nrIp = !reader.IsDBNull(reader.GetOrdinal("nr_ip")) ? reader["nr_ip"].ToString() : null;
                    string nIp = !reader.IsDBNull(reader.GetOrdinal("n_ip")) ? reader["n_ip"].ToString() : null;
                    
                    // Use NR if available, otherwise Nagios, otherwise default
                    details.IpAddress = nrIp ?? nIp ?? "0.0.0.0";

                    // --- 2. New Relic Mapping ---
                    if (!string.IsNullOrEmpty(app.NewRelicId) && !reader.IsDBNull(reader.GetOrdinal("nr_status")))
                    {
                        details.nrStatus = Convert.ToInt32(reader["nr_status"]);
                        details.nrLatency = Convert.ToInt32(reader["nr_latency"]);
                        details.nrCpuUsage = Convert.ToSingle(reader["nr_cpu"]);
                        details.nrThroughput = Convert.ToInt32(reader["nr_tput"]);
                        details.nrStatusUpdateTime = Convert.ToDateTime(reader["nr_time"]);
                        details.nrLastCheck = Convert.ToDateTime(reader["nr_check"]);
                    }

                    // --- 3. Nagios Mapping ---
                    if (!string.IsNullOrEmpty(app.NagiosId) && !reader.IsDBNull(reader.GetOrdinal("n_state")))
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
        return details;
    }

    public async Task<AppModel> RefreshAppStatusAsync(int appId)
    {
        string appName = "";
        string? nrId = null;
        string? nagiosId = null;

        // --- STEP 1: Get App Config from MONARCH DB ---
        using (var conn = new SqlConnection(_monarchConnectionString))
        {
            await conn.OpenAsync();
            var sql = "SELECT appName, newRelicId, nagiosId FROM apps WHERE appId = @id";
            
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", appId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        appName = reader["appName"].ToString();
                        if (!reader.IsDBNull(reader.GetOrdinal("newRelicId")))
                            nrId = reader["newRelicId"].ToString();
                        if (!reader.IsDBNull(reader.GetOrdinal("nagiosId")))
                            nagiosId = reader["nagiosId"].ToString();
                    }
                }
            }
        }

        // --- STEP 2: Get Live Status from MONAPI DB ---
        int nrStatus = -1;
        int nagiosState = -1;

        using (var conn = new SqlConnection(_monapiConnectionString))
        {
            await conn.OpenAsync();

            // Check New Relic
            if (!string.IsNullOrEmpty(nrId))
            {
                var sqlNr = "SELECT status FROM newRelicApps WHERE appId = @nrId";
                using (var cmd = new SqlCommand(sqlNr, conn))
                {
                    // Ensure we pass the ID as the right type (int vs string)
                    // Assuming your New Relic ID in Monapi is INT based on previous schema
                    if (int.TryParse(nrId, out int nrIdInt))
                    {
                        cmd.Parameters.AddWithValue("@nrId", nrIdInt);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            nrStatus = Convert.ToInt32(result);
                    }
                }
            }

            // Check Nagios
            if (!string.IsNullOrEmpty(nagiosId))
            {
                var sqlNagios = "SELECT currentState FROM nagiosApps WHERE hostObjectId = @nId";
                using (var cmd = new SqlCommand(sqlNagios, conn))
                {
                    if (int.TryParse(nagiosId, out int nIdInt))
                    {
                        cmd.Parameters.AddWithValue("@nId", nIdInt);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            nagiosState = Convert.ToInt32(result);
                    }
                }
            }
        }

        // --- STEP 3: Calculate Worst Case Logic (0=Good) ---
        var newStatus = StatusType.Operational; 

        // Check Nagios (0=OK, 1=Warn, 2=Crit)
        if (nagiosState == 2) newStatus = StatusType.Outage;
        else if (nagiosState == 1 && newStatus != StatusType.Outage) newStatus = StatusType.DegradedPerformance;

        // Check New Relic (Assuming 0=Good, 2=Bad)
        if (nrStatus >= 2) newStatus = StatusType.Outage;
        else if (nrStatus == 1 && newStatus != StatusType.Outage) newStatus = StatusType.DegradedPerformance;


        // --- STEP 4: Update MONARCH DB with new Status ---
        using (var conn = new SqlConnection(_monarchConnectionString))
        {
            await conn.OpenAsync();
            // Update the status column (Schema says it's VARCHAR)
            var sqlUpdate = "UPDATE apps SET status = @s WHERE appId = @id";
            
            using (var cmd = new SqlCommand(sqlUpdate, conn))
            {
                // Convert Enum to Int String ("0", "1", "2")
                cmd.Parameters.AddWithValue("@s", ((int)newStatus).ToString());
                cmd.Parameters.AddWithValue("@id", appId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        return new AppModel 
        { 
            AppId = appId, 
            AppName = appName, 
            Status = newStatus,
            NewRelicId = nrId,
            NagiosId = nagiosId
        };
    }
  }
}