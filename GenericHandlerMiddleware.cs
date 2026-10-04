using ISRORBilling.Models.Authentication;

namespace ISRORBilling;

public class GenericHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GenericHandlerMiddleware> _logger;
    private readonly string? _portalCgiAgentHeader;

    public GenericHandlerMiddleware(RequestDelegate next, ILogger<GenericHandlerMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _portalCgiAgentHeader = configuration.GetSection("PortalCGIAgentHeader").Value;

        // Configuration problems are reported once at startup instead of on every request.
        if (string.IsNullOrEmpty(configuration.GetSection("SaltKey").Value))
            _logger.LogWarning(
                "THERE'S NO SALT KEY CONFIGURED IN APPSETTINGS; WE CAN'T VALIDATE IF REQUEST WAS TAMPERED!");

        if (string.IsNullOrEmpty(_portalCgiAgentHeader))
            _logger.LogWarning(
                "THERE'S NO PORTAL AGENT CONFIGURED IN APPSETTINGS; ANY BROWSER CAN BROWSE YOUR BILLING!");
    }

    public async Task Invoke(HttpContext context)
    {
        var response = context.Response;
        var request = context.Request;

        // NOTE: the User-Agent is trivially spoofable, it only filters out casual browsing. The SaltKey is the real protection.
        var userAgentHeader = context.Request.Headers.UserAgent;
        if (!string.IsNullOrEmpty(_portalCgiAgentHeader) &&
            userAgentHeader.All(userAgent => userAgent != _portalCgiAgentHeader))
        {
            _logger.LogCritical(
                "PORTAL AGENT DOES NOT MATCH; SOMEONE TRYING TO BROWSE YOUR BILLING URL FROM A NORMAL BROWSER\nUserAgent: [{UserAgent}] \nMethod: [{RequestMethod}] \nPath: [{RequestPath}]",
                userAgentHeader, request.Method, request.Path);
            await response.WriteAsync(new AUserLoginResponse
                { ReturnValue = LoginResponseCodeEnum.BrowserAgentNotMatch }.ToString());
        }
        else
        {
            await _next(context);
            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
                _logger.LogWarning("Unhandled [{RequestMethod}] request received to: {RequestPath}",
                    request.Method, request.Path);
        }
    }
}
