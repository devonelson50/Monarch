
/*  
  Brady Brown
  Nagios App Details for Monarch
  Stores nagios config details, used primarily for inital configuration lists
*/

namespace Monarch.Models
{
  /*
  Overall model for Nagios application
  Includes the following:
      -int AppId
      -string AppName
  */
  public class NagiosApp
  {
    //Id pulled from nagiosApps table, which is pulled from Nagios directly
    public int AppId { get; set; } = 0;
    //Name pulled from nagiosApps table, which is pulled from Nagios directly
    public string AppName { get; set; } = string.Empty;
  }
}