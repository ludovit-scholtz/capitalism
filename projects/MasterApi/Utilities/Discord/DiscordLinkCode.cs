using System.Security.Cryptography;

namespace MasterApi.Utilities.Discord;

/// <summary>
/// Generates short, single-use, human-typable codes used to link a Discord account to a master
/// account. Ambiguous characters (0/O, 1/I) are omitted to reduce transcription errors.
/// </summary>
public static class DiscordLinkCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 8;

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
