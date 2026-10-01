using System.Text.Json;
using System.Text.Json.Serialization;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Mods;

/// <summary>
/// Exports the server's enabled mods (in load order) as a Client Mod Bundle for standalone
/// clients. The bundle is assembled in a hidden ".partial" sibling and only renamed into place
/// once every copy has been hash-verified, so a failed export leaves no half-written bundle.
/// </summary>
public sealed class ClientModBundleService : IClientModBundleService
{
    public const string ManifestFileName = "manifest.json";
    public const string ReadmeFileName = "README.txt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISettingsService _settings;
    private readonly IAppPaths _paths;
    private readonly ILogger<ClientModBundleService> _logger;

    public ClientModBundleService(ISettingsService settings, IAppPaths paths, ILogger<ClientModBundleService> logger)
    {
        _settings = settings;
        _paths = paths;
        _logger = logger;
    }

    public string DefaultExportRoot => Path.Combine(_paths.DataDirectory, "client-bundles");

    public async Task<ClientModBundleExport> ExportAsync(string? outputParentDirectory = null, CancellationToken cancellationToken = default)
    {
        var parent = string.IsNullOrWhiteSpace(outputParentDirectory) ? DefaultExportRoot : outputParentDirectory.Trim();
        if (!PathValidator.IsSafeAbsolutePath(parent))
        {
            throw new UserFacingException("Invalid export folder", parent, "Choose an absolute folder for the client mod bundle.");
        }

        parent = PathValidator.NormalizeFullPath(parent);
        foreach (var (label, location) in ProtectedLocations())
        {
            if (PathValidator.Overlaps(parent, location))
            {
                throw new UserFacingException(
                    "Export folder is not allowed",
                    $"{parent} overlaps the {label}:{Environment.NewLine}{location}",
                    "Choose a separate folder (for example on the desktop). Bundles are never written into a game client or the dedicated server install.");
            }
        }

        var install = InstallDirectory() ?? throw new UserFacingException(
            "Server install directory is not configured",
            "The bundle is built from the dedicated server's installed mod files.",
            "Configure the dedicated server in Settings first.");
        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");

        var enabled = _settings.Current.Mods.Mods
            .Where(m => m.Enabled)
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var mod in enabled)
        {
            if (string.IsNullOrWhiteSpace(mod.LocalFileName) || !PathValidator.IsSafeRelativeName(mod.LocalFileName))
            {
                throw new UserFacingException(
                    "A required mod is not installed",
                    $"{ModKeys.Describe(mod)} ({mod.Name}) has no installed .pak file.",
                    "Install or disable that mod, then export again.");
            }

            var live = Path.Combine(modsDir, mod.LocalFileName);
            if (!File.Exists(live) || new FileInfo(live).Length <= 0)
            {
                throw new UserFacingException(
                    "A required mod file is missing on the server",
                    live,
                    "Reinstall that mod on the server, then export again. Nothing was exported.");
            }
        }

        Directory.CreateDirectory(parent);
        var name = UniqueBundleName(parent);
        var final = Path.Combine(parent, name);
        var partial = Path.Combine(parent, "." + name + ".partial");
        var partialMods = Path.Combine(partial, "Mods");
        Directory.CreateDirectory(partialMods);
        try
        {
            var entries = new List<ClientModBundleEntry>(enabled.Length);
            var order = 1;
            foreach (var mod in enabled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var live = Path.Combine(modsDir, mod.LocalFileName!);
                var sourceHash = BackupFileHasher.Sha256File(live);
                var copy = Path.Combine(partialMods, mod.LocalFileName!);
                File.Copy(live, copy, overwrite: false);
                var copyHash = BackupFileHasher.Sha256File(copy);
                if (!string.Equals(sourceHash, copyHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Copy of {mod.LocalFileName} does not match the server file (SHA-256 mismatch).");
                }

                entries.Add(new ClientModBundleEntry(
                    order++,
                    mod.LocalFileName!,
                    new FileInfo(copy).Length,
                    copyHash,
                    mod.SourceType,
                    mod.SourceType == ModSourceType.Workshop && mod.WorkshopId > 0 ? mod.WorkshopId : null,
                    mod.Name));
            }

            var manifest = new ClientModBundleManifest
            {
                CreatedAt = DateTimeOffset.Now,
                Generator = $"{AppConstants.ApplicationName} {typeof(ClientModBundleService).Assembly.GetName().Version}",
                Mods = entries
            };

            var modList = string.Join(Environment.NewLine, entries.Select(e => e.FileName));
            await File.WriteAllTextAsync(Path.Combine(partialMods, AppConstants.ModListFileName), modList, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(partial, ManifestFileName), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(partial, ReadmeFileName), Readme(entries), cancellationToken).ConfigureAwait(false);

            Directory.Move(partial, final);
            _logger.LogInformation("Exported client mod bundle with {Count} mod(s) to {Path}.", entries.Count, final);
            return new ClientModBundleExport(
                final,
                Path.Combine(final, ManifestFileName),
                Path.Combine(final, "Mods", AppConstants.ModListFileName),
                manifest);
        }
        catch
        {
            TryDeletePartial(partial, parent);
            throw;
        }
    }

