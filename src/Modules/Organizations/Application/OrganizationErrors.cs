using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application;

public static class OrganizationErrors
{
    public static readonly Error NotFound =
        new("Organizations.NotFound", "Organization bulunamadı veya erişim yetkiniz yok.");

    public static readonly Error Archived =
        new("Organizations.Archived", "Archive edilmiş organization üzerinde bu işlem yapılamaz.");

    public static readonly Error Forbidden =
        new("Organizations.Forbidden", "Bu işlemi yapma yetkiniz yok.");

    public static readonly Error MemberNotFound =
        new("Memberships.NotFound", "Üye bulunamadı.");

    public static readonly Error ExistingMember =
        new("Invitations.ExistingMember", "Bu e-posta adresi organization'ın aktif bir üyesi.");

    public static readonly Error InvitationNotFound =
        new("Invitations.NotFound", "Invitation bulunamadı.");

    public static readonly Error InvitationExpired =
        new("Invitations.Expired", "Invitation süresi dolmuş.");

    public static readonly Error InvitationRevoked =
        new("Invitations.Revoked", "Invitation revoke edilmiş.");

    public static readonly Error InvitationAlreadyUsed =
        new("Invitations.AlreadyUsed", "Invitation daha önce kullanılmış.");

    public static readonly Error InvitationEmailMismatch =
        new("Invitations.EmailMismatch", "Invitation farklı bir e-posta hesabı için oluşturulmuş.");

    public static readonly Error LastOwner =
        new("Memberships.LastOwner", "Organization'ın son OWNER üyesi kaldırılamaz veya rolü düşürülemez.");

    public static readonly Error EmailDeliveryFailed =
        new("Invitations.EmailDeliveryFailed", "Invitation e-postası gönderilemedi. Lütfen tekrar deneyin.");

    public static readonly Error ConcurrentInvitation =
        new("Invitations.ConcurrentRequest", "Aynı e-posta için eşzamanlı bir invitation oluşturuldu. Lütfen tekrar deneyin.");

    public static readonly Error ConcurrentMembership =
        new("Memberships.ConcurrentRequest", "Membership eşzamanlı olarak değişti. Lütfen tekrar deneyin.");
}
