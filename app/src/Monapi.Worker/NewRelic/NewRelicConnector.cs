using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
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
    private readonly String accountId;

    public NewRelicConnector()
    {
        this.accountId = File.ReadAllText("/run/secrets/monarch_simulator_account_number").Trim();
        this.apiKey = File.ReadAllText("/run/secrets/monarch_newrelic_api_key").Trim();
        this.monapiKey = File.ReadAllText("/run/secrets/monarch_sql_monapi_password").Trim();
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
        var uri = "https://api.newrelic.com/v1/accounts/{accountId}/query";
        do
        {
            var options = new RestClientOptions(uri);
            var client = new RestClient(options);
            var request = new RestRequest();
            request.AddHeader("Api-Key", this.apiKey);
            
            ///Query sent to New Relic
            string nrql = @"SELECT 
            latest(hostObjectId) as id, 
            latest(ipAddress) as ip, 
            latest(currentState) as state, 
            latest(latency) as lat, 
            latest(cpuUsage) as cpu, 
            latest(throughput) as tput, 
            latest(output) as msg,
            latest(statusUpdateTime) as updated
            FROM Metric FACET hostName LIMIT MAX SINCE 5 minutes ago";

            request.AddQueryParameter("nrql", nrql);

            var response = await client.ExecuteAsync(request);

            if (!(response.IsSuccessful))
            {
                throw new Exception("New Relic API Error: {response.Content}");
            }
            else
            {
                var jNode = JsonNode.Parse(response.Content!);
                var results = jNode?["results"]?[0]?["facets"]?.AsArray();

        if (results != null)
        {
            foreach (var facet in results)
            {
                var data = facet["results"];
                apps.Add(new NewRelicApp
                {
                    AppName = facet["name"]?.ToString() ?? "Unknown",
                    AppId = int.Parse(data?[0]?["latest"]?.ToString() ?? "0"),
                    IpAddress = data?[1]?["latest"]?.ToString() ?? "0.0.0.0",
                    Status = int.Parse(data?[2]?["latest"]?.ToString() ?? "0"),
                    Latency = data?[3]?["latest"]?.ToString() ?? "0ms",
                    CpuUsagePercent = double.Parse(data?[4]?["latest"]?.ToString() ?? "0"),
                    ThroughputRpm = int.Parse(data?[5]?["latest"]?.ToString() ?? "0"),
                    Output = data?[6]?["latest"]?.ToString() ?? "",
                    StatusUpdateTime = DateTime.TryParse(data?[7]?["latest"]?.ToString(), out var dt) ? dt : DateTime.Now
                });
            }
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
    public async Task WriteToDatabase(List<NewRelicApp> apps)
    {
        var connectionString = $"Server=sqlserver,1433;Database=monapi;User Id=monapi;Password={monapiKey};TrustServerCertificate=True;";
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            //Transaction to ensure there is no period with missing data
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var deleteQuery = "DELETE FROM newRelicApps";
                    using (var deleteCmd = new SqlCommand(deleteQuery, connection, transaction))
                    {
                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    var insertQuery = @"INSERT INTO newRelicApps 
                        (hostObjectId, hostName, ipAddress, currentState, latency, cpuUsage_percent, throughput_rpm, output, statusUpdateTime, lastCheck) 
                        VALUES (@id, @name, @ip, @state, @lat, @cpu, @tput, @msg, @updated, GETDATE())";

                    foreach (var app in apps)
                    {
                        using (var insertCmd = new SqlCommand(insertQuery, connection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@id", app.AppId);
                            insertCmd.Parameters.AddWithValue("@name", app.AppName);
                            insertCmd.Parameters.AddWithValue("@ip", app.IpAddress);
                            insertCmd.Parameters.AddWithValue("@state", app.Status);
                            insertCmd.Parameters.AddWithValue("@lat", app.Latency);
                            insertCmd.Parameters.AddWithValue("@cpu", app.CpuUsagePercent);
                            insertCmd.Parameters.AddWithValue("@tput", app.ThroughputRpm);
                            insertCmd.Parameters.AddWithValue("@msg", app.Output);
                            insertCmd.Parameters.AddWithValue("@updated", app.StatusUpdateTime);
                            
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}