namespace SecureFlow.Api.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public string[] AllowedOrigins { get; set; } = [];
    public int AuthPermitLimit { get; set; } = 10;
    public int EmailPermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;

    public bool IsValid() => AllowedOrigins is not null
        && AllowedOrigins.All(IsValidOrigin)
        && AuthPermitLimit is >= 1 and <= 1000
        && EmailPermitLimit is >= 1 and <= 1000
        && WindowSeconds is >= 1 and <= 3600;

    public static bool IsValidOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !origin.Contains('*')
        && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
}
