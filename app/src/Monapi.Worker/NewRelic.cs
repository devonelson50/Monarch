using System.Collections;
using System.Threading.Tasks;
using RestSharp;

namespace Monapi.Worker;

// https://api.newrelic.com/docs/#
public class NewRelic
{
    private readonly String apiKey;

    public NewRelic()
    {
        this.apiKey = File.ReadAllText("/run/secrets/monarch_newrelic_api_key");
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password");
        var apps = GetApps();
        WriteToDatabse(apps);

    }


    public async Task<ArrayList> GetApps()
    {
        var apps = new ArrayList();
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
                var content = response.Content;    
            }

        } while (true);








            return apps;
    }
    
    public void WriteToDatabase(ArrayList apps)
    {
        
    }
}