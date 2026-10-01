using System.Diagnostics;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;

namespace ConanServerControl.LiveHarness;

/// <summary>
/// Pass-through decorator over the real <see cref="ISteamCmdService"/> that records SteamCMD
/// output, exit code, duration and (for Workshop downloads) the staged file tree before the
/// application consumes and deletes it. Changes no behaviour.
/// </summary>
internal sealed class RecordingSteamCmd : ISteamCmdService
{
    private readonly ISteamCmdService _inner;

    public RecordingSteamCmd(ISteamCmdService inner) => _inner = inner;

    public List<SteamCmdRecord> Records { get; } = new();

    public string? ExecutablePath => _inner.ExecutablePath;

    public bool IsInstalled => _inner.IsInstalled;

    public Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        RecordAsync("install-steamcmd (download official zip + '+quit' self-update)", progress,
            p => _inner.InstallAsync(p, cancellationToken), null);

    public Task UpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        RecordAsync("steamcmd +quit (self-update)", progress, p => _inner.UpdateAsync(p, cancellationToken), null);

    public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => _inner.ValidateAsync(cancellationToken);

    public async Task<ProcessExecutionResult> InstallOrUpdateDedicatedServerAsync(
        string installDirectory,
        bool validate,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ProcessExecutionResult? result = null;
        await RecordAsync(
            $"+force_install_dir \"{installDirectory}\" +login anonymous +app_update 443030{(validate ? " validate" : string.Empty)} +quit",
            progress,
            async p => result = await _inner.InstallOrUpdateDedicatedServerAsync(installDirectory, validate, p, cancellationToken),
            null).ConfigureAwait(false);
        return result!;
    }

    public async Task<ProcessExecutionResult> DownloadWorkshopItemAsync(
        long workshopId,
        string installDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ProcessExecutionResult? result = null;
        await RecordAsync(
            $"+force_install_dir \"{installDirectory}\" +login anonymous +workshop_download_item 440900 {workshopId} +quit",
            progress,
            async p => result = await _inner.DownloadWorkshopItemAsync(workshopId, installDirectory, p, cancellationToken),
            installDirectory).ConfigureAwait(false);
        return result!;
    }

    private async Task RecordAsync(string command, IProgress<string>? outer, Func<IProgress<string>, Task> run, string? treeRoot)
    {
        var record = new SteamCmdRecord { Command = command };
        Records.Add(record);
        var progress = new SyncProgress(line =>
        {
            lock (record.Output)
            {
                record.Output.Add(line);
            }

            outer?.Report(line);
            Console.WriteLine("    steamcmd> " + line);
        });
        var clock = Stopwatch.StartNew();
        try
        {
            await run(progress).ConfigureAwait(false);
            record.Succeeded = true;
        }
        catch (Exception ex)
        {
            record.Error = ex.Message;
            throw;
        }
        finally
        {
            record.Duration = clock.Elapsed;
            if (treeRoot is not null && Directory.Exists(treeRoot))
            {
                foreach (var file in Directory.EnumerateFiles(treeRoot, "*", SearchOption.AllDirectories))
                {
                    record.StagedFiles.Add((Path.GetRelativePath(treeRoot, file), new FileInfo(file).Length));
                }
            }
        }
    }

    /// <summary>Synchronous progress so output lines are captured in order on the calling thread.</summary>
    private sealed class SyncProgress(Action<string> handler) : IProgress<string>
    {
        public void Report(string value) => handler(value);
    }
}

internal sealed class SteamCmdRecord
{
    public string Command { get; set; } = string.Empty;

    public List<string> Output { get; } = new();

    public List<(string RelativePath, long Size)> StagedFiles { get; } = new();

    public TimeSpan Duration { get; set; }

    public bool Succeeded { get; set; }

    public string? Error { get; set; }
}
