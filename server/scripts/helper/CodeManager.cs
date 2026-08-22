using System.Security.Cryptography;
using System.Text;
using Max35050.Auth;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Helper;

public static class CodeManager
{
    public static async Task<CodeRecord> GenerateCode(string accountID, bool generatePin = true)
    {
        // Generate a random 24 digit code
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$%^&*()-_=+[]{}|;:,.<>?";
        // Crypto random generator
        var random = RandomNumberGenerator.GetString(chars, 24);
        // Create bcrypt hash of the code
        string hashedKey = BCrypt.Net.BCrypt.HashPassword($"{accountID}:{random}", workFactor: 12);

        // Generate Checksum
        string checksum = GetSha256(hashedKey).ToUpper();

        // Upload to database
        
        // Check if code exists
        try
        {
            dynamic result = await new RestCall(Server.databaseURL + $"/api/collections/account_validation/records?filter=(account='{accountID}')", ServerAuth.authToken).GetDynamic();
            if (result != null && result!.items.Count > 0)
            {
                dynamic cr = new
                {
                    account = accountID,
                    security = hashedKey,
                    checksum = checksum,
                    invalidate = false,
                    frozen = false,
                };
                // Update existing record
                result = await new RestCall(Server.databaseURL + $"/api/collections/account_validation/records/{result!.items[0].id}", ServerAuth.authToken, Newtonsoft.Json.JsonConvert.SerializeObject(cr)).PatchDynamic();
                CodeRecord codeRecordResult = new CodeRecord
                {
                    id = result.id.ToString(),
                    account = result.account.ToString(),
                    security = result.security.ToString(),
                    checksum = result.checksum.ToString(),
                    invalidate = result.invalidate.ToObject<bool>(),
                    frozen = result.frozen.ToObject<bool>(),
                    number = result.number.ToString(),
                    codeRequired = result.codeRequired.ToObject<bool>(),
                    pin = result.pin.ToString(),
                    created = DateTime.Parse(result.created.ToString()),
                    updated = DateTime.Parse(result.updated.ToString())
                };
                return codeRecordResult;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error in GenerateCode: " + ex.Message);
            // No existing code
        }
        dynamic codeRecord = new
        {
            account = accountID,
            security = hashedKey,
            checksum,
            invalidate = false,
            frozen = false,
            codeRequired = generatePin,
            pin = generatePin ? Random.Shared.Next(0, 10000).ToString("D4") : ""
        };
        string jsonPayload = JsonConvert.SerializeObject(codeRecord);
        dynamic response = await new RestCall($"{Server.databaseURL}/api/collections/account_validation/records", ServerAuth.authToken, jsonPayload).PostDynamic();
        return new CodeRecord
        {
            id = response.id.ToString(),
            account = response.account.ToString(),
            security = response.security.ToString(),
            checksum = response.checksum.ToString(),
            invalidate = response.invalidate.ToObject<bool>(),
            frozen = response.frozen.ToObject<bool>(),
            number = response.number.ToString(),
            codeRequired = response.codeRequired.ToObject<bool>(),
            pin = response.pin.ToString(),
            created = DateTime.Parse(response.created.ToString()),
            updated = DateTime.Parse(response.updated.ToString())
        };
    }
    public static string GetSha256(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = sha256.ComputeHash(inputBytes);

        return Convert.ToHexString(hashBytes);
    }
    public static async Task<bool> IsCodeValid(string accountID, string code, string pin = "", bool ignorePinCheck = false)
    {
        // Retrieve code record from database
        dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/account_validation/records?filter=(account='{accountID}')", ServerAuth.authToken).GetDynamic();
        var item = result.items.Count > 0 ? result.items[0] : null;
        if (item != null)
        {
            if (item.codeRequired == true && !ignorePinCheck)
            {
                if (item.pin != pin)
                {
                    return false;
                }
            }
            if (item.invalidate == true || item.frozen == true)
            {
                return false;  
            } 
            string storedHash = item.security;
            if (storedHash == code)
            {
                // Verify checksum
                string expectedChecksum = GetSha256(storedHash).ToUpper();
                if (item.checksum != expectedChecksum)
                {
                    return false;
                }
                return true;
            }
        }
        return false;
    }
}