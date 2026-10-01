using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Mods;

/// <summary>
/// Builds Conan dedicated server modlist.txt content.
/// Load order in the UI is the server load order: first line loads first.
/// </summary>
public static class ModListGenerator
{
    public static string Generate(IEnumerable<WorkshopMod> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);

        var lines = mods
            .Where(m => m.Enabled)
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToModListLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return string.Join(Environment.NewLine, lines);
    }

    public static IReadOnlyList<string> Parse(string? modListContent)
    {
        if (string.IsNullOrWhiteSpace(modListContent))
        {
            return Array.Empty<string>();
        }

        return modListContent
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("//"))
            .ToArray();
    }

    public static IReadOnlyList<WorkshopMod> Reorder(IReadOnlyList<WorkshopMod> mods, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(mods);
        if (fromIndex < 0 || fromIndex >= mods.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        }

        if (toIndex < 0 || toIndex >= mods.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        }

        var list = mods.OrderBy(m => m.LoadOrder).ToList();
        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(toIndex, item);
        ApplySequentialOrder(list);
        return list;
    }

    public static void ApplySequentialOrder(IList<WorkshopMod> mods)
    {
        for (var i = 0; i < mods.Count; i++)
        {
            mods[i].LoadOrder = i + 1;
        }
    }

    private static string ToModListLine(WorkshopMod mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.LocalFileName))
        {
            return Path.GetFileName(mod.LocalFileName);
        }

        if (!string.IsNullOrWhiteSpace(mod.Name))
        {
            var candidate = mod.Name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)
                ? mod.Name
                : mod.Name + ".pak";
            return candidate;
        }

        return string.Empty;
    }
}
