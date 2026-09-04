using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Modules.Notifications.Application;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule<TDbContext>(this IServiceCollection services)
        where TDbContext : class, INotificationsDbContext
    {
        services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<NotificationHandlers>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
