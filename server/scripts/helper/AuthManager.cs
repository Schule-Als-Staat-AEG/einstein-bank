using Max35050.Net;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using RestSharp;
using System.IdentityModel.Tokens.Jwt;

namespace Max35050.Auth;

public static class AuthManager
{
    public static async Task<User> GetUserFromToken(string token)
    {
        dynamic result;
        User user;
        try 
        {
            result = await new RestCall($"{Server.databaseURL}/api/collections/users/auth-refresh", token, expectError: true).PostDynamic();
            user = User.FromDynamic(result.record);
        }
        catch
        {
            result = await new RestCall($"{Server.databaseURL}/api/collections/_superusers/auth-refresh", token).PostDynamic();
            result = await new RestCall($"{Server.databaseURL}/api/collections/users/records/{result.record.user}", token).GetDynamic();
            user = User.FromDynamic(result);
        }
        return user;
    }
    public static async Task<bool> IsTokenSuperuser(string token)
    {
        try 
        {
            dynamic result = await new RestCall($"{Server.databaseURL}/api/collections/_superusers/auth-refresh", token, expectError: true).PostDynamic();
            string newToken = result.token; // Try to assign. If token is wrong the field won't exist
            if (string.IsNullOrEmpty(newToken))
                throw new Exception("No token returned");
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string CreateJWT(Dictionary<string, object> claims, TimeSpan? lifetime = null, DateTime? issuedAt = null)
    {
        // Create
        byte[] keyBytes = Convert.FromBase64String(ServerAuth.jwtSecret);
        var securityKey = new SymmetricSecurityKey(keyBytes);

        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var jwtClaims = new List<Claim>();

        foreach (var kv in claims)
        {
            jwtClaims.Add(new Claim(
                kv.Key,
                kv.Value.ToString()!
            ));
        }
        var token = new JwtSecurityToken(
            issuer: "Max35050;Atzock;waldmeister20",
            audience: "Clients",
            claims: jwtClaims,
            expires: lifetime != null ? (issuedAt != null ? issuedAt.Value.Add(lifetime.Value) : DateTime.UtcNow.Add(lifetime.Value)) : (issuedAt != null ? issuedAt.Value.AddYears(1000) : DateTime.UtcNow.AddYears(1000)),
            signingCredentials: creds,
            notBefore: issuedAt ?? DateTime.UtcNow
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    internal static JWTResult DecodeJWT(string token)
    {
        var result = new JWTResult
        {
            valid = false,
            claims = null,
            error = null,
            expiresUtc = null
        };

        try
        {
            var keyBytes = Convert.FromBase64String(ServerAuth.jwtSecret);
            var securityKey = new SymmetricSecurityKey(keyBytes);

            var tokenHandler = new JwtSecurityTokenHandler();

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "Max35050;Atzock;waldmeister20",

                ValidateAudience = true,
                ValidAudience = "Clients",

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(
                token,
                parameters,
                out SecurityToken validatedToken
            );

            var jwtToken = (JwtSecurityToken)validatedToken;

            var claims = new Dictionary<string, object>();
            foreach (var claim in principal.Claims)
            {
                claims[claim.Type] = claim.Value;
            }

            result.valid = true;
            result.claims = claims;
            result.expiresUtc = jwtToken.ValidTo;

            return result;
        }
        catch (SecurityTokenInvalidLifetimeException)
        {
            result.error = "token_expired";
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            result.error = "invalid_signature";
        }
        catch (SecurityTokenException ex)
        {
            result.error = ex.GetType().Name;
        }
        catch (Exception)
        {
            result.error = "invalid_token";
        }

        return result;
    }
}
public class JWTResult
{
    public bool valid;
    public Dictionary<string, object>? claims;
    public string? error;
    public DateTime? expiresUtc;
}
