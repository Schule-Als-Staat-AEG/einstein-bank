using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Max35050.Auth;
using Max35050.Extras.Worktime;
using Max35050.Helper;
using Max35050.Net;
using Max35050.Systems;
using Newtonsoft.Json;

namespace Max35050.IO;

public static class FileManager
{
    public static string DataPath { get; private set; } = string.Empty;
    public static string TempPath => Path.Combine(DataPath, "temp");
    public static string PocketBaseDataPath = string.Empty;
    public static void Initialize(string? configPath = null)
    {
        if (configPath != null)
        {
            DataPath = configPath;
            Debug.Log("Data Path: " + DataPath);
            if(!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }
        }
        else
        {
            string path = AppContext.BaseDirectory;
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            directory = directory.Replace("\\", "/");
            directory += "/config/";
            Debug.Log("Data Path: " + directory);
            if(!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            DataPath = directory;
            if(!Directory.Exists(Path.Combine(DataPath, "temp")))
            {
                Directory.CreateDirectory(Path.Combine(DataPath, "temp"));
            }
        }

        // Delete all files in the temp directory but not the directory itself
        if (Directory.Exists(TempPath))
        {
            foreach (string file in Directory.GetFiles(TempPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to delete temp file: {file}");
                    Debug.LogError(e.ToString());
                }
            }
        }
        

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "server.embedded.settings.json";
        using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream != null)
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    if (!File.Exists(Path.Combine(DataPath, "settings.json")))
                    {
                        File.WriteAllText(Path.Combine(DataPath, "settings.json"), result);
                        Debug.Log("Created default settings.json file.", true);
                        Debug.Log("Please configure the database settings before starting the server again.", true);
                        Environment.Exit(0);
                    }                
                }
            }
            else
            {
                Debug.LogError("Failed to load default settings.");
            }
        }

        resourceName = "server.embedded.default_tax_rules.json";
        using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream != null)
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    if (!File.Exists(Path.Combine(DataPath, "default_tax_rules.json")))
                    {
                        File.WriteAllText(Path.Combine(DataPath, "default_tax_rules.json"), result);
                        Debug.Log("Created default_tax_rules.json file.", true);
                    }                
                }
            }
            else
            {
                Debug.LogError("Failed to load default tax rules.");
            }
        }
        // Create JWT File
        string jwtFilePath = Path.Combine(DataPath, "jwt.secret");
        if (!File.Exists(jwtFilePath))
        {
            byte[] key = new byte[64]; // 512 bits
            RandomNumberGenerator.Fill(key);

            ServerAuth.jwtSecret = Convert.ToBase64String(key);
            File.WriteAllText(jwtFilePath, ServerAuth.jwtSecret);
        }

        // Create an ip Blocklist file if it doesn't exist
        string ipBlocklistPath = Path.Combine(DataPath, "banned_ips.ini");
        if (!File.Exists(ipBlocklistPath))
        {
            File.WriteAllText(ipBlocklistPath, "");
        }

        string execUserPath = Path.Combine(DataPath, "exec_users.dat");
        if (!File.Exists(execUserPath))
        {
            File.WriteAllText(execUserPath, "");
        }
        try
        {
            string codeKeyPath = Path.Combine(DataPath, "code.rsa");
            string codePubKeyPath = Path.Combine(DataPath, "code.rsa.pub");
            if (!File.Exists(codeKeyPath))
            {
                using (RSA rsa = RSA.Create(4096)) // Create a new 4096-bit RSA key pair
                {
                    string prvKey = rsa.ExportRSAPrivateKeyPem();
                    string pubKey = rsa.ExportSubjectPublicKeyInfoPem();
    
                    File.WriteAllText(codeKeyPath, prvKey);
                    File.WriteAllText(codePubKeyPath, pubKey);
                }
            }
            string privateKey = File.ReadAllText(codeKeyPath);
            string publicKey = File.ReadAllText(codePubKeyPath);

            RSA pubRsa = RSA.Create();
            RSA prvRsa = RSA.Create();
            prvRsa.ImportFromPem(privateKey);
            pubRsa.ImportFromPem(publicKey);
            CryptoHelper.pubRsa = pubRsa;
            CryptoHelper.prvRsa = prvRsa;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to load or create RSA keys. Code signing will not work.");
            Debug.LogError(e.ToString());
        }
        dynamic settings = JsonConvert.DeserializeObject(File.ReadAllText(Path.Combine(DataPath, "settings.json")))!;
        Debug.Log($"Loaded settings from file.");
        Server.databaseURL = settings.database.remote;
        ServerAuth.identity = settings.database.identity;
        ServerAuth.secret = settings.database.password;
        ServerAuth.location = settings.database.location;
        Program.port = settings.server.port;

        EmailManager.sendEmail = settings.email.sendEmail.ToObject<bool>();
        EmailManager.smtpServer = settings.email.smtpServer.ToString();
        EmailManager.smtpPort = settings.email.smtpPort.ToObject<int>();
        EmailManager.username = settings.email.username.ToString();
        EmailManager.password = settings.email.password.ToString();

        SettingsManager.enableRateLimiting = settings.server.enableRateLimiting.ToObject<bool>();
        SettingsManager.rateLimitWindowSeconds = settings.server.rateLimitWindowSeconds.ToObject<int>();
        SettingsManager.rateLimitPermitLimit = settings.server.rateLimitPermitLimit.ToObject<int>();
        SettingsManager.rateLimitQueueProcessingOrder = settings.server.rateLimitQueueProcessingOrder.ToString();
        SettingsManager.rateLimitQueueLimit = settings.server.rateLimitQueueLimit.ToObject<int>();

        CaptchaManager.captchaEnabled = settings.security.captchaEnabled.ToObject<bool>();
        CaptchaManager.captchaRateLimit = settings.security.captchaRateLimit.ToObject<int>();
        CaptchaManager.captchaValidity = settings.security.captchaValidity.ToObject<int>();

        SecurityManager.IPBlockingEnabled = settings.security.ipBlockingEnabled.ToObject<bool>();
        SecurityManager.IPBlocklistEnabled = settings.security.ipBlocklistEnabled.ToObject<bool>();
        SecurityManager.RequestScanningEnabled = settings.security.requestScanningEnabled.ToObject<bool>();
        SecurityManager.AccessLogMaxRetentionMinutes = settings.security.accessLogMaxRetentionMinutes.ToObject<int>();
        SecurityManager.TransactionMaxIps = settings.security.transactionMaxIps.ToObject<int>();
        SecurityManager.TransactionIpTimeWindowSeconds = settings.security.transactionIpTimeWindowSeconds.ToObject<int>();
        SecurityManager.BlockAfterAttempts = settings.security.blockAfterAttempts.ToObject<int>();
        SecurityManager.BlockDurationMinutes = settings.security.blockDurationMinutes.ToObject<int>();

        WorktimeManager.Enabled = settings.extras_worktime.enabled.ToObject<bool>();
        WorktimeManager.Remote = settings.extras_worktime.remote.ToString();
        WorktimeManager.Identity = settings.extras_worktime.identity.ToString();
        WorktimeManager.Password = settings.extras_worktime.password.ToString();
        WorktimeManager.Location = settings.extras_worktime.location.ToString();

        StatsManager.collectStats = settings.stats.collect.ToObject<bool>();
        StatsManager.statsToKeep = settings.stats.statsRetention.ToObject<int>();

        // Load JWT Secret
        ServerAuth.jwtSecret = File.ReadAllText(jwtFilePath);
        Debug.Log("Loaded JWT secret from file.");
    }
    internal static void CreateExecUser(string username, string password)
    {
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        string execUserPath = Path.Combine(DataPath, "exec_users.dat");
        string currentUserData = File.ReadAllText(execUserPath);
        string newUserData = $"{username}:{passwordHash}";
        if (!string.IsNullOrEmpty(currentUserData))
        {
            newUserData = currentUserData + Environment.NewLine + newUserData;
        }
        File.WriteAllText(execUserPath, newUserData);
    }
    internal static void DeleteExecUser(string username)
    {
        string execUserPath = Path.Combine(DataPath, "exec_users.dat");
        string currentUserData = File.ReadAllText(execUserPath);
        var lines = currentUserData.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        var filteredLines = lines.Where(line => !line.StartsWith(username + ":"));
        File.WriteAllText(execUserPath, string.Join(Environment.NewLine, filteredLines));
    }
    internal static bool ValidateExecUser(string username, string password)
    {
        string execUserPath = Path.Combine(DataPath, "exec_users.dat");
        string currentUserData = File.ReadAllText(execUserPath);
        var lines = currentUserData.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length == 2 && parts[0] == username)
            {
                return BCrypt.Net.BCrypt.Verify(password, parts[1]);
            }
        }
        return false;
    }
}