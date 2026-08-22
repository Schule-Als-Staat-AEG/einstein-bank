using System.Diagnostics;
using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;
using Max35050.Systems;

namespace Max35050.API;

public static class TransactionAPI
{
    public static bool allowTransactions = true;
    public static Dictionary<string, DateTime> pendingTransactions = new Dictionary<string, DateTime>();
    public static CancellationTokenSource cts = new CancellationTokenSource();
    public static ulong blockedTransactions = 0;
    public static async Task Shutdown()
    {
        allowTransactions = false;
        int retryCount = 0;
        while (pendingTransactions.Count > 0 && retryCount < 15)
        {
            Debug.Log($"Waiting for pending transactions to complete... {pendingTransactions.Count} remaining. Attempt {retryCount + 1}/15", true);
            await Task.Delay(1000);
            retryCount++;
        }
    }
    public static void Initialize()
    {
        Task.Run(() => CleanupPendingTransactions(cts.Token), cts.Token);
    }
    public static async Task CleanupPendingTransactions(CancellationToken ct)
    {
        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, DateTime> pair in pendingTransactions)
            {
                if ((DateTime.UtcNow - pair.Value).TotalSeconds > 10)
                {
                    toRemove.Add(pair.Key);
                }
            }
            foreach (string id in toRemove)
            {
                pendingTransactions.Remove(id);
            }
            await Task.Delay(60000, ct); // Check for pending transactions every minute
        }
    }
    public static async Task<IResult> ProcessTransaction(string data, string authToken, HttpContext context)
    {
        string taskID = "";
        try
        {
            generateID:
            taskID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
            if (pendingTransactions.ContainsKey(taskID))
            {
                goto generateID;
            }
            pendingTransactions[taskID] = DateTime.UtcNow;
        }
        catch
        {
            return Results.InternalServerError(new { valid = false, message = "Internal server error while processing transaction." });
        }
        try
        {
            TransactionData transactionData = TransactionDataManager.ExtractDataFromBody(data);
            bool isAdministrator = await AuthManager.IsTokenSuperuser(authToken);
            if (!isAdministrator)
            {
                // ---------------
                // SECURITY CHECKS
                // ---------------
                if (!CaptchaManager.ValidateCaptcha(authToken, transactionData.captchaID, transactionData.captchaAnswer))
                {
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = "Captcha validation failed." });
                }
                (bool, SecurityResponse) securityResult = await SecurityManager.AllowRequest(context, transactionData.sender, true);
                if (!securityResult.Item1)
                {
                    await SecurityManager.LogAccess(context, transactionData.sender, false);
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = securityResult.Item2.ToString() });
                }
                await SecurityManager.LogAccess(context, transactionData.sender, true);

                // -------------------
                // END SECURITY CHECKS
                // -------------------

                transactionData.logTransaction = true;
                transactionData.moveMoney = true;
                if(!allowTransactions)
                {
                    blockedTransactions++;
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = "Transactions are currently disabled. [FULL LOAD REJECTION]" });
                }
                transactionData = TransactionDataManager.SanitizeData(transactionData);
                string message = TransactionDataManager.IsTransactionDataValid(transactionData);
                if (message != "OK")
                {   
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message});
                }
                string userID = (await AuthManager.GetUserFromToken(authToken)).id;
                if((await AccountManager.GetAccountByUserID(userID)).id != transactionData.sender)
                {
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = "Authentication token does not match sender account. Account is " + transactionData.sender + ", token belongs to " + (await AccountManager.GetAccountByUserID(userID)).id });
                }
                transactionData.status = TransactionStatus.Pending;
                transactionData.timestamp = DateTime.UtcNow;
            }
            TaxResult taxResult = await TaxManager.CalculateTaxes(transactionData);
            transactionData.taxResult = taxResult;
    
            // Check balance and move money
            if (isAdministrator || await AccountManager.HasBalance(transactionData.sender, transactionData.totalDeductable))
            {
                string transactionID = await MoveMoney(transactionData);
                decimal newBalance = await AccountManager.GetBalance(transactionData.sender);
                if(SettingsManager.notifyTransactions)
                {
                    await NotificationManager.CreateNotification(transactionData.sender, "Transaktion erfolgreich", $"Die Transaktion in Höhe von {transactionData.amount} an {await AccountManager.GetAccountNameFromID(transactionData.receiver)} wurde erfolgreich durchgeführt. Transaktions-ID: {transactionID}", $"transactions#{transactionID}");
                    await NotificationManager.CreateNotification(transactionData.receiver, "Geld erhalten", $"Du hast Geld in Höhe von {transactionData.amount} von {await AccountManager.GetAccountNameFromID(transactionData.sender)} erhalten. Transaktions-ID: {transactionID}", $"transactions#{transactionID}");
                }
                pendingTransactions.Remove(taskID);
                return Results.Ok(new { valid = true, message = "Transaction completed successfully.", transactionID = transactionID, newBalance = newBalance });
            }
            else
            {
                pendingTransactions.Remove(taskID);
                return Results.BadRequest(new { valid = false, message = "Insufficient funds." });
            }
        }
        catch
        {
            pendingTransactions.Remove(taskID);
            return Results.InternalServerError(new { valid = false, message = "Internal server error while processing transaction." });
        }
    }
    public static async Task<IResult> ProcessTransactionViaCode(string data, string authToken, HttpContext context)
    {
        string taskID = "";
        try
        {
            generateID:
            taskID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
            if (pendingTransactions.ContainsKey(taskID))
            {
                goto generateID;
            }
            pendingTransactions[taskID] = DateTime.UtcNow;
        }
        catch
        {
            return Results.InternalServerError(new { valid = false, message = "Internal server error while processing transaction." });
        }
        try
        {
            TransactionData transactionData = TransactionDataManager.ExtractDataFromBody(data);
            bool isAdministrator = await AuthManager.IsTokenSuperuser(authToken);
            if (!isAdministrator)
            {
                User user = await AuthManager.GetUserFromToken(authToken);
                string userID = user.id;

                // ---------------
                // SECURITY CHECKS
                // ---------------
                if (!CaptchaManager.ValidateCaptcha(authToken, transactionData.captchaID, transactionData.captchaAnswer))
                {
                    CaptchaAnswer captchaAnswer = CaptchaManager.CaptchaSessions.FirstOrDefault(c => c.CaptchaID == transactionData.captchaID && c.UserID == userID)!;
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = "Captcha validation failed." });
                }
                (bool, SecurityResponse) securityResult = await SecurityManager.AllowRequest(context, transactionData.sender, true);
                if (!securityResult.Item1)
                {
                    await SecurityManager.LogAccess(context, transactionData.sender, false);
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = securityResult.Item2.ToString() });
                }
                await SecurityManager.LogAccess(context, transactionData.sender, true);

                // -------------------
                // END SECURITY CHECKS
                // -------------------

                transactionData.logTransaction = true;
                transactionData.moveMoney = true;
                if (!allowTransactions)
                {
                    blockedTransactions++;
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = "Transactions are currently disabled. [FULL LOAD REJECTION]" });
                }
                transactionData = TransactionDataManager.SanitizeData(transactionData);
                string message = TransactionDataManager.IsTransactionDataValid(transactionData);
                if (message != "OK")
                {
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message});
                }

                transactionData.status = TransactionStatus.Pending;
                transactionData.timestamp = DateTime.UtcNow;
                transactionData.receiver = (await AccountManager.GetAccountByUserID(userID)).id; // Override receiver with user's main account to prevent abuse of this endpoint to send money to other accounts
            }
            if (!isAdministrator)
            {
                bool isCodeValid = await CodeManager.IsCodeValid(transactionData.sender, transactionData.code, transactionData.pin);
                if (!isCodeValid)
                {
                    pendingTransactions.Remove(taskID);
                    return Results.BadRequest(new { valid = false, message = "Invalid Transaction code." });
                }
            }
            TaxResult taxResult = await TaxManager.CalculateTaxes(transactionData);
            transactionData.system += "##QR_CODE_AUTHORIZATION_ACKNOWLEDGED##\n";
            transactionData.taxResult = taxResult;
    
            // Check balance and move money
            if (isAdministrator || await AccountManager.HasBalance(transactionData.sender, transactionData.totalDeductable))
            {
                string transactionID = await MoveMoney(transactionData);
                decimal newBalance = await AccountManager.GetBalance(transactionData.sender);
                if (SettingsManager.notifyTransactions)
                {
                    await NotificationManager.CreateNotification(transactionData.sender, "Transaktion erfolgreich", $"Die Transaktion in Höhe von {transactionData.amount} an {await AccountManager.GetAccountNameFromID(transactionData.receiver)} wurde erfolgreich durchgeführt. Transaktions-ID: {transactionID}", $"transactions#{transactionID}");
                    await NotificationManager.CreateNotification(transactionData.receiver, "Geld erhalten", $"Du hast Geld in Höhe von {transactionData.amount} von {await AccountManager.GetAccountNameFromID(transactionData.sender)} erhalten. Transaktions-ID: {transactionID}", $"transactions#{transactionID}");
                }
                pendingTransactions.Remove(taskID);
                return Results.Ok(new { valid = true, message = "Transaction completed successfully.", transactionID = transactionID, newBalance = newBalance });
            }
            else
            {
                pendingTransactions.Remove(taskID);
                return Results.BadRequest(new { valid = false, message = "Insufficient funds." });
            }
        }
        catch
        {
            pendingTransactions.Remove(taskID);
            return Results.InternalServerError(new { valid = false, message = "Internal server error while processing transaction." });
        }
    }

    /// <summary>
    /// This method moves money between accounts, logs the transaction and creates tax reports if necessary. It is used by the ProcessTransaction method after all checks have been passed and the transaction is ready to be executed. The reason why this is a separate method is because there are some internal transactions (like the order handshake) where we want to move money without doing all the checks that are in the ProcessTransaction method, but we still want to log the transaction and create tax reports. By having this as a separate method, we can reuse it for those internal transactions without having to duplicate code.
    /// </summary>
    /// <param name="transactionData"></param>
    /// <returns>The Transaction ID</returns>
    public static async Task<string> MoveMoney(TransactionData transactionData)
    {
        if (transactionData.moveMoney)
        {
            await AccountManager.SubtractBalance(transactionData.sender, transactionData.totalDeductable);
            await AccountManager.AddBalance(transactionData.receiver, transactionData.amount);
        }
        if(transactionData.taxResult != null)
        {
            await AccountManager.AddBalance(SettingsManager.taxAccountID, transactionData.taxResult.TotalTaxAmount);
            await AccountManager.SubtractPool(transactionData.sender, transactionData.taxResult.TaxFreePoolUsed);
            if (transactionData.taxData.IncomeTaxApplicable)
            {
                await AccountManager.AddIncomeReceived(transactionData.sender, transactionData.amount);
            }
        }
        string id = "";
        if (transactionData.logTransaction)
        {
            id = await TransactionManager.LogTransaction(transactionData);
            transactionData.transactionID = id;
            await StatsManager.LogStats(transactionData);
        }
        if (transactionData.taxResult != null)
        {
            await TaxReportManager.CreateTaxReport(transactionData);
        }
        return id;
    }
}