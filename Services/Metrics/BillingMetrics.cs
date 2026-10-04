using ISRORBilling.Models.Authentication;
using Prometheus;

namespace ISRORBilling.Services.Metrics;

/// <summary>
/// Prometheus metrics for the billing, exposed on <see cref="Path"/>.
/// </summary>
public class BillingMetrics
{
    public const string Path = "/metrics";

    public static class NotificationTypes
    {
        public const string SecondPassword = "second_password";
        public const string ItemLock = "item_lock";
    }

    private static readonly string[] LoginResults =
    [
        "success", "wrong_password", "user_not_found", "error", "blocked_user", "blocked_ip",
        "expired_request", "invalid_signature", "malformed", "other",
    ];

    private readonly Counter _logins;
    private readonly Histogram _loginDuration;
    private readonly Counter _notifications;

    public BillingMetrics(CollectorRegistry registry)
    {
        var factory = Prometheus.Metrics.WithCustomRegistry(registry);

        _logins = factory.CreateCounter("billing_logins_total",
            "Login requests from the GatewayServer, by result.",
            new CounterConfiguration { LabelNames = ["result"] });

        _loginDuration = factory.CreateHistogram("billing_login_duration_seconds",
            "Time spent checking a login (auth service + database).",
            new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) }); // 1 ms .. ~8 s

        _notifications = factory.CreateCounter("billing_notifications_total",
            "Second password / item lock requests, by type and result.",
            new CounterConfiguration { LabelNames = ["type", "result"] });

        // Create every series up front so they show as 0 instead of missing until the first event.
        foreach (var result in LoginResults)
            _logins.WithLabels(result);
        foreach (var type in new[] { NotificationTypes.SecondPassword, NotificationTypes.ItemLock })
        foreach (var result in new[] { "success", "failure", "malformed" })
            _notifications.WithLabels(type, result);
    }

    public static string ResultLabel(LoginResponseCodeEnum code) => code switch
    {
        LoginResponseCodeEnum.Success => "success",
        LoginResponseCodeEnum.WrongPassword => "wrong_password",
        LoginResponseCodeEnum.NotFoundUid => "user_not_found",
        LoginResponseCodeEnum.Error => "error",
        LoginResponseCodeEnum.BlockedJid => "blocked_user",
        LoginResponseCodeEnum.BlockedIp => "blocked_ip",
        LoginResponseCodeEnum.ExpiredRequestUrl => "expired_request",
        LoginResponseCodeEnum.Emergency => "invalid_signature",
        _ => "other",
    };

    public void LoginResult(LoginResponseCodeEnum code) => _logins.WithLabels(ResultLabel(code)).Inc();

    public void LoginMalformed() => _logins.WithLabels("malformed").Inc();

    public void LoginDuration(TimeSpan duration) => _loginDuration.Observe(duration.TotalSeconds);

    public void Notification(string type, bool success) =>
        _notifications.WithLabels(type, success ? "success" : "failure").Inc();

    public void NotificationMalformed(string type) => _notifications.WithLabels(type, "malformed").Inc();
}
