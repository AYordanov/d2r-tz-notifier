using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MephistoTzNotifier;

public sealed class EmailSender
{
    private readonly string _apiKey;
    private readonly string _from;
    private readonly string _to;

    public EmailSender(IConfiguration config)
    {
        _apiKey = Required(config, "SENDGRID_API_KEY");
        _from = Required(config, "MEPHISTO_FROM_EMAIL");  // must be a SendGrid-verified sender
        _to = Required(config, "MEPHISTO_TO_EMAIL");
    }

    public async Task SendAsync(EmailContent content, CancellationToken ct)
    {
        var client = new SendGridClient(_apiKey);
        var msg = MailHelper.CreateSingleEmail(
            new EmailAddress(_from),
            new EmailAddress(_to),
            content.Subject,
            content.PlainBody,
            content.HtmlBody);

        var response = await client.SendEmailAsync(msg, ct);

        if ((int)response.StatusCode >= 300)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"SendGrid returned {(int)response.StatusCode}: {body}");
        }
    }

    private static string Required(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException($"Required setting '{key}' is not configured.");
}
