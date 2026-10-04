using ISRORBilling.Models.Authentication;

namespace ISRORBilling.Tests;

public class AUserLoginResponseTests
{
    [Fact]
    public void ToString_Uses24HourClock()
    {
        var response = new AUserLoginResponse
        {
            ReturnValue = LoginResponseCodeEnum.Success,
            JID = 7,
            CurrentDate = new DateTime(2024, 5, 6, 15, 4, 5),
            VipExpireTime = new DateTime(2030, 1, 2, 23, 59, 58),
        };

        var parts = response.ToString().Split('|');
        Assert.Equal("0", parts[0]);
        Assert.Equal("7", parts[1]);
        Assert.Equal("2024-05-06 15:04:05", parts[2]);
        Assert.Equal("2030-01-02 23:59:58", parts[7]);
    }
}
