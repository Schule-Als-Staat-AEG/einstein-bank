namespace Max35050;

public class CodeRecord
{
    public string id { get; set; } = string.Empty;
    public string account { get; set; } = string.Empty;
    public string security { get; set; } = string.Empty;
    public string checksum { get; set; } = string.Empty;
    public bool invalidate { get; set; } = false;
    public bool frozen { get; set; } = false;
    public string number { get; set; } = string.Empty;
    public bool codeRequired { get; set; } = false;
    public string pin { get; set; } = string.Empty;
    public DateTime created { get; set; } = DateTime.UtcNow;
    public DateTime updated { get; set; } = DateTime.UtcNow;
}