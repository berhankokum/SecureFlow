using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Modules.Identity.Application.ConfirmEmail;
using SecureFlow.Modules.Identity.Application.ForgotPassword;
using SecureFlow.Modules.Identity.Application.Login;
using SecureFlow.Modules.Identity.Application.Logout;
using SecureFlow.Modules.Identity.Application.Register;
using SecureFlow.Modules.Identity.Application.ResetPassword;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Infrastructure;
using SecureFlow.Modules.Identity.Infrastructure.Email;
using SecureFlow.Modules.Identity.Presentation.Contracts;

namespace SecureFlow.Modules.Identity;

/// <summary>
/// Identity modülünün kendi DI kaydını üstlenir; composition root (Api) yalnızca
/// hangi DbContext'in kullanılacağını (tek, paylaşılan SecureFlowDbContext) bildirir.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : DbContext
    {
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));

        services.AddTransient<IIdentityEmailSender, SmtpIdentityEmailSender>();

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ConfirmEmailHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();

        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<ConfirmEmailRequest>, ConfirmEmailRequestValidator>();
        services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true; // BR-ID-001

                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;

                // Email confirmation desteklenir (BR-ID-003) ama FAZ 2 kapsamında login'i bloklamaz.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<TDbContext>()
            .AddDefaultTokenProviders() // BR-ID-004: süreli, tek kullanımlık, tahmin edilemez token'lar
            .AddSignInManager();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "SecureFlow.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        return services;
    }
}
