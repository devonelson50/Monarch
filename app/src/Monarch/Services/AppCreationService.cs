using Monarch.Models;
using Microsoft.Data.SqlClient;

namespace Monarch.Services
{
  public class AppCreationService(string monapiConn, string monarchConn)
  {
    private readonly string _monapiConnectionString = monapiConn;
    private readonly string _monarchConnectionString = monarchConn;

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
              AppId = reader.GetInt32(0),
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
              AppId = reader.GetInt32(0),
              AppName = reader.GetString(1)
            });
          }
        }
      }
      return results;
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
          cmd.Parameters.AddWithValue("@stat", "Unknown");
          
          cmd.Parameters.AddWithValue("@nrId",
            app.NewRelicId.HasValue ? app.NewRelicId.Value : DBNull.Value);
          cmd.Parameters.AddWithValue("@nId",
            app.NagiosId.HasValue ? app.NagiosId.Value : DBNull.Value);

          await cmd.ExecuteNonQueryAsync();
        }
      }
    }
  }
}