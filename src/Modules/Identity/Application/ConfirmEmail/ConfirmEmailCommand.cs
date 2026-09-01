namespace SecureFlow.Modules.Identity.Application.ConfirmEmail;

public sealed record ConfirmEmailCommand(Guid UserId, string Code);
