namespace Max35050;

public class TransactionTaxData
{
    public bool VATApplicable { get; set; } = false; // User defined value - when wrong: IRS go brrr
    public bool IncomeTaxApplicable { get; set; } = false; // User defined value - when wrong: IRS go brrr
    public bool TransactionTaxApplicable { get; set; } = true; // Used for Admins only
}