using System.Threading.Tasks;
using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Cmd;

public static class AccountCommands
{
    public static async Task PrintAccountInfo(string accountID)
    {
        dynamic account = await AccountManager.GetAccount(accountID);
        if (account != null)
        {
            Debug.LogOK($"Account Info for {accountID}:\n" +
                $"  User: {account.user}\n" +
                $"  Name: {account.expand.user.name}\n" +
                $"  User Loggin Blocked: {account.expand.user.locked}\n" +
                $"  Is Company: {account.expand.user.isCompany}\n" +
                (bool.TryParse(account.expand.user.isCompany, out bool isCompany) && isCompany ? $"  Company Name: {account.expand.user.companyName}\n" : "") +
                $"  Balance: {account.balance}\n" +
                $"  Frozen: {account.frozen}\n" +
                $"  Tax Exempt: {account.taxExempt}\n" +
                $"  Tax Free Pool: {account.taxFreePool}\n" +
                $"  Tax Rate Override: {(account.taxRate <= 0 ? "Default Tax Rate" : (decimal)account.taxRate * 100 + "%")}\n" +
                $"  Created At: {account.createdAt}\n" +
                $"  Updated At: {account.updatedAt}", true);
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.", true);
        }
    }
    public static void SetFreezeAccount(string accountID, bool freeze)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            account.frozen = freeze;
            string jsonPayload = JsonConvert.SerializeObject(account);
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic().Result;
            Debug.LogOK($"Account {accountID} freeze status set to {freeze}.");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void ToggleFreezeAccount(string accountID)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            bool currentFreezeStatus = account.frozen != null && account.frozen == true;
            account.frozen = !currentFreezeStatus;
            string jsonPayload = JsonConvert.SerializeObject(account);
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic().Result;
            Debug.LogOK($"Account {accountID} freeze status toggled to {!currentFreezeStatus}.");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void SetTaxExemptStatus(string accountID, bool taxExempt)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            account.taxExempt = taxExempt;
            string jsonPayload = JsonConvert.SerializeObject(account);
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic().Result;
            Debug.LogOK($"Account {accountID} tax exempt status set to {taxExempt}.");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void ToggleTaxExemptStatus(string accountID)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            bool currentTaxExemptStatus = account.taxExempt != null && account.taxExempt == true;
            account.taxExempt = !currentTaxExemptStatus;
            string jsonPayload = JsonConvert.SerializeObject(account);
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic().Result;
            Debug.LogOK($"Account {accountID} tax exempt status toggled to {!currentTaxExemptStatus}.");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void PrintBalance(string accountID)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            Debug.LogOK($"Account {accountID} Balance: {account.balance}");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void PrintTaxFreePool(string accountID)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            Debug.LogOK($"Account {accountID} Tax Free Pool: {account.taxFreePool}");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void SetTaxFreePool(string accountID, decimal amount)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            account.taxFreePool = amount;
            string jsonPayload = JsonConvert.SerializeObject(account);
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic().Result;
            Debug.LogOK($"Account {accountID} tax free pool set to {amount}.");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void PrintTaxRate(string accountID)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            decimal taxRate = account.taxRate != null ? (decimal)account.taxRate : 0;
            if (taxRate <= 0)
            {
                Debug.LogOK($"Account {accountID} is using the default tax rate of {decimal.Parse(SettingsManager.GetSetting("tax_rate")) * 100}%.");
            }
            else
            {
                Debug.LogOK($"Account {accountID} Tax Rate Override: {taxRate * 100}%");
            }
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
    public static void SetTaxRate(string accountID, decimal taxRate)
    {
        dynamic account = AccountManager.GetAccount(accountID).Result;
        if (account != null)
        {
            account.taxRate = taxRate;
            string jsonPayload = JsonConvert.SerializeObject(account);
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/accounts/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic().Result;
            Debug.LogOK($"Account {accountID} tax rate override set to {taxRate * 100}%.");
        }
        else
        {
            Debug.LogError($"Account {accountID} does not exist.");
        }
    }
}