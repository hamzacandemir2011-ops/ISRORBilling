namespace ISRORBilling.Models.Options;

/// <summary>
/// Brute-force protection for logins. Failed logins are counted per user id and per player IP
/// (the IP the GatewayServer sends inside the signed request, not the HTTP caller, which is always the GatewayServer).
/// </summary>
public class LoginThrottleOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Failures are counted in fixed windows of this length; the counter resets when the window ends.
    /// </summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// Failed logins allowed for one user id per window. 0 disables the per-user limit.
    /// </summary>
    public int MaxFailuresPerUser { get; set; } = 10;

    /// <summary>
    /// Failed logins allowed from one player IP per window. 0 disables the per-IP limit.
    /// </summary>
    public int MaxFailuresPerIp { get; set; } = 30;
}
