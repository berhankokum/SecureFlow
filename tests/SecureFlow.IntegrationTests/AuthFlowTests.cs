using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Modules.Identity.Domain;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AuthFlowTests(AuthWebApplicationFactory factory)
{
    private static string UniqueEmail() => $"itest-{Guid.NewGuid():N}@example.com";

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/antiforgery/token");
        response.EnsureSuccessStatusCode();

        var setCookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", setCookie, StringComparison.OrdinalIgnoreCase);
        return setCookie["XSRF-TOKEN=".Length..setCookie.IndexOf(';')];
    }

    private static async Task RegisterAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task ConfirmEmailAsync(HttpClient client, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email", new { userId = user!.Id, code = encodedCode });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Register_confirm_login_me_logout_end_to_end_succeeds()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        await RegisterAsync(client, email, password);
        await ConfirmEmailAsync(client, email);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);

        var authCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .First(cookie => cookie.StartsWith("SecureFlow.Auth=", StringComparison.Ordinal));
        Assert.Contains("HttpOnly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Expires=", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Max-Age=", authCookie, StringComparison.OrdinalIgnoreCase);

        var meResponse = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Login principal'ı değiştirdi: logout'tan önce antiforgery token yenilenmeli.
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        var logoutResponse = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var meAfterLogout = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Register_with_existing_email_fails()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        await RegisterAsync(client, email, password);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task Email_uniqueness_is_case_insensitive()
    {
        var client = factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        var email = UniqueEmail();

        await RegisterAsync(client, email.ToLowerInvariant(), "Sup3rSecret!");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email = email.ToUpperInvariant(), password = "Sup3rSecret!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_unknown_email_and_wrong_password_return_same_generic_problem()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        await RegisterAsync(client, email, password);
        await ConfirmEmailAsync(client, email);

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = "WrongPassword1" });

        var unknownEmail = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = UniqueEmail(), password = "whatever123" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        using var wrongBody = JsonDocument.Parse(await wrongPassword.Content.ReadAsStringAsync());
        using var unknownBody = JsonDocument.Parse(await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal(
            wrongBody.RootElement.GetProperty("title").GetString(),
            unknownBody.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unconfirmed_user_cannot_login()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        await RegisterAsync(client, email, password);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        await RegisterAsync(client, email, password);
        await ConfirmEmailAsync(client, email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await client.PostAsJsonAsync(
                "/api/v1/auth/login", new { email, password = "WrongPassword1" });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var correctPasswordWhileLocked = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, correctPasswordWhileLocked.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsLockedOutAsync(user!));
    }

    [Fact]
    public async Task Password_reset_token_is_single_use()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string oldPassword = "Sup3rSecret!";
        const string newPassword = "An0therSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        await RegisterAsync(client, email, oldPassword);
        await ConfirmEmailAsync(client, email);

        string code;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            var token = await userManager.GeneratePasswordResetTokenAsync(user!);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        }

        var firstReset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new { email, code, newPassword });
        var secondReset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new { email, code, newPassword = "YetAn0therSecret!" });

        Assert.Equal(HttpStatusCode.NoContent, firstReset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondReset.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_expose_smtp_failure_or_account_existence()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        await RegisterAsync(client, email, "Sup3rSecret!");

        var sender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        sender.ThrowOnSend = true;

        try
        {
            var existing = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
            var missing = await client.PostAsJsonAsync(
                "/api/v1/auth/forgot-password", new { email = UniqueEmail() });

            Assert.Equal(HttpStatusCode.NoContent, existing.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, missing.StatusCode);
        }
        finally
        {
            sender.ThrowOnSend = false;
        }
    }

    [Fact]
    public async Task Registration_rolls_back_when_confirmation_email_cannot_be_sent()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        var sender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        sender.ThrowOnSend = true;

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/register", new { email, password = "Sup3rSecret!" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            sender.ThrowOnSend = false;
        }

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Me_without_authentication_returns_unauthorized()
    {
        var client = factory.CreateSecureClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_without_csrf_token_is_rejected()
    {
        var client = factory.CreateSecureClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email = UniqueEmail(), password = "Sup3rSecret!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
