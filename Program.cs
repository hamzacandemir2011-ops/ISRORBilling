using ISRORBilling;
using ISRORBilling.Database;
using ISRORBilling.Database.CommunityProvided.Nemo07;
using ISRORBilling.Models.Authentication;
using ISRORBilling.Models.Notification;
using ISRORBilling.Models.Options;
using ISRORBilling.Models.Ping;
using ISRORBilling.Services.Authentication;
using ISRORBilling.Services.Authentication.CommunityProvided.Nemo07;
using ISRORBilling.Services.Notification;
using ISRORBilling.Services.Notification.CommunityProvided;
using ISRORBilling.Services.Ping;
using ISRORBilling.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NReco.Logging.File;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLogging(loggingBuilder => {
    var loggingSection = builder.Configuration.GetSection("Logging");
    loggingBuilder.AddFile(loggingSection);
});
builder.Services.AddDbContext<AccountContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetSection("DbConfig")["AccountDB"]);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

builder.Services.AddDbContext<JoymaxPortalContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetSection("DbConfig")["JoymaxPortalDB"]);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

builder.Services.AddMemoryCache();
builder.Services.Configure<LoginThrottleOptions>(builder.Configuration.GetSection("LoginThrottle"));
builder.Services.AddSingleton<LoginThrottle>();

builder.Services.Configure<NationPingServiceOptions>(builder.Configuration.GetSection("NationPingService"));
builder.Services.AddHostedService<NationPingService>();

Enum.TryParse(builder.Configuration.GetSection("NotificationService:Type")?.Value, true, out NotificationServiceType notificationServiceType);
switch (notificationServiceType)
{
    case NotificationServiceType.Email:
        builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("EmailService"));
        builder.Services.AddSingleton<INotificationService, EmailNotificationService>();
        break;
    
    case NotificationServiceType.Ferre:
        builder.Services.AddScoped<INotificationService, FerreNotificationService>(); // Scoped: it depends on AccountContext
        break;
    
    case NotificationServiceType.None:
    default:
        builder.Services.AddSingleton<INotificationService, NoneNotificationService>();
        break;
}

Enum.TryParse(builder.Configuration.GetSection("AuthService")?.Value, true, out SupportedLoginServicesEnum loginService);
switch (loginService)
{
    case SupportedLoginServicesEnum.Full:
        builder.Services.AddScoped<IAuthService, FullAuthService>();
        break;
    case SupportedLoginServicesEnum.Bypass:
        builder.Services.AddScoped<IAuthService, BypassAuthService>();
        break;
    case SupportedLoginServicesEnum.Nemo:
        builder.Services.AddDbContext<NemoAccountContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetSection("DbConfig")["AccountDB"]);
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
        builder.Services.AddScoped<IAuthService, NemoAuthService>();
        break;

    case SupportedLoginServicesEnum.Simple:
    default:
        builder.Services.AddScoped<IAuthService, SimpleAuthService>();
        break;
}

// /health reports Healthy only if the databases used by the selected services are reachable.
var healthChecks = builder.Services.AddHealthChecks().AddDbContextCheck<AccountContext>("AccountDB");
if (loginService == SupportedLoginServicesEnum.Full)
    healthChecks.AddDbContextCheck<JoymaxPortalContext>("JoymaxPortalDB");
if (loginService == SupportedLoginServicesEnum.Nemo)
    healthChecks.AddDbContextCheck<NemoAccountContext>("NemoAccountDB");

var serviceCompany = int.Parse(builder.Configuration.GetSection("ServiceCompany").Value ?? "1");
var requestTimeoutSeconds = int.Parse(builder.Configuration.GetSection("RequestTimeoutSeconds").Value ?? "3600");
var saltKey = builder.Configuration.GetSection("SaltKey").Value ?? string.Empty;
var app = builder.Build();

if (loginService == SupportedLoginServicesEnum.Bypass)
    app.Logger.LogCritical("AuthService is set to Bypass: passwords are NOT checked. Never use this in production!");

app.MapGet("/Property/Silkroad-r/checkuser.aspx",
    ([FromQuery] string values, [FromServices] ILogger<Program> logger, [FromServices] IAuthService authService,
        [FromServices] LoginThrottle loginThrottle) =>
    {
        if (!CheckUserRequest.TryParse(values, saltKey, serviceCompany, requestTimeoutSeconds, out var request))
        {
            logger.LogWarning("Received malformed checkuser request ({Length} chars)", values.Length);
            return new AUserLoginResponse { ReturnValue = LoginResponseCodeEnum.Error }.ToString();
        }

        logger.LogDebug("Received checkuser request: {Request}", request);
        if (loginThrottle.Check(request) is { } blockedCode)
            return new AUserLoginResponse { ReturnValue = blockedCode }.ToString();

        var response = authService.Login(request);
        loginThrottle.Record(request, response.ReturnValue);
        return response.ToString();
    });

app.MapGet("/cgi/EmailPassword.asp",
    async ([FromQuery] string values, [FromServices] ILogger<Program> logger, [FromServices] INotificationService notificationService) =>
    {
        if (!SendCodeRequest.TryParse(values, saltKey, out var request))
        {
            logger.LogWarning("Received malformed EmailPassword request ({Length} chars)", values.Length);
            return -1;
        }

        logger.LogDebug("Received EmailPassword request: {Request}", request);
        if (await notificationService.SendSecondPassword(request))
            return 0;

        return -1;
    });

app.MapGet("/cgi/Email_Certification.asp",
    async ([FromQuery] string values, [FromServices] ILogger<Program> logger, [FromServices] INotificationService notificationService) =>
    {
        if (!SendCodeRequest.TryParse(values, saltKey, out var request))
        {
            logger.LogWarning("Received malformed Email_Certification request ({Length} chars)", values.Length);
            return -1;
        }

        logger.LogDebug("Received Email_Certification request: {Request}", request);
        if (await notificationService.SendItemLockCode(request))
            return 0;

        return -1;
    });

app.MapHealthChecks(GenericHandlerMiddleware.HealthPath);

app.UseMiddleware<GenericHandlerMiddleware>(); //Useful to log incoming unknown requests

app.Run();

public partial class Program; // Lets the test project start the app with WebApplicationFactory.