    public ClientModSyncPlan PlanClientSync(string bundleDirectory, string clientRoot)
    {
        var manifestPath = Path.Combine(bundleDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new UserFacingException("Not a client mod bundle", $"{manifestPath} was not found.", "Choose a folder created by Export Client Bundle.");
        }

        var manifest = JsonSerializer.Deserialize<ClientModBundleManifest>(File.ReadAllText(manifestPath), JsonOptions)
                       ?? throw new UserFacingException("Invalid client mod bundle", manifestPath, "Export the bundle again.");
        return ClientModSyncPlanner.Plan(manifest, clientRoot);
    }

    private IEnumerable<(string Label, string Path)> ProtectedLocations()
    {
        var client = _settings.Current.Client.RootDirectory;
        if (PathValidator.IsSafeAbsolutePath(client))
        {
            yield return ("standalone client", client!);
            var launcher = Path.GetDirectoryName(PathValidator.NormalizeFullPath(client!));
            if (launcher is not null && File.Exists(Path.Combine(launcher, ConanExecutableClassifier.ClientLauncherBatch)))
            {
                yield return ("standalone client launcher folder", launcher);
            }
        }

        var install = InstallDirectory();
        if (PathValidator.IsSafeAbsolutePath(install))
        {
            yield return ("dedicated server install", install!);
        }
    }

    private string? InstallDirectory()
    {
        var install = _settings.Current.ServerPaths.ServerInstallDirectory ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        return string.IsNullOrWhiteSpace(install) ? null : install;
    }

    private static string UniqueBundleName(string parent)
    {
        var stem = $"ConanClientModBundle-{DateTime.Now:yyyyMMdd-HHmmss}";
        var name = stem;
        for (var i = 2; Directory.Exists(Path.Combine(parent, name)) || Directory.Exists(Path.Combine(parent, "." + name + ".partial")); i++)
        {
            name = $"{stem}-{i}";
        }

        return name;
    }

    private void TryDeletePartial(string partial, string parent)
    {
        try
        {
            // Only ever delete the hidden partial folder this export created.
            if (Directory.Exists(partial) &&
                PathValidator.IsUnderRoot(partial, parent) &&
                Path.GetFileName(partial).EndsWith(".partial", StringComparison.Ordinal))
            {
                Directory.Delete(partial, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the partial client mod bundle {Path}.", partial);
        }
    }

    private static string Readme(IReadOnlyList<ClientModBundleEntry> entries)
    {
        var lines = new List<string>
        {
            "Conan Server Control - Client Mod Bundle",
            "",
            "This folder contains ONLY the server's required mod files. It contains no Conan Exiles game files.",
            "You need your own licensed Conan Exiles client.",
            "",
            "To match the server (standalone client):",
            "  1. Back up your client's ConanSandbox\\Mods folder if it already has mods.",
            "  2. Copy everything inside this bundle's Mods folder (the .pak files and modlist.txt)",
            "     into <your Conan client folder>\\ConanSandbox\\Mods.",
            "  3. Check the SHA-256 values below (or in manifest.json) if you want to verify the files.",
            "",
            "Required mods in load order:"
        };
        lines.AddRange(entries.Select(e =>
            $"  {e.LoadOrder}. {e.FileName}  {e.SizeBytes} bytes  SHA-256 {e.Sha256}  ({e.SourceType}{(e.WorkshopId is null ? string.Empty : $" {e.WorkshopId}")})"));
        if (entries.Count == 0)
        {
            lines.Add("  (the server has no enabled mods)");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
