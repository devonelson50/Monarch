using System.Collections;
using System.Threading.Tasks;
using System.Text.Json;
using RestSharp;

namespace Monapi.Worker.NewRelic;

// https://api.newrelic.com/docs/#
// Upon instantiation, complete a repetitive loop to handle retrieving paginated
// application data from New Relic's API. Retrieved data is abstracted before being
// written to the monapi database.
public class NewRelicConnector
{
    private readonly String apiKey;
    private readonly String monapiKey;

    public NewRelicConnector()
    {
        this.apiKey = File.ReadAllText("/run/secrets/monarch_newrelic_api_key");
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password");
        List<NewRelicApp> apps = GetApps();
        this.WriteToDatabase(apps);
    }

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
                uri = jNode["link"]["next"]; // Get the next page, if null terminate the loop
            }
        } while (uri != null);

        return apps;
    }

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
                    command.Parameters.AddWithValue("@appId", app.appId);
                    command.Parameters.AddWithValue("@appName", app.appName);
                    command.Parameters.AddWithValue("@status", app.status);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}