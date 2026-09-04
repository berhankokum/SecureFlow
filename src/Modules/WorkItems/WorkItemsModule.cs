using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Modules.WorkItems.Application;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Presentation.Contracts;
using SecureFlow.SharedKernel;
using SecureFlow.Contracts.Organizations;
using SecureFlow.Contracts.Projects;

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
        services.AddScoped<LabelHandlers>();
        services.AddScoped<AssignmentHandler>();
        services.AddScoped<WorkflowHandlers>();
        services.AddScoped<IOrganizationCreationProvisioner, DefaultWorkflowProvisioner>();
        services.AddScoped<IOrganizationMembershipRemovalCleanup, WorkItemOrganizationMembershipRemovalCleanup>();
        services.AddScoped<IProjectMembershipRemovalCleanup, WorkItemProjectMembershipRemovalCleanup>();
        services.AddScoped<IValidator<LabelNameRequest>, LabelNameRequestValidator>();
        services.AddScoped<IValidator<CreateWorkItemRequest>, CreateWorkItemRequestValidator>();
        services.AddScoped<IValidator<UpdateWorkItemRequest>, UpdateWorkItemRequestValidator>();
        services.AddScoped<IValidator<AssignWorkItemRequest>, AssignWorkItemRequestValidator>();
        services.AddScoped<IValidator<TransitionWorkItemRequest>, TransitionWorkItemRequestValidator>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
