namespace SecureFlow.Modules.Identity.Application.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword);
