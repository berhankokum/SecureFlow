using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SecureFlow.Api.Persistence;

// Explicit opt-in tool; never connects to the user's .env or development database.
if (args is not ["--run"] || !File.Exists("compose.production.yml"))
    throw new InvalidOperationException("Run from repository root with --run; builds must already exist.");
var root = Path.GetFullPath(".");
var project = "sf-p27-" + Guid.NewGuid().ToString("N")[..12];
var directory = Path.Combine(root, "artifacts", project);
Directory.CreateDirectory(directory);
var httpsPort = FreePort();
var smtpPort = FreePort();
var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
var accountPassword = "Sm0ke!" + password;
var s3Access = "smoke" + Guid.NewGuid().ToString("N")[..12];
var origin = new Uri($"https://localhost:{httpsPort}");
var environment = new Dictionary<string, string>
{
    ["PRODUCTION_HOST"] = "localhost",
    ["PRODUCTION_SECRETS_DIR"] = directory,
    ["PRODUCTION_HTTP_PORT"] = FreePort().ToString(),
    ["PRODUCTION_HTTPS_PORT"] = httpsPort.ToString(),
};
var overlay = Path.Combine(directory, "smoke.yml");
File.WriteAllText(overlay, $$"""
services:
  mailpit:
    image: axllent/mailpit@sha256:98b916bd3c8d61f7633a52d3ea2f58d00620cb01ca57ab59edde68c347a95365
    networks: [data, mailbox]
    ports: ["127.0.0.1:{{smtpPort}}:8025"]
networks:
  mailbox: {}
""");
// Empty interpolation file prevents Compose from reading repository .env.
var emptyEnv = Path.Combine(directory, "empty.env");
File.WriteAllText(emptyEnv, "");
var compose = new[] { "compose", "--env-file", emptyEnv, "-p", project, "-f", Path.Combine(root, "compose.production.yml"), "-f", overlay };

