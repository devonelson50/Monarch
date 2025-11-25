using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using RestSharp;

namespace Monapi.Worker.Nagios;

/// <summary>
/// Devon Nelson
/// 
/// Retrieve and abstract Nagios data, insert into monapi database.
/// </summary>
public class NagiosConnector
{

    private readonly JsonNode nagiosApiDetails;
    private readonly String monapiKey;

    public NagiosConnector()
    {
        this.nagiosApiDetails = JsonNode.Parse(File.ReadAllText("/run/secrets/monarch_nagios_api_details"));
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password");
    }
    
    /// <summary>
    /// Complete a connector loop
    /// </summary>
    /// <returns></returns>
    public async Task RunConnector()
    {
        List<NagiosApp> apps = await GetApps();
        await this.WriteToDatabase(apps);
    }

    /// <summary>
    /// Retrieves list of nagios applications from nagios API
    /// </summary>
    /// <returns>Dynamic list of abstracted applications</returns>
    /// <exception cref="Exception"></exception>
    private async Task<List<NagiosApp>> GetApps()
    {
        var apps = new List<NagiosApp>();
        var uri = BuildConnectionString(); // parse key and hostname JsonNode

        var options = new RestClientOptions(uri);
        var client = new RestClient(options);
        var request = new RestRequest();
        var response = await client.ExecuteAsync(request);

        if (!(response.IsSuccessful))
        {
            throw new Exception("API request failed.");
        }
        else
        {
            JsonNode jNode = JsonNode.Parse(response.Content);
            foreach (var app in jNode["hoststatus"].AsArray())
            {
                apps.Add(new NagiosApp
                {
                    AppId = app["host_id"].ToString(),
                    AppName = app["host_name"].ToString(),
                    Status = app["current_state"].ToString()
                });
            }
        }
        return apps;
    }
    /// <summary>
    /// Writes application list to monapi database
    /// </summary>
    /// <param name="apps">Dynamic list of NagiosApp objects</param>
    /// <returns></returns>
    private async Task WriteToDatabase(List<NagiosApp> apps)
    {
        var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={monapiKey};TrustServerCertificate=True;";
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            foreach (NagiosApp app in apps)
            {
                var query = "INSERT INTO nagiosApps (appId, appName, status) VALUES (@appId, @appName, @status)";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@appId", app.AppId);
                    command.Parameters.AddWithValue("@appName", app.AppName);
                    command.Parameters.AddWithValue("@status", app.Status);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }

    /// <summary>
    /// Builds the uri using details parsed from JsonNode secret data
    /// </summary>
    /// <returns>Uri as string</returns>
    private string BuildConnectionString()
    {
        // NOTE: Parsed JsonNode structure allows non-standard port declaration.
        // Non-standard port can be added to the hostname value. i.e. "nagios.interstatestudio.com:123"
        var uri = "";
        if (this.nagiosApiDetails["TLS"].ToString().ToLower() == "true")
        {
            uri += "https://";
        }
        else
        {
            uri += "http://";
        }
        uri += this.nagiosApiDetails["Hostname"].ToString();
        uri += "/nagiosxi/api/v1/objects/hoststatus?apikey=";
        uri += this.nagiosApiDetails["ApiKey"].ToString();
        uri += "&pretty=1";

        return uri;
    }
}
