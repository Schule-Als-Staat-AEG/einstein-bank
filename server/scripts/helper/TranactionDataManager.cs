using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Ganss.Xss;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Helper;

public static class TransactionDataManager
{
    public static TransactionData ExtractDataFromBody(string body)
    {
        dynamic data = JsonConvert.DeserializeObject(body)!;
        TransactionData transactionData = new TransactionData();
        transactionData.captchaAnswer = data.captchaAnswer == null ? 0 : long.Parse(data.captchaAnswer.ToObject<string>() ?? "0");
        transactionData.captchaID = data.captchaID == null ? "" : data.captchaID.ToObject<string>();
        transactionData.sender = data.sender == null ? "" : data.sender.ToObject<string>();
        transactionData.receiver = data.receiver == null ? "" : data.receiver.ToObject<string>();
        transactionData.amount = Math.Round((decimal?)(data.amount == null ? 0 : data.amount.ToObject<decimal>()) ?? 0, 2); // Round to 2 decimal places if needed
        transactionData.description = data.description == null ? "" : data.description.ToObject<string>();
        transactionData.code = data.code == null ? "" : data.code.ToObject<string>();
        transactionData.pin = data.pin == null ? "" : data.pin.ToObject<string>();
        transactionData.taxData.VATApplicable = data.vat == null ? false : data.vat.ToObject<bool>();
        transactionData.taxData.IncomeTaxApplicable = data.incomeTax == null ? false : data.incomeTax.ToObject<bool>();
        transactionData.taxData.TransactionTaxApplicable = data.transactionTax == null ? false : data.transactionTax.ToObject<bool>(); // Admin only
        transactionData.moveMoney = data.moveMoney == null ? true : data.moveMoney.ToObject<bool>(); // Admin only
        transactionData.logTransaction = data.logTransaction == null ? true : data.logTransaction.ToObject<bool>(); // Admin only
        transactionData.applyTaxes = data.applyTaxes == null ? true : data.applyTaxes.ToObject<bool>(); // Admin only
        transactionData.system = data.system == null ? "" : data.system.ToObject<string>(); // Admin only
        transactionData.timestamp = data.timestamp == null ? DateTime.UtcNow : data.timestamp.ToObject<DateTime>(); // Admin only - used for redoing signatures with correct timestamp if needed
        transactionData.status = TransactionStatusExtentions.FromString(data.status == null ? "pending" : data.status.ToObject<string>()); // Admin only - used for setting initial status of transaction if needed
        return transactionData;
    }
    public static TransactionData SanitizeData(TransactionData transactionData)
    {
        var sanitizer = new HtmlSanitizer();
        transactionData.description = sanitizer.Sanitize(transactionData.description.Trim().Trim('\n', '\r')); // Trim description and remove excessive newlines to prevent abuse of description field for formatting purposes
        (transactionData.description, bool foundInvisible) = UnicodeSanitizer.SanitizeString(transactionData.description);
        transactionData.description = transactionData.description.Replace("##QR_CODE_AUTHORIZATION_ACKNOWLEDGED##", "{USER_ADDED_FORGERY_INTERCEPTED_AND_REPLACED}"); // Remove QR code acknowledgement if present to prevent forgery
        transactionData.description = transactionData.description.Replace("##RECURRING_TRANSACTION_AUTHORIZED##", "{USER_ADDED_FORGERY_INTERCEPTED_AND_REPLACED}"); // Remove recurring transaction authorization if present to prevent forgery
        transactionData.description += foundInvisible ? "\n{PURGED_INVISIBLE_UNICODE_CHARACTERS}" : "";
        transactionData.system += foundInvisible ? "##PURGED_INVISIBLE_UNICODE_CHARACTERS##\n" : "";
        transactionData.system += transactionData.description.ToLower().Contains("ficsit") ? "Construct, Automate, Explore & Exploit.\n" : ""; // Easter egg
        transactionData.system += transactionData.description.ToLower().Contains("einstein") ? "Relativität im Banking.\n" : ""; // Easter egg
        transactionData.system += transactionData.description.ToLower().Contains("albert einstein") ? "Relativität im Banking.\n" : ""; // Easter egg
        transactionData.system += transactionData.description.ToLower().Contains("e=mc^2") ? "Relativität im Banking.\n" : ""; // Easter egg
        transactionData.system += transactionData.description.ToLower().Contains("e=mc2") ? "Relativität im Banking.\n" : ""; // Easter egg
        transactionData.system += transactionData.description.ToLower().Contains("emc2") ? "Relativität im Banking.\n" : ""; // Easter egg
        transactionData.system = transactionData.system.Trim(); // Trim system field after appending easter egg text to prevent excessive whitespace
        transactionData.system = transactionData.system.Trim('\n', '\r'); // Trim newlines from system field to prevent excessive whitespace
        return transactionData;
    }
    public static string IsTransactionDataValid(TransactionData transactionData)
    {
        if (string.IsNullOrWhiteSpace(transactionData.sender))
            return "01#Sender account ID is required.";
        if (string.IsNullOrWhiteSpace(transactionData.receiver))
            return "02#Receiver account ID is required.";
        if (transactionData.amount <= 0)
            return "03#Transaction amount must be greater than zero.";
        if (transactionData.amount > 1000000000) // 1 billion limit for sanity
            return "04#Transaction amount exceeds the maximum allowed limit.";
        if (transactionData.description.Length > 5000)
            return "05#Transaction description exceeds the maximum length of 5000 characters.";
        if (AccountManager.GetAccount(transactionData.sender).Result == null)
            return "06#Sender account does not exist.";
        if (AccountManager.GetAccount(transactionData.receiver).Result == null)
            return "07#Receiver account does not exist.";
        if (AccountManager.GetAccount(transactionData.sender).Result.frozen == true)
            return "08#Account is frozen.";
        if (AccountManager.HasBalance(transactionData.sender, transactionData.amount).Result == false)
            return "09#Insufficient funds in sender's account.";
        return "OK";
    }
    public static async Task<UserTaxData> GetTaxData(string accountID)
    {
        dynamic result = (await Server.db.Collections("user_tax_data").GetFirstListItem($"user=\"{accountID}\""))!;
        UserTaxData? utd = new UserTaxData();
        if (result == null)
        {
            return utd; // Return default tax data if none exists
        }
        try { utd.TaxExempt = bool.Parse(result!.taxExempt.ToObject<string>() ?? "false"); } catch { utd.TaxExempt = false; }
        try { utd.VatExempt = bool.Parse(result!.vatExempt.ToObject<string>() ?? "false"); } catch { utd.VatExempt = false; }
        try { utd.IncomeExempt = bool.Parse(result!.incomeExempt.ToObject<string>() ?? "false"); } catch { utd.IncomeExempt = false; }
        try { utd.WealthExempt = bool.Parse(result!.wealthExempt.ToObject<string>() ?? "false"); } catch { utd.WealthExempt = false; }
        try { utd.TransactionExempt = bool.Parse(result!.transactionExempt.ToObject<string>() ?? "false"); } catch { utd.TransactionExempt = false; }
        try { utd.CompanyExempt = bool.Parse(result!.companyExempt.ToObject<string>() ?? "false"); } catch { utd.CompanyExempt = false; }
        try { utd.VatOverride = decimal.Parse(result!.vatOverride.ToObject<string>() ?? "-1.0"); } catch { utd.VatOverride = -1.0m; }
        try { utd.IncomeOverride = decimal.Parse(result!.incomeOverride.ToObject<string>() ?? "-1.0"); } catch { utd.IncomeOverride = -1.0m; }
        try { utd.TransactionAmountOverride = decimal.Parse(result!.transactionAmountOverride.ToObject<string>() ?? "-1.0"); } catch  { utd.TransactionAmountOverride = -1.0m; }
        try { utd.TransactionFreePoolOverride = decimal.Parse(result!.transactionFreeOverride.ToObject<string>() ?? "-1.0"); } catch  { utd.TransactionFreePoolOverride = -1.0m; }
        try { utd.CompanyOverride = decimal.Parse(result!.companyOverride.ToObject<string>() ?? "-1.0"); } catch { utd.CompanyOverride = -1.0m; }
        try { utd.GlobalMultiplier = decimal.Parse(result!.globalMultiplier.ToObject<string>() ?? "1.0"); } catch { utd.GlobalMultiplier = 1.0m; }
        try { utd.TransactionTaxFreePool = decimal.Parse(result!.taxFreePool.ToObject<string>() ?? "0.0"); } catch { utd.TransactionTaxFreePool = 0.0m; }
        try { utd.TotalIncomeReceived = decimal.Parse(result!.totalIncomeReceived.ToObject<string>() ?? "0.0"); } catch { utd.TotalIncomeReceived = 0.0m; }
        return utd;
    }
    public static async Task<TransactionData> GetTransactionByID(string transactionID)
    {
        dynamic result = (await Server.db.Collections("transactions").GetFirstListItem($"id=\"{transactionID}\""))!;
        TransactionData td = new TransactionData();
        td.transactionID = result.id.ToObject<string>() ?? "";
        td.sender = result.sender.ToObject<string>() ?? "";
        td.receiver = result.receiver.ToObject<string>() ?? "";
        td.amount = Math.Round((decimal?)(result.amount.ToObject<decimal?>() ?? 0) ?? 0, 2);
        td.received = Math.Round((decimal?)(result.received.ToObject<decimal?>() ?? 0) ?? 0, 2);
        td.description = result.description.ToObject<string>() ?? "";
        td.system = result.system.ToObject<string>() ?? "";
        try { td.timestamp = result.created != null ? DateTime.Parse(result.created.ToObject<string>() ?? "") : DateTime.UtcNow; } catch { td.timestamp = DateTime.UtcNow; }
        return td;
    }
}