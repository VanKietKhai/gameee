using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;

namespace ConanServerControl.Core.Mods;

/// <summary>
/// Source-agnostic mod identity. Workshop mods are keyed by Workshop ID; Local mods by their
/// .pak file name, which is also their identity in modlist.txt and the server Mods folder.
/// </summary>
public static class ModKeys
{
    public const string WorkshopPrefix = "workshop:";
    public const string LocalPrefix = "local:";

    public static string For(WorkshopMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return mod.SourceType == ModSourceType.Local
            ? Local(mod.LocalFileName ?? string.Empty)
            : Workshop(mod.WorkshopId);
    }

    public static string Workshop(long workshopId) => WorkshopPrefix + workshopId;

    public static string Local(string pakFileName) => LocalPrefix + pakFileName;

    public static bool Matches(WorkshopMod mod, string key) =>
        string.Equals(For(mod), key, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Interprets what an administrator typed: a Workshop ID (digits) or a local .pak file name.
    /// </summary>
    public static bool TryParseUserInput(string? text, out string key)
    {
        key = string.Empty;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith(WorkshopPrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            key = trimmed;
            return true;
        }

        if (WorkshopIdValidator.TryParse(trimmed, out var id))
        {
            key = Workshop(id);
            return true;
        }

        if (trimmed.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) && PathValidator.IsSafeRelativeName(trimmed))
        {
            key = Local(trimmed);
            return true;
        }

        return false;
    }

    public static string Describe(WorkshopMod mod) =>
        mod.SourceType == ModSourceType.Local
            ? $"LOCAL (manual update) {mod.LocalFileName}"
            : $"Workshop ID {mod.WorkshopId}";
}

/// <summary>
/// A local .pak copied into isolated application staging and hashed. The administrator's
/// source file is only ever read.
/// </summary>
public sealed record StagedLocalPak(
    string SourcePath,
    string FileName,
    string StagedPath,
    string StagingDirectory,
    long SizeBytes,
    string Sha256);
