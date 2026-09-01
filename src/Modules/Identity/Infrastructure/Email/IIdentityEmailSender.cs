namespace SecureFlow.Modules.Identity.Infrastructure.Email;

public interface IIdentityEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
