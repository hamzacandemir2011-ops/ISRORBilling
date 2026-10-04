using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace ISRORBilling.Models.Authentication;

public class CheckUserRequest : GatewayRequest
{
    public short ChannelId { get; }
    public string UserId { get; }
    [JsonIgnore]
    public string HashedUserPassword { get; }
    public string UserIp { get; }
    public long UnixTimeStamp { get; }
    public int ServiceCompany { get; }
    /// <summary>
    /// Unix time (seconds) after which this request must no longer be accepted.
    /// </summary>
    public long ExpiresAtUnixTimeSeconds { get; }

    protected override string CalculatedToken
    {
        get
        {
            using var md5 = MD5.Create();
            return Convert.ToHexString(md5.ComputeHash(
                Encoding.ASCII.GetBytes($"{ChannelId}{UserId}{HashedUserPassword}{UserIp}{UnixTimeStamp}{SaltKey}")));
        }
    }

    /// <summary>
    /// Parses the "values" sent by the GatewayServer: channel|userId|hashedPw[|userIp|unixTimeStamp|token]
    /// </summary>
    /// <exception cref="FormatException">If the values are malformed.</exception>
    public CheckUserRequest(string values, string? saltKey = null, int serviceCompany = 11, int requestTimeout = 60)
    {
        var allValues = values.Split('|');
        if (allValues.Length < 3 || !short.TryParse(allValues[0], out var channelId))
            throw new FormatException("Malformed checkuser request values");

        long unixTimeStamp = 0;
        var rawTimeStamp = allValues.ElementAtOrDefault(4);
        if (rawTimeStamp != null && !long.TryParse(rawTimeStamp, out unixTimeStamp))
            throw new FormatException("Malformed checkuser request timestamp");

        SaltKey = saltKey;
        ChannelId = channelId;
        UserId = allValues[1];
        HashedUserPassword = allValues[2];
        UserIp = allValues.ElementAtOrDefault(3) ?? "0";
        UnixTimeStamp = unixTimeStamp;
        UserProvidedValidationToken = allValues.ElementAtOrDefault(5);
        ServiceCompany = serviceCompany;
        ExpiresAtUnixTimeSeconds = UnixTimeStamp + requestTimeout;
    }

    public static bool TryParse(string? values, string? saltKey, int serviceCompany, int requestTimeout,
        [NotNullWhen(true)] out CheckUserRequest? request)
    {
        request = null;
        if (string.IsNullOrEmpty(values))
            return false;

        try
        {
            request = new CheckUserRequest(values, saltKey, serviceCompany, requestTimeout);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool IsExpired() => IsExpired(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public bool IsExpired(long nowUnixTimeSeconds) => ExpiresAtUnixTimeSeconds < nowUnixTimeSeconds;
}
