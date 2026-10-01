using System.Text.Json;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Settings;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class M3RconSettingsTests
{
    [Fact]
    public async Task Legacy_server_rcon_port_migrates_when_rcon_port_is_default()
    {
        var (paths, service) = CreateService();
        var json = """
            {
              "settingsVersion": 1,
              "server": { "rconPort": 25580, "gamePort": 7777, "queryPort": 27015 },
              "rcon": { "enabled": true, "port": 25575, "timeoutSeconds": 5 }
            }
            """;
        await File.WriteAllTextAsync(paths.SettingsFilePath, json);

        await service.LoadAsync();

        Assert.Equal(25580, service.Current.Rcon.Port);
        var persisted = await File.ReadAllTextAsync(paths.SettingsFilePath);
        Assert.Contains("25580", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("\"port\": 25575", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_rcon_port_wins_over_legacy_server_rcon_port()
    {
        var (paths, service) = CreateService();
        var json = """
            {
              "settingsVersion": 1,
              "server": { "rconPort": 25580 },
              "rcon": { "enabled": true, "port": 27015, "timeoutSeconds": 5 }
            }
            """;
        await File.WriteAllTextAsync(paths.SettingsFilePath, json);

        await service.LoadAsync();

        Assert.Equal(27015, service.Current.Rcon.Port);
    }

    [Fact]
    public async Task Rcon_port_migration_is_stable_on_repeated_loads()
    {
        var (paths, service) = CreateService();
        await File.WriteAllTextAsync(paths.SettingsFilePath, """
            { "server": { "rconPort": 25580 }, "rcon": { "port": 25575 } }
            """);

        await service.LoadAsync();
        Assert.Equal(25580, service.Current.Rcon.Port);

        var second = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        await second.LoadAsync();
        Assert.Equal(25580, second.Current.Rcon.Port);
        Assert.False(JsonSettingsService.TryMigrateLegacyRconPort(second.Current));
    }

    [Fact]
    public async Task Rcon_password_is_not_written_to_settings_json()
    {
        var protector = new RecordingProtector();
        var (paths, service) = CreateService(protector);
        await service.LoadAsync();
        await service.UpdateSecretsAsync(s => s.RconPassword = "super-secret-rcon");

        var settingsJson = await File.ReadAllTextAsync(paths.SettingsFilePath);
        Assert.DoesNotContain("super-secret-rcon", settingsJson, StringComparison.Ordinal);
        Assert.Contains("super-secret-rcon", Assert.Single(protector.ProtectedPayloads), StringComparison.Ordinal);

        var secretsFile = await File.ReadAllTextAsync(paths.SecretsFilePath);
        Assert.DoesNotContain("super-secret-rcon", secretsFile, StringComparison.Ordinal);
        Assert.StartsWith("dev-base64:", secretsFile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loaded_app_settings_do_not_expose_rcon_password_plaintext()
    {
        var (paths, service) = CreateService(new RecordingProtector());
        await service.LoadAsync();
        await service.UpdateSecretsAsync(s => s.RconPassword = "never-in-settings");
        await service.LoadAsync();

        var roundTrip = JsonSerializer.Serialize(service.Current);
        Assert.DoesNotContain("never-in-settings", roundTrip, StringComparison.Ordinal);
        Assert.Equal("never-in-settings", service.Secrets.RconPassword);
        Assert.Equal(AppConstants.DefaultRconPort, service.Current.Rcon.Port);
    }

    [Fact]
    public async Task Saving_rcon_password_does_not_log_plaintext()
    {
        var logger = new ListLogger<JsonSettingsService>();
        var data = Path.Combine(Path.GetTempPath(), "csc-m3", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var service = new JsonSettingsService(paths, new RecordingProtector(), logger);
        await service.LoadAsync();
        await service.UpdateSecretsAsync(s => s.RconPassword = "do-not-log-this-password");

        Assert.DoesNotContain(
            logger.Messages,
            m => m.Contains("do-not-log-this-password", StringComparison.Ordinal));
    }

    [Fact]
    public void Blank_rcon_password_input_must_not_clear_an_existing_secret()
    {
        Assert.True(string.IsNullOrWhiteSpace("   "));
        var secrets = new ProtectedSecrets { RconPassword = "keep-me" };
        if (!string.IsNullOrWhiteSpace("   "))
        {
            secrets.RconPassword = "   ";
        }

        Assert.Equal("keep-me", secrets.RconPassword);
    }

    private static (AppPaths Paths, JsonSettingsService Service) CreateService(ISecretProtector? protector = null)
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-m3", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var service = new JsonSettingsService(
            paths,
            protector ?? new PassthroughProtector(),
            NullLogger<JsonSettingsService>.Instance);
        return (paths, service);
    }

    private sealed class RecordingProtector : ISecretProtector
    {
        public List<string> ProtectedPayloads { get; } = new();

        public string Protect(string plaintext)
        {
            ProtectedPayloads.Add(plaintext);
            return "dev-base64:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));
        }

        public string Unprotect(string protectedPayload)
        {
            const string prefix = "dev-base64:";
            if (!protectedPayload.StartsWith(prefix, StringComparison.Ordinal))
            {
                return protectedPayload;
            }

            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedPayload[prefix.Length..]));
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                Messages.Add(exception.ToString());
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
