using System.Net;
using System.Net.Mail;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

/// <summary>
/// SMTP email via System.Net.Mail. All settings come from configuration /
/// environment variables so SendGrid, Gmail App Passwords, Mailgun, etc. can be
/// injected on Render without code changes:
///   Smtp__Host, Smtp__Port, Smtp__User, Smtp__Pass, Smtp__From, Smtp__FromName, Smtp__EnableSsl
/// If Smtp__Host is not set, email is skipped gracefully (non-fatal).
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<SmtpEmailService> _log;

    public SmtpEmailService(IConfiguration cfg, ILogger<SmtpEmailService> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _cfg["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _log.LogWarning("SMTP not configured (Smtp__Host missing) — email to {To} skipped.", toEmail);
            return false;
        }

        int port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = _cfg["Smtp:User"];
        var pass = _cfg["Smtp:Pass"];
        var from = _cfg["Smtp:From"] ?? user ?? "no-reply@myothant.dev";
        var fromName = _cfg["Smtp:FromName"] ?? "Vedin · Sayar Myo Thant Naing";
        bool ssl = !bool.TryParse(_cfg["Smtp:EnableSsl"], out var e) || e;   // default true

        try
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(from, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8,
            };
            msg.To.Add(toEmail);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = ssl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            if (!string.IsNullOrWhiteSpace(user))
                client.Credentials = new NetworkCredential(user, pass);

            await client.SendMailAsync(msg);
            _log.LogInformation("Email sent to {To}.", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Email send failed to {To}.", toEmail);
            return false;
        }
    }
}
