using Cronos;

namespace Max35050;

public class Contract
{
    public string id { get; set; } = "";
    public string title { get; set; } = "";
    public string description { get; set; } = "";
    public string sender { get; set; } = "";
    public string receiver { get; set; } = "";
    public decimal amount { get; set; }
    public string cron { get; set; } = "";
    public int maximum { get; set; } = -1;
    public int executed { get; set; } = 0;
    public bool completed { get; set; } = false;
    public DateTime created { get; set; } = DateTime.UtcNow;
    public DateTime? nextExecution { get; set; } = null;
    public CronExpression? cronExpression { get; set; } = null;
}