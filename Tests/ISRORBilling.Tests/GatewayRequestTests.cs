using ISRORBilling.Models.Authentication;
using ISRORBilling.Models.Notification;
using static ISRORBilling.Tests.TestHelpers;

namespace ISRORBilling.Tests;

public class GatewayRequestTests
{
    [Fact]
    public void CheckUserRequest_ParsesAllFields()
    {
        var values = SignedCheckUserValues("user", "pwhash", timeStamp: 1700000000);

        Assert.True(CheckUserRequest.TryParse(values, SaltKey, 11, 60, out var request));
        Assert.Equal(1, request.ChannelId);
        Assert.Equal("user", request.UserId);
        Assert.Equal("pwhash", request.HashedUserPassword);
        Assert.Equal("127.0.0.1", request.UserIp);
        Assert.Equal(1700000000, request.UnixTimeStamp);
        Assert.Equal(11, request.ServiceCompany);
        Assert.Equal(1700000060, request.ExpiresAtUnixTimeSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("x|user|pw")]
    [InlineData("1|user")]
    [InlineData("1|user|pw|127.0.0.1|notanumber")]
    [InlineData("99999999|user|pw")]
    public void CheckUserRequest_TryParse_RejectsMalformedValues(string values)
    {
        Assert.False(CheckUserRequest.TryParse(values, SaltKey, 11, 60, out _));
    }

    [Fact]
    public void CheckUserRequest_TimestampAfter2038_IsSupported()
    {
        var values = SignedCheckUserValues("user", "pw", timeStamp: 3000000000);
        Assert.True(CheckUserRequest.TryParse(values, SaltKey, 11, 60, out var request));
        Assert.True(request.Validate());
    }

    [Fact]
    public void Validate_AcceptsCorrectSignature_UpperAndLowerCase()
    {
        Assert.True(new CheckUserRequest(SignedCheckUserValues("user", "pw"), SaltKey).Validate());
        Assert.True(new CheckUserRequest(SignedCheckUserValues("user", "pw", lowerCaseToken: true), SaltKey).Validate());
    }

    [Fact]
    public void Validate_RejectsWrongSaltMissingSaltOrMissingToken()
    {
        var values = SignedCheckUserValues("user", "pw");
        Assert.False(new CheckUserRequest(values, "other-salt").Validate());
        Assert.False(new CheckUserRequest(values, null).Validate());
        Assert.False(new CheckUserRequest("1|user|pw", SaltKey).Validate());
    }

    [Fact]
    public void Validate_RejectsTamperedPassword()
    {
        var values = SignedCheckUserValues("user", "pw").Replace("|pw|", "|other|");
        Assert.False(new CheckUserRequest(values, SaltKey).Validate());
    }

    [Fact]
    public void IsExpired_UsesTimestampPlusTimeout()
    {
        var request = new CheckUserRequest(SignedCheckUserValues("user", "pw", timeStamp: 1000), SaltKey, requestTimeout: 60);
        Assert.False(request.IsExpired(1060));
        Assert.True(request.IsExpired(1061));
    }

    [Fact]
    public void ToString_DoesNotLeakSecrets()
    {
        var checkUser = new CheckUserRequest(SignedCheckUserValues("user", "secretpwhash"), SaltKey);
        var text = checkUser.ToString();
        Assert.Contains("user", text);
        Assert.DoesNotContain("secretpwhash", text);

        var sendCode = new SendCodeRequest(SignedSendCodeValues(5, "secretcode", "a@b.c"), SaltKey);
        text = sendCode.ToString();
        Assert.Contains("a@b.c", text);
        Assert.DoesNotContain("secretcode", text);
    }

    [Fact]
    public void SendCodeRequest_ParsesAndValidates()
    {
        Assert.True(SendCodeRequest.TryParse(SignedSendCodeValues(42, "1234", "a@b.c"), SaltKey, out var request));
        Assert.Equal(42, request.jid);
        Assert.Equal("1234", request.code);
        Assert.Equal("a@b.c", request.email);
        Assert.True(request.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("x|1234|a@b.c")]
    [InlineData("42|1234")]
    public void SendCodeRequest_TryParse_RejectsMalformedValues(string values)
    {
        Assert.False(SendCodeRequest.TryParse(values, SaltKey, out _));
    }
}
