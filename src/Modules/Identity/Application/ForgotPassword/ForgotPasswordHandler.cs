using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Infrastructure;
using SecureFlow.Modules.Identity.Infrastructure.Email;

namespace SecureFlow.Modules.Identity.Application.ForgotPassword;

public sealed class ForgotPasswordHandler(
    UserManager<ApplicationUser> userManager,
    IIdentityEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions)
{
    /// <summary>
    /// Sonuç kasıtlı olarak sinyal vermez: hesap var/yok bilgisi sızdırılmaz,
    /// controller her koşulda aynı cevabı döner.
    /// </summary>
    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
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

        await emailSender.SendEmailAsync(
            user.Email!,
            "SecureFlow - Şifre sıfırlama",
            $"""Şifrenizi sıfırlamak için <a href="{resetLink}">buraya tıklayın</a>.""",
            cancellationToken);
    }
}
