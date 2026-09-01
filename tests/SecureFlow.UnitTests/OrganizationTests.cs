using SecureFlow.Modules.Organizations.Application.CreateOrganization;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.UnitTests;

public class OrganizationTests
{
    [Fact]
    public void Create_adds_creator_as_the_only_active_owner()
    {
        var creatorUserId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

        var organization = Organization.Create(
            "  ACME Software  ",
            "acme-software",
            null,
            creatorUserId,
            createdAt);

        Assert.Equal("ACME Software", organization.Name);
        Assert.Equal("acme-software", organization.Slug);
        Assert.Equal(creatorUserId, organization.CreatedBy);
        Assert.Equal(createdAt, organization.CreatedAt);
        Assert.Equal(createdAt, organization.UpdatedAt);
        Assert.False(organization.IsArchived);

        var owner = Assert.Single(organization.Members);
        Assert.Equal(creatorUserId, owner.UserId);
        Assert.Equal(OrganizationRole.Owner, owner.Role);
        Assert.True(owner.IsActive);
        Assert.Null(owner.InvitedBy);
    }

    [Theory]
    [InlineData("ACME Software", "acme-software")]
    [InlineData("  Güvenli İş Akışı  ", "guvenli-is-akisi")]
    [InlineData("SecureFlow -- Türkiye", "secureflow-turkiye")]
    public void Slug_generator_produces_stable_ascii_slugs(string name, string expected)
    {
        Assert.Equal(expected, OrganizationSlugGenerator.Generate(name));
    }

    [Fact]
    public void Create_rejects_an_empty_creator_id()
    {
        Assert.Throws<ArgumentException>(() => Organization.Create(
            "ACME",
            "acme",
            null,
            Guid.Empty,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Slug_generator_never_exceeds_the_database_limit()
    {
        var slug = OrganizationSlugGenerator.Generate($"{new string('a', 99)} Z");

        Assert.True(slug.Length <= 100);
        Assert.False(slug.EndsWith('-'));
    }
}
