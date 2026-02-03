using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using RestSharp;

namespace Monapi.Worker.NewRelic;

/// <summary>
/// Devon Nelson & Brady Brown
///
/// Connect to New Relic API, retrieve an up to date application list,
/// and write it to the database.
/// </summary>


public class NewRelicConnector
{
    private readonly String apiKey;
    private readonly String monapiKey;
    private readonly int accountId;

    public NewRelicConnector()
    {
        var accountIdText = File.ReadAllText("/run/secrets/newrelic_account_number").Trim();
        this.accountId = int.Parse(accountIdText);
        this.apiKey = File.ReadAllText("/run/secrets/monarch_newrelic_api_key").Trim();
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password").Trim();
    }

    /// <summary>
    /// Complete a connector loop
    /// </summary>
    /// <returns></returns>
    public async Task RunConnector()
    {
        Console.WriteLine("DEBUG: Calling New Relic API...");

        List<NewRelicApp> apps = await GetApps();
        
        Console.WriteLine($"DEBUG: New Relic returned {apps.Count} apps.");

        if (apps.Count > 0)
        {
            await this.WriteToDatabase(apps);
        }
        else
        {
            Console.WriteLine("DEBUG: Skipping Database write because 0 apps were found.");
        }
    }

    /// <summary>
    /// Retrieve a list of NewRelic applications from the API
    /// </summary>
    /// <returns>Dynamic list of abstracted applications</returns>
    /// <exception cref="Exception"></exception>
    private async Task<List<NewRelicApp>> GetApps()
    {
        var apps = new List<NewRelicApp>();
        var uri = $"https://api.newrelic.com/graphql";
        var options = new RestClientOptions(uri)
        {
            ThrowOnAnyError = true,
            Timeout = TimeSpan.FromSeconds(15)
        };

        var client = new RestClient(options);
        
        var query = @"
        {
            actor {
                account(id: " + accountId + @") {
                    nrql(query: ""SELECT latest(hostObjectId) as id, latest(ipAddress) as ip, latest(currentState) as state, latest(latency) as lat, latest(cpuUsage) as cpu, latest(throughput) as tput, latest(output) as msg, latest(statusUpdateTime) as updated FROM Metric FACET hostName LIMIT MAX SINCE 5 minutes ago"") {
                        results
                    }
                }
            }
        }";

        var request = new RestRequest("", Method.Post);
        request.AddHeader("Api-Key", $"{this.apiKey}");
        request.AddJsonBody(new { query = query });

        var response = await client.ExecuteAsync(request);

        if (!(response.IsSuccessful))
        {
            throw new Exception($"API Error: {response.StatusCode} - {response.Content}");
        }
        else
        {
            var jNode = JsonNode.Parse(response.Content!);
            var results = jNode?["data"]?["actor"]?["account"]?["nrql"]?["results"]?.AsArray();

            if (results != null)
            {
                foreach (var result in results)
                {
                    var hostName = result["facet"]?.ToString() ?? "Unknown";
                    
                    apps.Add(new NewRelicApp
                    {
                        AppName = hostName,
                        AppId = Convert.ToInt32(double.Parse(result["id"]?.ToString() ?? "0")),
                        IpAddress = result["ip"]?.ToString() ?? "0.0.0.0",
                        Status = Convert.ToInt32(double.Parse(result["state"]?.ToString() ?? "0")),
                        Latency = Convert.ToInt32(result["lat"]?.ToString() ?? "0"),
                        CpuUsage = double.Parse(result["cpu"]?.ToString() ?? "0.0"),
                        Throughput = Convert.ToInt32(double.Parse(result["tput"]?.ToString() ?? "0")),
                        Output = result["msg"]?.ToString() ?? "",
                        StatusUpdateTime =  DateTime.TryParse(result["updated"]?.ToString(), out var dt) ? dt : DateTime.Now
                    });
                }
            }
        }

        return apps;
    }

    /// <summary>
    /// Writes application list to the monapi database
    /// </summary>
    /// <param name="apps">Dynamic list of NewRelicApp objects</param>
    /// <returns></returns>
    public async Task WriteToDatabase(List<NewRelicApp> apps)
    {     
        var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={monapiKey};TrustServerCertificate=False;";
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            if (apps.IsNullOrEmpty())
            {
                Console.WriteLine("DEBUG: Apps list is Empty");
            }
            foreach (var app in apps)
            {
                int rowsUpdated = 0;
                var updateQuery = "UPDATE newRelicApps SET appName=@name, ipAddress=@ip, status=@state, latency=@lat, cpuUsage=@cpu, throughput=@tput, output=@msg, statusUpdateTime=@updated, lastCheck=@check WHERE appId=@id";

                await using (var command = new SqlCommand(updateQuery, connection))
                {
                    AddQueryParameters(command, app);
                    rowsUpdated = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    if (rowsUpdated == 0)
                    {
                        command.CommandText = "INSERT INTO newRelicApps (appId, appName, ipAddress, status, latency, cpuUsage, throughput, output, statusUpdateTime, lastCheck) VALUES (@id, @name, @ip, @state, @lat, @cpu, @tput, @msg, @updated, @check)";
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }

        private void AddQueryParameters(SqlCommand command, NewRelicApp app)
    {
        command.Parameters.AddWithValue("@id", app.AppId);
        command.Parameters.AddWithValue("@name", app.AppName);
        command.Parameters.AddWithValue("@ip", app.IpAddress);
        command.Parameters.AddWithValue("@state", app.Status);
        command.Parameters.AddWithValue("@lat", app.Latency);
        command.Parameters.AddWithValue("@cpu", app.CpuUsage);
        command.Parameters.AddWithValue("@tput", app.Throughput);
        command.Parameters.AddWithValue("@msg", app.Output);
        command.Parameters.AddWithValue("@updated", app.StatusUpdateTime);
        command.Parameters.AddWithValue("@check", System.DateTime.Now);
    }

}