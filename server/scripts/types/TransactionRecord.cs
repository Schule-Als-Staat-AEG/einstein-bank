namespace Max35050;

public class TransactionRecord
{
    public string id { get; set; } = "";
    public string sender { get; set; } = "";
    public string receiver { get; set; } = "";
    public decimal amount { get; set; }
    public decimal received { get; set; }
    public string description { get; set; } = "";
    public string system { get; set; } = "";
    public string signature { get; set; } = "";
    public string status { get; set; } = "";
    public DateTime created { get; set; }
}