namespace SecureFlow.Modules.Identity.Infrastructure;

/// <summary>
/// E-posta doğrulama / şifre sıfırlama bağlantılarının işaret edeceği SPA adresi.
/// </summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; init; } = "http://localhost:5173";
}
