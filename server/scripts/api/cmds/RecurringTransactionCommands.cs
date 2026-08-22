using Max35050.Systems;

namespace Max35050.Cmd;

public static class RecurringTransactionCommands
{
    public static void ShowInfo()
    {
        Debug.LogOK("Recurring Transactions Info:\n" +
            $"  Clock Info:\n" +
            $"  Current Time (Server): {DateTime.Now}\n" +
            $"  Current Time (UTC): {DateTime.UtcNow}\n" +
            $"  Total Recurring Transactions: {RecurringTransactionManager.recurringTransactions.Count}\n" +
            $"  Last Reset: {RecurringTransactionManager.lastReset}\n" +
            $"  Next Check: {RecurringTransactionManager.nextCheck}\n" +
            $"  Worker Running: {(RecurringTransactionManager.Worker != null && !RecurringTransactionManager.Worker.IsCompleted)}", true);
    }
    public static async Task ResetWorker()
    {
        // Cancel existing worker
        if (RecurringTransactionManager.Worker != null && !RecurringTransactionManager.Worker.IsCompleted)
        {
            RecurringTransactionManager.cts.Cancel();
            await RecurringTransactionManager.Worker;
        }
        // Reset the list
        RecurringTransactionManager.recurringTransactions.Clear();
        await RecurringTransactionManager.Initialize();
    }
    public static void GetRecurringTransaction(string id)
    {
        RecurringTransaction? rt = RecurringTransactionManager.recurringTransactions.Find(r => r.id == id);
        if (rt != null)
        {
            Debug.LogOK($"Recurring Transaction Info for ID {id}:\n" +
                $"  From Account ID: {rt.fromAccountID}\n" +
                $"  To Account ID: {rt.toAccountID}\n" +
                $"  Amount: {rt.amount}\n" +
                $"  Max Amount: {rt.maxAmount}\n" +
                $"  Paid Amount: {rt.paid}\n" +
                $"  Created At: {rt.createdAt}\n" +
                $"  Paid Today: {rt.paidToday}\n" +
                $"  Time: {rt.time}\n" +
                $"  Pays On (UTC): {DateTime.Today.AddSeconds(rt.time).AddHours(rt.TimezoneOffset * -1)}\n" +
                $"  Days: {rt.days.ToString()}\n" +
                $"  Pays On (Zone adjusted): {DateTime.Today.AddSeconds(rt.time)}\n" +
                $"  Timezone: {rt.timezone} - {rt.TimezoneOffset}\n", true);
        }
        else
        {
            Debug.LogError($"Recurring Transaction with ID {id} does not exist.", true);
        }
    }
    public static void SetPaidToday(string id, bool paidToday)
    {
        RecurringTransaction? rt = RecurringTransactionManager.recurringTransactions.Find(r => r.id == id);
        if (rt != null)
        {
            rt.paidToday = paidToday;
            Debug.LogOK($"Set Recurring Transaction {id} paidToday to {paidToday}.", true);
        }
        else
        {
            Debug.LogError($"Recurring Transaction with ID {id} does not exist.", true);
        }
    }
    public static void SetPaidToday(string id)
    {
        RecurringTransaction? rt = RecurringTransactionManager.recurringTransactions.Find(r => r.id == id);
        if (rt != null)
        {
            rt.paidToday = !rt.paidToday;
            Debug.LogOK($"Toggled Recurring Transaction {id} paidToday to {rt.paidToday}.", true);
        }
        else
        {
            Debug.LogError($"Recurring Transaction with ID {id} does not exist.", true);
        }
    }
}