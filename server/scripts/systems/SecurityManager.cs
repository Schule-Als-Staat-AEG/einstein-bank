using Max35050.IO;

namespace Max35050.Systems;

public static class SecurityManager
{
    public static bool ForwardedIPEnabled = false;
    public static string ForwardedIPHeader = "X-Forwarded-For";
    public static List<AccessLog> AccessLogs = new List<AccessLog>();
    public static int AccessLogMaxRetentionMinutes = 60;
    public static int TransactionMaxIps = 5;
    public static int BlockAfterAttempts = 5;
    public static int BlockDurationMinutes = 10;
    public static bool IPBlocklistEnabled = false;
    public static bool IPBlockingEnabled = false;
    public static int TransactionIpTimeWindowSeconds = 30;
    public static CancellationTokenSource cts = new CancellationTokenSource();
    public static Dictionary<string, DateTime> BannedIPs = new Dictionary<string, DateTime>();
    private static bool dirty = false;
    public static bool RequestScanningEnabled = true;
    public static void Initialize()
    {
        cts.Cancel();
        SaveBannedIPs();
        LoadBannedIPs();
        CancellationTokenSource newCts = new CancellationTokenSource();
        cts = newCts;
        Task.Run(() => CleanupOldAccessLogs(cts.Token), cts.Token);
    }
    public static void Stop()
    {
        cts.Cancel();
        SaveBannedIPs(true);
    }
    public static void SaveBannedIPs(bool forceSave = false)
    {
        if (!dirty && !forceSave) return;
        Dictionary<string, Dictionary<string, string>> bannedIps = new Dictionary<string, Dictionary<string, string>>();
        foreach (KeyValuePair<string, DateTime> pair in BannedIPs)
        {
            bannedIps[pair.Key] = new Dictionary<string, string>
            {
                { "expiresAt", pair.Value.ToString("o") }
            };
        }
        File.WriteAllText(Path.Combine(FileManager.DataPath, "banned_ips.ini"), INIParser.WriteINI(bannedIps));
        dirty = false;
    }
    public static void LoadBannedIPs()
    {
        string path = Path.Combine(FileManager.DataPath, "banned_ips.ini");
        if (File.Exists(path))
        {
            Dictionary<string, Dictionary<string, string>> bannedIps = INIParser.ReadINI(File.ReadAllText(path));
            foreach (KeyValuePair<string, Dictionary<string, string>> pair in bannedIps)
            {
                if (pair.Value.ContainsKey("expiresAt"))
                {
                    DateTime expiresAt = DateTime.Parse(pair.Value["expiresAt"]);
                    if (expiresAt > DateTime.UtcNow)
                    {
                        BannedIPs[pair.Key] = expiresAt;
                    }
                }
            }
        }
    }
    public static async Task CleanupOldAccessLogs(CancellationToken token)
    {
        while (true)
        {
            AccessLogs.RemoveAll(log => (DateTime.UtcNow - log.Timestamp).TotalMinutes > AccessLogMaxRetentionMinutes);
            // Save the banned ips to a file
            SaveBannedIPs();
            await Task.Delay(60000, token); // Check for old logs every minute
            if (token.IsCancellationRequested)
            {
                break;
            }
        }
    }
    public static async Task<(bool, SecurityResponse)> AllowRequest(HttpContext context, string account, bool allowBlocking = true)
    {
        if (!RequestScanningEnabled)
        {
            return (true, SecurityResponse.Allowed);
        }
        string ip = context.Connection.RemoteIpAddress?.ToString()!;
        if (ForwardedIPEnabled)
        {
            if (context.Request.Headers.ContainsKey(ForwardedIPHeader))
            {
                ip = context.Request.Headers[ForwardedIPHeader].ToString().Split(',')[0].Trim();
            }
        }
        if (IPBlocklistEnabled)
        {
            if (BannedIPs.ContainsKey(ip))
            {
                if (BannedIPs[ip] > DateTime.UtcNow)
                {
                    return (false, SecurityResponse.Blocked);
                }
                else
                {
                    BannedIPs.Remove(ip);
                    dirty = true;
                }
            }
        }
        // Check if there are more than TransactionMaxIpLimit transactions from different IPs for the same account in the last TransactionIpTimeWindowSeconds seconds
        var recentLogs = AccessLogs.Where(log => log.accountID == account && (DateTime.UtcNow - log.Timestamp).TotalSeconds <= TransactionIpTimeWindowSeconds).ToList();
        var uniqueIps = recentLogs.Select(log => log.IP).Distinct().ToList();
        if (uniqueIps.Count > TransactionMaxIps)
        {
            if (allowBlocking && IPBlockingEnabled)
            {
                // Block the IP for BlockDurationMinutes minutes
                BannedIPs[ip] = DateTime.UtcNow.AddMinutes(BlockDurationMinutes);
                dirty = true;
            }
            return (false, SecurityResponse.ToManyIPs);
        }
        // Check if there has been over BlockAfterAttempts failed attempts from the same IP for the same account in the last TransactionIpTimeWindowSeconds seconds
        var failedAttempts = recentLogs.Count(log => log.IP == ip && !log.Success);
        if (failedAttempts >= BlockAfterAttempts)
        {
            if (allowBlocking && IPBlockingEnabled)
            {
                // Block the IP for BlockDurationMinutes minutes
                BannedIPs[ip] = DateTime.UtcNow.AddMinutes(BlockDurationMinutes);
                dirty = true;
            }
            return (false, SecurityResponse.ToManyFailedAttempts);
        }
        return (true, SecurityResponse.Allowed);
    }
    public static async Task LogAccess(HttpContext context, string account, bool success)
    {
        AccessLog accessLog = new AccessLog();
        string ip = context.Connection.RemoteIpAddress?.ToString()!;
        if (ForwardedIPEnabled)
        {
            if (context.Request.Headers.ContainsKey(ForwardedIPHeader))
            {
                ip = context.Request.Headers[ForwardedIPHeader].ToString().Split(',')[0].Trim();
            }
        }
        accessLog.IP = ip;
        accessLog.Timestamp = DateTime.UtcNow;
        accessLog.accountID = account;
        accessLog.UserAgent = context.Request.Headers["User-Agent"].ToString();
        accessLog.Success = success;
        AccessLogs.Add(accessLog);
    }
}
public class AccessLog
{
    public string IP { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string accountID { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool Success { get; set; }
}
public enum SecurityResponse
{
    Allowed,
    ToManyRequests,
    ToManyIPs,
    ToManyFailedAttempts,
    Blocked
}