using System.Text;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Domain;

public sealed class Label : AggregateRoot
{
    public const int NameMaxLength = 50;

    private Label() { }

    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Label Create(Guid organizationId, Guid projectId, string name, Guid createdBy, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new ArgumentException("Label scope and creator cannot be empty.");
        }

        var label = new Label
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ProjectId = projectId,
            CreatedBy = createdBy,
            CreatedAt = now,
        };
        label.Update(name, now);
        return label;
    }

    public void Update(string name, DateTimeOffset now)
    {
        Name = CanonicalizeName(name);
        NormalizedName = Name.ToUpperInvariant();
        UpdatedAt = now;
    }

    public static string CanonicalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var canonical = name.Normalize(NormalizationForm.FormKC).Trim();
        if (string.IsNullOrWhiteSpace(canonical) || canonical.Length > NameMaxLength)
        {
            throw new ArgumentException($"Label name must contain 1–{NameMaxLength} characters.", nameof(name));
        }

        return canonical;
    }

    public static bool IsValidName(string? name)
    {
        try { CanonicalizeName(name!); return true; }
        catch (ArgumentException) { return false; }
    }
}
