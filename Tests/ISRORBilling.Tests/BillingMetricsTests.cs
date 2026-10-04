using ISRORBilling.Models.Authentication;
using ISRORBilling.Services.Metrics;
using Prometheus;

namespace ISRORBilling.Tests;

public class BillingMetricsTests
{
    private static async Task<string> Export(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task AllSeriesExistAtZero()
    {
        var registry = Metrics.NewCustomRegistry();
        _ = new BillingMetrics(registry);

        var text = await Export(registry);

        Assert.Contains("billing_logins_total{result=\"success\"} 0", text);
        Assert.Contains("billing_logins_total{result=\"blocked_user\"} 0", text);
        Assert.Contains("billing_logins_total{result=\"malformed\"} 0", text);
        Assert.Contains("billing_notifications_total{type=\"item_lock\",result=\"failure\"} 0", text);
        Assert.Contains("billing_login_duration_seconds_count 0", text);
    }

    [Fact]
    public async Task RecordsLoginsNotificationsAndDuration()
    {
        var registry = Metrics.NewCustomRegistry();
        var metrics = new BillingMetrics(registry);

        metrics.LoginResult(LoginResponseCodeEnum.Success);
        metrics.LoginResult(LoginResponseCodeEnum.Success);
        metrics.LoginResult(LoginResponseCodeEnum.WrongPassword);
        metrics.LoginResult(LoginResponseCodeEnum.BlockedIp);
        metrics.LoginResult(LoginResponseCodeEnum.ServerMaintenance);
        metrics.LoginMalformed();
        metrics.LoginDuration(TimeSpan.FromMilliseconds(5));
        metrics.Notification(BillingMetrics.NotificationTypes.SecondPassword, true);
        metrics.Notification(BillingMetrics.NotificationTypes.ItemLock, false);
        metrics.NotificationMalformed(BillingMetrics.NotificationTypes.ItemLock);

        var text = await Export(registry);

        Assert.Contains("billing_logins_total{result=\"success\"} 2", text);
        Assert.Contains("billing_logins_total{result=\"wrong_password\"} 1", text);
        Assert.Contains("billing_logins_total{result=\"blocked_ip\"} 1", text);
        Assert.Contains("billing_logins_total{result=\"other\"} 1", text);
        Assert.Contains("billing_logins_total{result=\"malformed\"} 1", text);
        Assert.Contains("billing_login_duration_seconds_count 1", text);
        Assert.Contains("billing_notifications_total{type=\"second_password\",result=\"success\"} 1", text);
        Assert.Contains("billing_notifications_total{type=\"item_lock\",result=\"failure\"} 1", text);
        Assert.Contains("billing_notifications_total{type=\"item_lock\",result=\"malformed\"} 1", text);
    }

    [Theory]
    [InlineData(LoginResponseCodeEnum.Success, "success")]
    [InlineData(LoginResponseCodeEnum.NotFoundUid, "user_not_found")]
    [InlineData(LoginResponseCodeEnum.Error, "error")]
    [InlineData(LoginResponseCodeEnum.BlockedJid, "blocked_user")]
    [InlineData(LoginResponseCodeEnum.ExpiredRequestUrl, "expired_request")]
    [InlineData(LoginResponseCodeEnum.Emergency, "invalid_signature")]
    [InlineData(LoginResponseCodeEnum.BlockedCountry, "other")]
    public void ResultLabels(LoginResponseCodeEnum code, string label)
    {
        Assert.Equal(label, BillingMetrics.ResultLabel(code));
    }
}
