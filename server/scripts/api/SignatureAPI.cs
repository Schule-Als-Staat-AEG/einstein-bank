using System.Security.Cryptography;
using System.Text;
using Max35050.Auth;
using Max35050.Helper;

namespace Max35050.API;

public static class SignatureAPI
{
    public static IResult VerifySignature(string signature, bool addCode)
    {
        try
        {
            // Verify and decode signature (jwt)
            var payload = AuthManager.DecodeJWT(signature);
            if(payload == null)
            {
                return Results.BadRequest(new { valid = false, message = "Invalid signature." });
            }
            string code = payload.claims!["code"].ToString() ?? "";
            try
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    byte[] encrytptedBytes = Convert.FromBase64String(code);
                    byte[] decryptedBytes = CryptoHelper.prvRsa.Decrypt(encrytptedBytes, RSAEncryptionPadding.OaepSHA256);
                    string decryptedCode = Encoding.UTF8.GetString(decryptedBytes);
                    code = decryptedCode;
                }
                else
                {
                    code = "NO_CODE_PROVIDED";
                }
            }
            catch
            {
                code = "ERROR::CODE_DECRYPTION";
            }

            dynamic failSafe = new {
                fromAccountID = "CLAIMS::BROKEN",
                toAccountID = "CLAIMS::BROKEN",
                amount = "CLAIMS::BROKEN",
                received = "CLAIMS::BROKEN",
                description = "CLAIMS::BROKEN",
                system = "CLAIMS::BROKEN",
                timestamp = "CLAIMS::BROKEN",
                vatApplicable = "CLAIMS::BROKEN",
                incomeTaxApplicable = "CLAIMS::BROKEN",
                flaggingScore = "CLAIMS::BROKEN",
                flaggingReasons = "CLAIMS::BROKEN",
                nbf = "CLAIMS::BROKEN",
                exp = "CLAIMS::BROKEN",
                iss = "CLAIMS::BROKEN",
                aud = "CLAIMS::BROKEN",
                code = "CLAIMS::BROKEN"
            };
            dynamic saveCode = new {
                valid = payload.valid,
                claims = payload.claims != null ? new
                {
                    fromAccountID = payload.claims["fromAccountID"] ?? "UNKNOWN",
                    toAccountID = payload.claims["toAccountID"] ?? "UNKNOWN",
                    amount = payload.claims["amount"] ?? "UNKNOWN",
                    received = payload.claims["received"] ?? payload.claims["amount"] ?? "UNKNOWN",
                    description = payload.claims["description"] ?? "UNKNOWN",
                    system = payload.claims["system"] ?? "UNKNOWN",
                    timestamp = payload.claims["timestamp"] ?? "UNKNOWN",
                    vatApplicable = payload.claims["vat_applicable"] ?? "UNKNOWN",
                    incomeTaxApplicable = payload.claims["income_tax_applicable"] ?? "UNKNOWN",
                    flaggingScore = payload.claims["flagging_score"] ?? "UNKNOWN",
                    flaggingReasons = payload.claims["flagging_reasons"] ?? "UNKNOWN",
                    nbf = payload.claims["nbf"] ?? "UNKNOWN",
                    exp = payload.claims["exp"] ?? "UNKNOWN",
                    iss = payload.claims["iss"] ?? "UNKNOWN",
                    aud = payload.claims["aud"] ?? "UNKNOWN",
                    code = addCode ? code : "REDACTED_FOR_SECURITY::NOT_A_SUPERUSER",
                } : failSafe,
                error = payload.error,
                expiresUtc = payload.expiresUtc
            };
            return Results.Ok(saveCode);
        }
        catch
        {
            return Results.StatusCode(500);
        }
    }
}