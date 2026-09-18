using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Security;
using SecureFlow.Contracts.Filters;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class SecurityHardeningTests(AuthWebApplicationFactory factory)
{
    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/forgot-password")]
    [InlineData("/api/v1/auth/reset-password")]
    [InlineData("/api/v1/auth/confirm-email")]
    public async Task Auth_limits_cannot_be_bypassed_with_forwarded_headers_and_return_retry_information(string path)
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Security:AuthPermitLimit", "2");
            builder.UseSetting("Security:EmailPermitLimit", "2");
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteAddressFilter>());
        });
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        for (var index = 0; index < 3; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{index + 1}");
            request.Content = JsonContent.Create(new { });
            var response = await client.SendAsync(request);
            if (index < 2)
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
                Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Contains(response.Headers.GetValues("X-Trace-Id").Single(), await response.Content.ReadAsStringAsync());
            }
        }
        // Other actions do not share a counter with the exhausted action.
        var other = path.EndsWith("login", StringComparison.Ordinal) ? "register" : "login";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/auth/{other}", new { })).StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("null")]
    [InlineData("https://localhost.evil.example")]
    [InlineData("https://localhost/path")]
    public async Task Foreign_origins_are_rejected_for_api_and_signalr_including_upgrades(string origin)
    {
        using var client = factory.CreateSecureClient();
        foreach (var path in new[] { "/api/v1/antiforgery/token", "/hubs/realtime", "/hubs/realtime/negotiate" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("Origin", origin);
            request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
            request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Fact]
    public async Task Allowed_origins_and_preflights_are_explicit_and_do_not_bypass_csrf_or_authentication()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("Security:AllowedOrigins:0", "https://ui.example"));
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("Origin", "https://ui.example");
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");
        var response = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://ui.example", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.com", password = "Password123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/v1/antiforgery/token")).StatusCode);
    }

    [Fact]
    public async Task Local_development_http_proxy_can_bootstrap_csrf_without_relaxing_auth_cookies()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = app.CreateClient(new() { BaseAddress = new Uri("http://localhost") });
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        var response = await client.GetAsync("/api/v1/antiforgery/token");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal) && cookie.Contains("secure", StringComparison.Ordinal));
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Responses_and_csrf_cookies_are_hardened_including_errors_and_hsts()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://secureflow.example") });
        foreach (var path in new[] { "/api/v1/me", "/api/v1/unknown", "/api/v1/antiforgery/token" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Contains("max-age=15552000", response.Headers.GetValues("Strict-Transport-Security").Single());
            Assert.True(response.Headers.CacheControl?.NoStore);
            if (path.EndsWith("token", StringComparison.Ordinal))
            {
                var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
                Assert.All(cookies, cookie => { Assert.Contains("secure", cookie); Assert.Contains("samesite=strict", cookie); });
                Assert.Contains(cookies, cookie => cookie.Contains("Antiforgery", StringComparison.Ordinal) && cookie.Contains("httponly", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Every_controller_action_explicitly_declares_auth_and_every_mutation_has_csrf()
    {
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();
        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var attributes = action.ControllerTypeInfo.GetCustomAttributes().Concat(action.MethodInfo.GetCustomAttributes()).ToArray();
            Assert.True(attributes.Any(a => a is AuthorizeAttribute or AllowAnonymousAttribute), action.DisplayName);
            var methods = action.MethodInfo.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods);
            if (methods.Any(m => m is "POST" or "PUT" or "PATCH" or "DELETE"))
            {
                Assert.True(attributes.Any(a => a is ValidateAntiforgeryAttribute), action.DisplayName);
                if (attributes.Any(a => a is AllowAnonymousAttribute))
                    Assert.True(attributes.Any(a => a is EnableRateLimitingAttribute), action.DisplayName);
            }
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://public.example")]
    [InlineData("https://user:pass@example.com")]
    [InlineData("https://example.com/")]
    [InlineData("https://example.com?secret=value")]
    [InlineData("https://example.com#fragment")]
    public void Unsafe_origin_configuration_fails_validation(string origin) =>
        Assert.False(new SecurityOptions { AllowedOrigins = [origin] }.IsValid());

    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // TestServer has no socket peer. Model an untrusted network client explicitly.
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
