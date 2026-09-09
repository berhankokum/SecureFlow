using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using SecureFlow.Contracts.BackgroundProcessing;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Modules.Notifications.Application;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.SharedKernel;
using SecureFlow.Modules.Notifications.Infrastructure;

namespace SecureFlow.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : class, INotificationsDbContext
    {
        services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<NotificationHandlers>();
        services.AddScoped<DeadlineReminderProcessor>();
        services.AddScoped<NotificationEmailDeliveryProcessor>();
        services.AddHostedService<NotificationsBackgroundService>();
        services.AddOptions<BackgroundProcessingOptions>()
            .Bind(configuration.GetSection(BackgroundProcessingOptions.SectionName));
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
