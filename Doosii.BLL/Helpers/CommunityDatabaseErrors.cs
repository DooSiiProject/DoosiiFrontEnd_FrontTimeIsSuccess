using Microsoft.Data.SqlClient;
namespace Doosii.BLL.Helpers;
public static class CommunityDatabaseErrors
{
    public static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current != null; current = current.InnerException)
            if (current is SqlException { Number: 1205 or 2601 or 2627 or 51001 }) return true;
        return false;
    }
}
