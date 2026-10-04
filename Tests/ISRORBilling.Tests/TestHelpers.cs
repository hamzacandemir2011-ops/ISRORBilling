using System.Security.Cryptography;
using System.Text;

namespace ISRORBilling.Tests;

internal static class TestHelpers
{
    public const string SaltKey = "test-salt";

    public static string Md5Hex(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(value)));

    public static string SignedCheckUserValues(string userId, string hashedPw, long? timeStamp = null,
        string userIp = "127.0.0.1", short channel = 1, string saltKey = SaltKey, bool lowerCaseToken = false)
    {
        var ts = timeStamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = Md5Hex($"{channel}{userId}{hashedPw}{userIp}{ts}{saltKey}");
        if (lowerCaseToken) token = token.ToLowerInvariant();
        return $"{channel}|{userId}|{hashedPw}|{userIp}|{ts}|{token}";
    }

    public static string SignedSendCodeValues(int jid, string code, string email, string saltKey = SaltKey) =>
        $"{jid}|{code}|{email}|{Md5Hex($"{jid}{code}{email}{saltKey}")}";
}
