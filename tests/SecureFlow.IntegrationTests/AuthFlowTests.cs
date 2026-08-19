using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Modules.Identity.Domain;

namespace SecureFlow.IntegrationTests;

public class AuthFlowTests(AuthWebApplicationFactory factory) : IClassFixture<AuthWebApplicationFactory>
{
    private static string UniqueEmail() => $"itest-{Guid.NewGuid():N}@example.com";

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/antiforgery/token");
        response.EnsureSuccessStatusCode();

        var setCookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        return setCookie["XSRF-TOKEN=".Length..setCookie.IndexOf(';')];
    }

    [Fact]
    public async Task Register_confirm_login_me_logout_end_to_end_succeeds()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, registerResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmationToken));

        var confirmResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email", new { userId = user!.Id, code = encodedCode });
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);

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
        var client = factory.CreateClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_generic_unauthorized()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = UniqueEmail(), password = "whatever123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_authentication_returns_unauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_without_csrf_token_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email = UniqueEmail(), password = "Sup3rSecret!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
