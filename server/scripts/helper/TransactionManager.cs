using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Max35050.Auth;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Helper;

public static class TransactionManager
{
    public static async Task<string> LogTransaction(TransactionData td)
    {
        // Create signature object (jwt)
        string signature = AuthManager.CreateJWT(new Dictionary<string, object>
        {
            { "fromAccountID", td.sender },
            { "toAccountID", td.receiver },
            { "amount", td.totalDeductable },
            { "received", td.amount },
            { "description", td.description.Trim() },
            { "system", td.system.Trim() },
            { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "code", td.code != "" ? Convert.ToBase64String(CryptoHelper.pubRsa.Encrypt(Encoding.UTF8.GetBytes(td.code), RSAEncryptionPadding.OaepSHA256)) : "" },
            { "vat_applicable", td.taxData.VATApplicable },
            { "income_tax_applicable", td.taxData.IncomeTaxApplicable },
            { "flagging_score", td.suspiciousScore },
            { "flagging_reasons", td.suspiciousReasons }
        });

        dynamic transaction = new
        {
            sender = td.sender,
            receiver = td.receiver,
            amount = td.totalDeductable,
            received = td.amount,
            description = td.description.Trim(),
            system = td.system.Trim(),
            signature = signature,
            status = TransactionStatusExtentions.ToString(td.status),
        };
        string jsonPayload = JsonConvert.SerializeObject(transaction);
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/transactions/records", ServerAuth.authToken, jsonPayload).PostDynamic();
        if (td.status == TransactionStatus.Pending)
        {
            IRSHelper.transactionsToCheck.Add(td);
        }
        return result.id ?? string.Empty;
    }
    /// <summary>
    /// Update the amound and received fields of a transaction. NOTE: This does not transfer any money
    /// </summary>
    /// <param name="transactionID"></param>
    /// <param name="newAmount"></param>
    /// <param name="newReceived"></param>
    /// <returns></returns>
    public static async Task<TransactionRecord?> UpdateTransactionAmount(string transactionID, decimal newAmount, decimal newReceived)
    {
        TransactionRecord? transaction = await GetTransaction(transactionID);
        if (transaction == null)
        {
            return null;
        }
        // Get JWT Contents
        JWTResult tokenResult = AuthManager.DecodeJWT(transaction.signature);
        Dictionary<string, object> claims = tokenResult.claims!.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
        claims["amount"] = newAmount;
        claims["received"] = newReceived;
        // Create new signature
        string newSignature = AuthManager.CreateJWT(claims);

        dynamic updateData = new
        {
            amount = newAmount,
            received = newReceived,
            signature = newSignature,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };
        await Server.db.Collections("transactions").Update(transactionID, updateData);
        await StatsManager.UpdateStats(transactionID, newAmount, newReceived);
        return transaction;
    }
    public static async Task UpdateTransaction(TransactionData td)
    {
        string signature = AuthManager.CreateJWT(new Dictionary<string, object>
        {
            { "fromAccountID", td.sender },
            { "toAccountID", td.receiver },
            { "amount", td.totalDeductable },
            { "received", td.amount },
            { "description", td.description.Trim() },
            { "system", td.system.Trim() },
            { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "code", td.code != "" ? Convert.ToBase64String(CryptoHelper.pubRsa.Encrypt(Encoding.UTF8.GetBytes(td.code), RSAEncryptionPadding.OaepSHA256)) : "NO:CODE:USED" },
            { "vat_applicable", td.taxData.VATApplicable },
            { "income_tax_applicable", td.taxData.IncomeTaxApplicable },
            { "flagging_score", td.suspiciousScore },
            { "flagging_reasons", td.suspiciousReasons }
        });

        dynamic transaction = new
        {
            sender = td.sender,
            receiver = td.receiver,
            amount = td.totalDeductable,
            received = td.amount,
            description = td.description.Trim(),
            system = td.system.Trim(),
            signature = signature,
            status = TransactionStatusExtentions.ToString(td.status),
        };
        string jsonPayload = JsonConvert.SerializeObject(transaction);
        await new RestCall($"{Server.databaseURL}/api/collections/transactions/records/{td.transactionID}", ServerAuth.authToken, jsonPayload).PatchDynamic();
        await StatsManager.UpdateStats(td.transactionID!, td.totalDeductable, td.amount);
    }
    public static async Task<TransactionRecord?> GetTransaction(string transactionID)
    {
        try
        {
            dynamic? result = await Server.db.Collections("transactions").GetOne(transactionID);
            if (result != null)
            {
                TransactionRecord tr = new TransactionRecord
                {
                    id = result.id,
                    sender = result.sender,
                    receiver = result.receiver,
                    amount = result.amount,
                    received = result.received,
                    description = result.description,
                    system = result.system,
                    signature = result.signature,
                    status = result.status,
                    created = result.created
                 };
                return tr;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
    public static async Task SetStatus(string transactionID, TransactionStatus status)
    {
        dynamic updateData = new
        {
            status = TransactionStatusExtentions.ToString(status)
        };
        await Server.db.Collections("transactions").Update(transactionID, updateData);
    }
}