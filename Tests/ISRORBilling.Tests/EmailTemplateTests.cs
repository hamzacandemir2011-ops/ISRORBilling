using ISRORBilling.Models.Notification;
using ISRORBilling.Models.Options;
using ISRORBilling.Services.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using static ISRORBilling.Tests.TestHelpers;

namespace ISRORBilling.Tests;

public class EmailTemplateTests
{
    private static EmailNotificationService CreateService(EmailOptions options) =>
        new(NullLogger<EmailNotificationService>.Instance, Microsoft.Extensions.Options.Options.Create(options));

    private static SendCodeRequest Request(string code = "1234") =>
        new(SignedSendCodeValues(42, code, "player@example.com"), SaltKey);

    [Fact]
    public void DefaultTemplates_KeepThePreviousText()
    {
        var service = CreateService(new EmailOptions { FromFriendlyName = "MyServer" });

        var message = service.BuildMessage(new EmailOptions().Templates.SecondPassword, Request());

        Assert.Equal("[MyServer] New secondary password!", message.Subject);
        Assert.Equal("Your new secondary password for server [MyServer] is [1234]", message.Content);
        Assert.False(message.IsHtml);

        message = service.BuildMessage(new EmailOptions().Templates.ItemLock, Request());
        Assert.Equal("[MyServer] Item lock code!", message.Subject);
        Assert.Equal("Your item lock code for server [MyServer] is [1234]", message.Content);
    }

    [Fact]
    public void CustomTemplate_ReplacesAllPlaceholders()
    {
        var service = CreateService(new EmailOptions { FromFriendlyName = "MyServer" });
        var template = new EmailTemplate
        {
            Subject = "{servername}: kod",
            Body = "Merhaba {Email} (#{Jid}), kodun: {Code}",
        };

        var message = service.BuildMessage(template, Request());

        Assert.Equal("MyServer: kod", message.Subject);
        Assert.Equal("Merhaba player@example.com (#42), kodun: 1234", message.Content);
    }

    [Fact]
    public void HtmlTemplate_EncodesValues()
    {
        var service = CreateService(new EmailOptions { FromFriendlyName = "<My & Server>" });
        var template = new EmailTemplate { Subject = "{ServerName}", Body = "<b>{ServerName}</b> {Code}", IsHtml = true };

        var message = service.BuildMessage(template, Request("<script>"));

        Assert.True(message.IsHtml);
        Assert.Equal("<My & Server>", message.Subject); // subjects are plain text
        Assert.Equal("<b>&lt;My &amp; Server&gt;</b> &lt;script&gt;", message.Content);
    }

    [Fact]
    public void BodyFile_IsUsedAndMissingFileFallsBackToInlineBody()
    {
        var file = Path.Combine(Path.GetTempPath(), $"template-{Guid.NewGuid()}.html");
        File.WriteAllText(file, "<p>{Code}</p>");
        try
        {
            var service = CreateService(new EmailOptions());
            var template = new EmailTemplate { Subject = "s", Body = "inline {Code}", BodyFile = file, IsHtml = true };

            Assert.Equal("<p>1234</p>", service.BuildMessage(template, Request()).Content);

            template.BodyFile = file + ".missing";
            Assert.Equal("inline 1234", service.BuildMessage(template, Request()).Content);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ExampleHtmlTemplates_AreCopiedToTheOutput()
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "EmailTemplates", "second-password.example.html")));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "EmailTemplates", "item-lock.example.html")));
    }

    [Fact]
    public void Configuration_OverridesOnlyWhatIsSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailService:Templates:ItemLock:Subject"] = "Custom subject",
            })
            .Build();

        var options = configuration.GetSection("EmailService").Get<EmailOptions>()!;

        Assert.Equal("Custom subject", options.Templates.ItemLock.Subject);
        Assert.Equal("Your item lock code for server [{ServerName}] is [{Code}]", options.Templates.ItemLock.Body);
        Assert.Equal("[{ServerName}] New secondary password!", options.Templates.SecondPassword.Subject);
    }
}
