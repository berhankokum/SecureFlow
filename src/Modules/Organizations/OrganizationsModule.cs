using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using SecureFlow.Modules.Organizations.Application.Invitations;
using SecureFlow.Modules.Organizations.Application.Members;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.CreateOrganization;
using SecureFlow.Modules.Organizations.Application.GetOrganization;
using SecureFlow.Modules.Organizations.Application.ListOrganizations;
using SecureFlow.Modules.Organizations.Presentation.Contracts;
using SecureFlow.Modules.Organizations.Infrastructure;
using SecureFlow.SharedKernel;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.BackgroundProcessing;

namespace SecureFlow.Modules.Organizations;

public static class OrganizationsModule
{
    public static IServiceCollection AddOrganizationsModule<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : class, IOrganizationsDbContext
    {
        services.AddScoped<IOrganizationsDbContext>(provider =>
            provider.GetRequiredService<TDbContext>());

        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<ListOrganizationsHandler>();
        services.AddScoped<GetOrganizationHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<GetInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<RevokeInvitationHandler>();
        services.AddScoped<ExpiredInvitationCleanupProcessor>();
        services.AddHostedService<ExpiredInvitationsBackgroundService>();
        services.AddScoped<IAuthorizationHandler, OrganizationPermissionAuthorizationHandler>();
        services.AddScoped<IValidator<CreateOrganizationRequest>, CreateOrganizationRequestValidator>();
        services.AddScoped<IValidator<CreateInvitationRequest>, CreateInvitationRequestValidator>();
        services.AddScoped<IValidator<ChangeMemberRoleRequest>, ChangeMemberRoleRequestValidator>();

        services.AddOptions<InvitationOptions>()
            .Bind(configuration.GetSection(InvitationOptions.SectionName))
            .Validate(options => options.ExpirationHours is >= 1 and <= 720,
                "Invitations:ExpirationHours must be between 1 and 720.")
            .ValidateOnStart();

        services.AddOptions<BackgroundProcessingOptions>()
            .Bind(configuration.GetSection(BackgroundProcessingOptions.SectionName))
            .Validate(options => options.PollingIntervalSeconds is >= 1 and <= 3600,
                "BackgroundProcessing:PollingIntervalSeconds must be between 1 and 3600.")
            .Validate(options => options.BatchSize is >= 1 and <= 1000,
                "BackgroundProcessing:BatchSize must be between 1 and 1000.")
            .ValidateOnStart();

        services.PostConfigure<InvitationOptions>(options =>
            options.FrontendBaseUrl = configuration["Frontend:BaseUrl"]
                ?? options.FrontendBaseUrl);

        services.TryAddSingleton<IClock, SystemClock>();

        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(
                    permission,
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.AddRequirements(new PermissionRequirement(permission));
                    });
            }
        });

        return services;
    }
}
