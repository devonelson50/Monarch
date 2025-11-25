namespace Monapi.Worker.Nagios;
/// <summary>
/// Devon Nelson
/// 
/// Used to abstract a Nagios application.
/// </summary>
public class NagiosApp
{
    public string AppId { get; set; }
    public string AppName { get; set; }
    public string Status { get; set; }
}