using System.Net;
using ISRORBilling.Database;
using ISRORBilling.Models.Authentication;
using ISRORBilling.Services.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using static ISRORBilling.Tests.TestHelpers;

namespace ISRORBilling.Tests;

public class EndpointTests
{
    private const string PortalAgent = "Portal_CGI_Agent";

    /// <summary>
    /// Auth service that always answers with a fixed code, so the endpoint can be tested without a database.
    /// </summary>
    private sealed class FixedAuthService(LoginResponseCodeEnum code) : IAuthService
    {
        public int Calls;

        public AUserLoginResponse Login(CheckUserRequest request)
        {
            Interlocked.Increment(ref Calls);
            return new AUserLoginResponse { ReturnValue = code, JID = 1 };
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configureServices = null,
        bool reachableDatabase = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("SaltKey", SaltKey);
            builder.UseSetting("PortalCGIAgentHeader", PortalAgent);
            builder.UseSetting("AuthService", "Simple");
            builder.UseSetting("NotificationService:Type", "None");
            builder.UseSetting("LoginThrottle:MaxFailuresPerUser", "2");
            builder.UseSetting("LoginThrottle:MaxFailuresPerIp", "0");
            builder.UseSetting("Logging:File:Path", Path.Combine(Path.GetTempPath(), $"billing-test-{Guid.NewGuid()}.txt"));
            builder.UseSetting("DbConfig:AccountDB", "Data Source=127.0.0.1,1;Initial Catalog=X;User ID=sa;Password=1;Connect Timeout=1");
            builder.ConfigureServices(services =>
            {
                // The ping service would bind a real TCP port.
                services.RemoveAll<IHostedService>();

                if (reachableDatabase)
                {
                    services.RemoveAll<DbContextOptions<AccountContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<AccountContext>>();
                    services.AddDbContext<AccountContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
                }

                configureServices?.Invoke(services);
            });
        });

    private static HttpClient CreatePortalClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(PortalAgent);
        return client;
    }

    private static string CheckUserUrl(string user) =>
        $"/Property/Silkroad-r/checkuser.aspx?values={Uri.EscapeDataString(SignedCheckUserValues(user, "pw"))}";

    private static int ReturnCode(string body) => int.Parse(body.Split('|')[0]);

    [Fact]
    public async Task CheckUser_BlocksUserAfterRepeatedFailures()
    {
        var auth = new FixedAuthService(LoginResponseCodeEnum.WrongPassword);
        await using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService>(auth);
        });
        var client = CreatePortalClient(factory);

        Assert.Equal((int)LoginResponseCodeEnum.WrongPassword, ReturnCode(await client.GetStringAsync(CheckUserUrl("user"))));
        Assert.Equal((int)LoginResponseCodeEnum.WrongPassword, ReturnCode(await client.GetStringAsync(CheckUserUrl("user"))));
        Assert.Equal((int)LoginResponseCodeEnum.BlockedJid, ReturnCode(await client.GetStringAsync(CheckUserUrl("user"))));
        Assert.Equal(2, auth.Calls); // the blocked attempt never reached the auth service

        Assert.Equal((int)LoginResponseCodeEnum.WrongPassword, ReturnCode(await client.GetStringAsync(CheckUserUrl("other"))));
    }

    [Fact]
    public async Task CheckUser_MalformedValues_ReturnsErrorCode()
    {
        await using var factory = CreateFactory();
        var client = CreatePortalClient(factory);

        var body = await client.GetStringAsync("/Property/Silkroad-r/checkuser.aspx?values=garbage");

        Assert.Equal((int)LoginResponseCodeEnum.Error, ReturnCode(body));
    }

    [Fact]
    public async Task CheckUser_WrongUserAgent_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var body = await client.GetStringAsync(CheckUserUrl("user"));

        Assert.Equal((int)LoginResponseCodeEnum.BrowserAgentNotMatch, ReturnCode(body));
    }

    [Fact]
    public async Task Health_DatabaseUnreachable_ReturnsUnhealthy()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient(); // no portal User-Agent needed

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_DatabaseReachable_ReturnsHealthy()
    {
        await using var factory = CreateFactory(reachableDatabase: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private static double ReadMetric(string text, string series)
    {
        var line = text.Split('\n').Single(l => l.StartsWith(series + " "));
        return double.Parse(line[(series.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task Metrics_AreExposedWithoutPortalAgent_AndCountLogins()
    {
        var auth = new FixedAuthService(LoginResponseCodeEnum.Success);
        await using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService>(auth);
        });
        var browser = factory.CreateClient();
        var gateway = CreatePortalClient(factory);

        var response = await browser.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var before = await response.Content.ReadAsStringAsync();

        await gateway.GetStringAsync(CheckUserUrl("metrics-user"));
        await gateway.GetStringAsync("/Property/Silkroad-r/checkuser.aspx?values=garbage");

        var after = await browser.GetStringAsync("/metrics");
        const string success = "billing_logins_total{result=\"success\"}";
        const string malformed = "billing_logins_total{result=\"malformed\"}";
        Assert.Equal(ReadMetric(before, success) + 1, ReadMetric(after, success));
        Assert.Equal(ReadMetric(before, malformed) + 1, ReadMetric(after, malformed));
        Assert.Contains("http_requests_received_total", after);
        Assert.Contains("billing_login_duration_seconds_count", after);
    }

    [Fact]
    public async Task Metrics_CanBeDisabled()
    {
        await using var factory = CreateFactory().WithWebHostBuilder(builder => builder.UseSetting("Metrics:Enabled", "false"));
        var client = CreatePortalClient(factory);

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
