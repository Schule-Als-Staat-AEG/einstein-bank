using System.Diagnostics;
using System.IO;

public static class Core
{
    public static void Main(string[] args)
    {
        string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        string configPath = Path.Combine(dataDir, "config.ini");
        if (!File.Exists(configPath))
        {
            File.WriteAllText(configPath, "");
            INI defaultConfig = new("");
            defaultConfig.WriteString("General", "BackupPath", "");
            defaultConfig.WriteString("Backups", "MaxBackups", "5");
            defaultConfig.WriteString("Backups", "CurrentBackup", "0");
            defaultConfig.WriteString("Networking", "Port", "8080");
            defaultConfig.WriteString("Networking", "Host", "localhost");
            defaultConfig.WriteString("Networking", "Password", "password");
            defaultConfig.WriteString("Networking", "Username", "root");
            File.WriteAllText(configPath, defaultConfig.ToString());
        }
        INI config = new(File.ReadAllText(configPath));

        string path = config.ReadString("General", "BackupPath", "");
        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine("Please set the backup path in the config.ini file.");
            return;
        }
        int currentBackup = int.Parse(config.ReadString("Backups", "CurrentBackup", "0"));
        int maxBackups = int.Parse(config.ReadString("Backups", "MaxBackups", "5"));

        FileSystemWatcher watcher = new(path);

        watcher.Renamed += (s, e) =>
        {
            try
            {
                if (!e.FullPath.EndsWith(".zip"))
                    return;

                Console.WriteLine($"New backup: {e.FullPath}");

                string password = config.ReadString("Networking", "Password", "password");
                string username = config.ReadString("Networking", "Username", "root");
                string host = config.ReadString("Networking", "Host", "localhost");

                string target =
                    $"{username}@{host}:/backups/backup_{currentBackup}.zip";

                ProcessStartInfo psi = new()
                {
                    FileName = "sshpass",
                    Arguments =
                        $"-p \"{password}\" rsync -e \"ssh -o StrictHostKeyChecking=no\" -avz \"{e.FullPath}\" \"{target}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(psi)!;

                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                        Console.WriteLine(args.Data);
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                        Console.WriteLine(args.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                currentBackup++;

                if (currentBackup >= maxBackups)
                    currentBackup = 0;

                config.WriteString(
                    "Backups",
                    "CurrentBackup",
                    currentBackup.ToString()
                );

                File.WriteAllText(configPath, config.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        };

        watcher.Created += (s, e) =>
        {
            try
            {
                if (!e.FullPath.EndsWith(".zip"))
                    return;

                Console.WriteLine($"New backup: {e.FullPath}");

                string password = config.ReadString("Networking", "Password", "password");
                string username = config.ReadString("Networking", "Username", "root");
                string host = config.ReadString("Networking", "Host", "localhost");

                string target =
                    $"{username}@{host}:/backups/backup_{currentBackup}.zip";

                ProcessStartInfo psi = new()
                {
                    FileName = "sshpass",
                    Arguments =
                        $"-p \"{password}\" rsync -e \"ssh -o StrictHostKeyChecking=no\" -avz \"{e.FullPath}\" \"{target}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(psi)!;

                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                        Console.WriteLine(args.Data);
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                        Console.WriteLine(args.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                currentBackup++;

                if (currentBackup >= maxBackups)
                    currentBackup = 0;

                config.WriteString(
                    "Backups",
                    "CurrentBackup",
                    currentBackup.ToString()
                );

                File.WriteAllText(configPath, config.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        };
        watcher.Error += (s, e) =>
        {
            Console.WriteLine("Watcher error:");
            Console.WriteLine(e.GetException());
        };
        watcher.EnableRaisingEvents = true;
        while (true)
        {
            Thread.Sleep(1000);
            if (!watcher.EnableRaisingEvents)
            {
                Console.WriteLine("Watcher stopped. Restarting...");
                watcher.EnableRaisingEvents = true;
            }
        }
    }
}