using var rsa = RSA.Create(2048);
var certRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder();
san.AddDnsName("localhost");
san.AddIpAddress(IPAddress.Loopback);
certRequest.CertificateExtensions.Add(san.Build());
using var certificate = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
File.WriteAllText(Path.Combine(directory, "fullchain.pem"), certificate.ExportCertificatePem());
File.WriteAllText(Path.Combine(directory, "privatekey.pem"), rsa.ExportPkcs8PrivateKeyPem());
using var protectionRsa = RSA.Create(2048);
var protectionRequest = new CertificateRequest("CN=SecureFlow Smoke Key Protection", protectionRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var protectionCertificate = protectionRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
File.WriteAllBytes(Path.Combine(directory, "protection.pfx"), protectionCertificate.Export(X509ContentType.Pfx, password));
File.WriteAllText(Path.Combine(directory, "postgres-password"), password);
File.WriteAllText(Path.Combine(directory, "redis.conf"), $"bind 0.0.0.0\nprotected-mode yes\nrequirepass {password}\nmaxmemory 128mb\nmaxmemory-policy allkeys-lru\nsave \"\"\nappendonly no\n");
File.WriteAllText(Path.Combine(directory, "s3-access-key"), s3Access);
File.WriteAllText(Path.Combine(directory, "s3-secret-key"), password);
File.WriteAllText(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(new
{
    AllowedHosts = "localhost",
    Frontend = new { BaseUrl = origin.ToString().TrimEnd('/') },
    Production = new
    {
        KnownProxies = new[] { "172.30.27.2" },
        KeysPath = "/var/lib/secureflow/keys",
        KeyCertificatePath = "/run/secureflow/protection.pfx",
        KeyCertificatePassword = password
    },
    ConnectionStrings = new
    {
        Postgres = $"Host=postgres;Database=secureflow;Username=secureflow_app;Password={password}",
        Redis = $"redis:6379,password={password},abortConnect=false"
    },
    ObjectStorage = new
    {
        ServiceUrl = "http://object-storage:9000",
        AccessKey = s3Access,
        SecretKey = password,
        BucketName = "smoke-attachments",
        Region = "us-east-1",
        CreateBucketIfMissing = true
    },
    Smtp = new { Host = "mailpit", Port = 1025, EnableSsl = false, FromAddress = "smoke@example.com" },
    BackgroundProcessing = new { Enabled = false },
    Outbox = new { Enabled = false },
}));

using var handler = new HttpClientHandler
{
    CookieContainer = new CookieContainer(),
    AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback = (_, peer, _, _) => peer?.Thumbprint == certificate.Thumbprint
};
using var client = new HttpClient(handler) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(10) };
try
{
    await Docker([.. compose, "config", "--quiet"]);
    await Docker([.. compose, "up", "-d", "--no-build", "--wait", "--wait-timeout", "120", "postgres", "redis", "object-storage", "mailpit"]);
    Console.WriteLine("PASS: isolated dependencies and secret mounts");
    var options = new DbContextOptionsBuilder<SecureFlowDbContext>().UseNpgsql("Host=unused;Database=unused").Options;
    using (var db = new SecureFlowDbContext(options, null!))
    {
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        sql += $"\nCREATE ROLE secureflow_app LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE;\n"
            + "GRANT CONNECT ON DATABASE secureflow TO secureflow_app; GRANT USAGE ON SCHEMA public TO secureflow_app;\n"
            + "GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO secureflow_app;\n"
            + "REVOKE INSERT,UPDATE,DELETE ON TABLE \"__EFMigrationsHistory\" FROM secureflow_app;\n"
            + "GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO secureflow_app;\n";
        await Docker([.. compose, "exec", "-T", "postgres", "psql", "-U", "secureflow_admin", "-d", "secureflow", "--set", "ON_ERROR_STOP=1"], sql);
    }
    Console.WriteLine("PASS: explicit migration of disposable database");
    await Docker([.. compose, "up", "-d", "--no-build", "--wait", "--wait-timeout", "120", "api", "edge"]);
    Require((await client.GetAsync("/health/ready")).StatusCode == HttpStatusCode.OK, "HTTPS readiness");
    using (var plain = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
    {
        var redirect = await plain.GetAsync($"http://localhost:{environment["PRODUCTION_HTTP_PORT"]}/login");
        Require(redirect.StatusCode == HttpStatusCode.PermanentRedirect && redirect.Headers.Location == new Uri(origin, "/login"), "canonical HTTPS redirect including mapped port");
    }
    var html = await client.GetAsync("/app/acme/projects/SF");
    Require(html.StatusCode == HttpStatusCode.OK && html.Headers.GetValues("Content-Security-Policy").Single().Contains("frame-ancestors 'none'"), "SPA fallback + CSP");
    Require(html.Headers.GetValues("Strict-Transport-Security").Single().Contains("15552000"), "HSTS");
    Require((await client.GetAsync("/api/v1/me")).StatusCode == HttpStatusCode.Unauthorized, "anonymous API not SPA");
    Require((await client.PostAsync("/hubs/realtime/negotiate?negotiateVersion=1", null)).StatusCode == HttpStatusCode.Unauthorized, "SignalR proxy authentication");
    var untrustedStatus = await Docker([.. compose, "exec", "-T", "object-storage", "curl", "--silent", "--output", "/dev/null",
        "--write-out", "%{http_code}", "--header", "Host: localhost", "--header", "X-Forwarded-Proto: https",
        "--header", "X-Forwarded-For: 198.51.100.8", "http://api:8080/api/v1/antiforgery/token"]);
    Require(untrustedStatus == "307", "API ignores forwarding from non-proxy container");
    using (var foreign = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me"))
    {
        foreign.Headers.Host = "evil.example";
        try { Require((await client.SendAsync(foreign)).StatusCode != HttpStatusCode.OK, "host rejection"); }
        catch (HttpRequestException) { /* Unknown SNI is rejected during TLS, before HTTP. */ }
    }
    using (var spoof = new HttpRequestMessage(HttpMethod.Get, "/api/v1/antiforgery/token"))
    {
        spoof.Headers.Add("X-Forwarded-Proto", "http");
        spoof.Headers.Add("X-Forwarded-For", "198.51.100.8");
        var response = await client.SendAsync(spoof);
        Require(response.StatusCode == HttpStatusCode.NoContent, "edge overwrites forwarded scheme");
        Require(response.Headers.GetValues("Set-Cookie").All(value => value.Contains("secure", StringComparison.OrdinalIgnoreCase)), "Secure cookies");
    }
    client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", handler.CookieContainer.GetCookies(origin)["XSRF-TOKEN"]!.Value);
    var email = "smoke@example.com";
    Require((await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = accountPassword })).IsSuccessStatusCode, "register through proxy");
    using var mailbox = new HttpClient { BaseAddress = new Uri($"http://localhost:{smtpPort}") };
    string? confirmLink = null;
    for (var attempt = 0; attempt < 30 && confirmLink is null; attempt++)
    {
        using var messages = JsonDocument.Parse(await mailbox.GetStringAsync("/api/v1/messages"));
        if (messages.RootElement.GetProperty("messages").GetArrayLength() > 0)
        {
            var id = messages.RootElement.GetProperty("messages")[0].GetProperty("ID").GetString();
            using var message = JsonDocument.Parse(await mailbox.GetStringAsync($"/api/v1/message/{id}"));
            confirmLink = WebUtility.HtmlDecode(Regex.Match(message.RootElement.GetProperty("HTML").GetString()!, "href=\"([^\"]+)\"").Groups[1].Value);
        }
        else await Task.Delay(500);
    }
    Require(!string.IsNullOrEmpty(confirmLink), "SMTP confirmation delivered to isolated Mailpit");
    await Docker([.. compose, "up", "-d", "--no-build", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "90", "api"]);
    // Nginx DNS cache expires after 10 seconds; bounded readiness polling below.
    await Ready();
    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(confirmLink!).Query);
    Require((await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = query["userId"].ToString(), code = query["code"].ToString() })).IsSuccessStatusCode,
        "old antiforgery and email token survive container recreation");
    Require((await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = accountPassword })).IsSuccessStatusCode, "login");
    await Docker([.. compose, "up", "-d", "--no-build", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "90", "api"]);
    await Ready();
    Require((await client.GetAsync("/api/v1/me")).IsSuccessStatusCode, "auth cookie survives container recreation");
    using (var socket = new System.Net.WebSockets.ClientWebSocket())
    {
        socket.Options.Cookies = handler.CookieContainer;
        socket.Options.RemoteCertificateValidationCallback = (_, peer, _, _) => peer?.GetCertHashString() == certificate.Thumbprint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(new Uri($"wss://localhost:{httpsPort}/hubs/realtime"), timeout.Token);
        Require(socket.State == System.Net.WebSockets.WebSocketState.Open, "authenticated WebSocket upgrade through Nginx");
        socket.Abort();
    }
    Console.WriteLine("PASS: HTTPS, headers, auth/CSRF, SMTP and persistent encrypted key ring");
    var keyXml = await Docker([.. compose, "exec", "-T", "api", "sh", "-c", "cat /var/lib/secureflow/keys/key-*.xml"]);
    Require(keyXml.Contains("encryptedSecret") && !keyXml.Contains("<value>"), "key ring encryption at rest");
    foreach (var service in new[] { "api", "edge" })
        Require((await Docker([.. compose, "exec", "-T", service, "id", "-u"])).Trim() != "0", "non-root " + service);
    Console.WriteLine("PASS: non-root runtimes");
    // Browser assertions use the actual edge, not Vite preview or API mocks.
    var browser = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "frontend"), UseShellExecute = false };
    browser.ArgumentList.Add(Path.Combine(root, "frontend", "node_modules", "@playwright", "test", "cli.js"));
    browser.ArgumentList.Add("test"); browser.ArgumentList.Add("--config"); browser.ArgumentList.Add("playwright.production.config.ts");
    browser.Environment["PRODUCTION_SMOKE_URL"] = origin.ToString();
    using var browserProcess = Process.Start(browser)!;
    await browserProcess.WaitForExitAsync();
    Require(browserProcess.ExitCode == 0, "production browser assertions");
    Console.WriteLine("Production smoke passed; disposing only " + project);
}
finally
{
    // Never use a default project or a user-supplied cleanup target.
    if (!Regex.IsMatch(project, "^sf-p27-[a-f0-9]{12}$")) throw new InvalidOperationException("Unsafe cleanup project");
    await Docker([.. compose, "down", "--volumes", "--remove-orphans"]);
    var expected = Path.Combine(root, "artifacts", project);
    if (Path.GetFullPath(directory) != expected) throw new InvalidOperationException("Unsafe cleanup directory");
    Directory.Delete(directory, recursive: true);
}

async Task<string> Docker(string[] arguments, string? stdin = null)
{
    var info = new ProcessStartInfo("docker")
    {
        WorkingDirectory = root,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true
    };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
    using var process = Process.Start(info)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    if (stdin is not null) await process.StandardInput.WriteAsync(stdin);
    process.StandardInput.Close();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { process.Kill(entireProcessTree: true); throw; }
    var result = await stdout;
    var errors = await stderr;
    if (process.ExitCode != 0)
    {
        // Do not dump configs/credentials. Diagnostics retained only in ignored artifacts.
        File.WriteAllText(Path.Combine(root, "artifacts", "phase27-smoke-error.log"), errors.Replace(password, "[REDACTED]"));
        throw new InvalidOperationException("Docker command failed; see artifacts/phase27-smoke-error.log");
    }
    return result;
}
async Task Ready()
{
    for (var attempt = 0; attempt < 30; attempt++)
    {
        try { if ((await client.GetAsync("/health/ready")).IsSuccessStatusCode) return; }
        catch (HttpRequestException) { }
        await Task.Delay(500);
    }
    throw new InvalidOperationException("API did not recover behind edge");
}
static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("Failed: " + name);
}
static int FreePort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}
