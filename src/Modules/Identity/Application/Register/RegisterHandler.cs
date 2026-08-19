using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Infrastructure;
using SecureFlow.Modules.Identity.Infrastructure.Email;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Identity.Application.Register;

public sealed class RegisterHandler(
    UserManager<ApplicationUser> userManager,
    IIdentityEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions)
{
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
            var message = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return Result.Failure(new Error("Identity.RegisterFailed", message));
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var confirmationLink =
            $"{frontendOptions.Value.BaseUrl}/confirm-email?userId={user.Id}&code={encodedToken}";

        await emailSender.SendEmailAsync(
            user.Email!,
            "SecureFlow - E-postanızı doğrulayın",
            $"""Hesabınızı doğrulamak için <a href="{confirmationLink}">buraya tıklayın</a>.""",
            cancellationToken);

        return Result.Success();
    }
}
