using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace SecureFlow.Api.Configuration;

public static class ProductionHosting
{
    public static void AddProductionHosting(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsProduction()) return;

        // Mounted file takes precedence intentionally; never copied into the image.
        builder.Configuration.AddJsonFile(
            builder.Configuration["Production:SettingsFile"] ?? "/run/secureflow/settings.json",
            optional: false, reloadOnChange: false);
        var config = builder.Configuration;
        Validate(config);
        var proxies = config.GetSection("Production:KnownProxies").Get<string[]>()!;
        builder.Services.PostConfigure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies) options.KnownProxies.Add(IPAddress.Parse(proxy));
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
        });

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            config["Production:KeyCertificatePath"]!, config["Production:KeyCertificatePassword"],
            X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
            || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        {
            certificate.Dispose();
            throw new InvalidOperationException("Production key-protection certificate is invalid or expired.");
        }
        builder.Services.AddSingleton(_ => certificate);
        var keys = config["Production:KeysPath"]!;
        Directory.CreateDirectory(keys);
        builder.Services.AddDataProtection().SetApplicationName("SecureFlow.Production")
            .PersistKeysToFileSystem(new DirectoryInfo(keys)).ProtectKeysWithCertificate(certificate);
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
    }

    public static void Validate(IConfiguration config)
    {
        var hosts = config["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (!Uri.TryCreate(config["Frontend:BaseUrl"], UriKind.Absolute, out var frontend)
            || frontend.Scheme != "https" || frontend.UserInfo.Length != 0
            || frontend.Query.Length != 0 || frontend.Fragment.Length != 0 || frontend.AbsolutePath != "/"
            || hosts.Length != 1 || !string.Equals(hosts[0], frontend.Host, StringComparison.OrdinalIgnoreCase)
            || hosts[0].Contains('*'))
            throw new InvalidOperationException("Production requires one explicit AllowedHosts host matching the HTTPS Frontend:BaseUrl.");

        var proxies = config.GetSection("Production:KnownProxies").Get<string[]>() ?? [];
        if (proxies.Length == 0 || proxies.Any(value => !IPAddress.TryParse(value, out var ip)
            || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)))
            throw new InvalidOperationException("Production requires explicit trusted proxy IP addresses.");
        foreach (var key in new[] { "ConnectionStrings:Postgres", "ConnectionStrings:Redis", "ObjectStorage:ServiceUrl",
            "ObjectStorage:AccessKey", "ObjectStorage:SecretKey", "ObjectStorage:BucketName", "Smtp:Host", "Smtp:FromAddress",
            "Production:KeysPath", "Production:KeyCertificatePath", "Production:KeyCertificatePassword" })
            if (string.IsNullOrWhiteSpace(config[key])) throw new InvalidOperationException($"Required production setting is missing: {key}.");
        if (!Path.IsPathFullyQualified(config["Production:KeysPath"]!)
            || !Path.IsPathFullyQualified(config["Production:KeyCertificatePath"]!))
            throw new InvalidOperationException("Production key paths must be absolute.");
        if (config.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED"))
            throw new InvalidOperationException("Automatic trust-all forwarding is forbidden.");
    }
}
