namespace Max35050;

public class TaxScoreRule
{
    public TaxCheckType Type { get; set; }
    public double Score { get; set; }
    public string Operator { get; set; } = "+"; // Default to +, also support "-", "*", "/", "="
    public double NScore { get; set; } = 0; // Used if the rule is evaluated to false
    public string NOperator { get; set; } = "+"; // Used if the rule is evaluated to false, default to +
}