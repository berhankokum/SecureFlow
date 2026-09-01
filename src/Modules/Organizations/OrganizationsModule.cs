using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.CreateOrganization;
using SecureFlow.Modules.Organizations.Application.GetOrganization;
using SecureFlow.Modules.Organizations.Application.ListOrganizations;
using SecureFlow.Modules.Organizations.Presentation.Contracts;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations;

public static class OrganizationsModule
{
    public static IServiceCollection AddOrganizationsModule<TDbContext>(this IServiceCollection services)
        where TDbContext : class, IOrganizationsDbContext
    {
        services.AddScoped<IOrganizationsDbContext>(provider =>
            provider.GetRequiredService<TDbContext>());

        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<ListOrganizationsHandler>();
        services.AddScoped<GetOrganizationHandler>();
        services.AddScoped<IValidator<CreateOrganizationRequest>, CreateOrganizationRequestValidator>();

        services.TryAddSingleton<IClock, SystemClock>();

        return services;
    }
}
