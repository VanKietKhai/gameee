using ConanServerControl.Core.Validation;

namespace ConanServerControl.Core.LiveTesting;

/// <summary>
/// Directory layout of the dedicated M3 live-test workspace. Everything the live harness
/// creates lives under <see cref="Root"/>, which must contain the <c>.csc-live-test</c> marker.
/// </summary>
public sealed record LiveTestLayout(string Root, string SteamCmd, string Server, string LiveTest, string AppData)
{
    public string Marker => Path.Combine(Root, LiveTestGuard.MarkerFileName);

    public IReadOnlyList<(string Label, string Path)> Children =>
    [
        ("steamcmd", SteamCmd),
        ("server", Server),
        ("live-test", LiveTest),
        ("app-data", AppData)
    ];
}

/// <summary>
/// Safety guard for the opt-in M3 Task 4 live harness. Live operations require BOTH
/// <c>CSC_LIVE_TESTS=1</c> and the marker file in the workspace root. Destructive harness
/// actions (cleanup) are refused for any path outside a marked workspace.
/// The marker is our harness convention, not a Conan requirement.
/// </summary>
public static class LiveTestGuard
{
    public const string MarkerFileName = ".csc-live-test";
    public const string EnableVariable = "CSC_LIVE_TESTS";

    public static LiveTestLayout CreateLayout(string root)
    {
        var normalized = PathValidator.NormalizeFullPath(root);
        return new LiveTestLayout(
            normalized,
            Path.Combine(normalized, "steamcmd"),
            Path.Combine(normalized, "server"),
            Path.Combine(normalized, "live-test"),
            Path.Combine(normalized, "app-data"));
    }

    /// <summary>
    /// Returns every safety problem with <paramref name="layout"/>; empty means safe.
    /// Pure path checks only; does not create or inspect files.
    /// </summary>
    /// <param name="forbiddenRoots">
    /// Locations the workspace must not overlap: the standalone client root, its launcher
    /// folder, and similar user installs.
    /// </param>
    public static IReadOnlyList<string> ValidateLayout(LiveTestLayout layout, IEnumerable<string?> forbiddenRoots)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var problems = new List<string>();

        if (!PathValidator.IsSafeAbsolutePath(layout.Root))
        {
            problems.Add($"Workspace root is not a safe absolute path: {layout.Root}");
            return problems;
        }

