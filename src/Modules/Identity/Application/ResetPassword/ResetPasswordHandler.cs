using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.SharedKernel;
using SecureFlow.Contracts.Audit;

namespace SecureFlow.Modules.Identity.Application.ResetPassword;

public sealed class ResetPasswordHandler(
    IAuditWriter auditWriter,
    UserManager<ApplicationUser> userManager,
    ILogger<ResetPasswordHandler> logger)
{
    private static readonly Error GenericFailure =
        new("Identity.ResetPasswordFailed", "Şifre sıfırlanamadı veya bağlantının süresi dolmuş.");

    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            return Result.Failure(GenericFailure);
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(command.Code));
        }
        catch (FormatException)
        {
            return Result.Failure(GenericFailure);
        }

        auditWriter.StagePasswordChanged(user.Id);
        var result = await userManager.ResetPasswordAsync(user, decodedToken, command.NewPassword);

        if (!result.Succeeded)
        {
            return Result.Failure(GenericFailure);
        }

        logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
        return Result.Success();
    }
}
