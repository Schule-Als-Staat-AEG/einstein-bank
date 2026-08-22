using Max35050.Net;
namespace Max35050.Helper;

public static class TaxManager
{
    public static TaxSettings CurrentTaxSettings { get; set; } = new TaxSettings();
    public static async Task Initialize()
    {
        await PullTaxSettings();
    }
    public static async Task PullTaxSettings()
    {
        CurrentTaxSettings.Enabled = bool.Parse(SettingsManager.GetSetting("tax_enabled"));
        CurrentTaxSettings.VATEnabled = bool.Parse(SettingsManager.GetSetting("tax_vat_enable"));
        CurrentTaxSettings.VATRate = decimal.Parse(SettingsManager.GetSetting("tax_vat_amount"));
        CurrentTaxSettings.IncomeTaxEnabled = bool.Parse(SettingsManager.GetSetting("tax_income_enable"));
        CurrentTaxSettings.WealthTaxEnabled = bool.Parse(SettingsManager.GetSetting("tax_wealth_enable"));
        CurrentTaxSettings.EnableTransactionTax = bool.Parse(SettingsManager.GetSetting("tax_transaction_enable"));
        CurrentTaxSettings.TransactionTaxRate = decimal.Parse(SettingsManager.GetSetting("tax_transaction_amount"));
        CurrentTaxSettings.TransactionTaxFreePool = decimal.Parse(SettingsManager.GetSetting("tax_transaction_free"));
        // Income Tax Brackets
        CurrentTaxSettings.IncomeTaxBrackets.Clear();
        try
        {
            (await Server.db.Collections("income_tax_brackets").GetFullList()).ForEach(record =>
            {
                TaxBracket bracket = new TaxBracket
                {
                    FromIncome = decimal.Parse(record.from.ToString()),
                    Rate = decimal.Parse(record.amount.ToString())
                };
                CurrentTaxSettings.IncomeTaxBrackets.Add(bracket);
            });
        }
        catch { }
        // Wealth Tax Brackets
        CurrentTaxSettings.WealthBrackets.Clear();
        try
        {
            (await Server.db.Collections("wealth_tax_brackets").GetFullList()).ForEach(record =>
            {
                WealthBracket bracket = new WealthBracket
                {
                    from = decimal.Parse(record.from.ToString()),
                    amount = decimal.Parse(record.amount.ToString()),
                    company = bool.Parse(record.company.ToString())
                };
                CurrentTaxSettings.WealthBrackets.Add(bracket);
            });
        }
        catch { }
    }
    public static async Task<TaxResult> CalculateTaxes(TransactionData td, bool admin = false)
    {
        UserTaxData utd = await TransactionDataManager.GetTaxData(td.sender);
        TaxResult result = new TaxResult();
        if (!CurrentTaxSettings.Enabled || utd.TaxExempt)
        {
            result.TaxFreePoolUsed = 0.0m;
            result.NewTaxFreePool = utd.TransactionTaxFreePool;
            result.VATAmount = 0.0m;
            result.IncomeTaxAmount = 0.0m;
            result.TransactionTaxAmount = 0.0m;
            td.taxResult = result;
            return result;
        }

        // VAT Calculation
        if (CurrentTaxSettings.VATEnabled && !utd.VatExempt && td.taxData.VATApplicable)
        {
            result.VATAmount = td.amount * (utd.VatOverride >= 0.0m ? utd.VatOverride : CurrentTaxSettings.VATRate) * utd.GlobalMultiplier;
        }

        // Income Tax Calculation
        UserTaxData rtd = await TransactionDataManager.GetTaxData(td.receiver); // rtd = Remote Tax Data
        result.PreviousIncomeReceived = rtd.TotalIncomeReceived;
        if (CurrentTaxSettings.IncomeTaxEnabled && !rtd.IncomeExempt && td.taxData.IncomeTaxApplicable)
        {
            // Pogressive Income Tax
            // Aka for 1000 income with brackets 0-500 5%, 500-1000 10%
            // You pay 25 + 50 = 75 tax
            decimal income = td.amount;
            foreach (TaxBracket bracket in CurrentTaxSettings.IncomeTaxBrackets.OrderBy(b => b.FromIncome)) // Make sure brackets are in correct order (sorted from lowest to highest)
            {
                if (income > bracket.FromIncome - rtd.TotalIncomeReceived) // If someone is receiving multiple income transactions we need to take into account the money already received in the past, so we don't reset to 0 with every transaction
                {
                    decimal taxableAmount = 0.0m;
                    if (CurrentTaxSettings.IncomeTaxBrackets.IndexOf(bracket) == CurrentTaxSettings.IncomeTaxBrackets.Count - 1)
                    {
                        // Last Bracket
                        taxableAmount = income - Math.Max(0, bracket.FromIncome - rtd.TotalIncomeReceived);
                    }
                    else
                    {
                        TaxBracket nextBracket = CurrentTaxSettings.IncomeTaxBrackets[CurrentTaxSettings.IncomeTaxBrackets.IndexOf(bracket) + 1];
                        taxableAmount = Math.Min(income, nextBracket.FromIncome) - Math.Max(0, bracket.FromIncome - rtd.TotalIncomeReceived);
                    }
                    result.IncomeTaxAmount += taxableAmount * bracket.Rate * rtd.GlobalMultiplier;
                }
            }
        }

        // Transaction Tax Calculation
        if ((CurrentTaxSettings.EnableTransactionTax && !utd.TransactionExempt && !admin && !td.taxData.IncomeTaxApplicable && !td.taxData.VATApplicable) || (admin && td.taxData.TransactionTaxApplicable && !utd.TransactionExempt && !td.taxData.IncomeTaxApplicable && !td.taxData.VATApplicable))
        {
            decimal taxFreePool = utd.TransactionTaxFreePool;
            decimal taxableAmount = Math.Max(0.0m, td.amount - taxFreePool);
            result.TransactionTaxAmount = taxableAmount * (utd.TransactionAmountOverride >= 0.0m ? utd.TransactionAmountOverride : CurrentTaxSettings.TransactionTaxRate) * utd.GlobalMultiplier;
            result.TransactionTaxAmount = Math.Round(result.TransactionTaxAmount, 2); // Round to 2 decimal places

            // Update Tax Free Pool
            decimal usedFromPool = Math.Min(td.amount, taxFreePool);
            result.TaxFreePoolUsed = usedFromPool;
            result.NewTaxFreePool = taxFreePool - usedFromPool;

            if (td.amount > 0.0m && result.TaxFreePoolUsed == 0.0m && result.NewTaxFreePool != 0.0m)
            {
                result.NewTaxFreePool = taxFreePool - 0.01m; // Minimum tax to prevent abuse of tax free pool
            }
            if (td.amount > 0.0m && result.TransactionTaxAmount == 0.0m && result.NewTaxFreePool == 0.0m)
            {
                result.TransactionTaxAmount = 0.01m; // Minimum tax to prevent abuse of tax free pool
            }
        }
        // Check if the transaction tax deduct is not exactly 0 but smaller than 0.01, if so set it to 0.01 to prevent abuse of tax free pool
        if (result.TransactionTaxAmount > 0.0m && result.TransactionTaxAmount < 0.01m)
        {
            result.TransactionTaxAmount = 0.01m;
        }
        td.taxResult = result;
        return result;
    }
    public static async Task<TaxResult?> GetTaxResult(string transactionID)
    {
        dynamic? taxLog = await Server.db.Collections("tax_logs").GetFirstListItem($"transaction=\"{transactionID}\"");
        if(taxLog == null)
        {
            return null;
        }
        TaxResult result = new TaxResult
        {
            VATAmount = decimal.Parse(taxLog.vatAmount.ToString()),
            IncomeTaxAmount = decimal.Parse(taxLog.incomeTaxAmount.ToString()),
            TransactionTaxAmount = decimal.Parse(taxLog.transactionTaxAmount.ToString()),
            TaxFreePoolUsed = decimal.Parse(taxLog.taxFreePoolUsed.ToString()),
            NewTaxFreePool = decimal.Parse(taxLog.newTaxFreePool.ToString()),
            PreviousIncomeReceived = decimal.Parse(taxLog.previousIncomeReceived.ToString())
        };
        return result;
    }
}

public class TaxSettings
{
    public bool Enabled { get; set; } = false;
    public bool VATEnabled { get; set; } = false;
    public bool IncomeTaxEnabled { get; set; } = false;
    public bool WealthTaxEnabled { get; set; } = false;
    public bool EnableTransactionTax { get; set; } = false;
    public decimal VATRate { get; set; } = 0.0m;
    public List<TaxBracket> IncomeTaxBrackets { get; set; } = new List<TaxBracket>();
    public decimal TransactionTaxRate { get; set; } = 0.0m;
    public decimal TransactionTaxFreePool { get; set; } = 0.0m;
    public int WealthTaxTime { get; set; } = 0; // Time in seconds when wealth tax should be deducted, 0 for disabled
    public List<WealthBracket> WealthBrackets { get; set; } = new List<WealthBracket>();
}
public class TaxBracket
{
    public decimal FromIncome { get; set; } = 0.0m;
    public decimal Rate { get; set; } = 0.0m;
}
public class WealthBracket
{
    public decimal from;
    public decimal amount; // In percent => 0.2 means 20% tax
    public bool company;
}