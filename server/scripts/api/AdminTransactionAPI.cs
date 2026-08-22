using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.API;

public static class AdminTransactionAPI
{
    public static async Task<IResult> CreateTransaction(string body, string adminAuthToken)
    {
        try
        {
            if (await AuthManager.IsTokenSuperuser(adminAuthToken) == false)
            {
                return Results.Unauthorized();
            }
            TransactionData td = TransactionDataManager.ExtractDataFromBody(body);
            TaxResult? taxResult = new TaxResult();
            if (td.applyTaxes)
            {
                taxResult = await TaxManager.CalculateTaxes(td, true);
            } else
            {
                taxResult = null;
            }
            await TransactionAPI.MoveMoney(td);
            return Results.Ok(new { valid = true, id = td.transactionID });
        }
        catch
        {
            return Results.StatusCode(500);
        }
    }
    public static async Task<IResult> GenerateSignature(string transactionData, string adminAuthToken)
    {
        try
        {
            if (await AuthManager.IsTokenSuperuser(adminAuthToken) == false)
            {
                return Results.Unauthorized();
            }
            TransactionData td = TransactionDataManager.ExtractDataFromBody(transactionData);
            // Create signature object (jwt)
            string signature = AuthManager.CreateJWT(new Dictionary<string, object>
            {
                { "fromAccountID", td.sender },
                { "toAccountID", td.receiver },
                { "amount", td.totalDeductable },
                { "received", td.amount },
                { "description", td.description.Trim() },
                { "system", td.system.Trim() },
                { "timestamp", td.timestamp != null ? new DateTimeOffset(td.timestamp.Value).ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "code", td.code },
                { "vat_applicable", td.taxData.VATApplicable },
                { "income_tax_applicable", td.taxData.IncomeTaxApplicable },
            });
            return Results.Ok(new { signature = signature });
        }
        catch
        {
            return Results.StatusCode(500);
        }
    }
    public static async Task<IResult> RefundTransaction(string body, string adminAuthToken)
    {
        try
        {
            if (await AuthManager.IsTokenSuperuser(adminAuthToken) == false)
            {
                return Results.Unauthorized();
            }
            dynamic payload = JsonConvert.DeserializeObject<dynamic>(body)!;
            string transaction = payload.transaction.ToString();
            string method = payload.method.ToString();
            string description = payload.description?.ToString() ?? "";

            TransactionData originalTransaction = await TransactionDataManager.GetTransactionByID(transaction);
            if (originalTransaction == null)
            {
                return Results.BadRequest(new { valid = false, message = "Original transaction not found." });
            }
            if (method == "erase")
            {
                // Undo original transaction
                decimal taxMoney = originalTransaction.amount - originalTransaction.received;
                await AccountManager.SubtractBalance(originalTransaction.receiver, originalTransaction.received);
                await AccountManager.AddBalance(originalTransaction.sender, originalTransaction.amount);
                if (taxMoney > 0)
                {
                    await AccountManager.SubtractBalance(SettingsManager.taxAccountID, taxMoney);
                }
                await Server.db.Collections("transactions").Delete(transaction);
                await StatsManager.DeleteStats(originalTransaction.transactionID!, originalTransaction.sender, originalTransaction.receiver);
                await StatsManager.CheckDeleteGDP(originalTransaction.transactionID!, originalTransaction.amount, originalTransaction.description);
                return Results.Ok(new { valid = true, message = "Transaction erased and undone successfully." });
            }
            else if (method == "new")
            {
                // Create new refund transaction
                decimal taxMoney = originalTransaction.amount - originalTransaction.received;
                TransactionData refundTransaction = new TransactionData
                {
                    sender = originalTransaction.receiver,
                    receiver = originalTransaction.sender,
                    amount = originalTransaction.amount,
                    received = originalTransaction.received,
                    description = $"Rückerstattung für Transaktion {originalTransaction.transactionID}. {description}",
                    system = "##REFUND##",
                    code = "",
                    taxData = new TransactionTaxData
                    {
                        VATApplicable = false,
                        IncomeTaxApplicable = false,
                        TransactionTaxApplicable = false
                    },
                    applyTaxes = false,
                    logTransaction = true,
                    moveMoney = true,
                    status = TransactionStatus.Approved,
                };
                await TransactionManager.LogTransaction(refundTransaction);
                await AccountManager.SubtractBalance(originalTransaction.receiver, originalTransaction.received);
                await AccountManager.AddBalance(originalTransaction.sender, originalTransaction.amount);
                if (taxMoney > 0)
                {
                    await AccountManager.SubtractBalance(SettingsManager.taxAccountID, taxMoney);
                }
                await StatsManager.DeleteStats(originalTransaction.transactionID!, originalTransaction.sender, originalTransaction.receiver);
                await StatsManager.CheckDeleteGDP(originalTransaction.transactionID!, originalTransaction.amount, originalTransaction.description);
                return Results.Ok(new { valid = true, id = refundTransaction.transactionID, message = "Refund transaction created successfully." });
            }
            else
            {
                return Results.BadRequest(new { valid = false, message = "Invalid refund method." });
            }
        }
        catch
        {
            return Results.StatusCode(500);
        }
    }
}