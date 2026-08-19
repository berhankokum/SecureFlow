using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Modules.Identity.Infrastructure.Email;

namespace SecureFlow.IntegrationTests;

/// <summary>
/// Auth entegrasyon testleri gerçek bir SMTP sunucusuna (Mailpit) bağımlı olmamalı;
/// e-posta gönderimi no-op bir sahteyle değiştirilir.
/// </summary>
public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdentityEmailSender>();
            services.AddSingleton<IIdentityEmailSender, NoOpIdentityEmailSender>();
        });
    }
}

file sealed class NoOpIdentityEmailSender : IIdentityEmailSender
{
    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
