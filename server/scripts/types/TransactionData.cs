using System.Numerics;

namespace Max35050;

public class TransactionData
{
    public string sender { get; set; } = "";
    public string receiver { get; set; } = "";
    public decimal amount { get; set; }
    public decimal received { get; set; }
    public string description { get; set; } = "";
    public string system { get; set; } = "";
    public string code { get; set; } = "";
    public string pin { get; set; } = "";
    public DateTime? timestamp { get; set; } = DateTime.UtcNow; // Used for admins only - clients will use DatTime.UtcNow when logging transaction
    public TransactionTaxData taxData { get; set; } = new TransactionTaxData();
    public UserTaxData? userTaxData { get; set; } = new UserTaxData();
    public TaxResult? taxResult { get; set; } = new TaxResult();
    public string taxReport { get; set; } = "";
    public bool logTransaction { get; set; } = true; // Admin use only
    public bool moveMoney { get; set; } = true; // Admin use only
    public bool applyTaxes { get; set; } = true; // Admin use only
    public string? transactionID { get; set; } = null;
    public TransactionStatus status { get; set; } = TransactionStatus.Pending;
    public double suspiciousScore { get; set; } = 0;
    public string suspiciousReasons { get; set; } = "";
    public string captchaID { get; set; } = "";
    public long captchaAnswer { get; set; } = 0;
    public decimal totalDeductable { get => 
        amount + 
        (taxResult != null ? 
            (taxResult.VATAmount + 
            taxResult.IncomeTaxAmount + 
            taxResult.TransactionTaxAmount) 
        : 0.0m);
    }
}
public enum TransactionStatus
{
    Flagged = 0,
    Pending = 1,
    Approved = 2,
    Denied = 3,
    UnderAudit = 4
}

public static class TransactionStatusExtentions {
    public static string ToFriendlyString(this TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.UnderAudit => "Under Audit",
            TransactionStatus.Denied => "Denied",
            TransactionStatus.Flagged => "Flagged",
            TransactionStatus.Pending => "Pending",
            TransactionStatus.Approved => "Approved",
            _ => "Unknown"
        };
    }
    public static TransactionStatus FromString(string status)
    {
        return status.ToLower() switch
        {
            "denied" => TransactionStatus.Denied,
            "flagged" => TransactionStatus.Flagged,
            "pending" => TransactionStatus.Pending,
            "approved" => TransactionStatus.Approved,
            "under_audit" => TransactionStatus.UnderAudit,
            _ => throw new ArgumentException("Invalid transaction status string")
        };
    }
    public static string ToString(this TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.UnderAudit => "under_audit",
            TransactionStatus.Denied => "denied",
            TransactionStatus.Flagged => "flagged",
            TransactionStatus.Pending => "pending",
            TransactionStatus.Approved => "approved",
            _ => "unknown"
        };
    }
}