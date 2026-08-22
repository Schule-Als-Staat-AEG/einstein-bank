namespace Max35050.Helper;

public static class WealthCalculator
{
    public static async Task<decimal> CalculateTotalWealthTax(string accountID)
    {
        if (TaxManager.CurrentTaxSettings.WealthTaxEnabled && TaxManager.CurrentTaxSettings.Enabled)
        {
            Account account = await AccountManager.GetAccount(accountID);
            UserTaxData utd = await TransactionDataManager.GetTaxData(account.id);
            if (utd.WealthExempt || utd.TaxExempt)
            {
                return 0;
            }
            decimal toTax = 0;

            AccountType accountType = await AccountManager.GetAccountType(account.id);

            List<WealthBracket> applicableBrackets = TaxManager.CurrentTaxSettings.WealthBrackets
                .Where(b => account.balance > b.from)
                .OrderByDescending(b => b.from)
                .Where(b => (b.company && accountType == AccountType.Business) || (!b.company && accountType == AccountType.Personal)).ToList();

            foreach (WealthBracket bracket in applicableBrackets)
            {
                decimal taxableAmount = account.balance - bracket.from;
                toTax += taxableAmount * bracket.amount;

                account.balance = bracket.from;
            }
            
            return toTax;
        }
        return 0;
    }
}