using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Modules.Outbox.Application;
using SecureFlow.Modules.Outbox.Infrastructure;
namespace SecureFlow.Modules.Outbox;

public static class OutboxModule
{
    public static IServiceCollection AddOutboxModule<TDbContext>(this IServiceCollection services, IConfiguration configuration) where TDbContext : DbContext, IOutboxDbContext
    {
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection("Outbox")).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<IOutboxDbContext>(sp => sp.GetRequiredService<TDbContext>());
        services.AddScoped<IOutboxWriter, OutboxWriter>(); services.AddSingleton<IDurableOutboxWriter, DurableOutboxWriter>(); services.AddHostedService<OutboxProcessor>(); return services;
    }
}
