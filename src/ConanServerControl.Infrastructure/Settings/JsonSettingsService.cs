using System.Text.Json;
using System.Text.Json.Serialization;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Settings;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Settings;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly IAppPaths _paths;
    private readonly ISecretProtector _protector;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsService(IAppPaths paths, ISecretProtector protector, ILogger<JsonSettingsService> logger)
    {
        _paths = paths;
        _protector = protector;
        _logger = logger;
        Current = new AppSettings();
        Secrets = new ProtectedSecrets();
    }

    public AppSettings Current { get; private set; }

    public ProtectedSecrets Secrets { get; private set; }

    public event EventHandler? Changed;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureCreated();
            if (File.Exists(_paths.SettingsFilePath))
            {
                await using var stream = File.OpenRead(_paths.SettingsFilePath);
                var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                Current = loaded ?? new AppSettings();
            }
            else
            {
                Current = CreateDefault();
                await WriteSettingsAsync(cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(Current.SteamCmd.InstallDirectory))
            {
                Current.SteamCmd.InstallDirectory = _paths.SteamCmdDefaultDirectory;
            }

            Secrets = await LoadSecretsAsync(cancellationToken).ConfigureAwait(false);
            return Current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteSettingsAsync(cancellationToken).ConfigureAwait(false);
            await WriteSecretsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            mutate(Current);
            await WriteSettingsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateSecretsAsync(Action<ProtectedSecrets> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            mutate(Secrets);
            await WriteSecretsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private AppSettings CreateDefault()
    {
        return new AppSettings
        {
            SteamCmd =
            {
                InstallDirectory = _paths.SteamCmdDefaultDirectory,
                UseAnonymousLogin = true,
                ValidateAfterUpdate = true
            }
        };
    }

    private async Task WriteSettingsAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        var temp = _paths.SettingsFilePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Copy(temp, _paths.SettingsFilePath, overwrite: true);
        File.Delete(temp);
    }

    private async Task<ProtectedSecrets> LoadSecretsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.SecretsFilePath))
        {
            return new ProtectedSecrets();
        }

        try
        {
            var payload = await File.ReadAllTextAsync(_paths.SecretsFilePath, cancellationToken).ConfigureAwait(false);
            var json = _protector.Unprotect(payload);
            return JsonSerializer.Deserialize<ProtectedSecrets>(json, JsonOptions) ?? new ProtectedSecrets();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read protected secrets. A new secrets file will be created on next save.");
            return new ProtectedSecrets();
        }
    }

    private async Task WriteSecretsAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        var json = JsonSerializer.Serialize(Secrets, JsonOptions);
        var protectedPayload = _protector.Protect(json);
        var temp = _paths.SecretsFilePath + ".tmp";
        await File.WriteAllTextAsync(temp, protectedPayload, cancellationToken).ConfigureAwait(false);
        File.Copy(temp, _paths.SecretsFilePath, overwrite: true);
        File.Delete(temp);
    }
}
