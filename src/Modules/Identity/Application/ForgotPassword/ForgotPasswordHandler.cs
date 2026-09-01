using System.Text;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Infrastructure;
using SecureFlow.Modules.Identity.Infrastructure.Email;

namespace SecureFlow.Modules.Identity.Application.ForgotPassword;

public sealed class ForgotPasswordHandler(
    UserManager<ApplicationUser> userManager,
    IIdentityEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<ForgotPasswordHandler> logger)
{
    private static readonly TimeSpan MinimumProcessingTime = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Sonuç kasıtlı olarak sinyal vermez: hesap var/yok bilgisi sızdırılmaz,
    /// controller her koşulda aynı cevabı döner.
    /// </summary>
    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var user = await userManager.FindByEmailAsync(command.Email);
            if (user is null)
            {
                return;
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var resetLink =
                $"{frontendOptions.Value.BaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&code={encodedToken}";

            try
            {
                await emailSender.SendEmailAsync(
                    user.Email!,
                    "SecureFlow - Şifre sıfırlama",
                    $"""Şifrenizi sıfırlamak için <a href="{resetLink}">buraya tıklayın</a>.""",
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Response her koşulda aynı kalır; SMTP durumu hesap varlığını sızdırmaz.
                logger.LogError(exception, "Password reset email delivery failed for user {UserId}.", user.Id);
            }
        }
        finally
        {
            var remaining = MinimumProcessingTime - Stopwatch.GetElapsedTime(startedAt);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }
    }
}
