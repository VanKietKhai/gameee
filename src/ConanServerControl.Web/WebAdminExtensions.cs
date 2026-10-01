using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Security;
using ConanServerControl.Infrastructure;
using ConanServerControl.Infrastructure.Diagnostics;
using ConanServerControl.Infrastructure.Paths;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;

namespace ConanServerControl.Web;

public static class WebAdminExtensions
{
    public const string CookieScheme = "ConanServerControl";
    public const string LoginPolicy = "webadmin-login";

    public static IServiceCollection AddConanWebAdmin(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieScheme)
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = AppConstants.WebAdminCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.LoginPath = "/login.html";
                options.AccessDeniedPath = "/login.html";
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "csc_csrf";
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginPolicy, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "text/plain";
                await context.HttpContext.Response.WriteAsync(
                    "Too many login attempts. Wait a minute and try again.", token);
            };
        });

        return services;
    }

    public static WebApplication MapConanWebAdmin(this WebApplication app)
    {
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!Directory.Exists(wwwroot))
        {
            wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        if (Directory.Exists(wwwroot))
        {
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot)
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot)
            });
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapGet("/api/status", (IServerProcessManager server, ISettingsService settings, DiagnosticsService diagnostics) =>
        {
            var state = server.State.Clone();
            var snap = diagnostics.Capture();
            return Results.Json(new
            {
                server = settings.Current.Server.ServerName,
                status = state.Status.ToString(),
                health = state.Health.ToString(),
                players = state.PlayerCount,
                maxPlayers = state.MaxPlayers,
                uptime = state.Uptime?.ToString(@"hh\:mm\:ss"),
                processId = state.ProcessId,
                cpu = state.CpuUsagePercent,
                ramBytes = state.WorkingSetBytes,
                mods = settings.Current.Mods.Mods.Count,
                modsNeedingUpdate = settings.Current.Mods.Mods.Count(m => m.UpdateAvailable),
                lastError = state.LastError,
                crashLoop = state.CrashLoopDetected,
                busy = state.ActionInProgress,
                currentAction = state.CurrentAction,
                webAdminUrl = snap.WebAdminUrl,
                tailscale = snap.TailscaleIPv4
            });
        }).RequireAuthorization();

        app.MapGet("/api/players", async (IRconService rcon, IServerProcessManager server) =>
        {
            IReadOnlyList<PlayerInfo> players;
            try
            {
                players = await rcon.GetPlayersAsync();
            }
            catch
            {
                players = server.State.Players;
            }

            return Results.Json(players);
        }).RequireAuthorization();

        app.MapGet("/api/logs", (ILiveLogBuffer logs) =>
        {
            return Results.Json(logs.Snapshot(200));
        }).RequireAuthorization();

        app.MapGet("/api/activity", async (IActivityLog activity) =>
        {
            return Results.Json(await activity.GetRecentAsync(40));
        }).RequireAuthorization();

        app.MapGet("/api/csrf", (HttpContext http, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Json(new { token = tokens.RequestToken });
        }).RequireAuthorization();

        app.MapPost("/api/login", async (
            HttpContext http,
            LoginRequest request,
            ISettingsService settings,
            IActivityLog activity,
            Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
        {
            var web = settings.Current.WebAdmin;
            if (!web.Enabled)
            {
                return Results.Json(new { error = "Web Admin is disabled in Settings." }, statusCode: 403);
            }

            if (string.IsNullOrWhiteSpace(settings.Secrets.WebAdminPasswordHash))
            {
                return Results.Json(new
                {
                    error = "Web Admin password is not configured. Set it in the desktop Settings page first."
                }, statusCode: 403);
            }

            var usernameOk = string.Equals(request.Username?.Trim(), web.Username, StringComparison.OrdinalIgnoreCase);
            var passwordOk = PasswordHasher.Verify(request.Password ?? string.Empty, settings.Secrets.WebAdminPasswordHash);
            if (!usernameOk || !passwordOk)
            {
                await activity.AddAsync("Security", "Failed Web Admin login attempt.");
                return Results.Json(new { error = "Invalid username or password." }, statusCode: 401);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, web.Username),
                new("role", "admin")
            };
            var identity = new ClaimsIdentity(claims, CookieScheme);
            var principal = new ClaimsPrincipal(identity);
            var lifetime = TimeSpan.FromMinutes(Math.Clamp(web.SessionMinutes, 5, 24 * 60));
            await http.SignInAsync(CookieScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime)
            });

            await activity.AddAsync("Security", $"{web.Username} signed in to Web Admin.", web.Username);
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Json(new { ok = true, csrf = tokens.RequestToken });
        }).RequireRateLimiting(LoginPolicy);

        app.MapPost("/api/logout", async (HttpContext http, IActivityLog activity) =>
        {
            var antiforgeryError = await ValidateAntiforgeryAsync(http);
            if (antiforgeryError is not null)
            {
                return antiforgeryError;
            }

            var name = http.User.Identity?.Name;
            await http.SignOutAsync(CookieScheme);
            await activity.AddAsync("Security", $"{name} signed out of Web Admin.", name);
            return Results.Ok();
        }).RequireAuthorization();

        app.MapPost("/api/server/start", (HttpContext http, IServerProcessManager server, IActivityLog activity) =>
            RunAction(http, activity, "Start Server", () => server.StartAsync(http.RequestAborted)))
            .RequireAuthorization();

        app.MapPost("/api/server/stop", (HttpContext http, IServerProcessManager server, IActivityLog activity) =>
            RunAction(http, activity, "Stop Server", () => server.StopAsync(false, http.RequestAborted)))
            .RequireAuthorization();

        app.MapPost("/api/server/restart", (HttpContext http, IServerProcessManager server, IActivityLog activity) =>
            RunAction(http, activity, "Restart Server", () => server.RestartAsync(http.RequestAborted)))
            .RequireAuthorization();

        app.MapPost("/api/server/backup", (HttpContext http, IBackupService backups, IActivityLog activity) =>
            RunAction(http, activity, "created backup", async () => { await backups.BackupNowAsync("web-admin", http.RequestAborted); }))
            .RequireAuthorization();

        app.MapPost("/api/server/check-updates", (HttpContext http, IServerUpdateService updates, IActivityLog activity) =>
            RunAction(http, activity, "Check Updates", async () => { await updates.CheckAsync(http.RequestAborted); }))
            .RequireAuthorization();

        app.MapPost("/api/server/update-server", (HttpContext http, IServerUpdateService updates, IActivityLog activity) =>
            RunAction(http, activity, "Update Server", () => updates.UpdateAsync(false, cancellationToken: http.RequestAborted)))
            .RequireAuthorization();

        app.MapPost("/api/server/update-mods", (HttpContext http, IServerUpdateService updates, IActivityLog activity) =>
            RunAction(http, activity, "Update Mods", () => updates.UpdateModsAsync(false, cancellationToken: http.RequestAborted)))
            .RequireAuthorization();

        app.MapPost("/api/server/delayed-restart", async (
            HttpContext http,
            DelayedRestartDto dto,
            IDelayedRestartService delayed,
            IActivityLog activity) =>
        {
            var antiforgeryError = await ValidateAntiforgeryAsync(http);
            if (antiforgeryError is not null)
            {
                return antiforgeryError;
            }

            var minutes = dto.Minutes is 5 or 10 or 15 or 30 or 60 ? dto.Minutes : 10;
            var actor = http.User.Identity?.Name ?? "web";
            await delayed.StartAsync(new DelayedRestartRequest
            {
                Delay = (RestartDelay)minutes,
                BackupFirst = true,
                Reason = "web-delayed-restart"
            }, http.RequestAborted);
            await activity.AddAsync("Server", $"{actor} requested delayed restart ({minutes} minutes).", actor);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization();

        app.MapPost("/api/server/cancel-restart", async (HttpContext http, IDelayedRestartService delayed, IActivityLog activity) =>
        {
            var antiforgeryError = await ValidateAntiforgeryAsync(http);
            if (antiforgeryError is not null)
            {
                return antiforgeryError;
            }

            await delayed.CancelAsync();
            var actor = http.User.Identity?.Name ?? "web";
            await activity.AddAsync("Server", $"{actor} cancelled delayed restart.", actor);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization();

        app.MapGet("/api/me", (HttpContext http) =>
        {
            return Results.Json(new { name = http.User.Identity?.Name, authenticated = http.User.Identity?.IsAuthenticated == true });
        }).RequireAuthorization();

        return app;
    }

    private static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext http)
    {
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(http);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Json(new { error = "Antiforgery token missing or invalid." }, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> RunAction(HttpContext http, IActivityLog activity, string action, Func<Task> work)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(http);
        if (antiforgeryError is not null)
        {
            return antiforgeryError;
        }

        var actor = http.User.Identity?.Name ?? "web";
        try
        {
            await work();
            await activity.AddAsync("Web", $"{actor} requested {action}.", actor);
            return Results.Ok(new { ok = true });
        }
        catch (UserFacingException ex)
        {
            return Results.Json(new { error = ex.FormatForDisplay() }, statusCode: 400);
        }
        catch (Exception ex)
        {
            await activity.AddAsync("Web", $"{actor} failed {action}: {ex.Message}", actor);
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed class DelayedRestartDto
{
    public int Minutes { get; set; } = 10;
}

public static class WebHostFactory
{
    public static WebApplication Create(string[] args)
    {
        var paths = new AppPaths();
        paths.EnsureCreated();
        var liveLog = new ConanServerControl.Infrastructure.Logging.LiveLogBuffer();

        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseConanLogging(paths, liveLog);
        builder.Services.AddSingleton<ConanServerControl.Core.Abstractions.ILiveLogBuffer>(liveLog);
        builder.Services.AddConanServerControl(paths);
        builder.Services.AddConanWebAdmin();

        var settingsPath = paths.SettingsFilePath;
        if (File.Exists(settingsPath))
        {
            builder.Configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: true);
        }

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<ISettingsService>();
        settings.LoadAsync().GetAwaiter().GetResult();
        ApplyKestrelBinding(app, settings);
        app.MapConanWebAdmin();
        return app;
    }

    public static void ApplyKestrelBinding(WebApplication app, ISettingsService settings)
    {
        var web = settings.Current.WebAdmin;
        if (!web.Enabled)
        {
            return;
        }

        var bind = DiagnosticsService.ResolveBindAddress(web);
        app.Urls.Clear();
        app.Urls.Add($"http://{bind}:{web.Port}");
    }
}
