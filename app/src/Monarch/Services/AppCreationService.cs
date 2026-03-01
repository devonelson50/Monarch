using Monarch.Models;
using Microsoft.Data.SqlClient;
using Monarch.Components;
using Monarch.Services;



namespace Monarch.Services
{
  public class AppCreationService(string monapiConn, string monarchConn)
  {
    private readonly string monapiConn = monapiConn;
    private readonly string monarchConn = monarchConn;

    
    public async Task<List<NewRelicApp>> GetAvailableNewRelicAppsAsync()
    {
      var results = new List<NewRelicApp>();

      using (var conn = new SqlConnection(monapiConn))
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

      using (var conn = new SqlConnection(monapiConn))
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
      using (var conn = new SqlConnection(monarchConn))
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
      using (var conn = new SqlConnection(monarchConn))
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
            app.NewRelicId == null ? DBNull.Value : app.NewRelicId);
          cmd.Parameters.AddWithValue("@nId",
            app.NewRelicId == null ? DBNull.Value : app.NagiosId);

          await cmd.ExecuteNonQueryAsync();
        }
      }
    }
  }
}