using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public static class WorkItemErrors
{
    public static readonly Error NotFound = new("WorkItems.NotFound", "WorkItem bulunamadı.");
    public static readonly Error Forbidden = new("WorkItems.Forbidden", "Bu işlem için yetkiniz yok.");
    public static readonly Error ProjectArchived = new(
        "WorkItems.ProjectArchived",
        "Archive edilmiş Project altındaki WorkItem'lar değiştirilemez.");
    public static readonly Error Archived = new("WorkItems.Archived", "Archive edilmiş WorkItem değiştirilemez.");
    public static readonly Error InvalidDueDate = new(
        "WorkItems.InvalidDueDate",
        "DueDate geçmiş bir tarih olamaz.");
    public static readonly Error NumberConflict = new(
        "WorkItems.NumberConflict",
        "WorkItem numarası eşzamanlı bir işlem nedeniyle oluşturulamadı.");
    public static readonly Error NumberExhausted = new(
        "WorkItems.NumberExhausted",
        "Project için WorkItem numara sınırına ulaşıldı.");
}
