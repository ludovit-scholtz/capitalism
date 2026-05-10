using System.Security.Cryptography;
using System.Text;

namespace MasterApi.Utilities;

public static class ShardKeyProtector
{
    public static string ComputeHash(string value)
    {
        var normalized = value.Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    public static string Mask(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
        {
            return "****";
        }

        return $"{trimmed[..4]}****{trimmed[^4..]}";
    }
}
