// YOU DOIN TAX EVASION?????????
// WE DOIN SCANS NOW, HIHIHI
// GLORY TO THE MINISTRY

using System.Diagnostics;
using Max35050;
using Max35050.IO;
using Max35050.Systems;

namespace Max35050.Helper;

public static class IRSHelper
{
    public static List<TransactionData> transactionsToCheck = new List<TransactionData>();
    public static double susScore = 0;
    public static string susOperator = "<";
    public static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    public static Task workerThread = null!;
    public static List<string> incomeWordList = new List<string>();
    public static List<string> vatWordList = new List<string>();
    public static void Initialize()
    {
        // Pull word list
        try
        {
            incomeWordList = File.ReadAllLines(Path.Combine(FileManager.DataPath, "income_words.txt")).ToList().Select(word => word.ToLower()).ToList();
            vatWordList = File.ReadAllLines(Path.Combine(FileManager.DataPath, "vat_words.txt")).ToList().Select(word => word.ToLower()).ToList();
            List<string> toRemove = new List<string>();
            incomeWordList.ForEach(word =>
            {
                word = word.Trim();
                if(word.StartsWith("#"))
                {
                    toRemove.Add(word);
                }
            });
            incomeWordList.RemoveAll(word => toRemove.Contains(word));
            toRemove.Clear();
            vatWordList.ForEach(word =>
            {
                word = word.Trim();
                if(word.StartsWith("#"))
                {
                    toRemove.Add(word);
                }
            });
            vatWordList.RemoveAll(word => toRemove.Contains(word));
        }
        catch
        {
            incomeWordList = new List<string> { "salary", "gehalt", "lohn", "vergütung", "honorar", "provision", "abfindung" };
            vatWordList = new List<string> { "zahlung an", "payment to", "transfer to" };
            string incomeDefault = string.Join(Environment.NewLine, incomeWordList);
            string vatDefault = string.Join(Environment.NewLine, vatWordList);
            if (!File.Exists(Path.Combine(FileManager.DataPath, "income_words.txt"))) { File.WriteAllText(Path.Combine(FileManager.DataPath, "income_words.txt"), incomeDefault); }
            if (!File.Exists(Path.Combine(FileManager.DataPath, "vat_words.txt"))) { File.WriteAllText(Path.Combine(FileManager.DataPath, "vat_words.txt"), vatDefault); }
            Debug.Log("Created default word lists.", true);
            Debug.Log("Please review and edit the word lists in the config folder to ensure optimal flagging performance.", true);
        }
        cancellationTokenSource.Cancel();
        cancellationTokenSource = new CancellationTokenSource();
        workerThread = TransactionFlaggingWorker(cancellationTokenSource);
    }
    public static async Task TransactionFlaggingWorker(CancellationTokenSource cts)
    {
        CancellationTokenSource currentTokenSource = cts;
        while (!currentTokenSource.IsCancellationRequested)
        {
            try
            {
                List<TransactionData> transactionsToProcess = [.. transactionsToCheck];
                transactionsToCheck.Clear();
                foreach (TransactionData td in transactionsToProcess)
                {
                    bool flagged = await CheckTransaction(td);
                    if (flagged && td.status == TransactionStatus.Pending)
                    {
                        td.status = TransactionStatus.Flagged;
                        await TransactionManager.UpdateTransaction(td);
                        await CaseManager.AddCase(td);
                    }
                    else if (!flagged && td.status == TransactionStatus.Pending)
                    {
                        td.status = TransactionStatus.Approved;
                        await TransactionManager.UpdateTransaction(td);
                    }
                }
            }
            catch { }
            await Task.Delay(2500, currentTokenSource.Token); // Check every 2,5 seconds
        }
    }
    public static async Task<bool> CheckTransaction(TransactionData td)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Account senderAccount = await AccountManager.GetAccount(td.sender);
        Account receiverAccount = await AccountManager.GetAccount(td.receiver);

        AccountType senderType = await AccountManager.GetAccountType(senderAccount.id);
        AccountType receiverType = await AccountManager.GetAccountType(receiverAccount.id);

        List<IncomeLog> incomeLogs = await StatsManager.GetIncomeLogs(td.receiver);
        List<ExpenseLog> expenseLogs = await StatsManager.GetExpenseLogs(td.sender);

        string extraInfo = "";

