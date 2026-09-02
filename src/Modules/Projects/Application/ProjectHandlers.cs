using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Application.Authorization;
using SecureFlow.Modules.Projects.Application.Models;
using SecureFlow.Modules.Projects.Domain;
using SecureFlow.SharedKernel;
using System.Data;

namespace SecureFlow.Modules.Projects.Application;

public sealed record CreateProjectCommand(
    Guid OrganizationId,
    string Key,
    string Name,
    string? Description,
    Guid CreatedBy);

public sealed record UpdateProjectCommand(Guid ProjectId, string Name, string? Description);
public sealed record AddProjectMemberCommand(Guid ProjectId, Guid UserId, ProjectRole Role);
public sealed record ChangeProjectMemberRoleCommand(Guid ProjectId, Guid UserId, ProjectRole Role);

internal static class ProjectModelMapper
{
    public static ProjectSummary ToSummary(
        Project project,
        PermissionRole organizationRole,
        ProjectRole? projectRole) =>
        new(
            project.Id,
            project.OrganizationId,
            project.Key,
            project.Name,
            project.Description,
            project.CreatedBy,
            project.CreatedAt,
            project.UpdatedAt,
            project.ArchivedAt,
            projectRole?.ToString().ToUpperInvariant(),
            ProjectAuthorizationEvaluator.GetPermissions(organizationRole, projectRole));
}

public sealed class CreateProjectHandler(IProjectsDbContext dbContext, IClock clock)
{
    public async Task<Result<ProjectSummary>> HandleAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        if (await dbContext.IsOrganizationArchivedAsync(command.OrganizationId, cancellationToken))
        {
            return Result.Failure<ProjectSummary>(ProjectErrors.OrganizationArchived);
        }

        var organizationRole = await dbContext.GetActiveOrganizationRoleAsync(
            command.OrganizationId,
            command.CreatedBy,
            cancellationToken);
        if (organizationRole is null)
        {
            return Result.Failure<ProjectSummary>(ProjectErrors.NotFound);
        }

        var project = Project.Create(
            command.OrganizationId,
            command.Key,
            command.Name,
            command.Description,
            command.CreatedBy,
            clock.UtcNow);
        dbContext.Projects.Add(project);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            return Result.Failure<ProjectSummary>(ProjectErrors.DuplicateKey);
        }

        return Result.Success(ProjectModelMapper.ToSummary(
            project,
            organizationRole.Value,
            ProjectRole.Manager));
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_Projects_OrganizationId_Key",
        };
}

public sealed class ListProjectsHandler(IProjectsDbContext dbContext)
{
    public async Task<Result<IReadOnlyList<ProjectSummary>>> HandleAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var organizationRole = await dbContext.GetActiveOrganizationRoleAsync(
            organizationId,
            currentUserId,
            cancellationToken);
        if (organizationRole is null)
        {
            return Result.Failure<IReadOnlyList<ProjectSummary>>(ProjectErrors.NotFound);
        }

        var canSeeEveryProject = organizationRole is PermissionRole.Owner or PermissionRole.Admin;
        var rows = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId)
            .Select(project => new
            {
                Project = project,
                ProjectRole = project.Members
                    .Where(member => member.UserId == currentUserId)
                    .Select(member => (ProjectRole?)member.Role)
                    .SingleOrDefault(),
            })
            .Where(row => canSeeEveryProject || row.ProjectRole != null)
            .OrderBy(row => row.Project.ArchivedAt != null)
            .ThenBy(row => row.Project.Name)
            .ToArrayAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProjectSummary>>(rows
            .Select(row => ProjectModelMapper.ToSummary(
                row.Project,
                organizationRole.Value,
                row.ProjectRole))
            .ToArray());
    }
}

