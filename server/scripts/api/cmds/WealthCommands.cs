using Max35050.Systems;

namespace Max35050.Cmd;

public static class WealthCommands
{
    public static void WealthInfoCommand()
    {
        Debug.Log($"Wealth Tax Manager Info:", true);
        Debug.Log($"  Time to Deduct: {WealthTaxManager.timeToDeduct}", true);
        Debug.Log($"  Last Deduction Time (UTC): {WealthTaxManager.lastDeductionTime}", true);
        Debug.Log($"  Deducted Today: {WealthTaxManager.deductedToday}", true);
        Debug.Log($"  Worker Thread Running: {WealthTaxManager.workerThread != null && !WealthTaxManager.workerThread.IsCompleted}", true);
    }
    public static void WealthResetCommand()
    {
        _ = WealthTaxManager.Initialize();
        Debug.Log("Wealth tax worker thread has been reset.", true);
    }
    public static void DeductNow()
    {
        WealthTaxManager.lastDeductionTime = DateTime.UtcNow.AddDays(-1); // Set last deduction time to 24 hours ago to force a deduction on the next worker loop
        WealthTaxManager.deductedToday = false; // Reset deductedToday to allow deduction
        Debug.Log("Wealth tax worker scheduled to deduct now.", true);
    }

}