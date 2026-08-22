using Max35050.Auth;
using Max35050.Net;
using Newtonsoft.Json.Linq;

namespace Max35050.Helper;

public static class StatsManager
{
    public static int statsToKeep = 100;
    public static bool collectStats = true;
    public static async Task LogStats(TransactionData td)
    {
        try
        {
            if (!collectStats) return;
            
            User sender = await UserManager.GetUserByAccountID(td.sender);
            User receiver = await UserManager.GetUserByAccountID(td.receiver);
    
            await AppendExpense(td.sender, td.totalDeductable, sender.isCompany, td.receiver, td.transactionID);
            await AppendIncome(td.receiver, td.amount, receiver.isCompany, td.sender, td.transactionID);

            if (td.description.ToLower().Contains("zahlung an "))
            {
                await AddGDP(td.amount, td.transactionID!);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error logging stats for transaction {td.transactionID}: {e.Message}");
        }
    }
    public static async Task UpdateStats(string transactionID, decimal newAmount, decimal newReceived)
    {
        try
        {
            dynamic? transaction = await Server.db.Collections("transactions").GetFirstListItem($"id=\"{transactionID}\"");
            if (transaction == null)
            {
                Debug.LogError($"Transaction {transactionID} not found for stats update.");
                return;
            }
    
            string sender = transaction.sender.ToString();
            string receiver = transaction.receiver.ToString();
    
            // Update sender's expense log
            dynamic? senderRecord = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{sender}\"");
            if (senderRecord != null)
            {
                List<ExpenseLog> expenseLog = senderRecord.expenseLog.ToObject<List<ExpenseLog>>() ?? new List<ExpenseLog>();
                var logEntry = expenseLog.FirstOrDefault(log => log.transaction == transactionID);
                if (logEntry != null)
                {
                    logEntry.amount = newAmount;
                    await Server.db.Collections("account_stats").Update(senderRecord.id.ToString(), new
                    {
                        expenseLog = expenseLog
                    });
                }
            }
    
            // Update receiver's income log
            dynamic? receiverRecord = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{receiver}\"");
            if (receiverRecord != null)
            {
                List<IncomeLog> incomeLog = receiverRecord.incomeLog.ToObject<List<IncomeLog>>() ?? new List<IncomeLog>();
                var logEntry = incomeLog.FirstOrDefault(log => log.transaction == transactionID);
                if (logEntry != null)
                {
                    logEntry.amount = newReceived;
                    await Server.db.Collections("account_stats").Update(receiverRecord.id.ToString(), new
                    {
                        incomeLog = incomeLog
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating stats for transaction {transactionID}: {e.Message}");
        }
    }
    public static async Task DeleteStats(string transactionID, string sender, string receiver)
    {
        try
        {
            // Get the logs for the sender and receiver
            dynamic? senderRecord = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{sender}\"");
            dynamic? receiverRecord = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{receiver}\"");
    
            // Remove the transaction from the logs
            if (senderRecord != null)
            {
                List<ExpenseLog> expenseLog = senderRecord.expenseLog.ToObject<List<ExpenseLog>>() ?? new List<ExpenseLog>();
                expenseLog.RemoveAll(log => log.transaction == transactionID);
                await Server.db.Collections("account_stats").Update(senderRecord.id.ToString(), new
                {
                    expenseLog = expenseLog
                });
            }
            if (receiverRecord != null)
            {
                List<IncomeLog> incomeLog = receiverRecord.incomeLog.ToObject<List<IncomeLog>>() ?? new List<IncomeLog>();
                incomeLog.RemoveAll(log => log.transaction == transactionID);
                await Server.db.Collections("account_stats").Update(receiverRecord.id.ToString(), new
                {
                    incomeLog = incomeLog
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error deleting stats for transaction {transactionID}: {e.Message}");
        }
    }
    public static async Task CheckDeleteGDP(string transactionID, decimal amount, string description)
    {
        try
        {
            if (description.ToLower().Contains("zahlung an "))
            {
                await RemoveGDP(amount, transactionID);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error checking/deleting GDP for transaction {transactionID}: {e.Message}");
        }
    }
    public static async Task AppendExpense(string account, decimal amount, bool company, string remote, string? transaction = "")
    {
        try
        {
            var log = new ExpenseLog
            {
                amount = amount,
                company = company,
                remote = remote,
                timestamp = DateTime.UtcNow,
                transaction = transaction ?? ""
            };
            dynamic? record = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{account}\"");
            if (record == null)
            {
                await Server.db.Collections("account_stats").Create(new
                {
                    account = account,
                    incomeLog = new List<IncomeLog>(),
                    expenseLog = new List<ExpenseLog> { log }
                });
            }
            else
            {
                List<ExpenseLog> expenseLog = record.expenseLog.ToObject<List<ExpenseLog>>() ?? new List<ExpenseLog>();
                expenseLog.Add(log);
                if (expenseLog.Count > statsToKeep)
                {
                    expenseLog.RemoveAt(0); // Remove the oldest entry
                }
                dynamic updateData = new
                {
                    expenseLog = expenseLog.ToArray()
                };
                await Server.db.Collections("account_stats").Update(record.id.ToString(), updateData);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error appending income for account {account}: {e.Message}");
            Debug.LogError(e.StackTrace!);
        }
    }
    public static async Task AppendIncome(string account, decimal amount, bool company, string remote, string? transaction = "")
    {
        try
        {
            var log = new IncomeLog
            {
                amount = amount,
                company = company,
                remote = remote,
                timestamp = DateTime.UtcNow,
                transaction = transaction ?? ""
            };
            dynamic? record = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{account}\"");
            if (record == null)
            {
                await Server.db.Collections("account_stats").Create(new
                {
                    account = account,
                    incomeLog = new List<IncomeLog> { log },
                    expenseLog = new List<ExpenseLog>()
                });
            }
            else
            {
                List<IncomeLog> incomeLog = record.incomeLog.ToObject<List<IncomeLog>>() ?? new List<IncomeLog>();
                incomeLog.Add(log);
                if (incomeLog.Count > statsToKeep)
                {
                    incomeLog.RemoveAt(0); // Remove the oldest entry
                }
                dynamic updateData = new
                {
                    incomeLog = incomeLog.ToArray()
                };
                await Server.db.Collections("account_stats").Update(record.id.ToString(), updateData);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error appending income for account {account}: {e.Message}");
            Debug.LogError(e.StackTrace!);
        }
    }
    public static async Task AddGDP(decimal amount, string transactionID)
    {
        // Get the full hour of the transaction
        DateTime transactionHour = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0);
        JObject data = new JObject();
        data["value+"] = amount;
        dynamic? oldRecord = await Server.db.Collections("stats").GetFirstListItem($"key=\"gdp\" && timestamp = \"{transactionHour.ToString("yyyy-MM-ddTHH:00:00Z")}\"");
        if (oldRecord == null)
        {
            data["key"] = "gdp";
            data["timestamp"] = transactionHour.ToString("yyyy-MM-ddTHH:00:00Z");
            await Server.db.Collections("stats").Create(data);
        }
        else
        {
            await Server.db.Collections("stats").Update(oldRecord.id.ToString(), data);
        }
    }
    public static async Task RemoveGDP(decimal amount, string transactionID)
    {
        // Get the full hour of the transaction
        DateTime transactionHour = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0);
        JObject data = new JObject();
        data["value-"] = amount;
        dynamic? oldRecord = await Server.db.Collections("stats").GetFirstListItem($"key=\"gdp\" && timestamp = \"{transactionHour.ToString("yyyy-MM-ddTHH:00:00Z")}\"");
        if (oldRecord == null)
        {
            data["key"] = "gdp";
            data["timestamp"] = transactionHour.ToString("yyyy-MM-ddTHH:00:00Z");
            await Server.db.Collections("stats").Create(data);
        }
        else
        {
            await Server.db.Collections("stats").Update(oldRecord.id.ToString(), data);
        }
    }
    public static async Task<List<IncomeLog>> GetIncomeLogs(string account)
    {
        dynamic? record = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{account}\"");
        if (record == null)
        {
            return new List<IncomeLog>();
        }
        return record.incomeLog.ToObject<List<IncomeLog>>() ?? new List<IncomeLog>();
    }
    public static async Task<List<ExpenseLog>> GetExpenseLogs(string account)
    {
        dynamic? record = await Server.db.Collections("account_stats").GetFirstListItem($"account = \"{account}\"");
        if (record == null)
        {
            return new List<ExpenseLog>();
        }
        return record.expenseLog.ToObject<List<ExpenseLog>>() ?? new List<ExpenseLog>();
    }
}
public class ExpenseLog
{
    public decimal amount { get; set; }
    public bool company { get; set; }
    public string remote { get; set; } = "";
    public DateTime timestamp { get; set; }
    public string transaction { get; set; } = "";
}
public class IncomeLog
{
    public decimal amount { get; set; }
    public bool company { get; set; }
    public string remote { get; set; } = "";
    public DateTime timestamp { get; set; }
    public string transaction { get; set; } = "";
}
