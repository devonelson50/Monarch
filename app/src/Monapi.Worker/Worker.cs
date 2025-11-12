using Monapi.Worker;

namespace Monapi.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        NewRelic.NewRelicSimulator nrs = new NewRelic.NewRelicSimulator();

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing NewRelic data");
            nrs.RunLoop();
            await Task.Delay(30000, stoppingToken);
        }
    }
}
