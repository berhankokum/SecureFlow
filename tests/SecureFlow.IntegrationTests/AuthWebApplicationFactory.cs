using System.Net.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecureFlow.Contracts.Email;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Identity.Infrastructure.Email;
using Testcontainers.PostgreSql;
using Xunit;

namespace SecureFlow.IntegrationTests;

/// <summary>
/// Her test run'ı kendine ait geçici PostgreSQL container'ı kullanır. Böylece
/// development/production connection string'leri hiçbir zaman testler tarafından
/// yazılmaz. SMTP de deterministik bir in-memory fake ile değiştirilir.
/// </summary>
public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:17-alpine@sha256:18cfe3ef5e6815560c98237d6216d1e5119702fb0f3894c8785dd58b8bbe5d73")
        .WithDatabase("secureflow_tests")
        .WithUsername("secureflow_tests")
        .WithPassword("secureflow_tests")
        .Build();

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    async Task IAsyncLifetime.InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdentityEmailSender>();
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<TestIdentityEmailSender>();
            services.AddSingleton<IIdentityEmailSender>(provider =>
                provider.GetRequiredService<TestIdentityEmailSender>());
            services.AddSingleton<IEmailSender>(provider =>
                provider.GetRequiredService<TestIdentityEmailSender>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        dbContext.Database.Migrate();

        return host;
    }
}

public sealed class TestIdentityEmailSender : IIdentityEmailSender, IEmailSender
{
    public bool ThrowOnSend { get; set; }

    public List<(string To, string Subject, string HtmlBody)> Messages { get; } = [];

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
        {
            throw new SmtpException("Test SMTP failure.");
        }

        Messages.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

[CollectionDefinition(IntegrationTestCollection.Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<AuthWebApplicationFactory>
{
    public const string Name = "SecureFlow integration tests";
}
