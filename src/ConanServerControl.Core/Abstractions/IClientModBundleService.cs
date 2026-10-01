using ConanServerControl.Core.Mods;

namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Exports the server's required mod set for standalone clients (no Steam Workshop assumed):
/// Mods\*.pak + modlist.txt + manifest.json. Never packages Conan game files and never writes
/// into a game client installation.
/// </summary>
public interface IClientModBundleService
{
    /// <summary>Default parent folder for exported bundles (under the application data folder).</summary>
    string DefaultExportRoot { get; }

    /// <summary>
    /// Creates a NEW bundle folder under <paramref name="outputParentDirectory"/> (or
    /// <see cref="DefaultExportRoot"/>). Refuses locations inside the standalone client or the
    /// dedicated server install. The server's live mod files are only read.
    /// </summary>
    Task<ClientModBundleExport> ExportAsync(string? outputParentDirectory = null, CancellationToken cancellationToken = default);

    /// <summary>Read-only: what synchronizing <paramref name="bundleDirectory"/> into a client would change.</summary>
    ClientModSyncPlan PlanClientSync(string bundleDirectory, string clientRoot);
}
