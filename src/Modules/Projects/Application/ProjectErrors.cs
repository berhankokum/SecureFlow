using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Projects.Application;

public static class ProjectErrors
{
    public static readonly Error NotFound =
        new("Projects.NotFound", "Project bulunamadı veya erişim yetkiniz yok.");
    public static readonly Error DuplicateKey =
        new("Projects.DuplicateKey", "Project key bu Organization içinde zaten kullanılıyor.");
    public static readonly Error OrganizationArchived =
        new("Projects.OrganizationArchived", "Archive edilmiş Organization içinde Project oluşturulamaz.");
    public static readonly Error Archived =
        new("Projects.Archived", "Archive edilmiş Project üzerinde bu işlem yapılamaz.");
    public static readonly Error MemberMustBelongToOrganization =
        new("Projects.MemberMustBelongToOrganization", "Kullanıcı aynı Organization'ın aktif üyesi olmalıdır.");
    public static readonly Error ExistingMember =
        new("Projects.ExistingMember", "Kullanıcı zaten Project üyesi.");
    public static readonly Error MemberNotFound =
        new("Projects.MemberNotFound", "Project membership bulunamadı.");
    public static readonly Error ConcurrentMembership =
        new("Projects.ConcurrentMembership", "Project membership eşzamanlı olarak değişti. Lütfen tekrar deneyin.");
}
