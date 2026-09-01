namespace SecureFlow.Contracts.Email;

/// <summary>
/// Modüllerin SMTP implementation'ına bağımlı olmadan e-posta gönderebilmesini
/// sağlayan ortak sözleşme. Delivery/outbox davranışı ilgili roadmap fazlarında
/// bu sözleşmenin arkasında geliştirilebilir.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
