using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Contracts.Audit;
using SecureFlow.Modules.Audit.Application;

namespace SecureFlow.Modules.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule<T>(this IServiceCollection services) where T : class, IAuditDbContext
    {
        services.AddScoped<IAuditDbContext>(provider => provider.GetRequiredService<T>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<AuditReadHandler>();
        return services;
    }
}
