using Microsoft.AspNetCore.Identity;
using SecureFlow.Modules.Identity.Domain;

namespace SecureFlow.Modules.Identity.Application.Logout;

public sealed class LogoutHandler(SignInManager<ApplicationUser> signInManager)
{
    public Task HandleAsync() => signInManager.SignOutAsync();
}
