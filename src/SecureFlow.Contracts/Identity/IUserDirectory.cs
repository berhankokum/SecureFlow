namespace SecureFlow.Contracts.Identity;

public sealed record UserDirectoryEntry(Guid Id, string Email);

/// <summary>
/// Identity verisini başka modüllere entity veya UserManager sızdırmadan sunar.
/// </summary>
public interface IUserDirectory
{
    Task<UserDirectoryEntry?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserDirectoryEntry?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetEmailsByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
