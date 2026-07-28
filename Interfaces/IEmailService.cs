namespace PortfolioApi.Interfaces;

/// <summary>Sends transactional HTML email (SMTP). Returns true on success.</summary>
public interface IEmailService
{
    Task<bool> SendAsync(string toEmail, string subject, string htmlBody);
}