        var full = PathValidator.NormalizeFullPath(layout.Root);
        var driveRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty);
        if (string.Equals(Path.TrimEndingDirectorySeparator(full), driveRoot, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"Workspace root is a drive root: {layout.Root}");
        }

        foreach (var forbidden in forbiddenRoots.Where(f => !string.IsNullOrWhiteSpace(f)))
        {
            if (PathValidator.Overlaps(layout.Root, forbidden))
            {
                problems.Add($"Workspace overlaps a protected location: {forbidden}");
            }
        }

        var children = layout.Children;
        foreach (var (label, path) in children)
        {
            if (!PathValidator.IsUnderRoot(path, layout.Root) || PathValidator.PathsEqual(path, layout.Root))
            {
                problems.Add($"{label} directory is not a sub-folder of the workspace root: {path}");
            }
        }

        for (var i = 0; i < children.Count; i++)
        {
            for (var j = i + 1; j < children.Count; j++)
            {
                if (PathValidator.Overlaps(children[i].Path, children[j].Path))
                {
                    problems.Add($"{children[i].Label} and {children[j].Label} directories overlap.");
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Validates a dedicated server directory that lives OUTSIDE the workspace root (for example
    /// a sibling of the standalone client: <c>D:\conan exiles\Conan Exiles Dedicated Server</c>
    /// next to <c>D:\conan exiles\Conan Exiles Enhanced</c>). Returns every problem; empty means safe.
    /// A sibling inside the client's launcher folder is allowed; the client itself, the launcher
    /// folder itself and anything containing them are not. An existing non-empty folder must
    /// already be a dedicated server install; client files inside it are refused.
    /// </summary>
    public static IReadOnlyList<string> ValidateExternalServerDirectory(
        string serverDirectory,
        LiveTestLayout layout,
        string? standaloneClientRoot)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var problems = new List<string>();
        if (!PathValidator.IsSafeAbsolutePath(serverDirectory))
        {
            problems.Add($"Server directory is not a safe absolute path: {serverDirectory}");
            return problems;
        }

        var full = PathValidator.NormalizeFullPath(serverDirectory);
        var driveRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty);
        if (string.Equals(Path.TrimEndingDirectorySeparator(full), driveRoot, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"Server directory is a drive root: {serverDirectory}");
        }

        if (PathValidator.IsSafeAbsolutePath(standaloneClientRoot))
        {
            var client = PathValidator.NormalizeFullPath(standaloneClientRoot!);
            if (PathValidator.Overlaps(full, client))
            {
                problems.Add($"Server directory overlaps the standalone client: {client}");
            }

            var launcher = Path.GetDirectoryName(client);
            if (launcher is not null && PathValidator.IsUnderRoot(launcher, full))
            {
                problems.Add($"Server directory is, or contains, the client launcher folder: {launcher}");
            }
        }

        foreach (var (label, path) in layout.Children.Where(c => c.Label != "server"))
        {
            if (PathValidator.Overlaps(full, path))
            {
                problems.Add($"Server directory overlaps the workspace {label} folder: {path}");
            }
        }

        if (Directory.Exists(full))
        {
            foreach (var clientFile in new[] { Diagnostics.ConanExecutableClassifier.ClientLauncherBatch, Diagnostics.ConanExecutableClassifier.ClientExecutable })
            {
                if (File.Exists(Path.Combine(full, clientFile)))
                {
                    problems.Add($"Server directory contains the game client file {clientFile}.");
                }
            }

            var manifest = Path.Combine(full, "steamapps", $"appmanifest_{AppConstants.ConanDedicatedServerAppId}.acf");
            var isServerInstall = File.Exists(manifest) || Mods.DedicatedServerLocator.Find(full) is not null;
            if (!isServerInstall && Directory.EnumerateFileSystemEntries(full).Any())
            {
                problems.Add($"Server directory is not empty and is not a dedicated server install; refusing to install over unknown files: {full}");
            }
        }

        return problems;
    }

    public static bool IsEnabled(string? environmentValue) =>
        string.Equals(environmentValue?.Trim(), "1", StringComparison.Ordinal);

    public static bool HasMarker(string root) =>
        PathValidator.IsSafeAbsolutePath(root) && File.Exists(Path.Combine(root, MarkerFileName));

    /// <summary>
    /// Throws unless live tests are explicitly enabled AND the workspace has its marker.
    /// </summary>
    public static void EnsureLiveAllowed(string? environmentValue, string root)
    {
        if (!IsEnabled(environmentValue))
        {
            throw new InvalidOperationException(
                $"Live tests are disabled. Set {EnableVariable}=1 to run guarded live operations.");
        }

        if (!HasMarker(root))
        {
            throw new InvalidOperationException(
                $"Live-test marker '{MarkerFileName}' was not found in {root}. Refusing live operations.");
        }
    }

    /// <summary>
    /// Throws unless <paramref name="path"/> is inside a workspace whose root carries the marker.
    /// Use before any harness cleanup/destructive action.
    /// </summary>
    public static void EnsureInsideMarkedWorkspace(string path, string root)
    {
        if (!HasMarker(root))
        {
            throw new InvalidOperationException($"Workspace {root} has no '{MarkerFileName}' marker.");
        }

        if (!PathValidator.IsUnderRoot(path, root) || PathValidator.PathsEqual(path, root))
        {
            throw new InvalidOperationException(
                $"Refusing destructive action outside the marked live-test workspace: {path}");
        }
    }
}
