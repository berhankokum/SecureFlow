using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Files.Application;

public static class FileErrors
{
    public static readonly Error NotFound = new("Files.NotFound", "Attachment bulunamadı.");
    public static readonly Error Forbidden = new("Files.Forbidden", "Bu işlem için yetkiniz yok.");
    public static readonly Error ProjectArchived = new("Files.ProjectArchived", "Archive edilmiş Project değiştirilemez.");
    public static readonly Error WorkItemArchived = new("Files.WorkItemArchived", "Archive edilmiş WorkItem değiştirilemez.");
    public static readonly Error Empty = new("Files.Empty", "Boş dosya yüklenemez.");
    public static readonly Error TooLarge = new("Files.TooLarge", "Dosya boyutu 10 MiB sınırını aşamaz.");
    public static readonly Error InvalidFileName = new("Files.InvalidFileName", "Dosya adı geçersiz.");
    public static readonly Error UnsupportedType = new("Files.UnsupportedType", "Yalnız PDF, PNG ve JPEG dosyaları yüklenebilir.");
    public static readonly Error SignatureMismatch = new("Files.SignatureMismatch", "Dosya uzantısı, içerik türü ve imzası eşleşmiyor.");
    public static readonly Error StorageUnavailable = new("Files.StorageUnavailable", "Dosya depolama servisi şu anda kullanılamıyor.");
}
