using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Max35050;
using System.Text.RegularExpressions;

internal static class Debug
{
    private enum LogLevel { Info, Warn, Error, Critical, Ok, Cmd, Connection }

    private sealed record LogEntry(LogLevel Level, string Message, string DeclaringType, string MethodName, DateTime Timestamp, bool Exit, int ExitCode);

    private static readonly BlockingCollection<LogEntry> _queue = new(new ConcurrentQueue<LogEntry>());
    private static readonly Task _worker;

    public static List<string> logs = new List<string>();
    private const int MaxLogCount = 1000;

    // Add this helper to convert log entries to ANSI string
    private static string FormatAnsiLog(LogEntry e)
    {
        string header = $"[{e.Timestamp:HH:mm:ss}] [{e.DeclaringType}.{e.MethodName}] ";
        string levelAnsi = e.Level switch
        {
            LogLevel.Info => "\u001b[34m[INFO]\u001b[0m ",
            LogLevel.Warn => "\u001b[33m[WARN]\u001b[0m ",
            LogLevel.Error => "\u001b[31m[FAIL]\u001b[0m ",
            LogLevel.Critical => "\u001b[35m[CRIT]\u001b[0m ",
            LogLevel.Ok => "\u001b[32m[OK]\u001b[0m ",
            LogLevel.Cmd => "\u001b[36m[CMD]\u001b[0m ",
            _ => ""
        };

        // Split message by newlines and prefix each line
        var lines = e.Message.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = $"{header}{levelAnsi}{lines[i]}";
        }
        return string.Join("\n", lines);
    }

    static Debug()
    {
        // Start the single background worker that processes console writes sequentially
        _worker = Task.Run(ProcessQueue);
    }

    internal static void Shutdown(TimeSpan? timeout = null)
    {
        // Signal completion and wait for the worker to finish pending logs
        _queue.CompleteAdding();
        try
        {
            if (timeout.HasValue)
            {
                _worker.Wait(timeout.Value);
            }
            else
            {
                _worker.Wait();
            }
        }
        catch (AggregateException) { /* ignore */ }
    }

    private static void Enqueue(LogLevel level, string message, bool exit = false, int exitCode = 1, bool forceLog = false)
    {
        if(!forceLog && !Program.excessiveLogging && level != LogLevel.Critical && level != LogLevel.Cmd)
        {
            return;
        }
        if(level == LogLevel.Connection && !Program.connectionLogging)
        {
            return;
        }
        // Capture caller info at the time of the call so the queue can be processed later
        string declaring = "Undefined";
        string method = "Worker";
        try
        {
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();
            if (frames != null && frames.Length > 0)
            {
                // Find the first meaningful caller frame (skip frames from this Debug class and common framework namespaces)
                foreach (var frame in frames)
                {
                    var m = frame.GetMethod();
                    if (m == null) continue;
                    var dt = m.DeclaringType;
                    if (dt == null) continue;

                    // skip internal logging frames
                    if (dt == typeof(Debug)) continue;

                    // skip system/framework noise
                    var ns = dt.Namespace ?? string.Empty;
                    if (ns.StartsWith("System", StringComparison.Ordinal)
                        || ns.StartsWith("Microsoft", StringComparison.Ordinal)
                        || ns.StartsWith("Windows", StringComparison.Ordinal)
                        || ns.StartsWith("Mono", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // found a likely user frame
                    declaring = dt.FullName ?? dt.Name ?? "Undefined";
                    method = m.Name ?? "Worker";
                    break;
                }

                // fallback: use first non-null declaring type/method if above didn't find a suitable frame
                if (declaring == "Undefined" || method == "Worker")
                {
                    foreach (var frame in frames)
                    {
                        var m = frame.GetMethod();
                        if (m == null) continue;
                        var dt = m.DeclaringType;
                        if (dt != null)
                        {
                            declaring = dt.FullName ?? dt.Name ?? declaring;
                            method = m.Name ?? method;
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // swallow any issues while capturing stack trace
        }

        var entry = new LogEntry(level, message, declaring, method, DateTime.Now, exit, exitCode);
        // Best-effort add; if CompleteAdding has been called this will throw and we ignore it
        try { _queue.Add(entry); } catch (InvalidOperationException) { /* shutting down */ }
    }

    private static void ProcessQueue()
    {
        foreach (var e in _queue.GetConsumingEnumerable())
        {
            try
            {
                switch (e.Level)
                {
                    case LogLevel.Info:
                        INFOText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.White);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        break;
                    case LogLevel.Warn:
                        WARNText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.Yellow);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        break;
                    case LogLevel.Error:
                        FAILText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.Red);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        break;
                    case LogLevel.Critical:
                        CRITText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.DarkRed);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        if (e.Exit)
                        {
                            // exit from the worker thread
                            Environment.Exit(e.ExitCode);
                        }
                        break;
                    case LogLevel.Ok:
                        OKText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.Green);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        break;
                    case LogLevel.Cmd:
                        CMDText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        break;
                    case LogLevel.Connection:
                        ConnText();
                        WriteHeader(e);
                        SetColor(ConsoleColor.Magenta);
                        Console.WriteLine(e.Message);
                        ResetColor();
                        break;
                }
                logs.Add(FormatAnsiLog(e));
                if (logs.Count > MaxLogCount)
                {
                    logs.RemoveRange(0, logs.Count - MaxLogCount);
                }

                // Broadcast to SignalR clients if event is set
                OnLogBroadcast?.Invoke(logs[^1]);
            }
            catch
            {
                // Swallow exceptions from logging to avoid crashing the worker
            }
        }
    }

    private static void WriteHeader(LogEntry e)
    {
        Console.Write("[" + e.Timestamp.ToString("HH:mm:ss") + "] ");
        // show full declaring type (namespace + class) and method separated by a dot
        Console.Write($"[{Normalize(e.DeclaringType + "." + e.MethodName)}] ");
    }

    private static string Normalize(string input)
    {
        var match = Regex.Match(input, @"^(.*?)\+.*?<<(.+?)>");
        if (match.Success)
            return $"{match.Groups[1].Value}.{match.Groups[2].Value}";

        return input;
    }

    private static ConsoleColor? _savedColor;
    private static void SetColor(ConsoleColor c)
    {
        try
        {
            _savedColor = Console.ForegroundColor;
            Console.ForegroundColor = c;
        }
        catch { /* ignore console issues */ }
    }

    private static void ResetColor()
    {
        try
        {
            if (_savedColor.HasValue) Console.ForegroundColor = _savedColor.Value;
        }
        catch { /* ignore */ }
        finally { _savedColor = null; }
    }

    internal static void Log(string message, bool forceLog = false) => Enqueue(LogLevel.Info, message, forceLog: forceLog);
    internal static void LogWarning(string message, bool forceLog = false) => Enqueue(LogLevel.Warn, message, forceLog: forceLog);
    internal static void LogError(string message, bool forceLog = false) => Enqueue(LogLevel.Error, message, forceLog: forceLog);
    internal static void LogCritical(string message, bool exit = false, int exitCode = 1) => Enqueue(LogLevel.Critical, message, exit, exitCode);
    internal static void LogOK(string message, bool forceLog = false) => Enqueue(LogLevel.Ok, message, forceLog: forceLog);
    internal static void LogCmd(string message) => Enqueue(LogLevel.Cmd, message);
    internal static void LogConnection(string message) => Enqueue(LogLevel.Connection, message);
    private static void OKText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[ ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("OK");
        Console.ForegroundColor = originalColor;
        Console.Write(" ] ");
    }
    private static void INFOText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("INFO");
        Console.ForegroundColor = originalColor;
        Console.Write("] ");
    }
    private static void WARNText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("WARN");
        Console.ForegroundColor = originalColor;
        Console.Write("] ");
    }
    private static void FAILText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("FAIL");
        Console.ForegroundColor = originalColor;
        Console.Write("] ");
    }
    private static void CRITText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write("CRIT");
        Console.ForegroundColor = originalColor;
        Console.Write("] ");
    }
    private static void CMDText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("CMD");
        Console.ForegroundColor = originalColor;
        Console.Write("] ");
    }
    private static void ConnText()
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("CONN");
        Console.ForegroundColor = originalColor;
        Console.Write("] ");
    }

    public delegate Task LogBroadcastDelegate(string ansiLog);
    public static event LogBroadcastDelegate? OnLogBroadcast;
}