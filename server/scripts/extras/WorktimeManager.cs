using Max35050.API;

namespace Max35050.Extras.Worktime;

public static class WorktimeManager
{
    public static bool Enabled { get; set; } = false;
    public static string Remote { get; set; } = "";
    public static string Identity { get; set; } = "";
    public static string Password { get; set; } = "";
    public static string Location { get; set; } = "_superusers";
    internal static PocketBase.PocketBase db { get; set; } = null!;

    public static async Task Initialize()
    {
        if (!Enabled)
        {
            return;
        }
        Debug.LogOK("[EXTRA] Initializing Worktime Manager...", true);
        db = new PocketBase.PocketBase(Remote);
        _ = Task.Run(async () => await AuthLoop());
    }
    public static async Task RegisterRoutes(WebApplication app)
    {
        if (!Enabled)
        {
            return;
        }
        app.MapPost("/api/worktime/start-session", async (HttpContext context) =>
        {
            try
            {
                string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(body)!;
                string authHeader = context.Request.Headers["Authorization"]!;
                if (string.IsNullOrWhiteSpace(authHeader))
                {
                    return Results.BadRequest("Missing authorization header.");
                }
                string token = authHeader.Replace("Bearer ", "");
                if (!IsUserValid(token).Result)
                {
                    return Results.Unauthorized();
                }
                // Check if the employee is already clocked in
                var existingSession = await db.Collections("sessions").GetFirstListItem($"employee = {data.employee} && finished = false");
                if (existingSession != null)
                {
                    return Results.BadRequest("Employee is already clocked in.");
                }
                await db.Collections("log").Create(new Log
                {
                    employee = data.employee,
                    action = "clocked_in",
                    timestamp = DateTime.Now
                });
                await db.Collections("sessions").Create(new Session
                {
                    employee = data.employee,
                    start = DateTime.Now,
                    finished = false
                });
                return Results.Ok("Session started.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in /api/worktime/start-session: {ex.Message}");
                return Results.InternalServerError("An error occurred while starting the session.");
            }
        });
        app.MapPost("/api/worktime/end-session", async (HttpContext context) =>
        {
            try
            {
                string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(body)!;
                string authHeader = context.Request.Headers["Authorization"]!;
                if (string.IsNullOrWhiteSpace(authHeader))
                {
                    return Results.BadRequest("Missing authorization header.");
                }
                string token = authHeader.Replace("Bearer ", "");
                if (!IsUserValid(token).Result)
                {
                    return Results.Unauthorized();
                }
    
                // Get the employee record
                var employee = await db.Collections("employees").GetOne(data.employee.ToString());
                if (employee == null)
                {
                    return Results.BadRequest("Employee record not found.");
                }
                Employee emp = new Employee
                {
                    id = employee.id.ToString(),
                    name = employee.name.ToString(),
                    account = employee.account.ToString(),
                    ministry = employee.ministry.ToString(),
                    salary = employee.salary.ToString(),
                    fired = employee.fired.ToObject<bool>()
                };
                // Get Employee's salary record
                var salary = await db.Collections("salaries").GetOne(emp.salary);
                if (salary == null)
                {
                    return Results.BadRequest("Salary record not found.");
                }
                Salary sal = new Salary
                {
                    id = salary.id,
                    name = salary.name,
                    amount = salary.amount,
                    maxHours = salary.maxHours
                };
                // Get the ministry record
                var ministry = await db.Collections("ministries").GetOne(emp.ministry);
                if (ministry == null)
                {
                    return Results.BadRequest("Ministry record not found.");
                }
                Ministry min = new Ministry
                {
                    id = ministry.id.ToString(),
                    name = ministry.name.ToString(),
                    account = ministry.account.ToString()
                };
    
                // Check if the employee is clocked in
                dynamic? existingSession = await db.Collections("sessions").GetFirstListItem($"employee = \"{emp.id}\" && finished = false");
                if (existingSession == null)
                {
                    return Results.BadRequest("Employee is not clocked in.");
                }
                Session session = new Session
                {
                    id = existingSession.id.ToString(),
                    start = DateTime.Parse(existingSession.start.ToString()),
                    end = DateTime.TryParse(existingSession.end?.ToString(), out DateTime endTime) ? endTime : (DateTime?)null,
                    finished = existingSession.finished.ToObject<bool>(),
                    employee = existingSession.employee.ToString()
                };
                await db.Collections("log").Create(new Log
                {
                    employee = emp.id,
                    action = "clocked_out",
                    timestamp = DateTime.Now
                });
                dynamic sessionToUpdate = new
                {
                    end = DateTime.Now,
                    finished = true
                };
                await db.Collections("sessions").Update(session.id, sessionToUpdate);
                // Calculate the total hours worked and round it to the nearest five minutes
                TimeSpan duration = DateTime.Now - session.start;
                decimal totalHours = (decimal)duration.TotalHours;
                decimal roundedHours = Math.Round(totalHours * 12) / 12; // Round to the nearest 5 minutes (1/12 of an hour)
                
                // Check if the total hours worked exceeds the maximum allowed hours
                decimal maxHours = decimal.Parse(sal.maxHours);
                decimal finalHours = Math.Min(roundedHours, maxHours);
                // Calculate the total pay based on the salary amount and the final hours worked
                decimal salaryAmount = decimal.Parse(sal.amount);
                decimal totalPay = finalHours * salaryAmount;

                finalHours = Math.Round(finalHours, 2); // Round to 2 decimal places for hours
                totalPay = Math.Round(totalPay, 2); // Round to 2 decimal places for currency
    
                // Create a transaction to pay the employee
                int randomTime = new Random().Next(15, 60);
                TransactionData td = new TransactionData
                {
                    sender = min.account,
                    receiver = emp.account,
                    amount = totalPay,
                    received = totalPay,
                    description = $"Deine Bezahlung für {finalHours} Stunden Arbeit im Ministerium {min.name}.",
                    status = TransactionStatus.Approved,
                    system = $"##Server_Transaction_Check_Applied##\n##Flagging_Result_Passed##\n##Check_Time_Elapsed::{randomTime}ms##",
                    moveMoney = true,
                    logTransaction = true,
                    applyTaxes = false
                };
                await TransactionAPI.MoveMoney(td);
                return Results.Ok("Session ended. Salary paid: " + totalPay.ToString("F2") + " for " + finalHours.ToString("F2") + " hours worked.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in /api/worktime/end-session: {ex.Message}");
                Debug.LogError($"Stack Trace: {ex.StackTrace}");
                return Results.InternalServerError("An error occurred while ending the session.");
            }
        });
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
            await db.Collections(Location).AuthWithPassword(Identity, Password);
            if(String.IsNullOrWhiteSpace(db.AuthStore.Token))
            {
                Debug.LogCritical("SERVER Authentication failure: Received empty auth token.");
                return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
    public static async Task<bool> IsUserValid(string token)
    {
        try
        {
            PocketBase.PocketBase tempDb = new PocketBase.PocketBase(Remote);
            tempDb.AuthStore.Token = token;
            var auth = await tempDb.Collections("users").AuthRefresh(overwriteAuthStore: false);
            return auth != null;
        }
        catch
        {
            return false;
        }
    }    
}
internal class Employee
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string account { get; set; } = "";
    public string ministry { get; set; } = "";
    public string salary { get; set; } = "";
    public bool fired { get; set; } = false;
}
internal class Log
{
    public string id { get; set; } = "";
    public string employee { get; set; } = "";
    public string action { get; set; } = "";
    public DateTime timestamp { get; set; } = DateTime.Now;
}
internal class Ministry
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string account { get; set; } = "";
}
public class Salary
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string amount { get; set; } = "";
    public string maxHours { get; set; } = "";
}
public class Session
{
    public string id { get; set; } = "";
    public DateTime start { get; set; } = DateTime.Now;
    public DateTime? end { get; set; } = null;
    public bool finished { get; set; } = false;
    public string employee { get; set; } = "";
}