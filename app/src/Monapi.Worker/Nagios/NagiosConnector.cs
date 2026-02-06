using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using RestSharp;

namespace Monapi.Worker.Nagios;

/// <summary>
/// Devon Nelson
/// 
/// Retrieve  Nagios data, insert into monapi database.
/// </summary>
public class NagiosConnector
{
    private readonly String nagiosApiKey;
    private readonly String monapiKey;
    private readonly String nagiosRequestUri;

    /// <summary>
    /// Constructor includes prep work that only needs to run once each time the monapi
    /// container starts.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public NagiosConnector()
    {
        this.nagiosApiKey = File.ReadAllText("/run/secrets/monarch_nagios_api_details");
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password");
        this.nagiosRequestUri = Environment.GetEnvironmentVariable("nagios_uri") ?? "";
        if (string.IsNullOrWhiteSpace(this.nagiosRequestUri))
        {
            throw new Exception("'nagios_uri' not set in Environment Variables.");
        }
        this.nagiosRequestUri += "api/v1/objects/hoststatus?apikey=" + this.nagiosApiKey;
    }

    /// <summary>
    /// Modifies the SqlCommand object to include data from REST API call. 
    /// This helper function exists to make RunConnector() easier to follow, and to
    /// potentially help avoid duplicate code blocks depending on future changes to
    /// RunConnector().
    /// </summary>
    /// <param name="command"></param>
    /// <param name="app"></param>
    private void AddQueryParameters(SqlCommand command, JsonNode app)
    {
        command.Parameters.AddWithValue("@hostObjectId", int.Parse(app["host_object_id"]?.ToString() ?? "0"));
        command.Parameters.AddWithValue("@currentState", int.Parse(app["current_state"]?.ToString() ?? "0"));
        command.Parameters.AddWithValue("@hostName", app["host_name"]?.GetValue<string>() ?? "");
        command.Parameters.AddWithValue("@displayName", app["display_name"]?.GetValue<string>() ?? "");
        command.Parameters.AddWithValue("@ipAddress", app["address"]?.GetValue<string>() ?? "");
        command.Parameters.AddWithValue("@output", app["output"]?.GetValue<string>() ?? "");
        command.Parameters.AddWithValue("@perfData", app["perf_data"]?.GetValue<string>() ?? "");
        command.Parameters.AddWithValue("@latency", app["latency"]?.ToString() ?? "0");
        command.Parameters.AddWithValue("@statusUpdateTime", DateTime.Parse(app["status_update_time"]?.ToString() ?? DateTime.MinValue.ToString()));
        command.Parameters.AddWithValue("@lastCheck", DateTime.Parse(app["last_check"]?.ToString() ?? DateTime.MinValue.ToString()));
        command.Parameters.AddWithValue("@lastStateChange", DateTime.Parse(app["last_state_change"]?.ToString() ?? DateTime.MinValue.ToString()));
        command.Parameters.AddWithValue("@lastTimeUp", DateTime.Parse(app["last_time_up"]?.ToString() ?? DateTime.MinValue.ToString()));
        command.Parameters.AddWithValue("@lastTimeDown", DateTime.Parse(app["last_time_down"]?.ToString() ?? DateTime.MinValue.ToString()));
        command.Parameters.AddWithValue("@lastTimeUnreachable", DateTime.Parse(app["last_time_unreachable"]?.ToString() ?? DateTime.MinValue.ToString()));
        command.Parameters.AddWithValue("@lastNotification", DateTime.Parse(app["last_notification"]?.ToString() ?? DateTime.MinValue.ToString()));
    }

    /// <summary>
    /// NagiosConnector's primary loop. This function makes an API call to the configured nagiosxi host, pulls hoststatus data,
    /// and writes to the monapi.nagiosApps table.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task RunConnector()
    {
        // API Request
        var client = new RestClient(new RestClientOptions(this.nagiosRequestUri));
        var response = await client.ExecuteAsync(new RestRequest());
        if (!(response.IsSuccessful))
        {
            throw new Exception("API request failed.");
        }

        // SQL
        //
        // Attempt to update an existing line, if no changes are made by the update query, prepare an insert query.
        // This replaces the prototype's logic, which would delete all rows and re-insert, causing very short periods of time
        // in which the table would be empty or re-populating. 
        await using (var sqlConnection = new SqlConnection($"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={monapiKey};TrustServerCertificate=False;"))
        {
            await sqlConnection.OpenAsync();
            JsonNode jNode = JsonNode.Parse(response.Content);
            var hostList = jNode["hoststatus"]?.AsArray();
            if (hostList == null)
            {
                Console.WriteLine("WARN: Host list is empty!");
            }
            foreach (var app in hostList)
            {
                int rowsUpdated = 0;
                var updateQuery = "UPDATE nagiosApps SET hostName=@hostName, displayName=@displayName, ipAddress=@ipAddress, statusUpdateTime=@statusUpdateTime, output=@output, perfData=@perfData, currentState=@currentState, lastCheck=@lastCheck, lastStateChange=@lastStateChange, lastTimeUp=@lastTimeUp, lastTimeDown=@lastTimeDown, lastTimeUnreachable=@lastTimeUnreachable, lastNotification=@lastNotification, latency=@latency WHERE hostObjectId=@hostObjectId";

                await using (var command = new SqlCommand(updateQuery, sqlConnection))
                {
                    AddQueryParameters(command, app);
                    rowsUpdated = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    if (rowsUpdated == 0)
                    {
                        command.CommandText = "INSERT INTO nagiosApps (hostObjectId, hostName, displayName, ipAddress, statusUpdateTime, output, perfData, currentState, lastCheck, lastStateChange, lastTimeUp, lastTimeDown, lastTimeUnreachable, lastNotification, latency) VALUES (@hostObjectId, @hostName, @displayName, @ipAddress, @statusUpdateTime, @output, @perfData, @currentState, @lastCheck, @lastStateChange, @lastTimeUp, @lastTimeDown, @lastTimeUnreachable, @lastNotification, @latency)";
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }

            }
        }
    }
}