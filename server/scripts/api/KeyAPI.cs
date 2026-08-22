using System.Security.Permissions;
using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.API;

public static class KeyAPI
{
    public static async Task<IResult> RegenerateKey(string authToken)
    {
        dynamic user = await AuthManager.GetUserFromToken(authToken);
        dynamic account = await AccountManager.GetAccountByUserID((string)user.id);
        dynamic code = await CodeManager.GenerateCode((string)account.id);
        dynamic returnCode = new {
            security = code.security,
            checksum = code.checksum
        };
        return Results.Ok(returnCode);
    }
    public static async Task<IResult> AdminRegenerateKey(string accountID, string adminAuthToken, bool generatePin = true)
    {
        try
        {
            if (await AuthManager.IsTokenSuperuser(adminAuthToken) == false)
            {
                return Results.Unauthorized();
            }
            dynamic account = await AccountManager.GetAccountByUserID(accountID);
            if (account == null)
            {
                return Results.BadRequest(new { message = "Account does not exist." });
            }
            CodeRecord code = await CodeManager.GenerateCode((string)account.id, generatePin);
            dynamic returnCode = new {
                id = code.id,
                account = code.account,
                invalidate = code.invalidate,
                frozen = code.frozen,
                security = code.security,
                checksum = code.checksum,
                codeRequired = code.codeRequired,
                pin = code.pin
            };
            return Results.Ok(returnCode);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error in AdminRegenerateKey: " + ex.Message);
            Debug.LogError(ex.StackTrace!);
            return Results.StatusCode(500);
        }
    }
    public static async Task<IResult> CheckPinRequired(string accountID)
    {
        dynamic? result = await Server.db.Collections("account_validation").GetFirstListItem($"account=\"{accountID}\"");
        if (result == null)
        {
            return Results.NotFound(new { message = "No validation code found for this account." });
        }
        bool codeRequired = false;
        if (result.codeRequired != null && bool.TryParse(result.codeRequired.ToString(), out codeRequired))
        {
            return Results.Ok(new { codeRequired });
        }
        return Results.Ok(new { codeRequired = false });
    }
    public static async Task<IResult> ValidatePin(string accountID, string pin)
    {
        dynamic? result = await Server.db.Collections("account_validation").GetFirstListItem($"account=\"{accountID}\"");
        if (result == null)
        {
            return Results.NotFound(new { message = "No validation code found for this account." });
        }
        bool codeRequired = false;
        if (result.codeRequired != null && bool.TryParse(result.codeRequired.ToString(), out codeRequired) && codeRequired)
        {
            string storedPin = result.pin.ToString();
            if (storedPin == pin)
            {
                return Results.Ok(new { valid = true });
            }
            else
            {
                return new Unauthorized(new { valid = false });
            }
        }
        return Results.BadRequest(new { message = "PIN validation is not required for this account." });
    }
}