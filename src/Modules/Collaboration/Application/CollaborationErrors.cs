using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration.Application;

public static class CollaborationErrors
{
    public static readonly Error NotFound = new(
        "Collaboration.NotFound",
        "WorkItem veya yorum bulunamadı.");
    public static readonly Error Forbidden = new(
        "Collaboration.Forbidden",
        "Bu işlem için yetkiniz yok.");
    public static readonly Error ProjectArchived = new(
        "Collaboration.ProjectArchived",
        "Archive edilmiş Project altında yorum değiştirilemez.");
    public static readonly Error WorkItemArchived = new(
        "Collaboration.WorkItemArchived",
        "Archive edilmiş WorkItem üzerinde yorum değiştirilemez.");
    public static readonly Error CommentDeleted = new(
        "Collaboration.CommentDeleted",
        "Silinmiş yorum değiştirilemez.");
}
