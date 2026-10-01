namespace ConanServerControl.Core.Diagnostics;

public enum ConanExecutableKind
{
    Empty = 0,
    DedicatedServer = 1,
    DedicatedServerShipping = 2,
    ClientLauncherBatch = 3,
    BatchOrScript = 4,
    StandaloneClient = 5,
    Unknown = 6
}

/// <summary>
/// Classifies a configured path by file name only. Never executes or opens the file.
/// The standalone Conan client (<c>ConanSandbox.exe</c>) and its <c>Run Me!.bat</c>
/// launcher must never be used as the dedicated-server executable.
/// </summary>
public static class ConanExecutableClassifier
{
    public const string ClientExecutable = "ConanSandbox.exe";
    public const string ClientShippingExecutable = "ConanSandbox-Win64-Shipping.exe";
    public const string ClientLauncherBatch = "Run Me!.bat";

    private static readonly string[] ScriptExtensions = [".bat", ".cmd", ".ps1", ".vbs", ".lnk"];

    public static ConanExecutableKind Classify(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ConanExecutableKind.Empty;
        }

        var name = GetFileName(path.Trim());
        if (string.Equals(name, AppConstants.DedicatedServerExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return ConanExecutableKind.DedicatedServer;
        }

        if (string.Equals(name, AppConstants.DedicatedServerShippingExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return ConanExecutableKind.DedicatedServerShipping;
        }

        if (string.Equals(name, ClientLauncherBatch, StringComparison.OrdinalIgnoreCase))
        {
            return ConanExecutableKind.ClientLauncherBatch;
        }

        var extension = Path.GetExtension(name);
        if (ScriptExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)))
        {
            return ConanExecutableKind.BatchOrScript;
        }

        if (string.Equals(name, ClientExecutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, ClientShippingExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return ConanExecutableKind.StandaloneClient;
        }

        return ConanExecutableKind.Unknown;
    }

    public static bool IsDedicatedServer(ConanExecutableKind kind) =>
        kind is ConanExecutableKind.DedicatedServer or ConanExecutableKind.DedicatedServerShipping;

    public static bool IsForbiddenAsServer(ConanExecutableKind kind) =>
        kind is ConanExecutableKind.ClientLauncherBatch
            or ConanExecutableKind.BatchOrScript
            or ConanExecutableKind.StandaloneClient;

    // Path.GetFileName on Linux does not split on '\', so handle both separators
    // to classify Windows paths consistently on any test host.
    private static string GetFileName(string path)
    {
        var index = path.LastIndexOfAny(['\\', '/']);
        return index >= 0 ? path[(index + 1)..] : path;
    }
}
