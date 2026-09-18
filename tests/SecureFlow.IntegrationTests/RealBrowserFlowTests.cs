using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RealBrowserFlowTests(AuthWebApplicationFactory factory)
{
    [Fact]
    [Trait("Category", "Browser")]
    public async Task Browser_uses_real_https_api_database_and_cookie_security_for_lifecycle()
    {
        var root = FindRoot();
        var frontend = Path.Combine(root, "frontend");
        Assert.True(File.Exists(Path.Combine(frontend, "dist", "index.html")), "Run npm ci and npm run build in frontend first.");
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var generatedCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        // Re-import the in-memory PFX so Windows Schannel can access the private key.
        using var certificate = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pkcs12), null);
        var mailboxKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new BrowserAssetsFilter(Path.Combine(frontend, "dist"), mailboxKey))));
        app.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        app.StartServer();

        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = frontend,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--config=playwright.real.config.ts");
        start.Environment["SECUREFLOW_E2E_URL"] = app.ClientOptions.BaseAddress.ToString();
        start.Environment["SECUREFLOW_E2E_MAIL_KEY"] = mailboxKey;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(4));
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        Assert.True(process.ExitCode == 0, $"Real browser test failed:\n{await stdout}\n{await stderr}");
    }

    private static string FindRoot([System.Runtime.CompilerServices.CallerFilePath] string path = "")
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SecureFlow.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    // Exists ONLY in this test assembly/derived host. No test endpoints in production.
    private sealed class BrowserAssetsFilter(string assets, string mailboxKey) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/__test/mail")
                {
                    if (context.Request.Method != "GET" || context.Request.Headers["X-Test-Key"] != mailboxKey)
                    {
                        context.Response.StatusCode = 404;
                        return;
                    }
                    var sender = context.RequestServices.GetRequiredService<TestIdentityEmailSender>();
                    string[] messages;
                    lock (sender.Messages)
                        messages = sender.Messages.Where(m => m.To == context.Request.Query["email"])
                            .Select(m => m.HtmlBody).ToArray();
                    context.Response.Headers.CacheControl = "no-store";
                    await context.Response.WriteAsJsonAsync(messages);
                    return;
                }
                await nextMiddleware(context);
            });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(assets) });
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Method == "GET" && !context.Request.Path.StartsWithSegments("/api")
                    && !context.Request.Path.StartsWithSegments("/hubs") && !context.Request.Path.StartsWithSegments("/health"))
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.SendFileAsync(Path.Combine(assets, "index.html"));
                    return;
                }
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
