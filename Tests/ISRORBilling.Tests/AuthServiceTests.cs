using ISRORBilling.Database;
using ISRORBilling.Database.CommunityProvided.Nemo07;
using ISRORBilling.Models.Authentication;
using ISRORBilling.Services.Authentication;
using ISRORBilling.Services.Authentication.CommunityProvided.Nemo07;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static ISRORBilling.Tests.TestHelpers;

namespace ISRORBilling.Tests;

public class AuthServiceTests
{
    private static AccountContext CreateAccountContext()
    {
        var context = new AccountContext(new DbContextOptionsBuilder<AccountContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.Add(new User { JID = 1, PortalJID = 100, StrUserID = "user", password = "pwhash" });
        context.SaveChanges();
        return context;
    }

    private static NemoAccountContext CreateNemoContext()
    {
        var context = new NemoAccountContext(new DbContextOptionsBuilder<NemoAccountContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.Add(new NemoUser
        {
            JID = 1, PortalJID = 100, StrUserID = "user", password = "pwhash", Email = "a@b.c", VIPLv = 3,
            VipExpireTime = new DateTime(2030, 1, 1, 15, 0, 0), VipUserType = 0
        });
        context.SaveChanges();
        return context;
    }

    private static CheckUserRequest Request(string user, string pw, long? ts = null) =>
        new(SignedCheckUserValues(user, pw, ts), SaltKey, 11, 60);

    [Fact]
    public void Simple_Success()
    {
        var service = new SimpleAuthService(CreateAccountContext(), NullLogger<SimpleAuthService>.Instance);
        var response = service.Login(Request("user", "pwhash"));
        Assert.Equal(LoginResponseCodeEnum.Success, response.ReturnValue);
        Assert.Equal(100, response.JID);
    }

    [Fact]
    public void Simple_WrongPassword_Fails()
    {
        var service = new SimpleAuthService(CreateAccountContext(), NullLogger<SimpleAuthService>.Instance);
        Assert.NotEqual(LoginResponseCodeEnum.Success, service.Login(Request("user", "wrong")).ReturnValue);
    }

    [Fact]
    public void Simple_ExpiredRequest_IsRejected()
    {
        var service = new SimpleAuthService(CreateAccountContext(), NullLogger<SimpleAuthService>.Instance);
        var response = service.Login(Request("user", "pwhash", ts: 1000));
        Assert.Equal(LoginResponseCodeEnum.ExpiredRequestUrl, response.ReturnValue);
    }

    [Fact]
    public void Simple_InvalidSignature_IsRejected()
    {
        var service = new SimpleAuthService(CreateAccountContext(), NullLogger<SimpleAuthService>.Instance);
        var request = new CheckUserRequest(SignedCheckUserValues("user", "pwhash"), "other-salt", 11, 60);
        Assert.Equal(LoginResponseCodeEnum.Emergency, service.Login(request).ReturnValue);
    }

    [Fact]
    public void Nemo_Success_ReturnsVipData()
    {
        var service = new NemoAuthService(CreateNemoContext(), NullLogger<NemoAuthService>.Instance);
        var response = ((IAuthService)service).Login(Request("user", "pwhash"));
        Assert.Equal(LoginResponseCodeEnum.Success, response.ReturnValue);
        Assert.Equal(100, response.JID);
        Assert.Equal(3, response.VipLevel);
        Assert.Equal("a@b.c", response.EmailAddr);
    }

    [Fact]
    public void Nemo_WrongPassword_IsRejected()
    {
        var service = new NemoAuthService(CreateNemoContext(), NullLogger<NemoAuthService>.Instance);
        var response = ((IAuthService)service).Login(Request("user", "wrong"));
        Assert.Equal(LoginResponseCodeEnum.WrongPassword, response.ReturnValue);
    }

    [Fact]
    public void Nemo_UnknownUser_IsRejected()
    {
        var service = new NemoAuthService(CreateNemoContext(), NullLogger<NemoAuthService>.Instance);
        var response = ((IAuthService)service).Login(Request("nobody", "pwhash"));
        Assert.Equal(LoginResponseCodeEnum.NotFoundUid, response.ReturnValue);
    }

    [Fact]
    public void Nemo_InvalidSignature_IsRejected()
    {
        var service = new NemoAuthService(CreateNemoContext(), NullLogger<NemoAuthService>.Instance);
        var request = new CheckUserRequest(SignedCheckUserValues("user", "pwhash"), "other-salt", 11, 60);
        Assert.Equal(LoginResponseCodeEnum.Emergency, ((IAuthService)service).Login(request).ReturnValue);
    }
}
