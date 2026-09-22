using Microsoft.Extensions.Configuration;
using SecureFlow.Api.Configuration;

namespace SecureFlow.IntegrationTests;

public class ProductionHostingTests
{
    private static Dictionary<string, string?> Settings() => new()
    {
        ["AllowedHosts"] = "app.example.com",
        ["Frontend:BaseUrl"] = "https://app.example.com",
        ["Production:KnownProxies:0"] = "172.30.27.2",
        ["Production:KeysPath"] = Path.GetFullPath("keys"),
        ["Production:KeyCertificatePath"] = Path.GetFullPath("protection.pfx"),
        ["Production:KeyCertificatePassword"] = "test-only",
        ["ConnectionStrings:Postgres"] = "test-only",
        ["ConnectionStrings:Redis"] = "test-only",
        ["ObjectStorage:ServiceUrl"] = "http://object-storage:9000",
        ["ObjectStorage:AccessKey"] = "test-only",
        ["ObjectStorage:SecretKey"] = "test-only",
        ["ObjectStorage:BucketName"] = "attachments",
        ["Smtp:Host"] = "smtp.example.com",
        ["Smtp:FromAddress"] = "noreply@example.com",
    };

    [Fact]
    public void Explicit_production_settings_are_accepted() =>
        ProductionHosting.Validate(new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build());

    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("AllowedHosts", "other.example.com")]
    [InlineData("Frontend:BaseUrl", "http://app.example.com")]
    [InlineData("Frontend:BaseUrl", "https://user:pass@app.example.com")]
    [InlineData("Production:KnownProxies:0", "0.0.0.0")]
    [InlineData("Production:KnownProxies:0", "172.30.27.0/24")]
    [InlineData("Production:KeyCertificatePassword", "")]
    [InlineData("Production:KeysPath", "relative-path")]
    [InlineData("ConnectionStrings:Postgres", "")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public void Unsafe_or_incomplete_configuration_fails_closed(string key, string value)
    {
        var settings = Settings();
        settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => ProductionHosting.Validate(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }
}
