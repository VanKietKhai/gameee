using ConanServerControl.Core.Models;
using ConanServerControl.Core.Mods;

namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Source-agnostic mod catalog operations (Workshop and Local) keyed by <see cref="ModKeys"/>,
/// plus the Local mod import path, which never requires SteamCMD.
/// </summary>
public interface IModCatalogService
{
    IReadOnlyList<WorkshopMod> Mods { get; }

    /// <summary>
    /// Validates the administrator's .pak (exists, .pak extension, non-zero size), COPIES it into
    /// isolated application staging and computes SHA-256. Never touches the live server or the
    /// source file. Safe to call before taking the server action gate.
    /// </summary>
    Task<StagedLocalPak> StageLocalPakAsync(string sourcePakPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a staged local .pak into the server Mods folder through the transactional mod
    /// commit (rollback copy, verified replacement, rollback on failure) and rewrites modlist.txt.
    /// <paramref name="replaceModKey"/> null imports a new Local mod; otherwise it is a manual
    /// update of that existing Local mod. Does not acquire the gate: the caller must hold it.
    /// </summary>
    Task<WorkshopMod> CommitLocalPakAsync(
        StagedLocalPak staged,
        string? replaceModKey,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the staging copy. Never touches the source file.</summary>
    void DiscardStagedLocalPak(StagedLocalPak staged);

    Task RemoveAsync(string modKey, bool confirmed, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string modKey, bool enabled, CancellationToken cancellationToken = default);

    Task MoveAsync(string modKey, int newIndex, CancellationToken cancellationToken = default);
}
