namespace Monapi.Worker.NewRelic;

/// <summary>
/// Devon Nelson
/// 
/// Used to abstract New Relic application data.
/// </summary>
public class NewRelicApp
{
    public string AppId { get; set; }
    public string AppName { get; set; }
    public string Status { get; set; }
}