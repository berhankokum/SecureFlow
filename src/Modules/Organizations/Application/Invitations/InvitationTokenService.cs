using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed record InvitationToken(string RawValue, string Hash);

public static class InvitationTokenService
{
    public static InvitationToken Generate()
    {
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new InvitationToken(rawToken, Hash(rawToken));
    }

    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
