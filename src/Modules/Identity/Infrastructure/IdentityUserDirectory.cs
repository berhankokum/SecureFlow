using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Identity.Domain;

namespace SecureFlow.Modules.Identity.Infrastructure;

public sealed class IdentityUserDirectory(UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<UserDirectoryEntry?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new UserDirectoryEntry(item.Id, item.Email!))
            .SingleOrDefaultAsync(cancellationToken);

        return user;
    }

    public async Task<UserDirectoryEntry?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());

        return await userManager.Users
            .AsNoTracking()
            .Where(item => item.NormalizedEmail == normalizedEmail)
            .Select(item => new UserDirectoryEntry(item.Id, item.Email!))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await userManager.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email!, cancellationToken);
    }
}
