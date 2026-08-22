namespace Max35050.Cmd;

public static class CaptchaCommands
{
    public static void PrintCaptchaSessions()
    {
        Debug.LogOK("Active Captcha Sessions:", true);
        foreach (var session in Helper.CaptchaManager.CaptchaSessions)
        {
            Debug.LogOK($"CaptchaID: {session.CaptchaID}, UserID: {session.UserID}, CreatedAt: {session.CreatedAt}, ExpiresAt: {session.ExpiresAt}", true);
        }
    }    
}