using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace ISRORBilling.Models.Notification;

/// <summary>
/// Exactly the same format seems to be used for both Item Lock and Sending Secondary Password by email.
/// </summary>
public class SendCodeRequest : GatewayRequest
{
    public int jid { get; }
    [JsonIgnore]
    public string code { get; }
    public string email { get; }

    protected override string CalculatedToken
    {
        get
        {
            using var md5 = MD5.Create();
            return Convert.ToHexString(md5.ComputeHash(
                Encoding.ASCII.GetBytes($"{jid}{code}{email}{SaltKey}")));
        }
    }

    /// <summary>
    /// Parses the "values" sent by the GatewayServer: jid|code|email[|token]
    /// </summary>
    /// <exception cref="FormatException">If the values are malformed.</exception>
    public SendCodeRequest(string values, string? saltKey = null)
    {
        var allValues = values.Split('|');
        if (allValues.Length < 3 || !int.TryParse(allValues[0], out var parsedJid))
            throw new FormatException("Malformed send code request values");

        SaltKey = saltKey;
        jid = parsedJid;
        code = allValues[1];
        email = allValues[2];
        UserProvidedValidationToken = allValues.ElementAtOrDefault(3);
    }

    public static bool TryParse(string? values, string? saltKey, [NotNullWhen(true)] out SendCodeRequest? request)
    {
        request = null;
        if (string.IsNullOrEmpty(values))
            return false;

        try
        {
            request = new SendCodeRequest(values, saltKey);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

}
