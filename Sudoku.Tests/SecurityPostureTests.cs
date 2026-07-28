using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sudoku.Tests;

// Every assertion here corresponds to a finding from the security review.
// They boot the real pipeline in-process, so a header or cookie policy removed
// during a refactor fails `dotnet test` rather than reaching production - the
// smoke test only runs against something already built and started.
public class SecurityPostureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityPostureTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // Production is the configuration that actually faces the internet, so the
    // header and cookie assertions are made against it rather than Development.
    private WebApplicationFactory<Program> Production() =>
        _factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

    [Fact]
    public async Task Responses_forbid_content_type_sniffing()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    [Fact]
    public async Task Responses_carry_a_referrer_and_permissions_policy()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");

        Assert.Contains("strict-origin", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"));
    }

    [Fact]
    public async Task Responses_are_protected_against_framing()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");

        Assert.Contains(Header(response, "X-Frame-Options"), new[] { "SAMEORIGIN", "DENY" });
        Assert.Contains("frame-ancestors 'self'", Header(response, "Content-Security-Policy"));
    }

    // The review found a policy consisting only of frame-ancestors: clickjacking
    // was covered, script injection was not.
    [Fact]
    public async Task Content_security_policy_restricts_scripts_and_objects()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");
        var csp = Header(response, "Content-Security-Policy");

        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("base-uri 'self'", csp);
    }

    // Inline scripts are the payload of most XSS. Style needs 'unsafe-inline'
    // because the board colour pickers write style attributes, but script must
    // never acquire it by accident.
    [Fact]
    public async Task Content_security_policy_never_allows_inline_scripts()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");
        var csp = Header(response, "Content-Security-Policy");

        var scriptDirective = csp.Split(';')
            .Select(part => part.Trim())
            .Single(part => part.StartsWith("script-src", StringComparison.Ordinal));

        Assert.DoesNotContain("unsafe-inline", scriptDirective);
        Assert.DoesNotContain("unsafe-eval", scriptDirective);
    }

    // Regression guard: the framework appends its own frame-ancestors policy
    // while rendering. Setting ours too early produced two CSP headers, which
    // browsers intersect - correct by luck, confusing to audit.
    [Fact]
    public async Task Exactly_one_content_security_policy_header_is_sent()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
        Assert.Single(values!);
    }

    // The P1 finding. Behind a TLS-terminating proxy the request looks like
    // plain HTTP, so the SameAsRequest default silently drops Secure.
    [Fact]
    public void Antiforgery_cookie_is_secure_and_locked_down_in_production()
    {
        using var factory = Production();
        var options = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
    }

    // ...but Always THROWS on a genuinely non-SSL request, so development over
    // plain http://localhost must not use it. Setting it unconditionally turned
    // every POST into a 500; this pins the distinction.
    [Fact]
    public void Antiforgery_cookie_policy_does_not_break_plain_http_development()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var options = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
    }

    // Without this the app cannot see the original scheme or client address
    // behind App Service, which is what dropped the Secure flag in the first
    // place and would make any IP-partitioned rate limiting meaningless.
    [Fact]
    public void Forwarded_headers_are_processed()
    {
        using var factory = Production();
        var options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
    }

    [Fact]
    public async Task Unknown_host_headers_are_rejected()
    {
        using var client = Production().CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "evil.example.com";
        var response = await client.SendAsync(request);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_configured_host_is_still_served()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // A 404 must not hand an attacker framework versions or stack frames.
    [Fact]
    public async Task Not_found_responses_do_not_leak_diagnostics()
    {
        using var client = Production().CreateClient();

        var response = await client.GetAsync("/no-such-page");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Stack trace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.AspNetCore", body, StringComparison.Ordinal);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : string.Empty;
}
