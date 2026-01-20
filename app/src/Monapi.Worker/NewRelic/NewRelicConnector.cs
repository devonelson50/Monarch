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
        this.accountId = File.ReadAllText("/run/secrets/monarch_account_number").Trim();
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
        do
        {
            var options = new RestClientOptions(uri)
            {
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
            request.AddHeader("Api-Key", this.apiKey);
            request.AddJsonBody(new { query = query });

            var response = await client.ExecuteAsync(request);

            if (!(response.IsSuccessful))
            {
                if (response.StatusCode == 0)
                {
                    throw new Exception($"Network Failure (Status 0): {response.ErrorMessage ?? response.ErrorException?.Message ?? "Unknown Network Error"}");
                }
                else
                {
                    // If StatusCode is > 0 (like 401 or 500), the server replied.
                    throw new Exception($"API Error: {response.StatusCode} - {response.Content}");
                }            
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
                            AppId = (int)(result["id"]?.GetValue<double>() ?? 0),
                            IpAddress = result["ip"]?.ToString() ?? "0.0.0.0",
                            Status = (int)(result["state"]?.GetValue<double>() ?? 0),
                            Latency = result["lat"]?.ToString() ?? "0ms",
                            CpuUsage = result["cpu"]?.GetValue<double>() ?? 0.0,
                            Throughput = (int)(result["tput"]?.GetValue<double>() ?? 0),
                            Output = result["msg"]?.ToString() ?? "",
                            StatusUpdateTime = DateTime.TryParse(result["updated"]?.ToString(), out var dt) ? dt : DateTime.Now
                        });
                    }
                }
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
                    // 1. Log before deleting
                    Console.WriteLine("DEBUG: Deleting old rows from newRelicApps...");

                    var deleteQuery = "DELETE FROM newRelicApps";
                    using (var deleteCmd = new SqlCommand(deleteQuery, connection, transaction))
                    {
                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    var insertQuery = @"INSERT INTO newRelicApps 
                        (appId, appName, ipAddress, status, latency, cpuUsage, throughput, output, statusUpdateTime, lastCheck) 
                        VALUES (@id, @name, @ip, @state, @lat, @cpu, @tput, @msg, @updated, GETDATE())";

                    // 2. Log every 10th insert to track progress
                    int count = 0;

                    foreach (var app in apps)
                    {
                        using (var insertCmd = new SqlCommand(insertQuery, connection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@id", app.AppId);
                            insertCmd.Parameters.AddWithValue("@name", app.AppName);
                            insertCmd.Parameters.AddWithValue("@ip", app.IpAddress);
                            insertCmd.Parameters.AddWithValue("@state", app.Status);
                            insertCmd.Parameters.AddWithValue("@lat", app.Latency);
                            insertCmd.Parameters.AddWithValue("@cpu", app.CpuUsage);
                            insertCmd.Parameters.AddWithValue("@tput", app.Throughput);
                            insertCmd.Parameters.AddWithValue("@msg", app.Output);
                            insertCmd.Parameters.AddWithValue("@updated", app.StatusUpdateTime);
                            
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                        count++;
                        if (count % 10 == 0) Console.WriteLine($"DEBUG: Inserted {count}/50 apps...");
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    // This is the most important log!
                    Console.WriteLine($"!!! SQL ERROR IN TRANSACTION: {ex.Message}");
                    throw;
                }
            }
        }
    }
}