using System.Security.Cryptography;
using System.Text;

namespace SecureFlow.Modules.Notifications.Application;

public static class ReminderEventId
{
    public static Guid Create(Guid workItemId, DateOnly dueDate)
    {
        var input = Encoding.UTF8.GetBytes($"secureflow:due-reminder:{workItemId:N}:{dueDate:yyyy-MM-dd}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        Span<byte> value = hash[..16];
        value[7] = (byte)((value[7] & 0x0F) | 0x50);
        value[8] = (byte)((value[8] & 0x3F) | 0x80);
        return new Guid(value);
    }
}
