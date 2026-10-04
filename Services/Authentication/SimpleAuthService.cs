using ISRORBilling.Database;
using ISRORBilling.Models.Authentication;

namespace ISRORBilling.Services.Authentication;

public class SimpleAuthService : IAuthService
{
    private readonly AccountContext _accountContext;
    private readonly ILogger<SimpleAuthService> _logger;

    public SimpleAuthService(AccountContext accountContext, ILogger<SimpleAuthService> logger)
    {
        _accountContext = accountContext;
        _logger = logger;
    }

    public AUserLoginResponse Login(CheckUserRequest request)
    {
        if (CheckUserRequestGuard.Check(request, _logger) is { } errorCode)
            return new AUserLoginResponse { ReturnValue = errorCode };

        return Login(request.UserId, request.HashedUserPassword, request.ChannelId.ToString());
    }
    public AUserLoginResponse Login(string userId, string userPw, string channel)
    {
        if (string.IsNullOrEmpty(userId)) 
            return new AUserLoginResponse() {ReturnValue = LoginResponseCodeEnum.Error};
        
        var user = channel switch
        {
            "1" => _accountContext.Users.FirstOrDefault(user => user.StrUserID == userId && user.password == userPw),
            // "2" => _accountContext.Users.FirstOrDefault(user => user.StrEmail == userId && user.passwordSha256 == userPw),
            _ => null
        };

        if (user == null) return new AUserLoginResponse() {ReturnValue = LoginResponseCodeEnum.Error};

        return new AUserLoginResponse
        {
            ReturnValue = LoginResponseCodeEnum.Success,
            JID = user.PortalJID,
            // CurrentDate = null,
            ARCode = null,
            EmailCertificationStatus = null,
            EmailUniqueStatus = null,
            NickName = null,
            VipLevel = null,
            VipExpireTime = null,
            VipUserType = null
        };
    }
}