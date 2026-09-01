using System.Globalization;
using System.Text;

namespace SecureFlow.Modules.Organizations.Application.CreateOrganization;

public static class OrganizationSlugGenerator
{
    private const int MaximumLength = 100;

    public static string Generate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(normalized.Length);
        var separatorPending = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var asciiCharacter = ToAscii(char.ToLowerInvariant(character));
            if (asciiCharacter is not null && char.IsAsciiLetterOrDigit(asciiCharacter.Value))
            {
                if (separatorPending && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(asciiCharacter.Value);
                separatorPending = false;
            }
            else
            {
                separatorPending = true;
            }

            if (slug.Length >= MaximumLength)
            {
                break;
            }
        }

        var result = slug.ToString().Trim('-');
        if (result.Length > MaximumLength)
        {
            result = result[..MaximumLength].TrimEnd('-');
        }

        return string.IsNullOrEmpty(result) ? "organization" : result;
    }

    private static char? ToAscii(char character) => character switch
    {
        'ç' => 'c',
        'ğ' => 'g',
        'ı' => 'i',
        'ö' => 'o',
        'ş' => 's',
        'ü' => 'u',
        >= 'a' and <= 'z' => character,
        >= '0' and <= '9' => character,
        _ => null,
    };
}