        td.system += td.system.EndsWith("\n") ? "##Server_Transaction_Check_Applied##" : "\n##Server_Transaction_Check_Applied##";

        UserTaxData rtd = await TransactionDataManager.GetTaxData(td.receiver); // rtd = Remote Tax Data, used for Income Tax
        
        foreach (TaxScoreRule rule in TaxFlagsManager.Rules)
        {
            switch (rule.Type)
            {
                case TaxCheckType.PaysVAT:
                    if ((td.taxResult != null && td.taxResult.VATAmount > 0) || td.taxData.VATApplicable || (td.userTaxData != null && td.userTaxData.VatExempt) || (rtd != null && rtd.VatExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PaysIncomeTax:
                    if (td.taxResult != null && td.taxResult.IncomeTaxAmount > 0 || td.taxData.IncomeTaxApplicable || (rtd != null && rtd.IncomeExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.BusinessToBusiness:
                    if (senderType == AccountType.Business && receiverType == AccountType.Business)
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.BusinessToPersonal:
                    if (senderType == AccountType.Business && receiverType == AccountType.Personal)
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PersonalToBusiness:
                    if (senderType == AccountType.Personal && receiverType == AccountType.Business)
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PersonalToPersonal:
                    if (senderType == AccountType.Personal && receiverType == AccountType.Personal)
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.BusinessToBusinessIncomePaid:
                    if (senderType != AccountType.Business || receiverType != AccountType.Business) { continue; }
                    if (td.taxResult != null && td.taxResult.IncomeTaxAmount > 0 || td.taxData.IncomeTaxApplicable || (rtd != null && rtd.IncomeExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.BusinessToPersonalIncomePaid:
                    if (senderType != AccountType.Business || receiverType != AccountType.Personal) { continue; }
                    if (td.taxResult != null && td.taxResult.IncomeTaxAmount > 0 || td.taxData.IncomeTaxApplicable || (rtd != null && rtd.IncomeExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PersonalToBusinessIncomePaid:
                    if (senderType != AccountType.Personal || receiverType != AccountType.Business) { continue; }
                    if (td.taxResult != null && td.taxResult.IncomeTaxAmount > 0 || td.taxData.IncomeTaxApplicable || (rtd != null && rtd.IncomeExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PersonalToPersonalIncomePaid:
                    if (senderType != AccountType.Personal || receiverType != AccountType.Personal) { continue; }
                    if (td.taxResult != null && td.taxResult.IncomeTaxAmount > 0 || td.taxData.IncomeTaxApplicable || (rtd != null && rtd.IncomeExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.BusinessToBusinessVATPaid:
                    if (senderType != AccountType.Business || receiverType != AccountType.Business) { continue; }
                    if ((td.taxResult != null && td.taxResult.VATAmount > 0) || td.taxData.VATApplicable || (td.userTaxData != null && td.userTaxData.VatExempt) || (rtd != null && rtd.VatExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.BusinessToPersonalVATPaid:
                    if (senderType != AccountType.Business || receiverType != AccountType.Personal) { continue; }
                    if ((td.taxResult != null && td.taxResult.VATAmount > 0) || td.taxData.VATApplicable || (td.userTaxData != null && td.userTaxData.VatExempt) || (rtd != null && rtd.VatExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PersonalToBusinessVATPaid:
                    if (senderType != AccountType.Personal || receiverType != AccountType.Business) { continue; }
                    if ((td.taxResult != null && td.taxResult.VATAmount > 0) || td.taxData.VATApplicable || (td.userTaxData != null && td.userTaxData.VatExempt) || (rtd != null && rtd.VatExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.PersonalToPersonalVATPaid:
                    if (senderType != AccountType.Personal || receiverType != AccountType.Personal) { continue; }
                    if ((td.taxResult != null && td.taxResult.VATAmount > 0) || td.taxData.VATApplicable || (td.userTaxData != null && td.userTaxData.VatExempt) || (rtd != null && rtd.VatExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    }
                    else
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.DescriptionIndicatesCashRegister: // Requires VAT
                    string description = td.description.ToLower();
                   (string Match, double Score) bestVatMatch = FindBestSubstringMatch(description, vatWordList.ToArray());
                    if((bestVatMatch.Score > 0.8 && td.taxResult != null && td.taxResult.VATAmount > 0) || td.taxData.VATApplicable || (td.userTaxData != null && td.userTaxData.VatExempt) || (rtd != null && rtd.VatExempt)) { 
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    } else if (bestVatMatch.Score > 0.8 && td.taxResult != null && td.taxResult.VATAmount == 0) {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.DescriptionIndicatesSalary: // Requires Income Tax
                    (string Match, double Score) bestIncomeMatch = FindBestSubstringMatch(td.description, incomeWordList.ToArray());
                    if(bestIncomeMatch.Score > 0.8 && td.taxResult != null && td.taxResult.IncomeTaxAmount > 0 || td.taxData.IncomeTaxApplicable || (rtd != null && rtd.IncomeExempt))
                    {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                    } else if (bestIncomeMatch.Score > 0.8 && td.taxResult != null && td.taxResult.IncomeTaxAmount == 0) {
                        td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                    }
                    break;
                case TaxCheckType.HighAverageTransactionAmount:
                    // Make sure there is enough data to calculate an average
                    if (incomeLogs.Count >= 7 && expenseLogs.Count >= 7)
                    {
                        decimal averageIncome = incomeLogs.Average(log => log.amount);
                        decimal averageExpense = expenseLogs.Average(log => log.amount);

                        decimal averageIncomeThreshold = averageIncome * 2.5m; // 150% higher than average income
                        decimal averageExpenseThreshold = averageExpense * 2.5m; // 150% higher than average expense

                        extraInfo += extraInfo.EndsWith("\n") ? $"Average Income: {averageIncome}\nAverage Expense: {averageExpense}\nAverage Income Threshold: {averageIncomeThreshold}\nAverage Expense Threshold: {averageExpenseThreshold}\n" : $"\nAverage Income: {averageIncome}\nAverage Expense: {averageExpense}\nAverage Income Threshold: {averageIncomeThreshold}\nAverage Expense Threshold: {averageExpenseThreshold}\n";

                        if (td.amount > averageIncomeThreshold || td.amount > averageExpenseThreshold)
                        {
                            td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                        }
                        else if (td.amount <= averageIncomeThreshold && td.amount <= averageExpenseThreshold)
                        {
                            td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                        }
                    }
                    break;
                case TaxCheckType.HighTransactionFrequencyBusiness:
                    if (senderType != AccountType.Business) { break; }

                    // Make sure there is enough data to calculate frequency
                    if (incomeLogs.Count >= 7 && expenseLogs.Count >= 7)
                    {
                        DateTime oneHourAgo = DateTime.UtcNow.AddHours(-1);
                        int recentIncomeCount = incomeLogs.Count(log => log.timestamp >= oneHourAgo);
                        int recentExpenseCount = expenseLogs.Count(log => log.timestamp >= oneHourAgo);

                        int averageIncomeFrequency = recentIncomeCount / 1; // Average per hour
                        int averageExpenseFrequency = recentExpenseCount / 1; // Average per hour

                        extraInfo += extraInfo.EndsWith("\n") ? $"Recent Income Count: {recentIncomeCount}\nRecent Expense Count: {recentExpenseCount}\nAverage Income Frequency: {averageIncomeFrequency}\nAverage Expense Frequency: {averageExpenseFrequency}\n" : $"\nRecent Income Count: {recentIncomeCount}\nRecent Expense Count: {recentExpenseCount}\nAverage Income Frequency: {averageIncomeFrequency}\nAverage Expense Frequency: {averageExpenseFrequency}\n";

                        int frequencyThreshold = 25; // More than 25 transactions in the last hour is considered high frequency for business accounts

                        if (recentIncomeCount > frequencyThreshold || recentExpenseCount > frequencyThreshold)
                        {
                            td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                        }
                        else if (recentIncomeCount <= frequencyThreshold && recentExpenseCount <= frequencyThreshold)
                        {
                            td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                        }
                    }
                    break;
                case TaxCheckType.HighTransactionFrequencyPersonal:
                    if (senderType != AccountType.Personal) { break; }

                    // Make sure there is enough data to calculate frequency
                    if (incomeLogs.Count >= 7 && expenseLogs.Count >= 7)
                    {
                        DateTime oneHourAgo = DateTime.UtcNow.AddHours(-1);
                        int recentIncomeCount = incomeLogs.Count(log => log.timestamp >= oneHourAgo);
                        int recentExpenseCount = expenseLogs.Count(log => log.timestamp >= oneHourAgo);

                        int averageIncomeFrequency = recentIncomeCount / 1; // Average per hour
                        int averageExpenseFrequency = recentExpenseCount / 1; // Average per hour

                        extraInfo += extraInfo.EndsWith("\n") ? $"Recent Income Count: {recentIncomeCount}\nRecent Expense Count: {recentExpenseCount}\nAverage Income Frequency: {averageIncomeFrequency}\nAverage Expense Frequency: {averageExpenseFrequency}\n" : $"\nRecent Income Count: {recentIncomeCount}\nRecent Expense Count: {recentExpenseCount}\nAverage Income Frequency: {averageIncomeFrequency}\nAverage Expense Frequency: {averageExpenseFrequency}\n";

                        int frequencyThreshold = 10; // More than 10 transactions in the last hour is considered high frequency for personal accounts

                        if (recentIncomeCount > frequencyThreshold || recentExpenseCount > frequencyThreshold)
                        {
                            td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, td: td);
                        }
                        else if (recentIncomeCount <= frequencyThreshold && recentExpenseCount <= frequencyThreshold)
                        {
                            td.suspiciousScore = ApplyScore(td.suspiciousScore, rule, true, td: td);
                        }
                    }
                    break;
            }
        }
        bool result = IsFlagged(td.suspiciousScore);
        if (senderAccount.trusted || receiverAccount.trusted)
        {
            result = false;
            td.suspiciousReasons += "ACC:TRUSTED#CHECK_OVERRULED";
        }
        if (!TaxManager.CurrentTaxSettings.Enabled)
        {
            td.system += "\n##Tax_Flagging_System#FULL_LOAD_REJECTION##";
            td.system += "\n##Tax_System_Disabled##";
            td.system += "\n##Flagging_Result_Passed##";
            sw.Stop();
            td.system += $"\n##Check_Time_Elapsed::{sw.ElapsedMilliseconds}ms##";
            return false;
        }
        td.system += $"\n##Flagging_Result_{(result == false ? "Passed" : "Rejected")}##";
        sw.Stop();
        td.system += $"\n##Check_Time_Elapsed::{sw.ElapsedMilliseconds}ms##";
        await TaxReportManager.AppendExtraInfo(td.transactionID!, extraInfo);
        return result;
    }
    public static double ApplyScore(double score, TaxScoreRule rule, bool negate = false, TransactionData td = null!)
    {
        if (td != null && (negate == false && rule.Operator == "=" || rule.Score != 0) || (negate == true && rule.NOperator == "=" && rule.NScore != 0))
        {
            td!.suspiciousReasons += $"{(negate ? "NOT " : "")}{rule.Type.ToString()}, ";
        }
        if (negate)
        {
            switch (rule.NOperator)
            {
                case "+":
                    return score + rule.NScore;
                case "*":
                    return score * rule.NScore;
                case "/":
                    return score / rule.NScore;
                case "-":
                    return score - rule.NScore;
                case "=":
                    return rule.NScore;
                default:
                    return score;
            }
        }
        switch (rule.Operator)
        {
            case "+":
                return score + rule.Score;
            case "*":
                return score * rule.Score;
            case "/":
                return score / rule.Score;
            case "-":
                return score - rule.Score;
            case "=":
                return rule.Score;
            default:
                return score;
        }
    }
    public static bool IsFlagged(double score)
    {
        switch (susOperator)
        {
            case ">":
                return score > susScore;
            case "<":
                return score < susScore;
            case ">=":
                return score >= susScore;
            case "<=":
                return score <= susScore;
            case "=":
                return score == susScore;
            default:
                return false;
        }
    }
    public static int LevenshteinDistance(string s, string t)
    {
        int[,] d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[s.Length, t.Length];
    }

    public static double Similarity(string a, string b)
    {
        int distance = LevenshteinDistance(a, b);
        int maxLen = Math.Max(a.Length, b.Length);

        return 1.0 - (double)distance / maxLen;
    }

    public static (string Match, double Score) FindBestSubstringMatch(string longText, string[] search)
    {
        longText = longText.ToLower();
        search = search.Select(s => s.ToLower()).ToArray();

        string bestMatch = "";
        double bestScore = 0;
        foreach (string s in search)
        {
            for (int i = 0; i <= longText.Length - s.Length; i++)
            {
                string window = longText.Substring(i, s.Length);

                double score = Similarity(window, s);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = window;
                }
            }
        }

        return (bestMatch, bestScore);
    }
}