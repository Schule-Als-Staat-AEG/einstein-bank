using Max35050.Auth;
using Max35050.Net;

namespace Max35050.Helper;

public static class NotificationManager
{
    public static async Task<bool> CreateNotification(string userID, string title, string message = "", string link = "")
    {
        try
        {
            if (userID.Replace("-", "").Trim().Length == 8)
            {
                userID = await UserManager.GetUserIDByAccountID(userID);
            }
            dynamic notification = new
            {
                user = userID,
                title = title,
                content = message,
                link = link,
                isRead = false
            };
            await new RestCall($"{Server.databaseURL}/api/collections/notifications/records", ServerAuth.authToken, Newtonsoft.Json.JsonConvert.SerializeObject(notification)).PostDynamic();
            return true;
        }
        catch
        {
            
        }
        return false;
    }
}