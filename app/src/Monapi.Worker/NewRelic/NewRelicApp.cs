namespace Monapi.Worker.NewRelic;

/// <summary>
/// Devon Nelson & Brady Brown
/// 
/// Used to abstract New Relic application data.
/// </summary>
public class NewRelicApp
{
    public int AppId { get; set; }
    public string AppName { get; set; }
    public string IpAddress { get; set; }
    public int Status { get; set; }
    public string Latency { get; set; }
    public double CpuUsage { get; set; }
    public int Throughput { get; set; }
    public string Output { get; set; }
    public DateTime StatusUpdateTime { get; set; }

}