using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Modules.Teams.Application;
using SecureFlow.Modules.Teams.Application.Abstractions;
using SecureFlow.Modules.Teams.Presentation.Contracts;
using SecureFlow.SharedKernel;
using SecureFlow.Contracts.Organizations;

namespace SecureFlow.Modules.Teams;

public static class TeamsModule
{
    public static IServiceCollection AddTeamsModule<TDbContext>(this IServiceCollection services)
        where TDbContext : class, ITeamsDbContext
    {
        services.AddScoped<ITeamsDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<CreateTeamHandler>();
        services.AddScoped<ListTeamsHandler>();
        services.AddScoped<GetTeamHandler>();
        services.AddScoped<FindTeamOrganizationHandler>();
        services.AddScoped<UpdateTeamHandler>();
        services.AddScoped<ArchiveTeamHandler>();
        services.AddScoped<AddTeamMemberHandler>();
        services.AddScoped<RemoveTeamMemberHandler>();
        services.AddScoped<IOrganizationMembershipRemovalCleanup, TeamMembershipRemovalCleanup>();
        services.AddScoped<IValidator<CreateTeamRequest>, CreateTeamRequestValidator>();
        services.AddScoped<IValidator<UpdateTeamRequest>, UpdateTeamRequestValidator>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
