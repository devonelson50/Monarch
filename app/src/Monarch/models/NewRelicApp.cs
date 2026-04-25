
/*  
  Brady Brown
  New Relic App Details for Monarch
  Stores New Relic config details, used primarily for inital configuration lists
*/

namespace Monarch.Models
{

  /*
  Overall model for New Relic application
  Includes the following:
      -int AppId
      -string AppName
  */

  public class NewRelicApp
  {
    //Id pulled from newRelicApps table, which is pulled from New Relic directly
    public int AppId { get; set; } = 0;
    //Mame pulled from newRelicApps table, which is pulled from New Relic directly
    public string AppName { get; set; } = string.Empty;
  }
}