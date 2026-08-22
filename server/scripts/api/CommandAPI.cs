using Max35050.Auth;
using Max35050.Helper;
using Max35050.Cmd;
using Max35050.Systems;
using Max35050.IO;

namespace Max35050.API;
public static class CommandAPI
{
    private static System.Diagnostics.Process lastExecProcess = null!;
    private static DateTime eventStartTime = DateTime.Now;
    public static async Task<IResult> ExecuteCommand(string command, string? identity = null, string? password = null)
    {
        // Split the command while preserving quoted substrings
        var args = System.Text.RegularExpressions.Regex.Matches(command, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value.Trim('"'))
            .ToArray();
        if (args.Length == 0)
        {
            return Results.BadRequest("No command provided.");
        }
        string commandName = args[0].ToLower();
        var commandArgs = args.Skip(1).ToArray();
        Debug.LogCmd($"> {command}");
        return await HandleCommand(commandName, commandArgs, identity, password);
    }
    public static async Task<IResult> HandleCommand(string commandName, string[] commandArgs, string? identity = null, string? password = null)
    {
        switch (commandName.ToLower())
        {
            case "ping":
                Debug.LogOK("Command received >> Ping acknowledged.", true);
            break;

            case "echo":
                if (commandArgs.Length == 0)
                {
                    Debug.LogError("Echo command requires a message to echo.", true);
                }
                else
                {
                    string message = string.Join(" ", commandArgs);
                    Debug.LogOK($"Echo: {message}", true);
                }
            break;

            case "help":
                PrintHelp();
            break;

            case "auth":
                Debug.LogOK($"Authentication Details:\nIdentity: {ServerAuth.identity}\nLocation: {ServerAuth.location}\nAuth Token: {ServerAuth.authToken.Substring(0, 10)}...", true);
            break;

            case "auth.refresh":
                await ServerAuth.Authenticate();
                Debug.LogOK("Authentication token refreshed.", true);
            break;

            case "settings":
                SettingsManager.PrintSettings();
                Debug.LogOK("Pull settings from database with 'settings.pull'", true);
            break;

            case "settings.pull":
                await SettingsManager.PullSettings();
                Debug.LogOK("Settings pulled from database.", true);
            break;

            case "account":
                if (commandArgs.Length == 0)
                {
                    Debug.LogError("Account command requires a subcommand.", true);
                    break;
                }
                await AccountCommands.PrintAccountInfo(commandArgs[0]);
            break;

            case "account.freeze":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("Account.freeze command requires an account ID and freeze status.", true);
                    break;
                }
                if (commandArgs.Length < 2)
                {
                    AccountCommands.ToggleFreezeAccount(commandArgs[0]);
                    break;
                }
                bool freeze = commandArgs[1].ToLower() == "true" || commandArgs[1] == "1";
                AccountCommands.SetFreezeAccount(commandArgs[0], freeze);
            break;

            case "account.balance":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("Account.balance command requires an account ID.", true);
                    break;
                }
                else if (commandArgs.Length < 2)
                {
                    AccountCommands.PrintBalance(commandArgs[0]);
                    break;
                }
                else
                {
                    AccountManager.SetBalance(commandArgs[0], decimal.Parse(commandArgs[1])).Wait();
                    Debug.LogOK($"Account {commandArgs[0]} balance set to {commandArgs[1]}.", true);
                }
            break;

            case "account.balance.add":
                if (commandArgs.Length < 2)
                {
                    Debug.LogError("Account.balance.add command requires an account ID and amount.", true);
                    break;
                }
                AccountManager.AddBalance(commandArgs[0], decimal.Parse(commandArgs[1])).Wait();
                Debug.LogOK($"Added {commandArgs[1]} to account {commandArgs[0]}.", true);
            break;

            case "account.balance.subtract":
                if (commandArgs.Length < 2)
                {
                    Debug.LogError("Account.balance.subtract command requires an account ID and amount.", true);
                    break;
                }
                AccountManager.SubtractBalance(commandArgs[0], decimal.Parse(commandArgs[1])).Wait();
                Debug.LogOK($"Subtracted {commandArgs[1]} from account {commandArgs[0]}.", true);
            break;

            case "recurring":
                RecurringTransactionCommands.ShowInfo();
            break;

            case "recurring.reset":
                await RecurringTransactionCommands.ResetWorker();
                Debug.LogOK("Recurring Transactions worker reset.", true);
            break;
            
