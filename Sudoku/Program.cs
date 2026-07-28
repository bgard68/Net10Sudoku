using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Sudoku.Application;
using Sudoku.Application.Interfaces;
using Sudoku.Components;
using Sudoku.Infrastructure;
using Sudoku.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server keeps per-player state in a circuit. The caps below bound what
// abandoned or hostile connections can hold on a small instance; the defaults
// (100 retained circuits for 3 minutes) are generous for a 1 GB Free tier.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitMaxRetained = 20;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
    })
    .AddHubOptions(options =>
    {
        // A player's messages are a few bytes; this only bounds abuse.
        options.MaximumReceiveMessageSize = 32 * 1024;
    });

// App Service terminates TLS and forwards to Kestrel over plain HTTP. Without
// this the app believes every request is insecure, which silently drops the
// Secure flag from cookies, confuses HTTPS redirection, and makes the client IP
// (used to partition the rate limiter below) the proxy's address for everyone.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    // The proxy is Azure's front end, not a host we enumerate; clearing these
    // is what App Service's own documented configuration does.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Behind a TLS-terminating proxy the request looks like plain HTTP, so the
// SameAsRequest default would emit the token cookie without Secure. Always
// fixes that - but the antiforgery system THROWS if it is set while a request
// genuinely is not SSL, so development (plain http://localhost) keeps
// SameAsRequest rather than 500-ing on every POST.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Generating a Professional puzzle is CPU-bound work any anonymous visitor can
// trigger, and the Free tier has a daily CPU quota - exhausting it suspends the
// site. This bounds HTTP requests per client; in-game moves travel over the
// already-established circuit and are unaffected.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        // The circuit's WebSocket is one long-lived request per player. Counting
        // it against a per-minute budget would disconnect people mid-game.
        if (http.WebSockets.IsWebSocketRequest)
            return RateLimitPartition.GetNoLimiter("websocket");

        var client = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            // A page load is a handful of requests, so this is far above human
            // use while still capping a flood.
            PermitLimit = 240,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// Sudoku DI registrations (Clean Architecture style)
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Scoped = per Blazor circuit, so each connected player gets their own storage.
builder.Services.AddScoped<GameStorage>();
// Same instance behind the application's persistence port.
builder.Services.AddScoped<IGameStore>(sp => sp.GetRequiredService<GameStorage>());
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Must run before anything that reads the scheme or the client address.
app.UseForwardedHeaders();

// Response hardening. Applied in every environment so that local runs and the
// CI smoke test exercise the same headers production serves - a header that is
// only set in production is a header nothing tests.
app.Use(async (context, next) =>
{
    // Written from OnStarting rather than inline: the framework appends its own
    // 'frame-ancestors' CSP while rendering, and setting ours up front left the
    // response carrying two Content-Security-Policy headers. This callback runs
    // last, so a single authoritative policy goes out.
    context.Response.OnStarting(static state =>
    {
        var headers = ((HttpContext)state).Response.Headers;

    // Never let a browser second-guess a declared Content-Type.
    headers["X-Content-Type-Options"] = "nosniff";
    // Legacy clickjacking defence; frame-ancestors below is the modern one.
    headers["X-Frame-Options"] = "SAMEORIGIN";
    // Do not leak the path a player was on to third-party sites.
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // This game needs none of these device capabilities.
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    // style-src needs 'unsafe-inline' because the board colour customisation
    // sets style attributes on elements, which no nonce or hash can cover.
    // script-src stays free of it: every script here is an external file.
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'";

        return Task.CompletedTask;
    }, context);

    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseRateLimiter();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Top-level statements compile to an internal Program; the security tests boot
// the real pipeline through WebApplicationFactory<Program>, which needs it public.
public partial class Program;
