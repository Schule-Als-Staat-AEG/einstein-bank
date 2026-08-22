using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Ganss.Xss;
using Max35050.API;
using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Systems;

public static class RecurringTransactionManager
{
    public static List<RecurringTransaction> recurringTransactions = new List<RecurringTransaction>();
    internal static DateTime lastReset;
    internal static Task Worker = null!;
    internal static DateTime nextCheck = DateTime.UtcNow;
    internal static CancellationTokenSource cts = new CancellationTokenSource();
    internal static CancellationToken cancellationToken = cts.Token;
    public static async Task Initialize()
    {
        dynamic listInfo = await new RestCall($"{Server.databaseURL}/api/collections/recurring_transactions/records?perPage=1", ServerAuth.authToken).GetDynamic();
        int totalRecords = (int)listInfo.totalItems;
        dynamic allRecords = await new RestCall($"{Server.databaseURL}/api/collections/recurring_transactions/records?perPage={totalRecords}", ServerAuth.authToken).GetDynamic();
        foreach (dynamic record in allRecords.items)
        {
            try
            {
                RecurringTransaction rt = new RecurringTransaction
                {
                    id = record.id,
                    fromAccountID = record.sender,
                    toAccountID = record.receiver,
                    amount = record.amount,
                    maxAmount = record.maxAmount,
                    paid = record.paid,
                    days = GetDaysFromStringArray(((IEnumerable<object>)record.days).Select(d => d.ToString()!).ToList()), // String array to enum conversion
                    time = (int)record.time,  // The local time in seconds
                    timezone = (string)record.timezone,  // Timezone string
                    description = record.description,
                    createdAt = record.created,
                };
                rt.paidToday = IsInFuture((int)record.time, rt.TimezoneOffset) ? false : true;
                recurringTransactions.Add(rt);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecurringTransactionManager] Error loading recurring transaction record: {ex}", true);
            }
        }
        Worker = RecurringTransactionWorker(cancellationToken);
    }
    public static async Task RecurringTransactionWorker(CancellationToken token = default)
    {
        lastReset = DateTime.UtcNow;
        while (true)
        {
            try
            {
                // Check for daily reset
                try
                {
                    bool hasResetToday = lastReset.Date == DateTime.UtcNow.Date;
                    if (!hasResetToday)
                    {
                        // Reset paid Today status for all recurring transactions
                        TimeSpan utcNow = DateTime.UtcNow.TimeOfDay;
                        if (utcNow.TotalSeconds >= 0) // Midnight UTC
                        {
                            lastReset = DateTime.UtcNow;
                            foreach (RecurringTransaction rt in recurringTransactions)
                            {
                                rt.paidToday = false;
                            }
                        }
                    }
                }
                catch
                {

                }
                foreach (RecurringTransaction rt in recurringTransactions)
                {
                    try
                    {
                        if (HasDay(DateTime.UtcNow.AddHours(rt.TimezoneOffset).DayOfWeek, rt.days) && !rt.paidToday)
                        {
                            DateTime utcNow = DateTime.UtcNow;
                            DateTime userTime = utcNow.AddHours(rt.TimezoneOffset);
                            int currentSeconds = userTime.Hour * 3600 + userTime.Minute * 60 + userTime.Second;
                            if (currentSeconds >= rt.time)
                            {
                                // Time to execute transaction
                                if (!TransactionAPI.allowTransactions)
                                {
                                    continue;
                                }
                                if (rt.maxAmount <= 0.0m || rt.paid < rt.maxAmount) // Check max amount limit
                                {
                                    rt.paidToday = true;
                                    TransactionData td = new TransactionData
                                    {
                                        sender = rt.fromAccountID,
                                        receiver = rt.toAccountID,
                                        amount = rt.amount,
                                        description = rt.description,
                                        taxData = new TransactionTaxData
                                        {
                                            VATApplicable = rt.vatApplicable,
                                            IncomeTaxApplicable = rt.incomeApplicable,
                                            TransactionTaxApplicable = true,
                                        },
                                        system = "##RECURRING_TRANSACTION_AUTHORIZED##",
                                        applyTaxes = true,
                                        logTransaction = true,
                                        moveMoney = true
                                    };
                                    string error = TransactionDataManager.IsTransactionDataValid(td);
                                    td = TransactionDataManager.SanitizeData(td);
                                    if (error != "OK")
                                    {
                                        await NotificationManager.CreateNotification(await UserManager.GetUserIDByAccountID(rt.fromAccountID), "Dauerüberweisung fehlgeschlagen", $"Die Dauerüberweisung konnte nicht durchgeführt werden, da die Transaktionsdaten ungültig sind: {error.Substring(3)}");
                                        continue;
                                    }
                                    td.taxResult = await TaxManager.CalculateTaxes(td);
    
                                    if ((await AccountManager.HasBalance(rt.fromAccountID, td.totalDeductable)) == false)
                                    {
                                        await NotificationManager.CreateNotification(await UserManager.GetUserIDByAccountID(rt.fromAccountID), "Dauerüberweisung fehlgeschlagen", $"Die Dauerüberweisung konnte nicht durchgeführt werden, da nicht genügend Guthaben auf dem Konto ist.");
                                        continue;
                                    }
    
                                    string transactionID = await TransactionAPI.MoveMoney(td);
                                    td.transactionID = transactionID;
                                    rt.paid++;
                                    await NotificationManager.CreateNotification(await UserManager.GetUserIDByAccountID(rt.fromAccountID), "Dauerüberweisung ausgeführt", $"Die Dauerüberweisung in Höhe von {td.amount} wurde erfolgreich durchgeführt. Transaktions-ID: {transactionID}");
                                    await NotificationManager.CreateNotification(await UserManager.GetUserIDByAccountID(rt.toAccountID), "Geld erhalten", $"Du hast Geld in Höhe von {td.amount} von {rt.fromAccountID} erhalten. Transaktions-ID: {transactionID}");
                                    // Save paid to database
                                    await new RestCall($"{Server.databaseURL}/api/collections/recurring_transactions/records/{rt.id}", ServerAuth.authToken, JsonConvert.SerializeObject(new { paid = rt.paid })).PatchDynamic();
                                }
                                else
                                {
                                    await new RestCall($"{Server.databaseURL}/api/collections/recurring_transactions/records/{rt.id}", ServerAuth.authToken).DeleteDynamic();
                                    recurringTransactions.RemoveAll(r => r.id == rt.id);
                                    //Debug.Log($"[RecurringTransactionManager] Recurring transaction {rt.id} has reached its max amount limit.");
                                }
                                
                            }
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                
            }
            nextCheck = DateTime.UtcNow.AddMilliseconds(SettingsManager.recurringPaymentsUpdateInterval < 1800000 ? SettingsManager.recurringPaymentsUpdateInterval : 1800000);
            int delay = SettingsManager.recurringPaymentsUpdateInterval < 1800000 ? SettingsManager.recurringPaymentsUpdateInterval : 1800000;
            try
            {
                await Task.Delay(delay, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
    public static async Task UpdateRecurringTransaction(string id)
    {
        dynamic? record = await new RestCall($"{Server.databaseURL}/api/collections/recurring_transactions/records/{id}", ServerAuth.authToken).GetDynamic();
        // If record is not found, remove from list
        if (record == null)
        {
            recurringTransactions.RemoveAll(rt => rt.id == id);
            return;
        }
        else if (record.id == null)
        {
            recurringTransactions.RemoveAll(rt => rt.id == id);
            return;
        }
        // Update with new data
        foreach (RecurringTransaction rt in recurringTransactions)
        {
            if (rt.id == id)
            {
                rt.fromAccountID = record.sender;
                rt.toAccountID = record.receiver;
                rt.amount = record.amount;
                rt.maxAmount = record.maxAmount;
                rt.paid = record.paid;
                rt.days = GetDaysFromStringArray(((IEnumerable<object>)record.days).Select(d => d.ToString()!).ToList()); // String array to enum conversion
                rt.time = (int)record.time;
                rt.timezone = (string)record.timezone;
                rt.description = record.description;
                rt.paidToday = IsInFuture((int)record.time, rt.TimezoneOffset) ? false : true;
                rt.vatApplicable = record.vatApplicable;
                rt.incomeApplicable = record.incomeApplicable;
                return;
            }
        }
        // If not found, add new
        RecurringTransaction newRt = new RecurringTransaction
        {
            id = record.id,
            fromAccountID = record.sender,
            toAccountID = record.receiver,
            amount = record.amount,
            maxAmount = record.maxAmount,
            paid = record.paid,
            days = GetDaysFromStringArray(((IEnumerable<object>)record.days).Select(d => d.ToString()!).ToList()), // String array to enum conversion
            time = (int)record.time,
            timezone = (string)record.timezone,
            description = record.description,
            createdAt = record.created,
            vatApplicable = record.vatApplicable,
            incomeApplicable = record.incomeApplicable
        };
        newRt.paidToday = IsInFuture((int)record.time, newRt.TimezoneOffset) ? false : true;
        recurringTransactions.Add(newRt);
    }
    public static bool IsInFuture(int timeInSeconds, double timezone)
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime userTime = utcNow.AddHours(timezone);
        int currentSeconds = userTime.Hour * 3600 + userTime.Minute * 60 + userTime.Second;
        return currentSeconds < timeInSeconds;
    }
    public static bool HasDay(DayOfWeek day, Days days)
    {
        return (days & (Days)(1 << (int)day)) != 0;
    }
    public static Days GetDaysFromStringArray(List<string> daysArray)
    {
        Days days = 0;
        foreach (string day in daysArray)
        {
            switch (day.ToLower())
            {
                case "monday":
                    days |= Days.Monday;
                    break;
                case "tuesday":
                    days |= Days.Tuesday;
                    break;
                case "wednesday":
                    days |= Days.Wednesday;
                    break;
                case "thursday":
                    days |= Days.Thursday;
                    break;
                case "friday":
                    days |= Days.Friday;
                    break;
                case "saturday":
                    days |= Days.Saturday;
                    break;
                case "sunday":
                    days |= Days.Sunday;
                    break;
            }
        }
        return days;
    }
}

public class RecurringTransaction
{
    public string id = "";
    public string fromAccountID = "";
    public string toAccountID = "";
    public decimal amount = 0.0m;
    public decimal maxAmount = 0.0m;
    public decimal paid = 0.0m;
    public Days days;
    public int time = 0; // In seconds
    public string timezone = "Europe/Berlin"; // e.g. "Europe/Berlin", "America/New_York"
    public double TimezoneOffset { get { return TimeZoneInfo.FindSystemTimeZoneById(timezone.Trim()).GetUtcOffset(DateTime.UtcNow).TotalHours; } }
    public string description = "";
    public DateTime createdAt;
    public bool paidToday = false;
    public bool vatApplicable = false;
    public bool incomeApplicable = false;
}

[Flags]
public enum Days
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64
}