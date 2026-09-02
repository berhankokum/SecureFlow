using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Teams.Application;

public static class TeamErrors
{
    public static readonly Error NotFound =
        new("Teams.NotFound", "Team bulunamadı veya erişim yetkiniz yok.");

    public static readonly Error OrganizationArchived =
        new("Teams.OrganizationArchived", "Archive edilmiş Organization içinde Team oluşturulamaz.");

    public static readonly Error Archived =
        new("Teams.Archived", "Archive edilmiş Team üzerinde bu işlem yapılamaz.");

    public static readonly Error MemberMustBelongToOrganization =
        new("Teams.MemberMustBelongToOrganization", "Kullanıcı aynı Organization'ın aktif üyesi olmalıdır.");

    public static readonly Error ExistingMember =
        new("Teams.ExistingMember", "Kullanıcı zaten Team üyesi.");

    public static readonly Error MemberNotFound =
        new("Teams.MemberNotFound", "Team membership bulunamadı.");
}
