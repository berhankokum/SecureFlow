using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Modules.WorkItems.Application;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Presentation.Contracts;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems;

public static class WorkItemsModule
{
    public static IServiceCollection AddWorkItemsModule<TDbContext>(this IServiceCollection services)
        where TDbContext : class, IWorkItemsDbContext
    {
        services.AddScoped<IWorkItemsDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<FindProjectScopeHandler>();
        services.AddScoped<FindWorkItemScopeHandler>();
        services.AddScoped<CreateWorkItemHandler>();
        services.AddScoped<ListWorkItemsHandler>();
        services.AddScoped<GetWorkItemHandler>();
        services.AddScoped<UpdateWorkItemHandler>();
        services.AddScoped<ArchiveWorkItemHandler>();
        services.AddScoped<IValidator<CreateWorkItemRequest>, CreateWorkItemRequestValidator>();
        services.AddScoped<IValidator<UpdateWorkItemRequest>, UpdateWorkItemRequestValidator>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
