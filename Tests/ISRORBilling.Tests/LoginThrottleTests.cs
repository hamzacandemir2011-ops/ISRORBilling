using ISRORBilling.Models.Authentication;
using ISRORBilling.Models.Options;
using ISRORBilling.Services.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using static ISRORBilling.Tests.TestHelpers;

namespace ISRORBilling.Tests;

public class LoginThrottleTests
{
    private static LoginThrottle Create(LoginThrottleOptions options) =>
        new(new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<LoginThrottle>.Instance);

    private static CheckUserRequest Request(string user, string ip = "1.2.3.4") =>
        new(SignedCheckUserValues(user, "pw", userIp: ip), SaltKey);

    [Fact]
    public void BlocksUserAfterMaxFailures()
    {
        var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUser = 3, MaxFailuresPerIp = 0 });

        for (var i = 0; i < 3; i++)
        {
            Assert.Null(throttle.Check(Request("user")));
            throttle.Record(Request("user"), LoginResponseCodeEnum.WrongPassword);
        }

        Assert.Equal(LoginResponseCodeEnum.BlockedJid, throttle.Check(Request("user")));
        Assert.Equal(LoginResponseCodeEnum.BlockedJid, throttle.Check(Request("USER", "9.9.9.9")));
        Assert.Null(throttle.Check(Request("other")));
    }

    [Fact]
    public void BlocksIpAfterMaxFailuresAcrossUsers()
    {
        var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUser = 0, MaxFailuresPerIp = 3 });

        throttle.Record(Request("a"), LoginResponseCodeEnum.NotFoundUid);
        throttle.Record(Request("b"), LoginResponseCodeEnum.Error);
        Assert.Null(throttle.Check(Request("c")));
        throttle.Record(Request("c"), LoginResponseCodeEnum.WrongPassword);

        Assert.Equal(LoginResponseCodeEnum.BlockedIp, throttle.Check(Request("d")));
        Assert.Null(throttle.Check(Request("d", "5.6.7.8")));
    }

    [Fact]
    public void SuccessResetsUserCounter()
    {
        var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUser = 2, MaxFailuresPerIp = 0 });

        throttle.Record(Request("user"), LoginResponseCodeEnum.WrongPassword);
        throttle.Record(Request("user"), LoginResponseCodeEnum.Success);
        throttle.Record(Request("user"), LoginResponseCodeEnum.WrongPassword);

        Assert.Null(throttle.Check(Request("user")));
    }

    [Theory]
    [InlineData(LoginResponseCodeEnum.Emergency)]
    [InlineData(LoginResponseCodeEnum.ExpiredRequestUrl)]
    [InlineData(LoginResponseCodeEnum.ServerMaintenance)]
    public void NonCredentialFailures_AreNotCounted(LoginResponseCodeEnum code)
    {
        var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUser = 1, MaxFailuresPerIp = 1 });

        throttle.Record(Request("user"), code);

        Assert.Null(throttle.Check(Request("user")));
    }

    [Fact]
    public void Disabled_NeverBlocks()
    {
        var throttle = Create(new LoginThrottleOptions { Enabled = false, MaxFailuresPerUser = 1, MaxFailuresPerIp = 1 });

        throttle.Record(Request("user"), LoginResponseCodeEnum.WrongPassword);

        Assert.Null(throttle.Check(Request("user")));
    }
}
