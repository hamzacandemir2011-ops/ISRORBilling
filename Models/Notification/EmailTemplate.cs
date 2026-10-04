using System.Net;

namespace ISRORBilling.Models.Notification;

/// <summary>
/// A configurable email. Subject and body support the placeholders {ServerName}, {Code}, {Email} and {Jid}.
/// </summary>
public class EmailTemplate
{
    public string Subject { get; set; } = "";

    /// <summary>
    /// Body text, used when <see cref="BodyFile"/> is not set or can't be read.
    /// </summary>
    public string Body { get; set; } = "";

    /// <summary>
    /// Optional path (absolute or relative to the billing folder) of a file with the body, e.g. an HTML template.
    /// It is read on every email, so it can be edited without restarting.
    /// </summary>
    public string? BodyFile { get; set; }

    /// <summary>
    /// Send the body as HTML. Placeholder values are HTML-encoded.
    /// </summary>
    public bool IsHtml { get; set; }

    public static string Render(string text, IReadOnlyDictionary<string, string> values, bool htmlEncode)
    {
        foreach (var (key, value) in values)
            text = text.Replace($"{{{key}}}", htmlEncode ? WebUtility.HtmlEncode(value) : value, StringComparison.OrdinalIgnoreCase);
        return text;
    }
}

public class EmailTemplates
{
    public EmailTemplate SecondPassword { get; set; } = new()
    {
        Subject = "[{ServerName}] New secondary password!",
        Body = "Your new secondary password for server [{ServerName}] is [{Code}]",
    };

    public EmailTemplate ItemLock { get; set; } = new()
    {
        Subject = "[{ServerName}] Item lock code!",
        Body = "Your item lock code for server [{ServerName}] is [{Code}]",
    };
}
