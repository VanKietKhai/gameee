using System.Net;
using System.Net.Http.Json;
using ConanServerControl.Core.Security;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Web;
using Microsoft.AspNetCore.Builder;

namespace ConanServerControl.Tests;

public class QaWebAdminTests
{
    [Fact]
    public async Task Unauthenticated_mutation_is_not_a_successful_action()
    {
        await using var host = await TestWeb.StartAsync(enableWebAdmin: false);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = host.BaseAddress };
        var response = await client.PostAsync("/api/server/update-mods", new StringContent(string.Empty));
        var body = await response.Content.ReadAsStringAsync();

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("\"ok\":true", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-010")]
    public async Task Unauthenticated_mutation_returns_401_instead_of_a_login_redirect()
    {
        await using var host = await TestWeb.StartAsync(enableWebAdmin: false);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = host.BaseAddress };
        var response = await client.PostAsync("/api/server/update-server", new StringContent(string.Empty));

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Action endpoints must answer 401 when the caller is not signed in. status={(int)response.StatusCode} location={response.Headers.Location}");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-010")]
    public async Task Anonymous_identity_endpoint_requires_authentication()
    {
        await using var host = await TestWeb.StartAsync(enableWebAdmin: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-010")]
    public async Task Authenticated_mutation_without_the_csrf_header_is_rejected()
    {
        await using var host = await TestWeb.StartAsync(enableWebAdmin: true);
        using var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = host.BaseAddress };
        var login = await client.PostAsJsonAsync("/api/login", new { username = "admin", password = TestWeb.Password });
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.True(login.IsSuccessStatusCode, $"Login failed: {(int)login.StatusCode} {loginBody}");

        var response = await client.PostAsync("/api/server/update-mods", new StringContent(string.Empty));
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden
            && body.Contains("antiforgery", StringComparison.OrdinalIgnoreCase),
            $"Mutations must require the antiforgery token. status={(int)response.StatusCode} body={body}");
    }

    private sealed class TestWeb : IAsyncDisposable
    {
        public const string Password = "Qa-password-1";

        private readonly WebApplication _app;

        private TestWeb(WebApplication app, Uri baseAddress)
        {
            _app = app;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public static async Task<TestWeb> StartAsync(bool enableWebAdmin)
        {
            var (data, paths, settings) = QaTestSupport.CreateData();
            var port = FreePort();
            await settings.UpdateAsync(s =>
            {
                s.WebAdmin.Enabled = enableWebAdmin;
                s.WebAdmin.Port = port;
                s.WebAdmin.Username = "admin";
                s.WebAdmin.BindMode = ConanServerControl.Core.Models.WebBindMode.LocalhostOnly;
                s.General.StartServerWhenManagerLaunches = false;
            });
            if (enableWebAdmin)
            {
                var hash = PasswordHasher.Hash(Password, iterations: 10_000);
                await settings.UpdateSecretsAsync(s => s.WebAdminPasswordHash = hash);
            }

            var previous = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryOverrideVariable);
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryOverrideVariable, data);
            WebApplication app;
            try
            {
                app = WebHostFactory.Create(enableWebAdmin
                    ? Array.Empty<string>()
                    : new[] { "--urls", "http://127.0.0.1:0" });
            }
            finally
            {
                Environment.SetEnvironmentVariable(AppPaths.DataDirectoryOverrideVariable, previous);
            }

            await app.StartAsync();
            var address = enableWebAdmin
                ? new Uri($"http://127.0.0.1:{port}")
                : new Uri(app.Urls.Single());
            return new TestWeb(app, address);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static int FreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
