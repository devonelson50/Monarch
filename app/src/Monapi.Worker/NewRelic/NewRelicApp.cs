using Microsoft.VisualBasic;

namespace Monapi.Worker.NewRelic;

// Used to abstract New Relic application data.
public class NewRelicApp
{
    public string AppId { get; set; }
    public string AppName { get; set; }
    public string Status { get; set; }
}