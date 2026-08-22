using Max35050.Auth;
using Max35050.Helper;
using Newtonsoft.Json;

namespace Max35050.API;

public static class AccountAPI
{
    public static async Task<IResult> GetAccountWithCode(string body, string token)
    {
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);
        if (data == null || data!.code == null || data!.account == null)
        {
            return Results.BadRequest(new { valid = false, message = "Invalid request body. 'code' and 'account' are required." });
        }
        string code = data!.code;
        string accountID = data!.account;
        string pin = data.pin != null ? data.pin.ToString() : "";
        if (!await AuthManager.IsTokenSuperuser(token))
        {
            if (!await CodeManager.IsCodeValid(accountID, code, pin))
            {
                return Results.Forbid();
            }
        }
        Account account = await AccountManager.GetAccount(accountID);
        User user = await UserManager.GetUser(account.user);
        if (account == null)
        {
            return Results.NotFound(new { valid = false, message = "Account not found." });
        }
        return Results.Ok(new { valid = true, account, user });
    }
}