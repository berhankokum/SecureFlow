using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.Email;

namespace SecureFlow.Modules.Identity.Infrastructure.Email;

/// <summary>
/// Development ortamında Mailpit'e (docker-compose, SMTP :1025) gönderir.
/// </summary>
public sealed class SmtpIdentityEmailSender(IOptions<SmtpOptions> options)
    : IIdentityEmailSender, IEmailSender
{
    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            Timeout = smtp.TimeoutMilliseconds,
        };
        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, smtp.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(smtp.TimeoutMilliseconds);
        try
        {
            await client.SendMailAsync(message, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("SMTP delivery timed out.", exception);
        }
    }
}
