using System.Security.Cryptography;
using System.Text;

namespace EasyCob.Core.Modules.Messaging;

public static class WhatsAppSignature
{
    public static bool IsValid(string body, string signature, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || !signature.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature[7..]));
        }
        catch (FormatException) { return false; }
    }
}
