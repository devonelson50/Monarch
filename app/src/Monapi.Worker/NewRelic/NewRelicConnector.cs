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
        ArrayList apps = GetApps();
        this.WriteToDatabse(apps);
    }

    public async Task<ArrayList> GetApps()
    {
        var apps = new ArrayList();
        var continueLoop = true;
        do
        {
            var uri = "https://api.newrelic.com/v2/applications.json";
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
                var content = System.Text.Json.JsonSerializer.Deserialize<ResponseData>(response.Content);
                foreach (var app in content.applications)
                {
                    apps.Add(new NewRelicApp
                    {
                        AppId = app.id,
                        AppName = app.name,
                        Status = app.health_status
                    });
                }


            }
        } while (continueLoop);








        return apps;
    }

    public void WriteToDatabase(ArrayList apps)
    {

    }
}