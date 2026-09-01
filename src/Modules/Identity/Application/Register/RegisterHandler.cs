using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Infrastructure;
using SecureFlow.Modules.Identity.Infrastructure.Email;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Identity.Application.Register;

public sealed class RegisterHandler(
    UserManager<ApplicationUser> userManager,
    IIdentityEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<RegisterHandler> logger)
{
    private static readonly Error DuplicateEmail =
        new("Identity.DuplicateEmail", "Bu e-posta adresi zaten kayıtlı.");

    private static readonly Error InvalidPassword =
        new("Identity.InvalidPassword", "Şifre güvenlik gereksinimlerini karşılamıyor.");

    private static readonly Error RegistrationFailed =
        new("Identity.RegisterFailed", "Hesap oluşturulamadı.");

    private static readonly Error EmailDeliveryFailed =
        new("Identity.EmailDeliveryFailed", "Doğrulama e-postası gönderilemedi. Lütfen tekrar deneyin.");

    public async Task<Result> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
        };

        var createResult = await userManager.CreateAsync(user, command.Password);

        if (!createResult.Succeeded)
        {
            return Result.Failure(MapCreateError(createResult.Errors));
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var confirmationLink =
            $"{frontendOptions.Value.BaseUrl}/confirm-email?userId={user.Id}&code={encodedToken}";

        try
        {
            await emailSender.SendEmailAsync(
                user.Email!,
                "SecureFlow - E-postanızı doğrulayın",
                $"""Hesabınızı doğrulamak için <a href="{confirmationLink}">buraya tıklayın</a>.""",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Confirmation email delivery failed for new user {UserId}.", user.Id);

            var rollbackResult = await userManager.DeleteAsync(user);
            if (!rollbackResult.Succeeded)
            {
                logger.LogCritical(
                    "Registration rollback failed for user {UserId}. Errors: {ErrorCodes}",
                    user.Id,
                    rollbackResult.Errors.Select(error => error.Code));
            }

            return Result.Failure(EmailDeliveryFailed);
        }

        return Result.Success();
    }

    private static Error MapCreateError(IEnumerable<IdentityError> errors)
    {
        var errorCodes = errors.Select(error => error.Code).ToHashSet(StringComparer.Ordinal);

        if (errorCodes.Contains("DuplicateEmail") || errorCodes.Contains("DuplicateUserName"))
        {
            return DuplicateEmail;
        }

        if (errorCodes.Any(code => code.StartsWith("Password", StringComparison.Ordinal)))
        {
            return InvalidPassword;
        }

        return RegistrationFailed;
    }
}
