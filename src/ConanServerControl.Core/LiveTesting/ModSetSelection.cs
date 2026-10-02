namespace ConanServerControl.Core.LiveTesting;

/// <summary>
/// Input rules for the live harness <c>set-mods</c> command, which enables exactly a listed set of catalog mods
/// (in that order) for controlled staging tests. Test/harness functionality only; not a product feature.
/// </summary>
public static class ModSetSelection
{
    /// <summary>
    /// Parses "<c>A.pak,B.pak</c>" (order kept, whitespace trimmed) or "<c>none</c>" (no mods). Rejects empty
    /// input, names that are not plain <c>.pak</c> file names, and duplicates.
    /// </summary>
    public static IReadOnlyList<string> Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            throw new ArgumentException("A mod set is required: a comma-separated list of .pak names, or 'none'.", nameof(spec));
        }

        if (spec.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var names = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var name in names)
        {
            if (!name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':']) >= 0)
            {
                throw new ArgumentException($"Not a plain .pak file name: '{name}'.", nameof(spec));
            }
        }

        var duplicate = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Listed more than once: '{duplicate.Key}'.", nameof(spec));
        }

        return names;
    }

    /// <summary>Requested names that are not in the catalog (case-insensitive).</summary>
    public static IReadOnlyList<string> Unknown(IEnumerable<string> requested, IEnumerable<string> catalogFileNames)
    {
        var catalog = catalogFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requested.Where(name => !catalog.Contains(name)).ToArray();
    }
}
