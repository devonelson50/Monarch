using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using RestSharp;

namespace Monapi.Worker.NewRelic;

public class NewRelicConnector
{
    private readonly String apiKey;
    private readonly String monapiKey;

    public NewRelicConnector()
    {
        this.apiKey = File.ReadAllText("/run/secrets/monarch_newrelic_api_key");
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password");
    }

    /// <summary>
    /// Complete a connector loop
    /// </summary>
    /// <returns></returns>
    public async Task RunConnector()
    {
        List<NewRelicApp> apps = await GetApps();
        await this.WriteToDatabase(apps);
    }

    /// <summary>
    /// Retrieve a list of NewRelic applications from the API
    /// </summary>
    /// <returns>Dynamic list of abstracted applications</returns>
    /// <exception cref="Exception"></exception>
    private async Task<List<NewRelicApp>> GetApps()
    {
        var apps = new List<NewRelicApp>();
        var uri = "https://api.newrelic.com/v2/applications.json";
        do
        {
            var options = new RestClientOptions(uri);
            var client = new RestClient(options);
            var request = new RestRequest();
            request.AddHeader("Api-Key", this.apiKey);
            var response = await client.ExecuteAsync(request);

            if (!(response.IsSuccessful))
            {
                throw new Exception("API request failed.");
            }
            else
            {
                JsonNode jNode = JsonNode.Parse(response.Content);
                foreach (var app in jNode["applications"].AsArray())
                {
                    apps.Add(new NewRelicApp
                    {
                        AppId = app["id"].ToString(),
                        AppName = app["name"].ToString(),
                        Status = app["health_status"].ToString()
                    });
                }
                uri = jNode["link"]["next"].ToString(); // Get the next page, if null terminate the loop
            }
        } while (uri != null);

        return apps;
    }

    /// <summary>
    /// Writes application list to the monapi database
    /// </summary>
    /// <param name="apps">Dynamic list of NewRelicApp objects</param>
    /// <returns></returns>
    private async Task WriteToDatabase(List<NewRelicApp> apps)
    {
        var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={monapiKey};TrustServerCertificate=True;";
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            foreach (NewRelicApp app in apps)
            {
                var query = "INSERT INTO newRelicApps (appId, appName, status) VALUES (@appId, @appName, @status)";
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
}