using ConanServerControl.Core.Validation;

namespace ConanServerControl.Core.Diagnostics;

public sealed record ServerExecutableGateResult(bool Allowed, ConanExecutableKind Kind, string Reason);

/// <summary>
/// Hard gate evaluated before every dedicated-server launch. A launch is allowed only when
/// the configured file is positively identified as <c>ConanSandboxServer.exe</c> and lies
/// outside the standalone client folder. Anything else is blocked, not merely warned about.
/// Pure: no filesystem access, so it is safe to call from diagnostics and tests.
/// </summary>
public static class ServerExecutableGate
{
    public static ServerExecutableGateResult Evaluate(string? executablePath, string? standaloneClientRoot)
    {
        var kind = ConanExecutableClassifier.Classify(executablePath);
        switch (kind)
        {
            case ConanExecutableKind.Empty:
                return Blocked(kind, "No dedicated server executable is configured.");
            case ConanExecutableKind.ClientLauncherBatch:
                return Blocked(kind, "Run Me!.bat is the standalone CLIENT launcher, not the dedicated server.");
            case ConanExecutableKind.StandaloneClient:
                return Blocked(kind, "ConanSandbox.exe is the Conan game CLIENT, not the dedicated server.");
            case ConanExecutableKind.BatchOrScript:
                return Blocked(kind, "Scripts and shortcuts cannot be launched as the dedicated server.");
            case ConanExecutableKind.DedicatedServerShipping:
                return Blocked(kind, $"Launch {AppConstants.DedicatedServerExecutable}, not the -Win64-Shipping binary directly.");
            case ConanExecutableKind.Unknown:
                return Blocked(kind, $"The executable is not {AppConstants.DedicatedServerExecutable}.");
        }

        if (!PathValidator.IsSafeAbsolutePath(executablePath))
        {
            return Blocked(kind, "The dedicated server executable path is not a safe absolute path.");
        }

        if (!string.IsNullOrWhiteSpace(standaloneClientRoot) &&
            PathValidator.IsSafeAbsolutePath(standaloneClientRoot) &&
            PathValidator.IsUnderRoot(executablePath, standaloneClientRoot))
        {
            return Blocked(kind, "The dedicated server executable is inside the standalone client folder.");
        }

        return new ServerExecutableGateResult(true, kind, $"{AppConstants.DedicatedServerExecutable} identified as the dedicated server.");
    }

    private static ServerExecutableGateResult Blocked(ConanExecutableKind kind, string reason) =>
        new(false, kind, reason);
}
