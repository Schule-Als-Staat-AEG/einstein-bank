using Max35050.Helper;
using Max35050.IO;
using Max35050.Systems;

namespace Max35050.Cmd;

public static class FlaggingCommands
{
    public static void PrintStatus()
    {
        Debug.LogOK("Status of the Internal Revenue Service Helper", true);
        Debug.LogOK($"  - Worker running: {IRSHelper.workerThread != null}", true);
        Debug.LogOK($"  - Suspicious Score: {IRSHelper.susScore}", true);
        Debug.LogOK($"  - Suspicious Operator: [tx score] {IRSHelper.susOperator} [suspicious score]", true);
        Debug.LogOK($"  - Transactions to Check: {IRSHelper.transactionsToCheck.Count}", true);
        Debug.LogOK($"  - Time to check: 2500ms", true);
        Debug.LogOK($"  - Words to check [VAT]: {IRSHelper.vatWordList.Count}", true);
        Debug.LogOK($"  - Words to check [Income Tax]: {IRSHelper.incomeWordList.Count}", true);
    }
    public static void ResetWorker()
    {
        IRSHelper.workerThread = null!;
        IRSHelper.Initialize(); // Restart the worker thread
        Debug.LogOK("Invoked Cancellation Token", true);
        Debug.LogOK("Internal Revenue Service Helper worker reset.", true);
    }
    public static void WriteSettingToJSON()
    {
        TaxFlagsManager.WriteToJSON();
        Debug.LogOK("Settings written to JSON file.", true);
        Debug.LogOK("File path: " + Path.Combine(FileManager.DataPath, "exported_tax_rules.json"), true);
    }
}