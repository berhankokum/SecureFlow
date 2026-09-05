namespace SecureFlow.Api.Realtime;

internal static class RealtimeGroupNames
{
    public static string User(Guid userId) => $"user:{userId:N}";

    public static string Organization(Guid organizationId) => $"organization:{organizationId:N}";

    public static string Project(Guid projectId) => $"project:{projectId:N}";
}
