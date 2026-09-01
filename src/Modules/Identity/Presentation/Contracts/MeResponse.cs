namespace SecureFlow.Modules.Identity.Presentation.Contracts;

public sealed record MeResponse(Guid Id, string Email, bool EmailConfirmed);
