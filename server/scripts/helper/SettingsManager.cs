using System.Threading.Tasks;
using Max35050.Auth;
using Max35050.Net;
using Max35050.Systems;
using Newtonsoft.Json;

namespace Max35050.Helper;

public static class SettingsManager
{
    public static int recurringPaymentsUpdateInterval = 60000; // 60 Seconds
    public static bool handleRecurringPayments = true;
    public static int recurringPaymentsReset = 0; // At Midnight, int in seconds
    public static int taxPoolReset = 0; // At Midnight, int in seconds
    public static int transactionTimeout = 10000; // 10 Seconds

    // Tax Settings
    public static string taxAccountID = "";
    public static bool notifyTransactions = false;

    // Warehouse Settings
    public static bool warehouseEnabled = true;
    public static string warehouseCollectionAccountID = "";

    // Case Settings
    public static bool sendEmail = false;
    public static string emailReceiver = "";

    public static bool enableRateLimiting = true;
    public static int rateLimitWindowSeconds = 1;
    public static int rateLimitPermitLimit = 5;
    public static string rateLimitQueueProcessingOrder = "OldestFirst";
    public static int rateLimitQueueLimit = 10;

    public static string GetSetting(string key)
    {
        try
        {
            dynamic result = new RestCall($"{Server.databaseURL}/api/collections/settings/records?filter=(key='{key}')", ServerAuth.authToken).GetDynamic().Result;
            if (result != null && result!.items.Count > 0)
            {
                return result!.items[0].value;
            }
        }
        catch
        {
            // Setting not found
        }
        return string.Empty;
    }
    public static async Task SetSetting(string key, string value)
    {
        try
        {
            dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/settings/records?filter=(key='{key}')", ServerAuth.authToken).GetDynamic();
            if (result != null && result!.items.Count > 0)
            {
                // Update existing setting
                dynamic settingRecord = result!.items[0];
                settingRecord.value = value;
                string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(settingRecord);
                await new RestCall($"{Server.databaseURL}/api/collections/settings/records/{settingRecord.id}", ServerAuth.authToken, jsonPayload).PatchDynamic();
                return;
            }
        }
        catch
        {
            // Setting not found, create new
        }
        // Create new setting
        dynamic newSettingRecord = new
        {
            key = key,
            value = value,
            type = "text"
        };
        string newJsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(newSettingRecord);
        await new RestCall($"{Server.databaseURL}/api/collections/settings/records", ServerAuth.authToken, newJsonPayload).PostDynamic();
    }
    public static async Task PullSettings()
    {
        string recurringPaymentsIntervalStr = GetSetting("recurring_payments_update_timer");
        if (int.TryParse(recurringPaymentsIntervalStr, out int interval))
        {
            recurringPaymentsUpdateInterval = interval;
        }
        string handleRecurringPaymentsStr = GetSetting("handle_recurring_payments");
        if (bool.TryParse(handleRecurringPaymentsStr, out bool handleRecurring))
        {
            handleRecurringPayments = handleRecurring;
        }
        string recurringPaymentsResetStr = GetSetting("recurring_payments_reset");
        if (int.TryParse(recurringPaymentsResetStr, out int reset))
        {
            recurringPaymentsReset = reset;
        }
        string taxPoolResetStr = GetSetting("tax_pool_reset");
        if (int.TryParse(taxPoolResetStr, out int taxReset))
        {
            taxPoolReset = taxReset;
        }
        string transactionTimeoutStr = GetSetting("transaction_timeout");
        if (int.TryParse(transactionTimeoutStr, out int timeout))
        {
            transactionTimeout = timeout;
        }

        string taxAccountIDStr = GetSetting("tax_collection_account");
        if (!string.IsNullOrEmpty(taxAccountIDStr))
        {
            taxAccountID = taxAccountIDStr;
        }
        string notifyTransactionsStr = GetSetting("notify_transactions");
        if (bool.TryParse(notifyTransactionsStr, out bool notify))
        {
            notifyTransactions = notify;
        }

        string taxSusFlagScoreStr = GetSetting("tax_sus_flag_score");
        if (double.TryParse(taxSusFlagScoreStr, out double taxSusFlagScore))
        {
            IRSHelper.susScore = taxSusFlagScore;
        }
        string taxSusFlagOperatorStr = GetSetting("tax_sus_operator");
        if (taxSusFlagOperatorStr.Length > 0)
        {
            IRSHelper.susOperator = taxSusFlagOperatorStr.ToString();
        }

        // Warehouse Settings
        string warehouseEnabledStr = GetSetting("warehouse_enabled");
        if (bool.TryParse(warehouseEnabledStr, out bool warehouseEnabledSetting))
        {
            warehouseEnabled = warehouseEnabledSetting;
        }
        string warehouseCollectionAccountIDStr = GetSetting("warehouse_account");
        if (!string.IsNullOrEmpty(warehouseCollectionAccountIDStr))
        {
            warehouseCollectionAccountID = warehouseCollectionAccountIDStr;
        }

        // Case Settings
        string sendEmailStr = GetSetting("case_send_email");
        if (bool.TryParse(sendEmailStr, out bool sendEmailSetting))
        {
            sendEmail = sendEmailSetting;
        }
        string emailReceiverStr = GetSetting("case_email_receiver");
        if (!string.IsNullOrEmpty(emailReceiverStr))
        {
            emailReceiver = emailReceiverStr;
        }
        await TaxManager.PullTaxSettings();
        await TaxFlagsManager.LoadTaxScoreRules();
    }
    public static void PrintSettings()
    {
        Debug.Log($"Current Settings:", true);
        Debug.Log($"Recurring Payments Update Interval: {recurringPaymentsUpdateInterval}", true);
        Debug.Log($"Handle Recurring Payments: {handleRecurringPayments}", true);
        Debug.Log($"Recurring Payments Reset: {recurringPaymentsReset}", true);
        Debug.Log($"Tax Pool Reset: {taxPoolReset}", true);
        Debug.Log($"Transaction Timeout: {transactionTimeout}", true);
        Debug.Log($"Notify Transactions: {notifyTransactions}", true);
        Debug.Log($"Tax Suspicious Flag Score: {IRSHelper.susScore}", true);
        Debug.Log($"Tax Suspicious Flag Operator: {IRSHelper.susOperator}", true);
        Debug.Log($"Warehouse Enabled: {warehouseEnabled}", true);
        Debug.Log($"Warehouse Collection Account ID: {warehouseCollectionAccountID}", true);
        Debug.Log($"Send Email on Case Update: {sendEmail}", true);
        Debug.Log($"Email Receiver for Case Updates: {emailReceiver}", true);
    }
}