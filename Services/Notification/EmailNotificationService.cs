using ISRORBilling.Models.Authentication;
using ISRORBilling.Models.Notification;
using ISRORBilling.Models.Options;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ISRORBilling.Services.Notification;

/// <summary>
/// If using GMAIL, follow https://code-maze.com/aspnetcore-send-email/ , under "How to Enable Less Secure Apps with Gmail"
/// </summary>
public class EmailNotificationService : INotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly EmailOptions _emailOptions;

    public EmailNotificationService(ILogger<EmailNotificationService> logger, IOptions<EmailOptions> emailOptions)
    {
        _logger = logger;
        _emailOptions = emailOptions.Value;
    }
    
    /// <summary>
    /// Sends the mail to the user
    /// </summary>
    /// <param name="mailMessage"></param>
    /// <returns>True if the sending worked; False if the sending failed.</returns>
    private async Task<bool> SendEmailAsync(MimeMessage mailMessage)
    {
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_emailOptions.SmtpServer, _emailOptions.Port, _emailOptions.UseSSL);
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            await client.AuthenticateAsync(_emailOptions.UserName, _emailOptions.Password);
            await client.SendAsync(mailMessage);
            await client.DisconnectAsync(true);
            return true;
        }
        catch(Exception e)
        {
            _logger.LogError(e, "Failed to send the email to [{To}] with subject [{Subject}]", mailMessage.To, mailMessage.Subject);
            return false;
        }
    }

    /// <summary>
    /// Fills the template placeholders for this request. Falls back to the inline body if the body file can't be read.
    /// </summary>
    public EmailMessage BuildMessage(EmailTemplate template, SendCodeRequest request)
    {
        var values = new Dictionary<string, string>
        {
            ["ServerName"] = _emailOptions.FromFriendlyName,
            ["Code"] = request.code,
            ["Email"] = request.email,
            ["Jid"] = request.jid.ToString(),
        };

        var body = template.Body;
        if (!string.IsNullOrWhiteSpace(template.BodyFile))
        {
            var path = Path.IsPathRooted(template.BodyFile)
                ? template.BodyFile
                : Path.Combine(AppContext.BaseDirectory, template.BodyFile);
            try
            {
                body = File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(e, "Couldn't read email body file [{BodyFile}], using the inline body instead", path);
            }
        }

        return new EmailMessage(request.email,
            EmailTemplate.Render(template.Subject, values, htmlEncode: false),
            EmailTemplate.Render(body, values, template.IsHtml),
            template.IsHtml);
    }

    private bool IsRequestValid(SendCodeRequest request)
    {
        if (_emailOptions.SkipTokenValidation || request.Validate())
            return true;

        _logger.LogCritical("Couldn't validate if request was legitimate. Ensure the SaltKey matches the one in GatewayServer. [Error Code: {ErrorCode}]\nDetails:{Request}", (int)LoginResponseCodeEnum.Emergency, request);
        return false;
    }

    public async Task<bool> SendSecondPassword(SendCodeRequest request)
    {
        if (!IsRequestValid(request))
            return false;
        
        var mimeMessage = BuildMessage(_emailOptions.Templates.SecondPassword, request)
            .ToMimeMessage(_emailOptions.FromFriendlyName, _emailOptions.From);
        
        if(await SendEmailAsync(mimeMessage)) 
            return true;
        
        _logger.LogError("Sending second password by email has Failed for [{StrEmail}]", request.email);
        return false;
    }

    public async Task<bool> SendItemLockCode(SendCodeRequest request)
    {
        if (!IsRequestValid(request))
            return false;

        var mimeMessage = BuildMessage(_emailOptions.Templates.ItemLock, request)
            .ToMimeMessage(_emailOptions.FromFriendlyName, _emailOptions.From);
        
        if(await SendEmailAsync(mimeMessage)) 
            return true;
        
        _logger.LogError("Sending Item Lock Key by email has failed for [{RequestEmail}]", request.email);
        return false;
    }
}
