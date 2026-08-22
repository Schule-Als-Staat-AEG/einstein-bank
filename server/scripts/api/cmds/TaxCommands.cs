using Max35050.Helper;

namespace Max35050.Cmd;

public static class TaxCommands
{
    public static void ShowTaxSettings()
    {
        Debug.LogOK("Current Tax Settings:", true);
        Debug.LogOK($"  Tax Account ID: {SettingsManager.taxAccountID}", true);
        Debug.LogOK($"  Taxation Enabled: {TaxManager.CurrentTaxSettings.Enabled}", true);
        Debug.LogOK($"  ---------------------------------", true);
        Debug.LogOK($"  Transaction Tax Enabled: {TaxManager.CurrentTaxSettings.EnableTransactionTax}", true);
        Debug.LogOK($"  Wealth Tax Enabled: {TaxManager.CurrentTaxSettings.WealthTaxEnabled}", true);
        Debug.LogOK($"  VAT Enabled: {TaxManager.CurrentTaxSettings.VATEnabled}", true);
        Debug.LogOK($"  Income Tax Enabled: {TaxManager.CurrentTaxSettings.IncomeTaxEnabled}", true);
        Debug.LogOK($"  ---------------------------------", true);
        Debug.LogOK($"  Transaction Tax Rate: {TaxManager.CurrentTaxSettings.TransactionTaxRate * 100}%", true);
        Debug.LogOK($"  Transaction Tax Free Pool: {TaxManager.CurrentTaxSettings.TransactionTaxFreePool}", true);
        Debug.LogOK($"  ---------------------------------", true);
        Debug.LogOK($"  VAT Rate: {TaxManager.CurrentTaxSettings.VATRate * 100}%", true);
        Debug.LogOK($"  ---------------------------------", true);
        Debug.LogOK($"  Income Tax Brackets:", true);
        if (TaxManager.CurrentTaxSettings.IncomeTaxBrackets.Count == 0)
        {
            Debug.LogOK($"    No income tax brackets defined.", true);
        }
        else
        {
            TaxManager.CurrentTaxSettings.IncomeTaxBrackets.ForEach(bracket =>
            {
                Debug.LogOK($"    From {bracket.FromIncome} - Rate {bracket.Rate * 100}%", true);
            });
        }
        Debug.LogOK($"  ---------------------------------", true);
        Debug.LogOK($"  Notify Transactions: {SettingsManager.notifyTransactions}", true);
    }
}