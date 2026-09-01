namespace SecureFlow.Modules.Identity.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;

    public string FromAddress { get; init; } = "noreply@secureflow.local";

    public string FromName { get; init; } = "SecureFlow";
}
