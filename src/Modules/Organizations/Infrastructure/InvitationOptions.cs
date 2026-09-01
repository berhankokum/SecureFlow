namespace SecureFlow.Modules.Organizations.Infrastructure;

public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    public int ExpirationHours { get; set; } = 72;

    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
