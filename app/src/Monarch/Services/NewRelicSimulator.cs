using System.Net.Http.Json;

public class NewRelicSimulator : BackgroundService{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly string _accountId = "7596990";
  private readonly string _licenseKey = "A0F8AE5C33010DCE92681FAD3FCAA15DFB3BDDFC8C2ED0E67F516BF418EBF0BC";
  private readonly string[] _appNames = Enumerable.Range(1, 20).Select(i => $"App-{i:D2}").ToArray();
  private readonly string[] _statuses = { "Healthy", "Healthy", "Warning", "Critical" };

  public NewRelicSimulator(IHttpClientFactory httpClientFactory){
    _httpClientFactory = httpClientFactory;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken){
    var client = _httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Add("Api-Key", _licenseKey);

    while (!stoppingToken.IsCancellationRequested){
      var events = _appNames.Select(name => new AppHealthEvent{
        appName = name,
        status = _statuses[Random.Shared.Next(_statuses.Length)],
        responseTime = Random.Shared.Next(50, 1000),
        cpuUsage = Random.Shared.Next(0, 100)
      }).ToList();

      var url = $"https://insights-collector.newrelic.com/v1/accounts/{_accountId}/events";

      try{
        await client.PostAsJsonAsync(url, events, stoppingToken);
      } catch (Exception ex){
        Console.Write("Error");
      }

      await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
  }
}

public class AppHealthEvent
{
  public string eventType { get; set; } = "ApplicationHealth";
  public string appName { get; set; }
  public string status { get; set; }
  public int responseTime { get; set; }
  public int cpuUsage { get; set; }
}