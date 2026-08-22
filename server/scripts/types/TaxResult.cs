using System.Text.Json.Serialization;

namespace Max35050;

public class TaxResult
{
    public decimal VATAmount { get; set; } = 0.0m;
    public decimal IncomeTaxAmount { get; set; } = 0.0m;
    public decimal TransactionTaxAmount { get; set; } = 0.0m;
    public decimal TaxFreePoolUsed { get; set; } = 0.0m;
    public decimal NewTaxFreePool { get; set; } = 0.0m;
    [JsonIgnore] [Newtonsoft.Json.JsonIgnore] public decimal PreviousIncomeReceived { get; set; } = 0.0m;
    public decimal TotalTaxAmount
    {
        get
        {
            return VATAmount + IncomeTaxAmount + TransactionTaxAmount;
        }
    }
}