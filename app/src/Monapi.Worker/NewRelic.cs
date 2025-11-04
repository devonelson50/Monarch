using System.Collections;

namespace Monapi.Worker;

public class NewRelic
{
    private readonly String apiKey;

    public NewRelic()
    {
        this.apiKey = File.ReadAllText("/run/secrets/monarch_newrelic_api_key");

    }
    public ArrayList GetApps()
    {
        var apps = new ArrayList();
        var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://api.newrelic.com/v2/applications.json");
        request.Method = "GET";
        request.Headers["X-Api-Key"] = this.apiKey;

    }
}