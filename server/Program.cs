using System.Threading.Tasks;
using Max35050.API;
using Max35050.Auth;
using Max35050.Helper;
using Max35050.IO;
using Max35050.Net;
using Max35050.Systems;

namespace Max35050;

static class Program
{
    internal static bool excessiveLogging = false;
    internal static bool connectionLogging = false;
    internal static int port = 8080;
    internal static string version = "1.0.0";
    internal static bool allowExecCommand = false; // The exec command allows executing shell commands on the server. This is extremely dangerous and should only be used for testing purposes. Do not use this in production.
    internal static string execCommandUser = "root"; // The user that is used to execute shell commands when the exec command is used. This should be set to a user with no permissions and no home directory for security reasons. This is only used for testing purposes and should not be used in production.
    public static DateTime startTime { get; private set; } = DateTime.Now;
    static async Task Main(string[] args)
    {
        Debug.Log($"Transaction Processing Server - Version {version}", true);
        Debug.Log("Max35050, 2026 - All rights reserved.", true);
        Debug.Log("", true);
        Console.CancelKeyPress += async (sender, e) =>
        {
            Debug.Log("Shutting down server...", true);
            await Server.app?.StopAsync()!;
            SecurityManager.Stop();
            CaptchaManager.cts.Cancel();
            await TransactionAPI.Shutdown();
            Debug.LogOK("Server stopped. Exiting...", true);
        };
        try
        {
            Debug.Log("Running full System Check...", true);
            Debug.Log($"Operating System: {Environment.OSVersion} | 64-bit: {Environment.Is64BitOperatingSystem}", true);
            Debug.Log($"Processor Count: {Environment.ProcessorCount}", true);
            Debug.Log($"Current User: {Environment.UserName} | Current Time: {DateTime.Now}", true);
            if(OperatingSystem.IsLinux()) {
                Debug.Log($"Ram Check: {GetTotalRAM()} GB", true);
            }
            Debug.Log("System Check Complete.", true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"System check failed: {e.Message}", true);
            Debug.LogError("Ignoring and continuing startup. This may cause issues later on, but the server will attempt to start anyway.", true);
        }
        Debug.Log("", true);
        Debug.Log("Initializing File Manager...", true);
        IO.FileManager.Initialize();
        for(int i = 0; i < args.Length; i++)
        {
            switch(args[i])
            {
                case "--excessive":
                case "-v":
                case "--verbose":
                    Debug.Log("Enabling excessive logging...", true);
                    excessiveLogging = true;
                    break;
                case "--port": // Override port from command line
                case "-p":
                    if(i + 1 < args.Length && int.TryParse(args[i + 1], out int port))
                    {
                        Program.port = port;
                        Debug.Log($"Overriding port to {port} from command line argument.", true);
                    }
                    break;
                case "--test":
                    Debug.Log("Running in test mode.", true);
                    break;
                case "--allow-exec":
                    Debug.LogWarning("Enabling exec command. This is extremely dangerous and should only be used for testing purposes. Do not use this in production.", true);
                    allowExecCommand = true;
                    break;
                case "--exec-user":
                    if(i + 1 < args.Length)
                    {
                        execCommandUser = args[i + 1];
                        Debug.Log($"Setting exec command user to {execCommandUser} from command line argument.", true);
                    }
                    break;
                case "--help":
                case "-h":
                    Debug.Log("Available command line arguments:", true);
                    Debug.Log("--excessive, -v, --verbose: Enable excessive logging. This will log a lot of information to the console, which can be useful for debugging but can also make it harder to find important information.", true);
                    Debug.Log("--port, -p [port]: Override the default port (8080) that the server listens on. Example: --port 3000", true);
                    Debug.Log("--test: Run the server in test mode. This may enable certain features or behaviors that are useful for testing but should not be used in production.", true);
                    Debug.Log("--allow-exec: Enable the exec command, which allows executing shell commands on the server. This is extremely dangerous and should only be used for testing purposes. Do not use this in production.", true);
                    Debug.Log("--exec-user [user]: Set the user that is used to execute shell commands when the exec command is used. This should be set to a user with no permissions and no home directory for security reasons. This is only used for testing purposes and should not be used in production.", true);
                    Debug.Log("--help, -h: Show this help message.", true);
                    Environment.Exit(0);
                    break;

                case "exec-users":
                    if(args[i + 1] == "create")
                    {
                        string username = args[i + 2];
                        string password = args[i + 3];
                        FileManager.CreateExecUser(username, password);
                        Debug.Log($"Created exec user {username}.", true);
                        Environment.Exit(0);
                    }
                    if (args[i + 1] == "delete")
                    {
                        string username = args[i + 2];
                        FileManager.DeleteExecUser(username);
                        Debug.Log($"Deleted exec user {username}.", true);
                        Environment.Exit(0);
                    }
                    break;
            }
        }
        Server.db = new PocketBase.PocketBase(Server.databaseURL);
        ServerAuth.Initialize(ServerAuth.identity, ServerAuth.secret, ServerAuth.location);
        await Server.Start(port);
    }
    internal static double GetTotalRAM()
    {
        if (OperatingSystem.IsWindows())
        {
            return -1;
        }
        else if (OperatingSystem.IsLinux())
        {
            var info = new System.IO.StreamReader("/proc/meminfo").ReadToEnd();
            var lines = info.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long memKb))
                    {
                        return Math.Round(memKb / (1024.0 * 1024.0), 2); // Convert from KB to GB
                    }
                }
            }
        }
        return -1; // Unknown
    }
}