public sealed class GetProjectHandler(
    IProjectsDbContext dbContext,
    IUserDirectory userDirectory,
    ProjectAuthorizationEvaluator authorizationEvaluator)
{
    public async Task<Result<ProjectDetails>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ProjectDetails>(ProjectErrors.NotFound);
        }

        var evaluation = await authorizationEvaluator.EvaluateAsync(
            project.Id,
            project.OrganizationId,
            currentUserId,
            cancellationToken);
        if (evaluation?.HasProjectAccess != true)
        {
            return Result.Failure<ProjectDetails>(ProjectErrors.NotFound);
        }

        var emails = await userDirectory.GetEmailsByUserIdsAsync(
            project.Members.Select(member => member.UserId).ToArray(),
            cancellationToken);
        var members = project.Members
            .OrderBy(member => emails.GetValueOrDefault(member.UserId, string.Empty))
            .Select(member => new ProjectMemberSummary(
                member.UserId,
                emails.GetValueOrDefault(member.UserId, "Bilinmeyen kullanıcı"),
                member.Role.ToString().ToUpperInvariant()))
            .ToArray();

        return Result.Success(new ProjectDetails(
            project.Id,
            project.OrganizationId,
            project.Key,
            project.Name,
            project.Description,
            project.CreatedBy,
            project.CreatedAt,
            project.UpdatedAt,
            project.ArchivedAt,
            evaluation.ProjectRole?.ToString().ToUpperInvariant(),
            evaluation.Permissions,
            members));
    }
}

public sealed class FindProjectOrganizationHandler(IProjectsDbContext dbContext)
{
    public Task<Guid?> HandleAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => (Guid?)project.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed class UpdateProjectHandler(IProjectsDbContext dbContext, IClock clock)
{
    public async Task<Result> HandleAsync(UpdateProjectCommand command, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(
            item => item.Id == command.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        if (project.IsArchived)
        {
            return Result.Failure(ProjectErrors.Archived);
        }

        project.Update(command.Name, command.Description, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ArchiveProjectHandler(IProjectsDbContext dbContext, IClock clock)
{
    public async Task<Result> HandleAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(
            item => item.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        project.Archive(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class AddProjectMemberHandler(IProjectsDbContext dbContext)
{
    public async Task<Result> HandleAsync(AddProjectMemberCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var project = await LoadAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        if (project.IsArchived)
        {
            return Result.Failure(ProjectErrors.Archived);
        }

        if (!await dbContext.IsActiveOrganizationMemberAsync(
                project.OrganizationId,
                command.UserId,
                cancellationToken))
        {
            return Result.Failure(ProjectErrors.MemberMustBelongToOrganization);
        }

        if (project.Members.Any(member => member.UserId == command.UserId))
        {
            return Result.Failure(ProjectErrors.ExistingMember);
        }

        project.AddMember(command.UserId, command.Role);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (IsDuplicateMembership(exception))
        {
            return Result.Failure(ProjectErrors.ExistingMember);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            return Result.Failure(ProjectErrors.ConcurrentMembership);
        }
    }

    private Task<Project?> LoadAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);

    private static bool IsDuplicateMembership(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "PK_ProjectMembers",
        };

    private static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class ChangeProjectMemberRoleHandler(IProjectsDbContext dbContext)
{
    public async Task<Result> HandleAsync(
        ChangeProjectMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        if (project.IsArchived)
        {
            return Result.Failure(ProjectErrors.Archived);
        }

        if (!await dbContext.IsActiveOrganizationMemberAsync(
                project.OrganizationId,
                command.UserId,
                cancellationToken))
        {
            return Result.Failure(ProjectErrors.MemberMustBelongToOrganization);
        }

        if (!project.ChangeMemberRole(command.UserId, command.Role))
        {
            return Result.Failure(ProjectErrors.MemberNotFound);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class RemoveProjectMemberHandler(IProjectsDbContext dbContext)
{
    public async Task<Result> HandleAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        if (project.IsArchived)
        {
            return Result.Failure(ProjectErrors.Archived);
        }

        if (!project.RemoveMember(userId))
        {
            return Result.Failure(ProjectErrors.MemberNotFound);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
