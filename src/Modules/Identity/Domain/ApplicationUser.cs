using Microsoft.AspNetCore.Identity;

namespace SecureFlow.Modules.Identity.Domain;

/// <summary>
/// SecureFlow-Final.md §7: authentication ASP.NET Core Identity üzerinden yürütülür.
/// BR-ID-001..004 (unique/normalized email, email confirmation, güvenli reset token)
/// büyük ölçüde Identity'nin kendi altyapısı tarafından karşılanır.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
