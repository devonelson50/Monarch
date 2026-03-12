using Monarch.Models;
using Microsoft.Data.SqlClient;
using Monarch.Components;
using Monarch.Services;

/*  
  Brady Brown
  App Creation Service for Monarch
  Used for information input into Monarch database
  Has few helper methods for creation purposes
  All data entered will go to apps table
*/


namespace Monarch.Services
{

    /*
    AppCreationService Class
    Class that contains all methods related to inserting app information to Monarch database
    Includes the following with more detail on each Method:
      - Helper Methods
        - GetAvailableNewRelicAppsAsync()
        - GetAvailableNagiosAppsAsync()
        - GetUsedNewRelicIdsAsync()
        - GetUsedNewRelicIdsAsync()
      - Creation Method
        - CreateMonarchAppAsync(AppModel)
  */

  public class AppCreationService(string monapiConn, string monarchConn)
  {
    //Monarch and Monapi database connections
    private readonly string monapiConn = monapiConn;
    private readonly string monarchConn = monarchConn;

    /*
      Brady Brown
      GetAvailableNewRelicAppsAsync Method
      Retrieves all New Relic apps currently being monitored in Monapi database
      Returns these as list of NewRelicApp models, to store id & name, sorted alphabetically by name
    */
    public async Task<List<NewRelicApp>> GetAvailableNewRelicAppsAsync()
    {
      //Creates empty New Relic app model list
      var results = new List<NewRelicApp>();

      //Connects to Monapi database
      using (var conn = new SqlConnection(monapiConn))
      {
        await conn.OpenAsync();

        //Query for all ids and names from newRelicApps, sorted by name
        var sql = "SELECT appId, appName FROM newRelicApps ORDER BY appName";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while(await reader.ReadAsync())
          {
            //Add NewRelicApp model to list with results from query
            results.Add(new NewRelicApp
            {
              AppId = reader.GetInt32(0),
              AppName = reader.GetString(1)
            });
          }
        }
      }
      //Return filled list
      return results;
    }

    /*
      Brady Brown
      GetAvailableNagiosAppsAsync Method
      Retrieves all Nagios apps currently being monitored in Monapi database
      Returns these as list of NagiosApp models, to store id & name, sorted alphabetically by name
    */

    public async Task<List<NagiosApp>> GetAvailableNagiosAppsAsync()
    {
      //Creates empty Nagios app model list
      var results = new List<NagiosApp>();

      //Connects to Monapi database
      using (var conn = new SqlConnection(monapiConn))
      {
        await conn.OpenAsync();

        //Query for all ids and names from nagiosApps, sorted by name
        var sql = "SELECT hostObjectId, hostName FROM nagiosApps ORDER BY hostName";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while(await reader.ReadAsync())
          {
            //Add NagiosApp model to list with results from query
            results.Add(new NagiosApp
            {
              AppId = reader.GetInt32(0),
              AppName = reader.GetString(1)
            });
          }
        }
      }
      //Return filled list
      return results;
    }

    /*
      Brady Brown
      GetUsedNewRelicIdsAsync Method
      Retrieves all currently used New Relic ids in Monarch database
      Returns these as list ints
    */

    public async Task<List<int>> GetUsedNewRelicIdsAsync()
    {
      //Instantiate empty list of ints
      var usedIds = new List<int>();

      //Connect to Monarch database
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();

        //Query for all newRelicIds in apps table
        var sql = "SELECT newRelicId FROM apps WHERE newRelicId IS NOT NULL";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          //Loops through all ids in table
          while (await reader.ReadAsync())
          {
            //Adds used ids to list
            usedIds.Add(reader.GetInt32(0));
          }
        }
      }
      //Returns list of all used ids
      return usedIds;
    }

    /*
      Brady Brown
      GetUsedNagiosIdsAsync Method
      Retrieves all currently used New Relic ids in Monarch database
      Returns these as list ints
    */

    public async Task<List<int>> GetUsedNagiosIdsAsync()
    {
      //Instantiate empty list of ints
      var usedIds = new List<int>();

      //Connect to Monarch database
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();

        //Query for all newRelicIds in apps table
        var sql = "SELECT nagiosId FROM apps WHERE nagiosId IS NOT NULL";

        using (var cmd = new SqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          //Loops through all ids in table
          while (await reader.ReadAsync())
          {
            //Adds used ids to list
            usedIds.Add(reader.GetInt32(0));
          }
        }
      }
      //Returns list of all used ids
      return usedIds;
    }

    /*
      Brady Brown
      CreateMonarchAppsAsync Method
      Given an AppModel, adds a row to the apps table in Monarch database
      Only adds name, New Relic id, Nagios id, and unknown status
      Actual status is added via refresh status method (see AppLoadService)
      Other info is added through admin panel configuration
      Returns auto-generated app id
    */

    public async Task<int> CreateMonarchAppAsync(AppModel app)
    {
      //Connection to Monarch database
      using (var conn = new SqlConnection(monarchConn))
      {
        await conn.OpenAsync();

        //Query to add into apps table
        //Also returns auto created id
        var sql = @"
            INSERT INTO apps (
                newRelicId,
                nagiosId,
                appName,
                status
                )
            OUTPUT INSERTED.appId
            VALUES (
                @nrId,
                @nId,
                @name,
                @stat
            )";
        
        using (var cmd = new SqlCommand(sql, conn))
        {
          //Adds all app model data to query
          cmd.Parameters.AddWithValue("@name", app.AppName);
          //3 is used for unknown status, refresh status will update to proper status
          cmd.Parameters.AddWithValue("@stat", 3);
          cmd.Parameters.AddWithValue("@nrId",
            app.NewRelicId == null ? DBNull.Value : app.NewRelicId);
          cmd.Parameters.AddWithValue("@nId",
            app.NagiosId == null ? DBNull.Value : app.NagiosId);

          //Gets auto id result
          var result = await cmd.ExecuteScalarAsync();

          //Convert auto id into int for better referencing and returns
          return Convert.ToInt32(result);
        }
      }
    }
  }
}