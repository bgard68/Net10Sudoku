[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)

# Lessons: security

Findings from the DevOps security review, plus the two defects the review itself introduced while fixing them.

## Security hardening

These came out of a DevOps security review of the pipeline, the cloud posture
and the runtime. Two of them are bugs the review *introduced* while fixing
something else, which is the more useful half of the story.

### The reverse proxy that silently unsecured a cookie
**Bug:** The antiforgery token cookie was served without the `Secure`
attribute. Probing production showed `httponly` and `samesite=strict` present,
`secure` absent - while Azure's own affinity cookie in the same response had
it, which is what made the omission obvious rather than theoretical.

**Root cause:** App Service terminates TLS and forwards to Kestrel over plain
HTTP. The app never called `UseForwardedHeaders`, so `Request.IsHttps` was
`false` inside the process, and the default `SameAsRequest` cookie policy
faithfully declined to mark the cookie secure. Nothing was misconfigured in the
cookie code; the app simply could not see that it was serving HTTPS. The same
blindness would have made any IP-partitioned rate limiting meaningless, since
every client would have looked like the proxy.

**Fix:** Process `X-Forwarded-Proto` and `X-Forwarded-For`, and set the cookie
policy to `Always` outside Development.

**Lesson:** Behind a TLS-terminating proxy, "is this request secure?" is not a
question the app can answer on its own. Configure forwarded headers *first*;
several unrelated-looking defects downstream are really this one.

### The Secure-cookie fix that broke every POST
**Bug (self-inflicted, caught before it shipped):** Setting
`options.Cookie.SecurePolicy = CookieSecurePolicy.Always` unconditionally
turned every POST into a **500**.

**How it was found:** The smoke test's existing "POST to a page route is
rejected (405 or 404)" check went red immediately after the change - it had
been green on the previous run, so the change was the suspect. Nothing about a
cookie policy suggests a POST failure, so guessing would have been slow.

**How it was diagnosed:** Rather than reason about it, the app was started with
its console redirected to a log, a single `curl -X POST` fired at a page route,
and the log read. The exception named itself exactly:

```
System.InvalidOperationException: The antiforgery system has the configuration
value AntiforgeryOptions.Cookie.SecurePolicy = Always, but the current request
is not an SSL request.
   at Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery.CheckSSLConfig(HttpContext)
```

**Root cause:** ASP.NET Core does not merely skip the `Secure` attribute when
the policy cannot be honoured - it throws. So `Always` is safe only where every
request genuinely is SSL. Locally (plain `http://localhost`) it is not.

**Why this mattered more than a dev-only annoyance:** production reaches
`IsHttps == true` *only because* forwarded headers are configured. Had that
configuration been wrong or removed, the same exception would have fired on the
live site instead of a local one - a hardening change turning into an outage.

**Fix:** `Always` outside Development, `SameAsRequest` inside it. Both halves
are now pinned by tests, so neither can be "simplified" away.

**Lesson:** A security setting that throws when it cannot be satisfied is a
liveness risk, not just a correctness one. Verify hardening in an environment
that actually resembles production, and read the exception instead of inferring
it - the framework named the property, the value and the reason in one line.

### An API error is not evidence a feature is off
**Bug (in the review, not the code):** The security review reported that GitHub
secret scanning and push protection were not enabled, and recommended turning
them on. Both were already on. The claim came from the secret-scanning REST
endpoint returning *"Repository does not have GitHub Advanced Security
enabled"* - which is a statement about the paid GHAS **API surface** on a free
public repository, not about whether scanning is running. Opening
*Settings → Advanced Security* showed Secret Protection, push protection and
CodeQL all active.

**Fix:** Verify platform settings in the settings UI, and record the actual
state in `security.md` rather than an inference.

**Lesson:** A failed API call answers "can I query this?", not "is this
configured?". When a security control is reported missing, confirm it in the
place that owns the setting before writing it down - a false negative in a
security report wastes attention and erodes trust in the rest of the findings.

### One header, sent twice
**Bug:** After adding a Content-Security-Policy, responses carried **two** CSP
headers: the new policy, and a bare `frame-ancestors 'self'` the framework
appends while rendering. Browsers intersect duplicate policies, so the effective
result was correct - by luck.

**Fix:** Write the headers from a `Response.OnStarting` callback so they land
last and overwrite, plus a test asserting exactly one header is sent.

**Lesson:** "It works in the browser" is not the same as "it is what I
declared". Middleware ordering decides who writes last, and a response header
set too early is a suggestion rather than a decision.


[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)
