using System.Collections;
using System.Threading.Tasks;
using System.Text.Json;
using RestSharp;

namespace Monapi.Worker.Nagios;

public class NagiosConnector
{

    private readonly JsonNode nagiosApiDetails;
    private readonly String monapiKey;

    public NagiosConnector()
    {
        this.nagiosApiDetails = JsonNode.Parse(File.ReadAllText("/run/secrets/monarch_nagios_api_details"));
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password");
        List<NagiosApp> apps = GetApps();
        this.WriteToDatabase(apps);
    }

    private async Task<List<NagiosApp>> GetApps()
    {
        var apps = new List<NagiosApp>();
        var uri = ""; // parse key and hostname from secret
        do
        {

        } while ();

        return apps;
    }
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
                    command.Parameters.AddWithValue("@appId", app.appId);
                    command.Parameters.AddWithValue("@appName", app.appName);
                    command.Parameters.AddWithValue("@status", app.status);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
}
