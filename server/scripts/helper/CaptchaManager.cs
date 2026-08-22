using System.Numerics;
using Max35050.Auth;

namespace Max35050.Helper;

public static class CaptchaManager
{
    public static List<CaptchaAnswer> CaptchaSessions = new List<CaptchaAnswer>();
    public static CancellationTokenSource cts = new CancellationTokenSource();
    public static bool captchaEnabled = true;
    public static int captchaRateLimit = 5000; // 5 Seconds
    public static int captchaValidity = 300; // 5 Minutes
    public static void Initialize()
    {
        Task.Run(() => CleanupExpiredCaptchas(cts), cts.Token);
    }
    public static async Task CleanupExpiredCaptchas(CancellationTokenSource? token = null)
    {
        while (true)
        {
            CaptchaSessions.RemoveAll(c => c.ExpiresAt < DateTime.UtcNow);
            await Task.Delay(60000, cts.Token); // Check for expired captchas every minute
            if (token != null && token.IsCancellationRequested)
            {
                break;
            }
        }
    }
    public static Dictionary<char, char> ObfuscationMap = new Dictionary<char, char>
    {
        {'0', '4'},
        {'1', '3'},
        {'2', 'c'},
        {'3', 't'},
        {'4', '7'},
        {'5', '0'},
        {'6', '1'},
        {'7', '9'},
        {'8', '8'},
        {'9', '2'},
        {'+', 'b'},
        {'-', '5'},
        {'*', '6'}
    };
    // Generate a new Captcha which is required to perform a transaction
    public static async Task<(string, string)> GenerateCaptcha(string token)
    {
        User user = await AuthManager.GetUserFromToken(token);
        string userID = user.id;
        
        generateID:
        string captchaID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
        if (CaptchaSessions.Any(c => c.CaptchaID == captchaID))
        {
            goto generateID;
        }

        // Check if the token already has an active captcha session
        if (CaptchaSessions.Any(c => c.UserID == userID))
        {
            CaptchaAnswer oldSession = CaptchaSessions.First(c => c.UserID == userID);
            // If session has been created less than 5 Seconds ago, return two empty strings to prevent spamming captcha generation
            if ((DateTime.UtcNow - oldSession.CreatedAt).TotalMilliseconds < captchaRateLimit)
            {
                return (string.Empty, string.Empty);
            }
            // Remove old session
            CaptchaSessions.RemoveAll(c => c.UserID == userID);
        }

        long num1 = Random.Shared.Next(1, 100000); 
        long num2 = Random.Shared.Next(1, 100000);
        long num3 = Random.Shared.Next(1, 100000);
        string operation1 = new[] { "+", "-", "*" }[Random.Shared.Next(0, 3)];
        string operation2 = new[] { "+", "-", "*" }[Random.Shared.Next(0, 3)];

        long result = 0;
        result = operation1 switch
        {
            "+" => num1 + num2,
            "-" => num1 - num2,
            "*" => num1 * num2,
            _ => result
        };
        result = operation2 switch
        {
            "+" => result + num3,
            "-" => result - num3,
            "*" => result * num3,
            _ => result
        };
        if (operation1 != "*" && operation2 == "*")
        {
            result = operation1 switch
            {
                "+" => num1 + (num2 * num3),
                "-" => num1 - (num2 * num3),
                _ => result
            };
        }
        result -= 17;
        // Submit the captcha answer to the session
        CaptchaSessions.Add(new CaptchaAnswer
        {
            UserID = userID,
            CaptchaID = captchaID,
            Answer = result,
            ExpiresAt = DateTime.UtcNow.AddSeconds(captchaValidity)
        });

        string captchaQuestion = $"{num1}{operation1}{num2}{operation2}{num3}";
        // Obfuscate the captcha question
        string obfuscatedCaptcha = string.Concat(captchaQuestion.Select(c => ObfuscationMap.ContainsKey(c) ? ObfuscationMap[c] : c));
        return (captchaID, obfuscatedCaptcha);
    }
    public static bool ValidateCaptcha(string token, string captchaID, long answer)
    {
        User user = AuthManager.GetUserFromToken(token).Result;
        string userID = user.id;

        captchaID = captchaID.Trim();
        if (captchaEnabled == false)
        {
            return true;
        }
        if (string.IsNullOrEmpty(userID) || string.IsNullOrEmpty(captchaID))
        {
            return false;
        }
        if (CaptchaSessions.Exists(c => c.UserID == userID && c.CaptchaID == captchaID && c.ExpiresAt > DateTime.UtcNow) == false)
        {
            return false;
        }
        CaptchaAnswer? session = CaptchaSessions.Find(c => c.UserID == userID && c.CaptchaID == captchaID);
        if (session != null && session.Answer == answer && session.ExpiresAt > DateTime.UtcNow)
        {
            // Remove the session after successful validation to prevent reuse
            CaptchaSessions.RemoveAll(c => c.UserID == userID && c.CaptchaID == captchaID);
            return true;
        }
        return false;
    }
}
public class CaptchaAnswer
{
    public string UserID { get; set; } = string.Empty;
    public string CaptchaID { get; set; } = string.Empty;
    public long Answer { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddSeconds(CaptchaManager.captchaValidity);
}