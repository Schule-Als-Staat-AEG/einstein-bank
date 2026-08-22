namespace Max35050;

public class UserTaxData
{
    public bool TaxExempt { get; set; } = false;
    public bool VatExempt { get; set; } = false;
    public bool IncomeExempt { get; set; } = false;
    public bool WealthExempt { get; set; } = false;
    public bool TransactionExempt { get; set; } = false;
    public bool CompanyExempt { get; set; } = false;

    // Overrides -1 means use default settings
    public decimal VatOverride { get; set; } = -1.0m;
    public decimal IncomeOverride { get; set; } = -1.0m;
    public decimal TransactionAmountOverride { get; set; } = -1.0m;
    public decimal TransactionFreePoolOverride { get; set; } = -1.0m;
    public decimal CompanyOverride { get; set; } = -1.0m;

    // Global Override
    public decimal GlobalMultiplier { get; set; } = 1.0m;

    // Tax Free Pool for Transaction Tax
    public decimal TransactionTaxFreePool { get; set; } = 0.0m;
    public decimal TotalIncomeReceived { get; set; } = 0.0m; // For Income Tax purposes, resets with tax pool reset
}