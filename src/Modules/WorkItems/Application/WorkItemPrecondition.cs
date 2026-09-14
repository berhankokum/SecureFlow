using System.Globalization;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public sealed class WorkItemPrecondition
{
    public string? Header { get; set; }
    public static string ETag(uint version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";
    public static readonly Error Stale = new("WorkItems.StaleVersion", "Kayıt başka bir işlemle değişti. Güncel sürümü yükleyip yeniden düzenleyin.");

    public Error? Check(uint version)
    {
        if (string.IsNullOrWhiteSpace(Header))
            return new Error("WorkItems.PreconditionRequired", "If-Match sürüm bilgisi zorunludur.");
        if (Header.Length < 3 || Header[0] != '"' || Header[^1] != '"'
            || !uint.TryParse(Header.AsSpan(1, Header.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var expected)
            || Header != ETag(expected))
            return new Error("WorkItems.InvalidPrecondition", "Tek bir strong ETag gönderilmelidir.");
        return expected == version ? null : Stale;
    }
}
