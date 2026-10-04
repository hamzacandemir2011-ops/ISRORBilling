using MimeKit;

namespace ISRORBilling.Models.Notification;

public class EmailMessage
{
    public IEnumerable<MailboxAddress> To { get; }
    public string Subject { get; }
    public string Content { get; }
    public bool IsHtml { get; }
    
    public EmailMessage(IEnumerable<string> to, string subject, string content, bool isHtml = false)
    {
        To = to.Select(x => new MailboxAddress(x, x));
        Subject = subject;
        Content = content;
        IsHtml = isHtml;
    }
    
    public EmailMessage(string to, string subject, string content, bool isHtml = false): this(new []{to}, subject, content, isHtml)
    {
    }
    
    public MimeMessage ToMimeMessage(string senderName, string senderAddress)
    {
        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress(senderName,senderAddress));
        emailMessage.To.AddRange(To);
        emailMessage.Subject = Subject;
        emailMessage.Body = new TextPart(IsHtml ? MimeKit.Text.TextFormat.Html : MimeKit.Text.TextFormat.Text) { Text = Content };
        return emailMessage;
    }

}