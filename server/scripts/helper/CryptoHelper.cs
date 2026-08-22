using System.Security.Cryptography;

namespace Max35050.Helper;

public static class CryptoHelper
{
    public static RSA pubRsa { get; set; } = null!;
    public static RSA prvRsa { get; set; } = null!;
}