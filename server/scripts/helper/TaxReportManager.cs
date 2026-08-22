using AngleSharp.Css.Dom;
using Max35050.Auth;
using Max35050.Net;

namespace Max35050.Helper;

public static class TaxReportManager
{
    public static decimal taxToAdd = 0.0m;
    public static async Task CreateTaxReport(TransactionData td)
    {
        try
        {
            dynamic record = new {
                account = td.sender,
                transaction = td.transactionID,
                originalAmount = td.amount,
                vatAmount = td.taxResult != null ? td.taxResult.VATAmount : 0.0m,
                incomeTaxAmount = td.taxResult != null ? td.taxResult.IncomeTaxAmount : 0.0m,
                transactionTaxAmount = td.taxResult != null ? td.taxResult.TransactionTaxAmount : 0.0m,
                taxFreePoolUsed = td.taxResult != null ? td.taxResult.TaxFreePoolUsed : 0.0m,
                newTaxFreePool = td.taxResult != null ? td.taxResult.NewTaxFreePool : 0.0m,
                totalTaxAmount = td.taxResult != null ? td.taxResult.TotalTaxAmount : 0.0m,
                previousIncomeReceived = td.taxResult != null ? td.taxResult.PreviousIncomeReceived : 0.0m,
            };
            td.taxReport = (await Server.db.Collections("tax_logs").Create(record)).id.ToString();
        }
        catch { }
    }
    public static async Task AppendExtraInfo(string id, string extraInfo)
    {
        try
        {
            dynamic? record = await Server.db.Collections("tax_logs").GetOne(id);
            if (record == null) { return; }
            record.extraInfo = extraInfo;
            await Server.db.Collections("tax_logs").Update(id, record);
        }
        catch { }
    }
}