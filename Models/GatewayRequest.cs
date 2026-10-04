using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ISRORBilling.Models;

public abstract class GatewayRequest
{
    /// <summary>
    /// This property should return the calculated token, which should match the received one on the request (UserProvidedValidationToken)
    /// </summary>
    protected abstract string CalculatedToken { get; }
    protected string? SaltKey { get; init; }
    protected string? UserProvidedValidationToken { get; init; }


    /// <summary>
    /// Compares the provided ValidationToken on the request with our own generated token. If no saltKey was provided, defaults to false.
    /// The comparison is case-insensitive (hex) and runs in constant time.
    /// </summary>
    /// <returns></returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(UserProvidedValidationToken) || string.IsNullOrEmpty(SaltKey))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(CalculatedToken.ToUpperInvariant()),
            Encoding.ASCII.GetBytes(UserProvidedValidationToken.ToUpperInvariant()));
    }

    /// <summary>
    /// Serializes the public properties of the concrete request. Secrets must be marked with [JsonIgnore].
    /// </summary>
    public override string ToString() => JsonSerializer.Serialize(this, GetType());

}
