using ISRORBilling.Models.Authentication;

namespace ISRORBilling.Services.Authentication;

/// <summary>
/// Checks shared by every real auth service: the request must not be expired and must be signed with the SaltKey.
/// </summary>
public static class CheckUserRequestGuard
{
    /// <returns>null if the request can be processed; otherwise the response code to return.</returns>
    public static LoginResponseCodeEnum? Check(CheckUserRequest request, ILogger logger)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (request.IsExpired(now))
        {
            logger.LogCritical("Request Login URL is expired [Error Code: {ErrorCode}]\nDetails: Request expiration UnixTimeStamp({ExpiresAt}) < Now UnixTimeStamp({Now})",
                (int)LoginResponseCodeEnum.ExpiredRequestUrl, request.ExpiresAtUnixTimeSeconds, now);
            return LoginResponseCodeEnum.ExpiredRequestUrl;
        }

        if (!request.Validate())
        {
            logger.LogCritical("Couldn't validate if request was legitimate. Ensure the SaltKey matches the one in GatewayServer. [Error Code: {ErrorCode}]\nDetails:{Request}",
                (int)LoginResponseCodeEnum.Emergency, request);
            return LoginResponseCodeEnum.Emergency;
        }

        return null;
    }
}
