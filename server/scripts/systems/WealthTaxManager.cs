using Max35050.Helper;

namespace Max35050.Systems;

public static class WealthTaxManager
{
    public static Task? workerThread = null;
    public static CancellationTokenSource cts = new CancellationTokenSource();
    public static TimeOnly timeToDeduct;
    public static bool deductedToday = false;
    public static DateTime lastDeductionTime = DateTime.MinValue;
    
    public static async Task Initialize()
    {
        // Convert seconds to hour, minute, second
        cts.Cancel(); // Cancel any existing worker thread
        while(workerThread != null && !workerThread.IsCompleted)
        {
            await Task.Delay(100); // Wait for the worker thread to finish
        }
        cts = new CancellationTokenSource();
        timeToDeduct = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(TaxManager.CurrentTaxSettings.WealthTaxTime));
        lastDeductionTime = DateTime.UtcNow;
        if (DateTime.Now.TimeOfDay > timeToDeduct.ToTimeSpan())
        {
            deductedToday = true;
        }
        else
        {
            deductedToday = false;
        }
        workerThread = Task.Run(() => WealthTaxWorker(cts.Token));
    }
    public static async Task WealthTaxWorker(CancellationToken token)
    {
        if(token.IsCancellationRequested)
        {
            return;
        }
        while(!token.IsCancellationRequested)
        {
            if(DateTime.Now.TimeOfDay > timeToDeduct.ToTimeSpan() && !deductedToday)
            {
                deductedToday = true;
                lastDeductionTime = DateTime.UtcNow;

                if (TaxManager.CurrentTaxSettings.WealthTaxEnabled && TaxManager.CurrentTaxSettings.Enabled)
                {
                    // Deduct wealth tax from all users
                    List<Account> accounts = await AccountManager.GetAllAccounts();
                    foreach (Account account in accounts)
                    {
                        decimal toTax = await WealthCalculator.CalculateTotalWealthTax(account.id);
                        
                        if (toTax > 0 && !account.frozen)
                        {
                            
                            toTax = Math.Round(toTax, 2); // Round to 2 decimal places
                            await AccountManager.SubtractBalance(account.id, toTax);
                            await AccountManager.AddBalance(SettingsManager.taxAccountID, toTax);
                            TransactionData td = new TransactionData
                            {
                                sender = account.id,
                                receiver = SettingsManager.taxAccountID,
                                amount = toTax,
                                description = $"Nach aktuellen Steuergesetzen wurde eine Vermögenssteuer von {toTax} Steinen abgezogen. Die aktuellen Steuergesetzte findest du im Moodle-Kurs.",
                                system = "##Wealth_Tax_Deducted##",
                                status = TransactionStatus.Approved
                            };
                            await TransactionManager.LogTransaction(td);
                        }
                    }
                }
            }
            if (DateTime.UtcNow.Date > lastDeductionTime.Date)
            {
                deductedToday = false;
            }
            await Task.Delay(30000, token);
        }
    }
}