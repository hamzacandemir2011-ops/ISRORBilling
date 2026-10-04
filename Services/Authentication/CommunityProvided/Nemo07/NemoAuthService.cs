using ISRORBilling.Database.CommunityProvided.Nemo07;
using ISRORBilling.Models.Authentication;
using ISRORBilling.Models.Enums;

namespace ISRORBilling.Services.Authentication.CommunityProvided.Nemo07;

/// <summary>
/// Simple auth (TB_User only) with VIP and email support. Requires the extra TB_User columns, see the readme.
/// </summary>
public class NemoAuthService : IAuthService
{
    private readonly NemoAccountContext _accountContext;
    private readonly ILogger<NemoAuthService> _logger;

    public NemoAuthService(NemoAccountContext accountContext, ILogger<NemoAuthService> logger)
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
        if (string.IsNullOrEmpty(userId)) return new AUserLoginResponse() {ReturnValue = LoginResponseCodeEnum.Error};
        var user = channel switch
        {
            "1" => _accountContext.Users.FirstOrDefault(user => user.StrUserID == userId ),
            _ => null
        };

        if (user == null) return new AUserLoginResponse() {ReturnValue = LoginResponseCodeEnum.NotFoundUid};

        if (!string.Equals(user.password, userPw, StringComparison.OrdinalIgnoreCase))
            return new AUserLoginResponse() {ReturnValue = LoginResponseCodeEnum.WrongPassword};

        return new AUserLoginResponse
        {
            ReturnValue = LoginResponseCodeEnum.Success,
            JID = user.PortalJID,
            // CurrentDate = null,
            ARCode = null,
            EmailAddr = user.Email,
            EmailCertificationStatus = user.EmailCertificationStatus,
            EmailUniqueStatus = user.EmailUniqueStatus,
            NickName = null,
            VipLevel = user.VIPLv,
            VipExpireTime = user.VipExpireTime,
            VipUserType = (VipUserType)user.VipUserType
        };
    }
}
