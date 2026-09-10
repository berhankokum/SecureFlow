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
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
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

    private readonly IContainer _objectStorage = new ContainerBuilder(
            "minio/minio@sha256:14cea493d9a34af32f524e538b8346cf79f3321eff8e708c1e2960462bd8936e")
        .WithEnvironment("MINIO_ROOT_USER", "secureflow_tests")
        .WithEnvironment("MINIO_ROOT_PASSWORD", "secureflow_tests_secret")
        .WithCommand("server", "/data", "--console-address", ":9001")
        .WithPortBinding(9000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request.ForPort(9000).ForPath("/minio/health/ready")))
        .Build();

    private readonly IContainer _redis = new ContainerBuilder(
            "redis:7-alpine@sha256:e7723ff73d963f5cc6d9c4643ea3d989527a402a319239054e9472a7fb9219a2")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
        .Build();

    public string RedisConnectionString => $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    async Task IAsyncLifetime.InitializeAsync() =>
        await Task.WhenAll(_postgres.StartAsync(), _objectStorage.StartAsync(), _redis.StartAsync());

    async Task IAsyncLifetime.DisposeAsync() =>
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _objectStorage.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionString);
        builder.UseSetting(
            "ObjectStorage:ServiceUrl",
            $"http://{_objectStorage.Hostname}:{_objectStorage.GetMappedPublicPort(9000)}");
        builder.UseSetting("ObjectStorage:AccessKey", "secureflow_tests");
        builder.UseSetting("ObjectStorage:SecretKey", "secureflow_tests_secret");
        builder.UseSetting("ObjectStorage:BucketName", "secureflow-attachments-tests");
        builder.UseSetting("ObjectStorage:Region", "us-east-1");
        builder.UseSetting("ObjectStorage:CreateBucketIfMissing", "true");
        builder.UseSetting("BackgroundProcessing:Enabled", "false");
        builder.UseSetting("Outbox:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<CacheQueryCounter>();
            services.ConfigureDbContext<SecureFlowDbContext>((provider, options) =>
                options.AddInterceptors(provider.GetRequiredService<CacheQueryCounter>()));
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

        lock (Messages)
        {
            Messages.Add((toEmail, subject, htmlBody));
        }
        return Task.CompletedTask;
    }
}

[CollectionDefinition(IntegrationTestCollection.Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<AuthWebApplicationFactory>
{
    public const string Name = "SecureFlow integration tests";
}
