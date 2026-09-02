using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Organizations;
using SecureFlow.Modules.Projects.Application;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Application.Authorization;
using SecureFlow.Modules.Projects.Presentation.Contracts;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Projects;

public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule<TDbContext>(this IServiceCollection services)
        where TDbContext : class, IProjectsDbContext
    {
        services.AddScoped<IProjectsDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<ProjectAuthorizationEvaluator>();
        services.AddScoped<IAuthorizationHandler, ProjectAccessAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ProjectPermissionAuthorizationHandler>();
        services.AddScoped<IOrganizationPermissionContributor, ProjectPermissionContributor>();
        services.AddScoped<IOrganizationMembershipRemovalCleanup, ProjectMembershipRemovalCleanup>();

        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<ListProjectsHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<FindProjectOrganizationHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<ArchiveProjectHandler>();
        services.AddScoped<AddProjectMemberHandler>();
        services.AddScoped<ChangeProjectMemberRoleHandler>();
        services.AddScoped<RemoveProjectMemberHandler>();

        services.AddScoped<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddScoped<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();
        services.AddScoped<IValidator<AddProjectMemberRequest>, AddProjectMemberRequestValidator>();
        services.AddScoped<IValidator<ChangeProjectMemberRoleRequest>, ChangeProjectMemberRoleRequestValidator>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
