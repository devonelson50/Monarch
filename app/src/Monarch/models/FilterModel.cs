
/*  
  Brady Brown
  App Filters for Monarch
  Stores filter details for applications, added to app's FilterList (see more in AppModel.cs)
*/

namespace Monarch.Models
{
  /*
  Overall model for filters
  Includes the following:
      -int FilterId
      -string FilterName (nullable)
  */

  public class FilterModel
  {
    //Auto-increment id pulled directly from filters table in Monarch
    public int FilterId { get; set; }
    //Name specified by user during config
    public string? FilterName { get; set; }
  }
}
