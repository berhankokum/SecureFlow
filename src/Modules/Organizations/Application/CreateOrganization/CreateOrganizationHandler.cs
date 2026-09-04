using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.SharedKernel;
using SecureFlow.Contracts.Organizations;

namespace SecureFlow.Modules.Organizations.Application.CreateOrganization;

public sealed class CreateOrganizationHandler(
    IOrganizationsDbContext dbContext,
    IClock clock,
    IEnumerable<IOrganizationCreationProvisioner> provisioners)
{
    private static readonly Error DuplicateSlug =
        new("Organizations.DuplicateSlug", "Bu isimden üretilen organization adresi zaten kullanılıyor.");

    public async Task<Result<OrganizationSummary>> HandleAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        var slug = OrganizationSlugGenerator.Generate(command.Name);
        var slugExists = await dbContext.Organizations
            .AnyAsync(organization => organization.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Result.Failure<OrganizationSummary>(DuplicateSlug);
        }

        var now = clock.UtcNow;
        var organization = Organization.Create(
            command.Name,
            slug,
            command.LogoUrl,
            command.CreatorUserId,
            now);

        dbContext.Organizations.Add(organization);

        foreach (var provisioner in provisioners)
        {
            await provisioner.ProvisionAsync(organization.Id, now, cancellationToken);
        }

        try
        {
            // Organization ve creator OWNER membership tek database transaction'ında yazılır.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateSlug(exception))
        {
            return Result.Failure<OrganizationSummary>(DuplicateSlug);
        }

        return Result.Success(new OrganizationSummary(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.LogoUrl,
            OrganizationRole.Owner.ToString().ToUpperInvariant(),
            OrganizationAccessRules.GetPermissions(OrganizationRole.Owner),
            organization.CreatedAt,
            organization.ArchivedAt));
    }

    private static bool IsDuplicateSlug(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_Organizations_Slug",
        };
}
