using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;

namespace Max35050.Systems;

public static class TaxPoolManager
{
    internal static DateTime lastReset;
    internal static int resetInSeconds = 0; // At 00:00 UTC
    public static async Task TaxPoolResetWorker()
    {
        lastReset = DateTime.UtcNow;
        while(true)
        {
            try
            {
                bool hasResetToday = lastReset.Date == DateTime.UtcNow.Date;
                if (!hasResetToday)
                {
                    // Check if time in seconds is passed resetInSeconds
                    TimeSpan utcNow = DateTime.UtcNow.TimeOfDay;
                    if (utcNow.TotalSeconds >= resetInSeconds)
                    {
                        lastReset = DateTime.UtcNow;
                        await ResetTaxPool();
                    }
                }
            }
            catch { }
            await Task.Delay(60000); // Check every 60 seconds
            resetInSeconds = SettingsManager.taxPoolReset;
        }
    }
    public static async Task ResetTaxPool()
    {
        // Get total accounts list
        dynamic listInfo = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records?perPage=0", ServerAuth.authToken).GetDynamic();
        int totalAccounts = listInfo.totalItems;
        dynamic allAccounts = await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records?perPage={totalAccounts}&fields=id", ServerAuth.authToken).GetDynamic();
        foreach (dynamic account in allAccounts.items)
        {
            try
            {
                string accountID = account.id;
                _ = ResetTaxPoolForAccount(accountID); // Fire and forget
            }
            catch { }
        }
    }
    public static async Task ResetTaxPoolForAccount(string accountID)
    {
        // Reset tax pool and income received pool for specific account
        dynamic newData = new
        {
            taxFreePool = TaxManager.CurrentTaxSettings.TransactionTaxFreePool,
            totalIncomeReceived = 0
        };
        string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(newData);
        await new RestCall($"{Server.databaseURL}/api/collections/user_tax_data/records/{accountID}", ServerAuth.authToken, jsonPayload).PatchDynamic();
    }
    public static void SetResetStatus(bool hasResetToday)
    {
        if (hasResetToday)
        {
            // Set to the last second before midnight UTC
            lastReset = DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);
        }
        else
        {
            lastReset = DateTime.UtcNow.AddDays(-1);
        }
    }
}