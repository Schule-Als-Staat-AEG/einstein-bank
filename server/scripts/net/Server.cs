using System.Threading.RateLimiting;
using Max35050.API;
using Max35050.Auth;
using Max35050.Extras.Worktime;
using Max35050.Helper;
using Max35050.Net;
using Max35050.Systems;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http; // added
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using PocketBase;

namespace Max35050.Net;

public static class Server
{
    public static string databaseURL = "localhost:8090";
    public static PocketBase.PocketBase db = null!;
    public static IHubContext<JobAPI>? jobHubContext = null;
    public static WebApplication? app = null;
    public static async Task Start(int port = 8080)
    {
        // Create ASP.NET Core Web Application
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(port);
        });
        builder.Services.AddControllers();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.None);
        builder.Logging.AddFilter("Microsoft", LogLevel.None);
        builder.Logging.AddFilter("System", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: "AllowFrontend",
                policy =>
                {
                    policy.SetIsOriginAllowed(_ => true); // Allow any origin
                    policy.AllowCredentials();
                    policy.WithMethods("GET", "POST", "DELETE", "OPTIONS", "PUT", "PATCH", "HEAD");
                    policy.AllowAnyHeader();
                });
        });

        builder.Services.AddSignalR();
        if (SettingsManager.enableRateLimiting)
            {
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("Fixed", limiterOptions =>
                {
                    limiterOptions.Window = TimeSpan.FromSeconds(SettingsManager.rateLimitWindowSeconds);
                    limiterOptions.PermitLimit = SettingsManager.rateLimitPermitLimit;
                    limiterOptions.QueueProcessingOrder = SettingsManager.rateLimitQueueProcessingOrder == "OldestFirst" ? QueueProcessingOrder.OldestFirst : QueueProcessingOrder.NewestFirst;
                    limiterOptions.QueueLimit = SettingsManager.rateLimitQueueLimit;
                });
            });
        }

        var app = builder.Build();
        Server.app = app;

        Debug.LogOK("Configuring CORS...", true);
        app.UseCors("AllowFrontend");
        if (SettingsManager.enableRateLimiting)
        {
            app.UseRateLimiter();
            Debug.LogOK("Rate Limititer armed", true);
        }

        Debug.Log("This Server follows the CRUD principles for REST APIs.", true);
        Debug.Log("Create => POST || Read => GET || Update => PATCH || Delete => DELETE", true);

        Debug.Log("Setting up log broadcasting via SignalR...", true);

        var logHubContext = app.Services.GetService(typeof(Microsoft.AspNetCore.SignalR.IHubContext<LogAPI>)) as Microsoft.AspNetCore.SignalR.IHubContext<LogAPI>;
        Debug.OnLogBroadcast += async (ansiLog) =>
        {
            if (logHubContext != null)
                await LogAPI.BroadcastLog(ansiLog, logHubContext);
        };

        var jobHubContext = app.Services.GetService(typeof(Microsoft.AspNetCore.SignalR.IHubContext<JobAPI>)) as Microsoft.AspNetCore.SignalR.IHubContext<JobAPI>;
        Server.jobHubContext = jobHubContext;

        // Start Server Systems
        await SettingsManager.PullSettings();
        _ = TaxPoolManager.TaxPoolResetWorker();
        _ = RecurringTransactionManager.Initialize();
        _ = WealthTaxManager.Initialize();
        _ = CaseManager.GetPotentialCases();
        _ = ContractManager.Initialize();
        SecurityManager.Initialize();
        TaxFlagsManager.Initialize();
        IRSHelper.Initialize();
        CaptchaManager.Initialize();
        TransactionAPI.Initialize();
        _ = WorktimeManager.Initialize();
        _ = InflationManager.Initialize();

        Debug.LogOK("Pulled settings from database!", true);
        Debug.LogOK("Service Workers started (11): TaxPoolWorker, RecurringTransactionManager, WealthTaxManager, CaseManager, SecurityManager, TaxFlagsManager, IRSHelper, CaptchaManager, TransactionAPI, ContractManager, InflationManager", true);


        Debug.LogOK("Server Systems started!", true);
        app.MapGet("/api/health", () => Results.Ok(new { status = "Server is running." }));
        //Debug.Log("Registering: GET     /api/health", true);

        app.MapPost("/api/regenerate-key", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");
            return await KeyAPI.RegenerateKey(authToken);
        });
        //Debug.Log("Registering: POST    /api/regenerate-key", true);

        app.MapPost("/api/transaction", async (HttpContext context) =>
        {
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");
    
                // Read request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
    
                // Process transaction
                return await TransactionAPI.ProcessTransaction(body, authToken, context);
                
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/transaction: {e.Message}");
                return Results.InternalServerError(new { valid = false, message = "Internal server error while processing transaction." });
            }
        });
        //Debug.Log("Registering: POST    /api/transaction", true);

        app.MapGet("/api/generate-captcha", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");
            (string, string) captcha = await CaptchaManager.GenerateCaptcha(authToken);
            string captchaID = captcha.Item1;
            string captchaQuestion = captcha.Item2;
            if (captchaID == "" && captchaQuestion == "")
            {
                return new RateLimited(new { valid = false, message = "Captcha generation rate limit exceeded. Please wait before generating a new captcha." });
            }
            return Results.Ok(new { captchaID, captchaQuestion });
        });
        //Debug.Log("Registering: GET     /api/generate-captcha", true);
        
        app.MapPost("/api/transaction-with-code", async (HttpContext context) =>
        {
            try
            {
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");
                // Read request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
    
                // Process transaction with code
                return await TransactionAPI.ProcessTransactionViaCode(body, authToken, context);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/transaction-with-code: {e.Message}");
                return Results.InternalServerError(new { valid = false, message = "Internal server error while processing transaction with code." });
            }
        });
        //Debug.Log("Registering: POST    /api/transaction-with-code", true);

        app.MapPost("/api/account-with-code", async (HttpContext context) =>
        {
            try
            {
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");
                // Read request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
    
                // Process account with code
                return await AccountAPI.GetAccountWithCode(body, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/account-with-code: {e.Message}");
                return Results.InternalServerError(new { valid = false, message = "Internal server error while processing account with code." });
            }
        });
        //Debug.Log("Registering: POST    /api/account-with-code", true);
        
        app.MapPost("/api/password-with-code", async (HttpContext context) =>
        {
            try
            {
                dynamic body = JsonConvert.DeserializeObject<dynamic>(await new StreamReader(context.Request.Body).ReadToEndAsync())!;
                string code = (string)body.security;
                string accountID = (string)body.accountID;
                string password = (string)body.password;
                string passwordConfirm = (string)body.passwordConfirm;
                string cardPin = (string?)body.pin ?? "";
                string companyName = (string?)body.companyName ?? "Unnamed Company";

                // Process password reset with code
                bool isCodeValid = await CodeManager.IsCodeValid(accountID, code, ignorePinCheck: true);
                User user = await UserManager.GetUserByAccountID(accountID);
                UserData userData = await UserManager.GetUserData(user.id);
                if (userData?.registered == true)
                {
                    return Results.BadRequest(new { valid = false, message = "Initial password has already been set for this account. Password reset with code can only be used for the initial password setup." });
                }
                if (!isCodeValid)
                {
                    return Results.BadRequest(new { valid = false, message = "Invalid code provided." });
                }
                if (password != passwordConfirm)
                {
                    return Results.BadRequest(new { valid = false, message = "Password and confirmation do not match." });
                }
                dynamic passwordRequest = new
                {
                    password,
                    passwordConfirm,
                    companyName
                };
                dynamic updatePinRequest = new
                {
                    pin = cardPin,
                    codeRequired = true
                };
                dynamic userDataRequest = new
                {
                    registered = true
                };
                
                dynamic? accValidation = await db.Collections("account_validation").GetFirstListItem($"account='{accountID}'");
                if (accValidation == null)
                {
                    return Results.BadRequest(new { valid = false, message = "No code record found for this account." });
                }
                await db.Collections("account_validation").Update(accValidation.id.ToString(), updatePinRequest);
                await db.Collections("users").Update(user.id, passwordRequest);
                await db.Collections("user_data").Update(userData!.id, userDataRequest);
                return Results.Ok(new { valid = true, message = "Password and Pin reset successful." });
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/password-with-code: {e.Message}");
                return Results.InternalServerError(new { valid = false, message = "Internal server error while processing password reset with code." });
            }
        });
        //Debug.Log("Registering: POST    /api/password-with-code", true);
        
        app.MapPost("/api/code-required", async (HttpContext context) =>
        {
            try
            {
                // Read request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                string accountID = (string)payload.accountID;
    
                // Check if code is required
                return await KeyAPI.CheckPinRequired(accountID);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/code-required: {e.Message}");
                return Results.InternalServerError(new { valid = false, message = "Internal server error while checking if code is required." });
            }
        });
        //Debug.Log("Registering: POST    /api/code-required", true);

        app.MapPost("/api/validate-pin", async (HttpContext context) =>
        {
            try
            {
                // Read request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                string accountID = (string)payload.accountID;
                string pin = (string)payload.pin;
    
                // Validate pin
                return await KeyAPI.ValidatePin(accountID, pin);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/validate-pin: {e.Message}");
                return Results.InternalServerError(new { valid = false, message = "Internal server error while validating pin." });
            }
        });
        //Debug.Log("Registering: POST    /api/validate-pin", true);

        app.MapPost("/api/admin/transaction", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");

            // Read request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            // Process admin transaction
            return await AdminTransactionAPI.CreateTransaction(body, authToken);
        });
        //Debug.Log("Registering: POST    /api/admin/transaction", true);

        app.MapPost("/api/admin/generate-signature", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");

            // Get Body parameters
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            // Generate signature
            return await AdminTransactionAPI.GenerateSignature(body, authToken);
        });
        //Debug.Log("Registering: POST    /api/admin/generate-signature", true);

        app.MapPost("/api/admin/regenerate-key", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string adminAuthToken = authHeader.ToString().Replace("Bearer ", "");

            // Read request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            dynamic payload = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(body)!;
            Debug.Log($"Admin Regenerate Key Payload: {JsonConvert.SerializeObject(payload)}");
            
            string accountID = (string)payload.accountID;
            bool generatePin = (bool?)payload.generatePin ?? true;

            return await KeyAPI.AdminRegenerateKey(accountID, adminAuthToken, generatePin);
        });
        //Debug.Log("Registering: POST    /api/admin/regenerate-key", true);

        app.MapPost("/api/admin/cmd", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");

            // Read request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;

            if (!await AuthManager.IsTokenSuperuser(authToken))
            {
                return Results.Unauthorized();
            }

            // Lock transaction
            return await CommandAPI.ExecuteCommand((string)payload.command, (string?)payload.identity, (string?)payload.password);
        });

        //Debug.Log("Registering: POST    /api/admin/cmd", true);

        app.MapPost("/api/admin/pull-settings", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");

            if (!await AuthManager.IsTokenSuperuser(authToken))
            {
                return Results.Unauthorized();
            }

            // Pull settings
            await SettingsManager.PullSettings();
            return Results.Ok(new { status = "Settings pulled successfully." });
        });

        app.MapPost("/api/admin/refund-transaction", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");

            if (!await AuthManager.IsTokenSuperuser(authToken))
            {
                return Results.Unauthorized();
            }

            return await AdminTransactionAPI.RefundTransaction(await new StreamReader(context.Request.Body).ReadToEndAsync(), authToken);
        });
        //Debug.Log("Registering: POST    /api/admin/refund-transaction", true);

        //Debug.Log("Registering: POST    /api/admin/pull-settings", true);

        app.MapPost("/api/sync-recurring-transaction", async (HttpContext context) =>
        {
            // get transaction id from query parameters
            if (!context.Request.Query.TryGetValue("id", out var idParam))
            {
                Debug.LogWarning("id query parameter missing");
                return Results.BadRequest(new { error = "id query parameter missing" });
            }

            string id = idParam.ToString();

            // Sync recurring transaction
            await RecurringTransactionManager.UpdateRecurringTransaction(id);
            _ = WealthTaxManager.Initialize(); // Reset the wealth tax worker to update the tax pool based on the new balances after the recurring transaction is synced
            return Results.Ok(new { status = "Recurring transaction synced successfully." });
        });
        //Debug.Log("Registering: POST    /api/sync-recurring-transaction", true);

        app.MapPost("/api/sync-contract", async (HttpContext context) =>
        {
            // get contract id from query parameters
            if (!context.Request.Query.TryGetValue("id", out var idParam))
            {
                Debug.LogWarning("id query parameter missing");
                return Results.BadRequest(new { error = "id query parameter missing" });
            }
            string authHeader = "";
            if (context.Request.Headers.TryGetValue("Authorization", out var authHeaderValue))
            {
                authHeader = authHeaderValue.ToString().Replace("Bearer ", "");
            }
            if (!await AuthManager.IsTokenSuperuser(authHeader))
            {
                return Results.Unauthorized();
            }

            string id = idParam.ToString();

            bool updated = await ContractManager.UpdateContract(id);

            if (!updated)
            {
                return Results.BadRequest(new { error = "Failed to update contract.", ok = false });
            }

            // Sync contract
            //await ContractManager.UpdateContract(id);
            return Results.Ok(new { status = "Contract synced successfully.", ok = true });
        });
        //Debug.Log("Registering: POST    /api/sync-contract", true);

        app.MapGet("/api/verify-signature", async (HttpContext context) =>
        {

            // Try to get the auth token from headers
            // For this the auth token is optional
            // The Transaction code is only send if an admin token is provided
            // Otherwise the code will be omitted in the response
            string authToken = "";
            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                authToken = authHeader.ToString().Replace("Bearer ", "");
            }
            bool addCode = false;
            if(await AuthManager.IsTokenSuperuser(authToken))
            {
                Debug.Log("Admin token provided for /api/verify-signature");
                addCode = true;
            }
            // Get signature from query parameters
            if (!context.Request.Query.TryGetValue("signature", out var signatureParam))
            {
                Debug.LogWarning("Signature query parameter missing");
                return Results.BadRequest(new { error = "Signature query parameter missing" });
            }
            string signature = signatureParam.ToString();

            // Verify signature
            return SignatureAPI.VerifySignature(signature, addCode);
        });
        //Debug.Log("Registering: GET     /api/verify-signature", true);

        app.MapGet("/api/admin/test-token", async (HttpContext context) =>
        {
            // Get auth token from headers
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                Debug.LogWarning("Authorization header missing");
                return Results.Unauthorized();
            }
            string authToken = authHeader.ToString().Replace("Bearer ", "");

            if (!await AuthManager.IsTokenSuperuser(authToken))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { status = "Token is valid and has superuser privileges." });
        });
        //Debug.Log("Registering: GET     /api/admin/test-token", true);

        app.MapPost("/api/calculate-tax", async (HttpContext context) =>
        {
            // Get body
            try
            {
                bool admin = false;
                string token = "";
                // Get auth token from headers
                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    string authToken = authHeader.ToString().Replace("Bearer ", "");
                    token = authToken;
                    if (await AuthManager.IsTokenSuperuser(authToken))
                    {
                        admin = true;
                    }
                }
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(body)!;
                if(!admin && token == "")
                {
                    Debug.LogWarning("Authorization header missing for /api/calculate-tax");
                    return Results.Unauthorized();
                }

                User user = null!;
                TransactionData transactionData = TransactionDataManager.ExtractDataFromBody(JsonConvert.SerializeObject(payload));
                if (!admin)
                {
                    user = await AuthManager.GetUserFromToken(token);
                    Account acc = await AccountManager.GetAccountByUserID(user.id);
                    transactionData.sender = acc.id;
                    transactionData.receiver = acc.id;
                }
                else
                {
                    if(payload.sender == null)
                    {
                        user = await AuthManager.GetUserFromToken(token);
                        Account acc = await AccountManager.GetAccountByUserID(user.id);
                        transactionData.sender = acc.id;
                    }
                    if(payload.receiver == null)
                    {
                        user = await AuthManager.GetUserFromToken(token);
                        Account acc = await AccountManager.GetAccountByUserID(user.id);
                        transactionData.receiver = acc.id;
                    }
                }
                if (payload.code != null && payload.sender != null)
                {
                    bool isCodeValid = await CodeManager.IsCodeValid((string)payload.sender, (string)payload.code, (string)payload.pin ?? "");
                    if (!isCodeValid)
                    {
                        return Results.BadRequest(new { valid = false, message = "Invalid code provided." });
                    }
                    transactionData.code = (string)payload.code;
                    transactionData.sender = (string)payload.sender;
                    transactionData.pin = (string)payload.pin ?? "";
                }
                
                TaxResult taxResult = await TaxManager.CalculateTaxes(transactionData, admin);
                return Results.Ok(taxResult);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/calculate-tax");
                Debug.LogError(e.Message);
                return Results.InternalServerError(new { valid = false, message = "Internal server error while calculating taxes." });
            }
        });
        //Debug.Log("Registering: POST    /api/calculate-tax", true);

        app.MapPost("/api/calculate-wealth-tax", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                User user = await AuthManager.GetUserFromToken(authToken);
                Account account = await AccountManager.GetAccountByUserID(user.id);

                decimal wealthTax = await WealthCalculator.CalculateTotalWealthTax(account.id);
                return Results.Ok(new { wealthTax, message = wealthTax > 0 ? "This account is liable for wealth tax." : "This account is not liable for wealth tax.", liable = wealthTax > 0 });
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/calculate-wealth-tax");
                Debug.LogError(e.Message);
                return Results.InternalServerError(new { valid = false, message = "Internal server error while calculating wealth tax." });
            }
        });
        // Debug.Log("Registering: POST    /api/calculate-wealth-tax", true);
        
        app.MapPost("/api/warehouse/create-order", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                return await OrderAPI.CreateOrder(payload, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/warehouse/create-order");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while creating warehouse order." });
            }
        });
        // Debug.Log("Registering: POST    /api/warehouse/create-order", true);
        
        app.MapPost("/api/warehouse/update-order", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                return await OrderAPI.UpdateOrder(payload, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/warehouse/update-order");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while updating warehouse order." });
            }
        });
        // Debug.Log("Registering: POST    /api/warehouse/create-order", true);
        
        app.MapPost("/api/warehouse/delete-order", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                return await OrderAPI.DeleteOrder(payload, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/warehouse/delete-order");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while deleting warehouse order." });
            }
        });
        // Debug.Log("Registering: POST    /api/warehouse/create-order", true);
        
        app.MapPost("/api/warehouse/set-order", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                return await OrderAPI.WarehouseUpdateOrder(payload, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/warehouse/set-order");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while setting warehouse order." });
            }
        });
        // Debug.Log("Registering: POST    /api/warehouse/create-order", true);
        
        app.MapPost("/api/admin/order", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
                return await OrderAPI.AdminOrderAPI(payload, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/admin/order");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while updating order." });
            }
        });
        // Debug.Log("Registering: POST    /api/admin/order", true);
        
        app.MapPost("/api/employee/request-case", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                return await CaseAPI.RequestCase(body, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/employee/request-case");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while requesting case." });
            }
        });
        //Debug.Log("Registering: POST    /api/employee/request-case", true);

        app.MapPost("/api/employee/submit-case", async (HttpContext context) =>
        {
            // Get body
            try
            {
                // Get auth token from headers
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    Debug.LogWarning("Authorization header missing");
                    return Results.Unauthorized();
                }
                string authToken = authHeader.ToString().Replace("Bearer ", "");

                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                return await CaseAPI.SubmitCase(body, authToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in /api/employee/submit-case");
                Debug.LogError(e.Message);
                Debug.LogError(e.StackTrace!);
                return Results.InternalServerError(new { success = false, message = "Internal server error while submitting case." });
            }
        });
        //Debug.Log("Registering: POST    /api/employee/submit-case", true);

        app.MapGet("/api/stats/infaltion", async (HttpContext context) =>
        {
            
        });
        app.MapGet("/api/stats/gdp", async (HttpContext context) =>
        {
            
        });
        await WorktimeManager.RegisterRoutes(app);

        app.MapHub<LogAPI>("/api/admin/log")
           .RequireCors("AllowFrontend");

        //Debug.Log("Registering: SIGNALR /api/admin/log", true);

        app.MapHub<JobAPI>("/api/employee/job")
           .RequireCors("AllowFrontend");

        //Debug.Log("Registering: SIGNALR /api/employee/job", true);

        Debug.Log($"Starting server on port {port}...", true);
        /*
        Debug.Log("");
        Debug.Log("         .d8888b.                      888");
        Debug.Log("        d88P  Y88b                     888");
        Debug.Log("        Y88b.                          888");
        Debug.Log("        \"Y888b.     888  888 .d8888b  888888 .d88b.  88888b.d88b.");
        Debug.Log("            \"Y88b.  888  888 88K       888   d8P  Y8b 888 \"888 \"88b");
        Debug.Log("            \"88888  888  Y88  8888b.   888   88888888 888  888  888");
        Debug.Log("        Y88b  d88P  Y88b 888     X88   Y88b. Y8b.     888  888  888");
        Debug.Log("        \"Y8888P\"    \"Y88888  88888P'   \"Y888 \"Y8888  888  888  888");
        Debug.Log("                        888");
        Debug.Log("                    Y8b d88");
        Debug.Log("                    \"Y88P\"");
        Debug.Log("        .d88888b.                                     888    d8b                            888");
        Debug.Log("        d88P\" \"Y88b                                   888    Y8P                            888");
        Debug.Log("        888     888                                   888                                   888");
        Debug.Log("        888     888 88888b.   .d88b.  888d888 8888b.  888888 888  .d88b.  88888b.   8888b.  888");
        Debug.Log("        888     888 888 \"88b d8P  Y8b 888P\"      \"88b 888    888 d88\"\"88b 888 \"88b     \"88b 888");
        Debug.Log("        888     888 888  888 88888888 888    .d888888 888    888 888  888 888  888 .d888888 888");
        Debug.Log("        Y88b. .d88P 888 d88P Y8b.     888    888  888 Y88b.  888 Y88..88P 888  888 888  888 888");
        Debug.Log("        \"Y88888P\"  88888P\"   \"Y8888   888    \"Y888888  \"Y888 888  \"Y88P\"  888  888 \"Y888888 888");
        Debug.Log("                    888");
        Debug.Log("                    888");
        Debug.Log("                    888");
        Debug.Log("                    888");
        Debug.Log("");
        */
        Debug.LogOK($"Server Startup completed in: {DateTime.Now - Program.startTime}", true);
        await app.RunAsync();
    }
}
