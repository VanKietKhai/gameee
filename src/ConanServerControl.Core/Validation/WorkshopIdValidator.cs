using System.Globalization;
using System.Text.RegularExpressions;

namespace ConanServerControl.Core.Validation;

public static class WorkshopIdValidator
{
    private static readonly Regex WorkshopIdPattern = new(@"^\d{1,20}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(string? value, out long workshopId)
    {
        workshopId = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!WorkshopIdPattern.IsMatch(trimmed))
        {
            return false;
        }

        if (!long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed <= 0)
        {
            return false;
        }

        workshopId = parsed;
        return true;
    }

    public static bool IsValid(string? value) => TryParse(value, out _);

    public static string DescribeRule() =>
        "Workshop IDs must be a positive whole number copied from the Steam Workshop page URL (for example 123456789).";
}
