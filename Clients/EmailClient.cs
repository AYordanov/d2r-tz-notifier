using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace TerrorZoneNotifier;

public sealed class EmailClient
{
    private readonly string _apiKey;
    private readonly string _from;
    private readonly string _to;
    private readonly string _templateId;

    public EmailClient(IConfiguration config)
    {
        _apiKey = Required(config, "SENDGRID_API_KEY");
        _from = Required(config, "MEPHISTO_FROM_EMAIL");  // must be a SendGrid-verified sender
        _to = Required(config, "MEPHISTO_TO_EMAIL");
        _templateId = Required(config, "SENDGRID_TEMPLATE_ID");  // the d-... dynamic template id
    }

    /// <summary>Sends via the SendGrid dynamic template; <paramref name="templateData"/> shapes its Handlebars vars.</summary>
    public async Task SendAsync(object templateData, CancellationToken ct)
    {
        var client = new SendGridClient(_apiKey);
        var msg = new SendGridMessage();
        msg.SetFrom(new EmailAddress(_from));
        msg.AddTo(new EmailAddress(_to));
        msg.SetTemplateId(_templateId);
        msg.SetTemplateData(templateData);

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
