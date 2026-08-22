namespace Max35050.Helper;

public static class EmailManager
{
    public static bool sendEmail = false;
    public static string smtpServer = "";
    public static int smtpPort = 587;
    public static string username = "";
    public static string password = "";
    public static async Task<bool> SendEmail(string to, string subject, string body, string name)
    {
        if (!sendEmail)
        {
            return false;
        }
        try
        {
            using (var client = new System.Net.Mail.SmtpClient(smtpServer, smtpPort))
            {
                client.Credentials = new System.Net.NetworkCredential(username, password);
                client.EnableSsl = true;

                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(username, name),
                    To = { new System.Net.Mail.MailAddress(to) },
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mailMessage);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}