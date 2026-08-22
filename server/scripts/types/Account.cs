public class Account
{
    public string id { get; set; } = string.Empty;
    public string user { get; set; } = string.Empty;
    public decimal balance { get; set; } = 0.0m;
    public bool frozen { get; set; } = false;
    public bool trusted { get; set; } = false; // If true all tax checks will be set to 'approved'. Tax will still be applied.
}