using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SecureFlow.Modules.Identity.Infrastructure.Email;

/// <summary>
/// Development ortamında Mailpit'e (docker-compose, SMTP :1025) gönderir.
/// </summary>
public sealed class SmtpIdentityEmailSender(IOptions<SmtpOptions> options) : IIdentityEmailSender
{
    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;

        using var client = new SmtpClient(smtp.Host, smtp.Port);
        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, smtp.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message, cancellationToken);
    }
}
