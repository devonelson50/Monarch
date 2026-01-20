using Monapi.Worker;
using System.Diagnostics;

namespace Monapi.Worker;
/// <summary>
/// Devon Nelson 
/// Primary loop for the service worker. For prototyping purposes, it will
/// refresh simulated New Relic data from NewRelicSimulator every 30 seconds
/// </summary>

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "./simulator.sh",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var simulatorProcess = Process.Start(psi);
        _logger.LogInformation(">>> Simulator started from C# with PID: {pid}", simulatorProcess?.Id);

        Nagios.NagiosConnector nc = new Nagios.NagiosConnector();
        NewRelic.NewRelicConnector nrc = new NewRelic.NewRelicConnector();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("--- Starting Sync Cycle: {time} ---", DateTime.Now);

                Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing NewRelic data");
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Refreshing Nagios data");
                await nc.RunConnector();
                await nrc.RunConnector();
            }
            catch (Exception ex)
            {
                _logger.LogCritical("!!! WORKER CRASHED DURING SYNC: {type} - {msg}", ex.GetType().Name, ex.Message);
                _logger.LogDebug(ex.StackTrace);
            }
            
            await Task.Delay(30000, stoppingToken);
        }

        simulatorProcess?.Kill();


    }
}
