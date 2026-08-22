using System.Dynamic;
using System.Runtime.CompilerServices;
using Max35050.Auth;
using Max35050.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Max35050.Helper;

public static class AccountManager
{
    public static async Task<Account> GetAccount(string id)
    {
        try
        {
            dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{id}?expand=user", ServerAuth.authToken).GetDynamic();
            result.id = result.id; // Throw if account not found
            Account account = new Account
            {
                id = result.id,
                user = result.user,
                balance = decimal.Parse(result.balance.ToString()),
                frozen = bool.Parse(result.frozen.ToString()),
                trusted = bool.Parse(result.trusted.ToString())
            };
            return account;
        }
        catch
        {
            return null!;
        }
    }
    public static async Task<Account> GetAccountByUserID(string id)
    {
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/accounts/records?filter=(user='{id}')&expand=user", ServerAuth.authToken).GetDynamic();
        result = result.items[0];
        result.id = result.id; // Throw if account not found
        Account account = new Account
        {
            id = result.id,
            user = result.user,
            balance = decimal.Parse(result.balance.ToString()),
            frozen = bool.Parse(result.frozen.ToString()),
            trusted = bool.Parse(result.trusted.ToString())
        };
        return account;
    }
    public static async Task<Account> AddBalance(string accountID, decimal amount)
    {
        JObject data = new JObject();
        amount = Math.Round(amount, 2);
        data["balance+"] = amount;
        string jsonPayload = JsonConvert.SerializeObject(data);
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        Account account = new Account
        {
            id = result.id,
            user = result.user,
            balance = decimal.Parse(result.balance.ToString()),
            frozen = bool.Parse(result.frozen.ToString()),
            trusted = bool.Parse(result.trusted.ToString())
        };
        return account;
    }
    public static async Task<Account> SubtractBalance(string accountID, decimal amount)
    {
        JObject data = new JObject();
        amount = Math.Round(amount, 2);
        data["balance-"] = amount;
        string jsonPayload = JsonConvert.SerializeObject(data);
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        Account account = new Account
        {
            id = result.id,
            user = result.user,
            balance = decimal.Parse(result.balance.ToString()),
            frozen = bool.Parse(result.frozen.ToString()),
            trusted = bool.Parse(result.trusted.ToString())
        };
        return account;
    }
    public static async Task<Account?> SetBalance(string accountID, decimal amount)
    {
        
        dynamic account = await GetAccount(accountID);
        amount = Math.Round(amount, 2);
        account.balance = amount;
        string jsonPayload = JsonConvert.SerializeObject(account);
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        Account updatedAccount = new Account
        {
            id = result.id,
            user = result.user,
            balance = decimal.Parse(result.balance.ToString()),
            frozen = bool.Parse(result.frozen.ToString()),
            trusted = bool.Parse(result.trusted.ToString())
        };
        return updatedAccount;
    }
    public static async Task<decimal> GetBalance(string accountID)
    {
        dynamic account = await GetAccount(accountID);
        return account.balance;
    }
    public static async Task<bool> HasBalance(string accountID, decimal amount)
    {
        dynamic account = await GetAccount(accountID);
        return account.balance >= amount;
    }
    public static async Task<decimal> SubtractPool(string accountID, decimal amount)
    {
        amount = Math.Round(amount, 2);
        JObject data = new JObject();
        data["taxFreePool-"] = amount;
        string jsonPayload = JsonConvert.SerializeObject(data);
        string id = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!.id;
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records/{id}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        return result.taxFreePool.ToObject<decimal>() ?? 0.0m;
    }
    public static async Task<decimal> AddPool(string accountID, decimal amount)
    {
        JObject data = new JObject();
        amount = Math.Round(amount, 2);
        data["taxFreePool+"] = amount;
        string jsonPayload = JsonConvert.SerializeObject(data);
        string id = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!.id;
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records/{id}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        return result.taxFreePool.ToObject<decimal>() ?? 0.0m;
    }
    public static async Task<decimal> GetPool(string accountID)
    {
        dynamic account = await GetAccount(accountID);
        dynamic result = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!;
        return result!.taxFreePool.ToObject<decimal>() ?? 0.0m;
    }
    public static async Task<decimal> SetPool(string accountID, decimal amount)
    {
        dynamic account = await GetAccount(accountID);
        amount = Math.Round(amount, 2);
        account.taxFreePool = amount;
        string jsonPayload = JsonConvert.SerializeObject(account);
        string id = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!.id;
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records/{id}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        return result!.taxFreePool.ToObject<decimal>() ?? 0.0m;
    }
    // Get the account associated with the user from the auth token (even if superuser)
    public static async Task<Account> GetAccountForTransaction(string token)
    {
        string userID = (await AuthManager.GetUserFromToken(token)).id;
        return await GetAccountByUserID(userID);
    }
    public static async Task<string> GetAccountNameFromID(string userID)
    {
        if (userID.Replace("-", "").Trim().Length == 8)
        {
            userID = await UserManager.GetUserIDByAccountID(userID);
        }
        dynamic user = await UserManager.GetUser(userID);
        return user.name;
    }
    public static async Task<List<Account>> GetAllAccounts()
    {
        List<Account> accounts = new List<Account>();
        (await Server.db.Collections("accounts").GetFullList()).ForEach(record =>
        {
            accounts.Add(new Account{
                id = record.id,
                user = record.user.ToString(),
                balance = decimal.Parse(record.balance.ToString()),
                frozen = bool.Parse(record.frozen.ToString()),
                trusted = bool.Parse(record.trusted.ToString())
            });
        });
        return accounts;
    }
    public static async Task<AccountType> GetAccountType(string accountID)
    {
        dynamic account = await GetAccount(accountID);
        dynamic user = await UserManager.GetUser(account.user.ToString());
        string typeStr = user.isCompany.ToString();
        if (typeStr.ToLower() == "true")
        {
            return AccountType.Business;
        }
        else
        {
            return AccountType.Personal;
        }
    }
    /// <summary>
    /// Get the account ID associated with a user ID. Automatically distinguishes between user IDs and account IDs (if an account ID is provided, it will return the same ID). This is useful for transaction processing where we often only have the user ID from the auth token but need the account ID for the transaction.
    /// </summary>
    /// <param name="userID">The user ID for which to find the associated account ID.</param>
    /// <returns>The account ID associated with the user ID.</returns>
    public static async Task<string> GetAccountID(string userID)
    {
        dynamic account = await GetAccountByUserID(userID);
        return account.id;
    }
    public static async Task<decimal> AddIncomeReceived(string accountID, decimal amount)
    {
        JObject data = new JObject();
        amount = Math.Round(amount, 2);
        data["totalIncomeReceived+"] = amount;
        string jsonPayload = JsonConvert.SerializeObject(data);
        string id = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!.id;
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records/{id}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        return result.totalIncomeReceived.ToObject<decimal>() ?? 0.0m;
    }
    public static async Task<decimal> SubtractIncomeReceived(string accountID, decimal amount)
    {
        JObject data = new JObject();
        amount = Math.Round(amount, 2);
        data["totalIncomeReceived-"] = amount;
        string jsonPayload = JsonConvert.SerializeObject(data);
        string id = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!.id;
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records/{id}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        return result.totalIncomeReceived.ToObject<decimal>() ?? 0.0m;
    }
    public static async Task<decimal> GetIncomeReceived(string accountID)
    {
        string id = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!.id;
        dynamic? result = await Server.db.Collections("user_tax_data").GetOne(id);
        return result!.totalIncomeReceived.ToObject<decimal>() ?? 0.0m;
    }
}