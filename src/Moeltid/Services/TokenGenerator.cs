using System.Security.Cryptography;

namespace Moeltid.Services;

public class TokenGenerator
{
    public string RandomUrlSafeString(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "")
            [..length];
    }
}
