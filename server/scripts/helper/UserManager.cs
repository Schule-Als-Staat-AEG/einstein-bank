using Max35050.Helper;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Auth;

public static class UserManager
{
    public static async Task<User> GetUser(string id)
    {
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/users/records/{id}", ServerAuth.authToken).GetDynamic();
        User user = User.FromDynamic(result);
        return user;
    }
    public static async Task<User> GetUserByName(string username)
    {
        dynamic result = (await new RestCall($"{Server.databaseURL}/api/collections/users/records?filter=(name='{username}')", ServerAuth.authToken).GetDynamic()).items[0];
        User user = User.FromDynamic(result);
        return user;
    }
    [Obsolete("WARNING: The company name is NOT unique and will always return the first company with the given name. Use any GetUser method instead if possible.")]
    public static async Task<User> GetCompany(string name)
    {
        dynamic result = (await new RestCall($"{Server.databaseURL}/api/collections/users/records?filter=(companyName='{name}')", ServerAuth.authToken).GetDynamic()).items[0];
        User company = User.FromDynamic(result);
        return company;
    }
    public static async Task<User> GetUserByAccountID(string id)
    {
        dynamic account = await AccountManager.GetAccount(id);
        string userID = account.user;
        User user = await GetUser(userID);
        return user;
    }
    public static async Task<string> GetUserIDByAccountID(string id)
    {
        Account account = await AccountManager.GetAccount(id);
        return account.user;
    }

    public static async Task<UserData> GetUserData(string id)
    {
        dynamic? result = await Server.db.Collections("user_data").GetOne(id);
        if (result == null)
        {
            return null!;
        }
        return UserData.FromDynamic(result);
    }
    public static async Task<decimal> SetEuroSpent(string userID, decimal newEuroSpent)
    {
        UserData? userData = await GetUserData(userID);
        if (userData == null)
        {
            return -1;
        }
        userData.euroSpent = newEuroSpent;
        await Server.db.Collections("user_data").Update(userID, new { euroSpent = newEuroSpent });
        return newEuroSpent;
    }
    public static async Task<decimal> AddEuroSpent(string userID, decimal amount)
    {
        UserData? userData = await GetUserData(userID);
        if (userData == null)
        {
            return -1;
        }
        decimal newEuroSpent = userData.euroSpent + amount;
        userData.euroSpent = newEuroSpent;
        await Server.db.Collections("user_data").Update(userID, new { euroSpent = newEuroSpent });
        return newEuroSpent;
    }
    public static async Task<decimal> SubtractEuroSpent(string userID, decimal amount)
    {
        UserData? userData = await GetUserData(userID);
        if (userData == null)
        {
            return -1;
        }
        decimal newEuroSpent = userData.euroSpent - amount;
        userData.euroSpent = newEuroSpent;
        await Server.db.Collections("user_data").Update(userID, new { euroSpent = newEuroSpent });
        return newEuroSpent;
    }
}