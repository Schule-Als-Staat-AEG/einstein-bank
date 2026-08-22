using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Auth;

public static class ServerAuth
{
    internal static string identity = "system@system.com";
    internal static string secret = "12345678";
    internal static string location = "_superusers";
    internal static string jwtSecret = "";
    internal static string authToken = "";

    public static void Initialize(string identity, string secret, string location)
    {
        ServerAuth.identity = identity;
        ServerAuth.secret = secret;
        ServerAuth.location = location;

        Debug.LogOK($"Identity: {identity}, Location: {location}", true);
        Debug.LogOK($"Secret: {new string('*', secret.Length)}", true);

        Task.Run(async () => await AuthLoop());
    }
    public static async Task AuthLoop()
    {
        while (true)
        {
            bool success = await Authenticate();
            if (success)
            {
                await Task.Delay(3600000); // Re-authenticate every hour
            }
            else
            {
                Debug.LogError("Authentication failed. Retrying in 5 seconds...");
                await Task.Delay(5000);
            }
        }
    }
    public static async Task<bool> Authenticate()
    {
        try
        {
            await Server.db.Collections("_superusers").AuthWithPassword(identity, secret);
            if(String.IsNullOrWhiteSpace(Server.db.AuthStore.Token))
            {
                Debug.LogCritical("SERVER Authentication failure: Received empty auth token.");
                return false;
            }
            authToken = Server.db.AuthStore.Token;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Authentication error: " + ex.Message);
            Debug.LogError($"Endpoint: {Server.databaseURL}/api/collections/{location}/auth-with-password");
            return false;
        }
    }
}