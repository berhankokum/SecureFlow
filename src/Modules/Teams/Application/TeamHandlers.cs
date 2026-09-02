using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Teams.Application.Abstractions;
using SecureFlow.Modules.Teams.Application.Models;
using SecureFlow.Modules.Teams.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Teams.Application;

public sealed record CreateTeamCommand(Guid OrganizationId, string Name, string? Description);
public sealed record UpdateTeamCommand(Guid TeamId, string Name, string? Description);
public sealed record AddTeamMemberCommand(Guid TeamId, Guid UserId, Guid AddedBy);

public sealed class CreateTeamHandler(ITeamsDbContext dbContext, IClock clock)
{
    public async Task<Result<TeamSummary>> HandleAsync(
        CreateTeamCommand command,
        CancellationToken cancellationToken)
    {
        if (await dbContext.IsOrganizationArchivedAsync(command.OrganizationId, cancellationToken))
        {
            return Result.Failure<TeamSummary>(TeamErrors.OrganizationArchived);
        }

        var team = Team.Create(command.OrganizationId, command.Name, command.Description, clock.UtcNow);
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToSummary(team, 0));
    }

    private static TeamSummary ToSummary(Team team, int memberCount) =>
        new(team.Id, team.OrganizationId, team.Name, team.Description, memberCount,
            team.CreatedAt, team.UpdatedAt, team.ArchivedAt);
}

public sealed class ListTeamsHandler(ITeamsDbContext dbContext)
{
    public async Task<IReadOnlyList<TeamSummary>> HandleAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.Teams
            .AsNoTracking()
            .Where(team => team.OrganizationId == organizationId)
            .OrderBy(team => team.ArchivedAt != null)
            .ThenBy(team => team.Name)
            .Select(team => new TeamSummary(
                team.Id,
                team.OrganizationId,
                team.Name,
                team.Description,
                team.Members.Count,
                team.CreatedAt,
                team.UpdatedAt,
                team.ArchivedAt))
            .ToArrayAsync(cancellationToken);
}

public sealed class GetTeamHandler(ITeamsDbContext dbContext, IUserDirectory userDirectory)
{
    public async Task<Result<TeamDetails>> HandleAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams
            .AsNoTracking()
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);

        if (team is null)
        {
            return Result.Failure<TeamDetails>(TeamErrors.NotFound);
        }

        var emails = await userDirectory.GetEmailsByUserIdsAsync(
            team.Members.Select(member => member.UserId).ToArray(),
            cancellationToken);

        var members = team.Members
            .OrderBy(member => member.AddedAt)
            .Select(member => new TeamMemberSummary(
                member.UserId,
                emails.GetValueOrDefault(member.UserId, "Bilinmeyen kullanıcı"),
                member.AddedAt,
                member.AddedBy))
            .ToArray();

        return Result.Success(new TeamDetails(
            team.Id,
            team.OrganizationId,
            team.Name,
            team.Description,
            team.CreatedAt,
            team.UpdatedAt,
            team.ArchivedAt,
            members));
    }
}

public sealed class FindTeamOrganizationHandler(ITeamsDbContext dbContext)
{
    public Task<Guid?> HandleAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => (Guid?)team.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed class UpdateTeamHandler(ITeamsDbContext dbContext, IClock clock)
{
    public async Task<Result> HandleAsync(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.SingleOrDefaultAsync(
            item => item.Id == command.TeamId,
            cancellationToken);

        if (team is null)
        {
            return Result.Failure(TeamErrors.NotFound);
        }

        if (team.IsArchived)
        {
            return Result.Failure(TeamErrors.Archived);
        }

        team.Update(command.Name, command.Description, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ArchiveTeamHandler(ITeamsDbContext dbContext, IClock clock)
{
    public async Task<Result> HandleAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result.Failure(TeamErrors.NotFound);
        }

        team.Archive(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class AddTeamMemberHandler(ITeamsDbContext dbContext, IClock clock)
{
    public async Task<Result> HandleAsync(AddTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == command.TeamId, cancellationToken);

        if (team is null)
        {
            return Result.Failure(TeamErrors.NotFound);
        }

        if (team.IsArchived)
        {
            return Result.Failure(TeamErrors.Archived);
        }

        if (!await dbContext.IsActiveOrganizationMemberAsync(
                team.OrganizationId,
                command.UserId,
                cancellationToken))
        {
            return Result.Failure(TeamErrors.MemberMustBelongToOrganization);
        }

        if (team.Members.Any(member => member.UserId == command.UserId))
        {
            return Result.Failure(TeamErrors.ExistingMember);
        }

        team.AddMember(command.UserId, command.AddedBy, clock.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (IsDuplicateMembership(exception))
        {
            return Result.Failure(TeamErrors.ExistingMember);
        }
    }

    private static bool IsDuplicateMembership(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "PK_TeamMembers",
        };
}

public sealed class RemoveTeamMemberHandler(ITeamsDbContext dbContext)
{
    public async Task<Result> HandleAsync(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);

        if (team is null)
        {
            return Result.Failure(TeamErrors.NotFound);
        }

        if (team.IsArchived)
        {
            return Result.Failure(TeamErrors.Archived);
        }

        if (!team.RemoveMember(userId))
        {
            return Result.Failure(TeamErrors.MemberNotFound);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
