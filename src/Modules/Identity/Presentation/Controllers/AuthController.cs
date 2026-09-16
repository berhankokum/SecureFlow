using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Identity.Application.ConfirmEmail;
using SecureFlow.Modules.Identity.Application.ForgotPassword;
using SecureFlow.Modules.Identity.Application.Login;
using SecureFlow.Modules.Identity.Application.Logout;
using SecureFlow.Modules.Identity.Application.Register;
using SecureFlow.Modules.Identity.Application.ResetPassword;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Presentation.Contracts;

namespace SecureFlow.Modules.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterHandler registerHandler,
    LoginHandler loginHandler,
    LogoutHandler logoutHandler,
    ConfirmEmailHandler confirmEmailHandler,
    ForgotPasswordHandler forgotPasswordHandler,
    ResetPasswordHandler resetPasswordHandler,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ConfirmEmailRequest> confirmEmailValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth-email")]
    [AllowAnonymous]
    [ValidateAntiforgery]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(registerValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await registerHandler.HandleAsync(new RegisterCommand(request.Email, request.Password), cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        switch (result.Error.Code)
        {
            case "Identity.DuplicateEmail":
                ModelState.AddModelError(nameof(request.Email), result.Error.Message);
                return ValidationProblem(ModelState);

            case "Identity.InvalidPassword":
                ModelState.AddModelError(nameof(request.Password), result.Error.Message);
                return ValidationProblem(ModelState);

            default:
                return Problem(title: result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ValidateAntiforgery]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(loginValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await loginHandler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : Problem(title: result.Error.Message, statusCode: StatusCodes.Status401Unauthorized);
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiforgery]
    public async Task<IActionResult> Logout()
    {
        await logoutHandler.HandleAsync();
        return NoContent();
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ValidateAntiforgery]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(confirmEmailValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await confirmEmailHandler.HandleAsync(
            new ConfirmEmailCommand(request.UserId, request.Code), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : Problem(title: result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-email")]
    [AllowAnonymous]
    [ValidateAntiforgery]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(forgotPasswordValidator, request) is { } validationError)
        {
            return validationError;
        }

        await forgotPasswordHandler.HandleAsync(new ForgotPasswordCommand(request.Email), cancellationToken);

        // Hesabın var olup olmadığı sızdırılmaz: koşulsuz aynı cevap.
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ValidateAntiforgery]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(resetPasswordValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await resetPasswordHandler.HandleAsync(
            new ResetPasswordCommand(request.Email, request.Code, request.NewPassword), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : Problem(title: result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
    }
}
