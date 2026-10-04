using ISRORBilling.Models.Authentication;
using ISRORBilling.Models.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ISRORBilling.Services.Security;

/// <summary>
/// Counts failed logins per user id and per player IP, and blocks further attempts once a limit is reached.
/// Only credential failures are counted. They are only returned after the request signature was validated,
/// so a caller without the SaltKey cannot raise the counters for someone else.
/// </summary>
public class LoginThrottle
{
    private static readonly HashSet<LoginResponseCodeEnum> CountedFailures = new()
    {
        LoginResponseCodeEnum.Error,
        LoginResponseCodeEnum.WrongPassword,
        LoginResponseCodeEnum.NotFoundUid,
    };

    private readonly IMemoryCache _cache;
    private readonly LoginThrottleOptions _options;
    private readonly ILogger<LoginThrottle> _logger;

    public LoginThrottle(IMemoryCache cache, IOptions<LoginThrottleOptions> options, ILogger<LoginThrottle> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    private sealed class Counter
    {
        public int Failures;
    }

    private static string UserKey(CheckUserRequest request) => $"login-user:{request.UserId.ToLowerInvariant()}";
    private static string IpKey(CheckUserRequest request) => $"login-ip:{request.UserIp}";

    private int GetFailures(string key) => _cache.TryGetValue<Counter>(key, out var counter) ? Volatile.Read(ref counter!.Failures) : 0;

    /// <returns>The response code to return if this request must be rejected; null if it can be processed.</returns>
    public LoginResponseCodeEnum? Check(CheckUserRequest request)
    {
        if (!_options.Enabled)
            return null;

        if (_options.MaxFailuresPerIp > 0 && GetFailures(IpKey(request)) >= _options.MaxFailuresPerIp)
        {
            _logger.LogWarning("Login blocked: too many failed logins from IP [{UserIp}] (user [{UserId}])", request.UserIp, request.UserId);
            return LoginResponseCodeEnum.BlockedIp;
        }

        if (_options.MaxFailuresPerUser > 0 && GetFailures(UserKey(request)) >= _options.MaxFailuresPerUser)
        {
            _logger.LogWarning("Login blocked: too many failed logins for user [{UserId}] (IP [{UserIp}])", request.UserId, request.UserIp);
            return LoginResponseCodeEnum.BlockedJid;
        }

        return null;
    }

    /// <summary>
    /// Records the outcome of a login. A success resets the user's counter.
    /// </summary>
    public void Record(CheckUserRequest request, LoginResponseCodeEnum result)
    {
        if (!_options.Enabled)
            return;

        if (result == LoginResponseCodeEnum.Success)
        {
            _cache.Remove(UserKey(request));
            return;
        }

        if (!CountedFailures.Contains(result))
            return;

        Increment(UserKey(request));
        Increment(IpKey(request));
    }

    private void Increment(string key)
    {
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, _options.WindowMinutes));
            return new Counter();
        })!;
        Interlocked.Increment(ref counter.Failures);
    }
}
