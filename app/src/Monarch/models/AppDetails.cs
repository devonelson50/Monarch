namespace Monarch.Models
{
  public class AppDetails
  {
    public string IpAddress { get; set; } = "0.0.0.0";

    //New Relic Data
    public int nrStatus { get; set; } = 0;
    public int nrLatency { get; set; } = 0;
    public float nrCpuUsage { get; set; } = 0;
    public int nrThroughput { get; set; } = 0;
    public DateTime nrStatusUpdateTime = DateTime.Now;
    public DateTime nrLastCheck = DateTime.Now;

    //Nagios Data
    public DateTime statusUpdateTime { get; set; } = DateTime.Now;
    public string output { get; set; } = "";
    public string perfData { get; set; } = "";
    public int currentState { get; set; } = 0;
    public DateTime lastCheck { get; set; } = DateTime.Now;
    public DateTime lastStateChange { get; set; } = DateTime.Now;
    public DateTime lastTimeUp { get; set; } = DateTime.Now;
    public DateTime lastTimeDown { get; set; } = DateTime.Now;
    public DateTime lastTimeUnreachable { get; set; } = DateTime.Now;
    public DateTime lastNotification { get; set; } = DateTime.Now;
    public string latency { get; set; } = "";
  }
}
