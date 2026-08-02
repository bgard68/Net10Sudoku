# Security

## Reporting a vulnerability

Use GitHub's private reporting:
[**Report a vulnerability**](https://github.com/bgard68/Net10Sudoku/security/advisories/new).
It opens a private advisory visible only to the maintainer, so a problem can be
fixed before it is described in public. Please do not open a public issue for
anything exploitable.

Expect a first response within a week. This is a personal project with one
maintainer, not a product with an on-call rotation — that is the honest
expectation to set rather than a service level nobody is paying for.

## What the threat model actually is

Worth stating precisely, because this app sits between the two shapes people
usually assume. It is not a static page, and it is not a data-holding service.

- **There is a server, and it does more than serve files.** Blazor
  Interactive Server means every click is a round trip: the component tree
  lives on the server and the browser holds a SignalR circuit to it.
- **There are no accounts.** Nothing to log into, no sessions to steal, no
  authorisation to bypass.
- **There is no database and no user data.** A puzzle is generated, solved and
  discarded. Nothing about a player is stored anywhere.
- **There are no application secrets.** The only credentials are the Azure
  deployment identifiers, which live in GitHub's secret store and use OIDC
  federation rather than a stored password.

So the realistic exposure is not data theft — there is no data. It is
**availability and the supply chain**: whether the server can be made to spend
resources it does not have, and whether the code that reaches it is the code
that was reviewed.

### Worth reporting

- **Circuit resource exhaustion.** Interactive Server holds per-connection
  state, so anything letting one client open many circuits, or make one circuit
  consume disproportionate CPU or memory, is a genuine denial of service. The
  rate limiter and the circuit options exist for this; a way around them is
  worth knowing.
- **Host-header handling.** `AllowedHosts` is an explicit allow-list rather
  than `*`, deliberately, because a wildcard permits host-header injection and
  cache poisoning. A way to get an unexpected Host accepted is a real finding.
- **Cross-site scripting** through any value the puzzle UI renders, or a
  Content-Security-Policy bypass.
- **Anything that escapes the browser boundary** — one origin reading another's
  state, or the server being made to act on a request it should have refused.
- **Supply chain.** A path by which code or a dependency could reach the
  running site without passing the gate below.

### Not vulnerabilities here

- **"The site has no login."** There is nothing behind one to protect. No
  accounts is a design decision, not an oversight.
- **"The puzzle solution is discoverable client-side."** It is a Sudoku. There
  is no adversary and nothing is being protected by keeping it secret.
- **"Dependency X has a CVE"** with no path to exploitation here. NuGet audit
  runs in `all` mode and fails the build on any advisory with a fix available;
  a report that adds nothing to that is noise.

## How the pipeline is protected

Every pull request must pass, and cannot be merged without: build and tests, an
HTTP smoke test against a real instance, and CodeQL over both C# and
JavaScript. Every action is pinned to a commit SHA. The deploy authenticates to
Azure with OIDC — there is no stored publish profile or password.

A separate job, `gate-probes.yml`, shows each of those gates something it must
reject and fails if any of them does not. A check that cannot fail looks
exactly like a check with nothing to report, and the difference is invisible
until something is deliberately broken in front of it.

The wider posture — what is checked into this repository and what is
deliberately kept out — is in [docs/security.md](docs/security.md).
