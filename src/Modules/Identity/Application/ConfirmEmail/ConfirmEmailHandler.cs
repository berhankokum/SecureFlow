using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Identity.Application.ConfirmEmail;

public sealed class ConfirmEmailHandler(UserManager<ApplicationUser> userManager)
{
    private static readonly Error InvalidOrExpired =
        new("Identity.ConfirmEmailFailed", "E-posta doğrulanamadı veya bağlantının süresi dolmuş.");

    public async Task<Result> HandleAsync(ConfirmEmailCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
        {
            return Result.Failure(InvalidOrExpired);
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(command.Code));
        }
        catch (FormatException)
        {
            return Result.Failure(InvalidOrExpired);
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedToken);

        return result.Succeeded ? Result.Success() : Result.Failure(InvalidOrExpired);
    }
}
