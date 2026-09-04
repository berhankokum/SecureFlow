using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Contracts.Activity;
using SecureFlow.Modules.Collaboration.Application;
using SecureFlow.Modules.Collaboration.Application.Abstractions;
using SecureFlow.Modules.Collaboration.Presentation.Contracts;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration;

public static class CollaborationModule
{
    public static IServiceCollection AddCollaborationModule<TDbContext>(this IServiceCollection services)
        where TDbContext : class, ICollaborationDbContext
    {
        services.AddScoped<ICollaborationDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<IActivityWriter, ActivityWriter>();
        services.AddScoped<CollaborationScopeHandler>();
        services.AddScoped<CommentHandlers>();
        services.AddScoped<ActivityHandlers>();
        services.AddScoped<IValidator<CommentContentRequest>, CommentContentRequestValidator>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