            case "recurring.get":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("Recurring.get command requires a recurring transaction ID.", true);
                    break;
                }
                RecurringTransactionCommands.GetRecurringTransaction(commandArgs[0]);
            break;

            case "recurring.paidtoday":
            case "recurring.paid_today":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("Recurring.paid_today command requires a recurring transaction ID", true);
                    break;
                }
                if (commandArgs.Length < 2)
                {
                    RecurringTransactionCommands.SetPaidToday(commandArgs[0]);
                    break;
                }
                RecurringTransactionCommands.SetPaidToday(commandArgs[0], bool.Parse(commandArgs[1]));
                Debug.LogOK($"Set paid amount of recurring transaction {commandArgs[0]} to {commandArgs[1]}.", true);
            break;

            case "wealth":
                WealthCommands.WealthInfoCommand();
            break;
            case "wealth.reset":
                WealthCommands.WealthResetCommand();
            break;
            case "wealth.deduct":
                WealthCommands.DeductNow();
            break;
            case "tax":
                TaxCommands.ShowTaxSettings();
            break;

            case "irs":
                FlaggingCommands.PrintStatus();
            break;

            case "irs.reset":
                FlaggingCommands.ResetWorker();
            break;

            case "irs.write":
                FlaggingCommands.WriteSettingToJSON();
            break;

            case "order":
                if (commandArgs.Length < 1)
                {
                    OrderCommands.PrintOrderHelp();
                    break;
                }
                OrderCommands.PrintOrder(commandArgs[0]);
            break;

            case "order.pay":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("order.pay command requires an order ID.", true);
                    break;
                }
                bool logTransaction = false;
                if (commandArgs.Length >= 2)
                {
                    logTransaction = commandArgs[1].ToLower() == "true" || commandArgs[1] == "1";
                }
                OrderCommands.PayOrder(commandArgs[0], logTransaction);
                break;

            case "order.paid":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("order.paid command requires an order ID.", true);
                    break;
                }
                bool? paid = null;
                if (commandArgs.Length >= 2)
                {
                    paid = commandArgs[1].ToLower() == "true" || commandArgs[1] == "yes" || commandArgs[1] == "1";
                }
                OrderCommands.SetOrderAsPaid(commandArgs[0], paid);
                break;
            
            case "order.status":
                if (commandArgs.Length < 2)
                {
                    Debug.LogError("order.status command requires an order ID and status.", true);
                    break;
                }
                OrderManager.SetStatus(commandArgs[0], commandArgs[1]);
                Debug.LogOK($"Set status of order {commandArgs[0]} to {commandArgs[1]}.", true);
                break;

            case "order.purge":
                if (commandArgs.Length < 1)
                {
                    Debug.LogError("order.purge command requires an order ID.", true);
                    break;
                }
                OrderCommands.PurgeOrder(commandArgs[0]);
                Debug.LogOK($"Order {commandArgs[0]} has been purged.", true);
                break;

            case "cases":
                CaseCommands.PrintCases();
            break;

            case "cases.fetch":
                CaseCommands.RetryCaseFetch();
            break;

            case "scram":
                TransactionAPI.allowTransactions = false;
                eventStartTime = DateTime.Now;
                TransactionAPI.blockedTransactions = 0;
                Debug.LogOK("BEGIN EVENT MONITOR", true);
                Debug.LogOK("", true);
                Debug.LogOK("SCRAM INITIATED", true);
                Debug.LogOK("SAFETY TRIP", true);
                Debug.LogOK("FULL LOAD REJECTION", true);
                Debug.LogOK("TRANSACTION TRIP", true);
                break;

            case "az-5":
            case "az5":
                TransactionAPI.allowTransactions = false;
                eventStartTime = DateTime.Now;
                TransactionAPI.blockedTransactions = 0;
                Debug.LogOK("аварийная защита 5-й категории", true);
                Debug.LogOK("EMERGENCY PROTECTION OF THE FIFTH CATEGORY INITIATED, TRANSACTIONS DISABLED", true);
                break;
            
            case "resa":
                eventStartTime = DateTime.Now;
                TransactionAPI.allowTransactions = false;
                TransactionAPI.blockedTransactions = 0;
                Debug.LogOK("REAKTORSCHNELLABSCHALTUNG EINGELEITET, TRANSAKTIONEN DEAKTIVIERT", true);
                break;
            
            case "descram":
                TransactionAPI.allowTransactions = true;
                Debug.LogOK($"TOTAL TRANSACTIONS LOST: {TransactionAPI.blockedTransactions}", true);
                Debug.LogOK("", true);
                Debug.LogOK("STABLE SHUTDOWN", true);
                Debug.LogOK("", true);
                Debug.LogOK("EVENT START: " + eventStartTime.ToString("yyyy-MM-dd HH:mm:ss"), true);
                Debug.LogOK("EVENT DURATION: " + (DateTime.Now - eventStartTime).ToString(@"hh\:mm\:ss"), true);
                Debug.LogOK("EVENT END: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), true);
                Debug.LogOK("", true);
                Debug.LogOK("END EVENT MONITOR", true);
                break;
            
            case "captchas":
                CaptchaCommands.PrintCaptchaSessions();
            break;
            case "excessive":
                if (commandArgs.Length == 0)
                {
                    Program.excessiveLogging = !Program.excessiveLogging;
                    Debug.LogOK($"Excessive logging toggled to {Program.excessiveLogging}.", true);
                    break;
                }
                bool excessive = commandArgs[0].ToLower() == "true" || commandArgs[0] == "1" || commandArgs[0].ToLower() == "on";
                Program.excessiveLogging = excessive;
                Debug.LogOK($"Excessive logging set to {Program.excessiveLogging}.", true);
            break;

            case "conlogging":
                if (commandArgs.Length == 0)
                {
                    Program.connectionLogging = !Program.connectionLogging;
                    Debug.LogOK($"Connection logging toggled to {Program.connectionLogging}.", true);
                    break;
                }
                bool conlogging = commandArgs[0].ToLower() == "true" || commandArgs[0] == "1" || commandArgs[0].ToLower() == "on";
                Program.connectionLogging = conlogging;
                Debug.LogOK($"Connection logging set to {Program.connectionLogging}.", true);
            break;
            
            case "restart.web":
                // Execute systemctl restart command for web server
                // Linux only
                Debug.LogOK("Summoning restart process for web server...", true);
                Debug.LogOK("Executing: systemctl restart aeg_site", true);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = "restart aeg_site",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                Debug.LogOK("Web server restart command executed.", true);
            break;
            
            case "restart.server":
                Debug.LogOK("Restarting... Connection will cut", true);
                Debug.LogOK("Summoning restart process for server application...", true);
                Debug.LogOK("Executing: systemctl restart aeg_server", true);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = "restart aeg_server",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            break;

            case "exec":
                if (!Program.allowExecCommand)
                {
                    Debug.LogError("Exec command is disabled. To enable it, start the server with the --allow-exec flag.", true);
                    break;
                }
                if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(password))
                {
                    Debug.LogError("Exec command requires identity and password for authentication.", true);
                    break;
                }
                if (!FileManager.ValidateExecUser(identity, password))
                {
                    Debug.LogError("Invalid exec user credentials.", true);
                    break;
                }
                if (commandArgs.Length == 0)
                {
                    Debug.LogError("Exec command requires a system command to execute.", true);
                    break;
                }
                string sysCommand = string.Join(" ", commandArgs);
                try
                {
                    // Dispose previous tracked process if it already exited
                    if (lastExecProcess != null)
                    {
                        try { if (lastExecProcess.HasExited) { lastExecProcess.Dispose(); lastExecProcess = null!; } } catch { }
                    }

                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"-c \"{sysCommand}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            UserName = Program.execCommandUser,
                        },
                        EnableRaisingEvents = true
                    };

                    process.Exited += (s, e) =>
                    {
                        try
                        {
                            Debug.LogOK($"Exec process exited with code {process.ExitCode}", true);
                        }
                        catch { }
                        try { if (!process.HasExited) process.Kill(); } catch { }
                        try { process.Dispose(); } catch { }
                        if (lastExecProcess == process) lastExecProcess = null!;
                    };

                    process.Start();

                    // Track this process for exec-append
                    lastExecProcess = process;

                    // Stream stdout asynchronously
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            var reader = process.StandardOutput;
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                Debug.LogOK(line, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Exec stdout streaming error: {ex.Message}", true);
                        }
                    });

                    // Stream stderr asynchronously
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            var reader = process.StandardError;
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                Debug.LogError(line, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Exec stderr streaming error: {ex.Message}", true);
                        }
                    });

                    Debug.LogOK($"Started exec: {sysCommand}", true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exec command failed to start: {ex.Message}", true);
                }
            break;

            case "exec-append":
                if (!Program.allowExecCommand)
                {
                    Debug.LogError("Exec-append command is disabled. To enable it, start the server with the --allow-exec flag.", true);
                    break;
                }
                if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(password))
                {
                    Debug.LogError("Exec command requires identity and password for authentication.", true);
                    break;
                }
                if (!FileManager.ValidateExecUser(identity, password))
                {
                    Debug.LogError("Invalid exec user credentials.", true);
                    break;
                }
                if (commandArgs.Length == 0)
                {
                    Debug.LogError("exec-append requires a string to append to the last exec process's stdin.", true);
                    break;
                }
                if (lastExecProcess == null || lastExecProcess.HasExited)
                {
                    Debug.LogError("No active exec process available to append to.", true);
                    break;
                }
                try
                {
                    string toWrite = string.Join(" ", commandArgs) + "\n";
                    await lastExecProcess.StandardInput.WriteAsync(toWrite);
                    await lastExecProcess.StandardInput.FlushAsync();
                    Debug.LogOK("Appended input to last exec process.", true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"exec-append failed: {ex.Message}", true);
                }
            break;

            case "exec-terminate":
                if (!Program.allowExecCommand)
                {
                    Debug.LogError("Exec-terminate command is disabled. To enable it, start the server with the --allow-exec flag.", true);
                    break;
                }
                if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(password))
                {
                    Debug.LogError("Exec command requires identity and password for authentication.", true);
                    break;
                }
                if (!FileManager.ValidateExecUser(identity, password))
                {
                    Debug.LogError("Invalid exec user credentials.", true);
                    break;
                }
                if (lastExecProcess == null || lastExecProcess.HasExited)
                {
                    Debug.LogError("No active exec process available to terminate.", true);
                    break;
                }
                try
                {
                    lastExecProcess.Kill();
                    Debug.LogOK("Terminated last exec process.", true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"exec-terminate failed: {ex.Message}", true);
                }
            break;

            case "clear":
            case "cls":
                Debug.logs.Clear();
                Debug.LogOK("Cleared console logs.", true);
            break;
            
            default:
                Debug.LogError($"Unknown command: {commandName}", true);
            break;
                
        }
        Debug.LogOK("Execution Exited with 0");
        return Results.Ok();
    }
    public static void PrintHelp()
    {
        Debug.LogOK("Available Commands:", true);
        Debug.LogOK("-------------------", true);
        Debug.LogOK("Layout: command <required> [optional]", true);
        Debug.LogOK("", true);
        Debug.LogOK("  help                                             - Show this help message.", true);
        Debug.LogOK("  ping                                             - Check server responsiveness.", true);
        Debug.LogOK("  echo <message>                                   - Echo back the provided message.", true);
        Debug.LogOK("  auth                                             - Authentication details.", true);
        Debug.LogOK("  auth.refresh                                     - Refresh authentication token.", true);
        Debug.LogOK("  settings                                         - List all server settings.", true);
        Debug.LogOK("  settings.pull                                    - Pull latest settings from database.", true);
        Debug.LogOK("  account <accountID>                              - Print account information.", true);
        Debug.LogOK("  account.freeze <accountID> [true|false]          - Freeze or unfreeze an account.", true);
        Debug.LogOK("  account.balance <accountID> [amount]             - Print or set account balance.", true);
        Debug.LogOK("  account.balance.add <accountID> <amount>         - Add amount to account balance.", true);
        Debug.LogOK("  account.balance.subtract <accountID> <amount>    - Subtract amount from account balance.", true);
        Debug.LogOK("  recurring                                        - Show recurring transactions info.", true);
        Debug.LogOK("  recurring.reset                                  - Reset the recurring transactions worker.", true);
        Debug.LogOK("  recurring.get <id>                               - Get info for a specific recurring transaction.", true);
        Debug.LogOK("  recurring.paid_today <id> [true|false]           - Set or toggle the paid Today status of a recurring transaction.", true);
        Debug.LogOK("  wealth                                           - Show wealth tax manager info.", true);
        Debug.LogOK("  wealth.reset                                     - Kill the Worker and Restart.", true);
        Debug.LogOK("  wealth.deduct                                    - Trigger Wealth Tax Deduction Now.", true);
        Debug.LogOK("  tax                                              - Show Tax Settings", true);
        Debug.LogOK("  irs                                              - Show Internal Revenue Service Helper info.", true);
        Debug.LogOK("  irs.reset                                        - Reset Internal Revenue Service Helper worker.", true);
        Debug.LogOK("  irs.write                                        - Write current Internal Revenue Service Helper settings to JSON file.", true);
        Debug.LogOK("  order <id>                                       - Show Details about an order", true);
        Debug.LogOK("  order                                            - Show more Order Commands", true);
        Debug.LogOK("  cases                                            - Print all current cases in memory.", true);
        Debug.LogOK("  cases.fetch                                      - Retry fetching potential cases from database.", true);
        Debug.LogOK("  scram | az-5 | resa                              - Disable Transactions (only effective until restart)", true);
        Debug.LogOK("  descram                                          - Enable Transactions after SCRAM", true);
        Debug.LogOK("  captchas                                         - Print active captcha sessions in memory.", true);
        Debug.LogOK("  excessive [true|false|on|off]                    - Toggle or set excessive logging.", true);
        Debug.LogOK("  conlogging [true|false|on|off]                   - Toggle or set connection logging.", true);
        Debug.LogOK("-------------------------------------------------", true);
        Debug.LogOK("System Commands:", true);
        Debug.LogOK("  restart.web                                      - Restart the web server.", true);
        Debug.LogOK("  restart.server                                   - Restart the server application.", true);
        Debug.LogOK("  exec <system command>                            - Execute a system command (as root). [bash]", true);
        Debug.LogOK("  exec-append <string>                             - Append string to last exec command's stdin.", true);
        Debug.LogOK("  exec-terminate                                   - Terminate the last exec command process.", true);
        Debug.LogOK("  clear / cls                                      - Clear console logs.", true);
    }
}