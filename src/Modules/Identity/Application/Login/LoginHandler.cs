using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.SharedKernel;
using SecureFlow.Contracts.Audit;

namespace SecureFlow.Modules.Identity.Application.Login;

public sealed class LoginHandler(
    IAuditWriter auditWriter,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<LoginHandler> logger)
{
    private static readonly Error InvalidCredentials =
        new("Identity.InvalidCredentials", "E-posta veya şifre hatalı.");

    public async Task<Result> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(command.Email);

        if (user is null)
        {
            await auditWriter.LoginFailureAsync(cancellationToken);
            logger.LogWarning("Login attempt failed: no user for the given email.");
            return Result.Failure(InvalidCredentials);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            command.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            await auditWriter.LoginFailureAsync(cancellationToken);
            logger.LogWarning("Login attempt failed for user {UserId}.", user.Id);
            return Result.Failure(InvalidCredentials);
        }

        logger.LogInformation("User {UserId} logged in.", user.Id);
        return Result.Success();
    }
}
