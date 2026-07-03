# Authentication

Tidalarr signs in to Tidal using **OAuth 2.0 with PKCE** (Proof Key for Code
Exchange, `S256` challenge method). No username or password is ever stored — the
plugin only keeps the access/refresh tokens returned by Tidal.

> Foundation auth plumbing (the `OAuthStreamingAuthenticationService` base class,
> token stores, refresh gating) lives in
> [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/SDK-and-Extension-Points.md).
> This page covers the Tidal-specific flow only.

## Sign-in flow (Lidarr UI)

1. In the plugin settings, open the Tidal sign-in step. The plugin generates an
   authorization URL containing the PKCE challenge, a CSRF `state` value, and a
   per-attempt client key.
2. Complete the login in your browser. Tidal redirects to its Android login
   callback (`https://tidal.com/android/login/auth`) with an authorization code
   in the URL.
3. Paste that **full redirect URL** back into the plugin. Tidalarr exchanges the
   code (plus the stored code verifier) for tokens and persists them.

Requested scope is `r_usr w_usr w_sub offline_access`; the `offline_access`
scope is what allows silent token refresh later.

## Sign-in flow (CLI)

`TidalCLI/` exposes the same flow for manual verification:

```bash
# 1. Print an authorization URL to open in your browser
dotnet run --project TidalCLI -- auth-start

# 2. Paste the full redirect URL Tidal sent you back
dotnet run --project TidalCLI -- auth-complete "<callbackUrl>"
```

The CLI persists its auth state to `%APPDATA%/Tidalarr/cli_auth_state.json`. (The
plugin runtime uses Lidarr's configured plugin config root instead.)

## Token storage & refresh

- Tokens are written through an `ITokenStore<TidalTokens>` abstraction
  (`src/Tidalarr/Infrastructure/Storage/`), stored with their expiry time.
- When a token is near or past expiry, Tidalarr refreshes it automatically using
  the refresh token. Refreshes are deduplicated by refresh-token value, so
  concurrent API calls that see the same expired token join one network refresh
  instead of racing Tidal's refresh-token rotation.
- Both the indexer and download client resolve credentials through the managed
  token provider. It checks the stored `ExpiresAt` value before each API use and
  proactively refreshes near-expiry tokens, so a long-running Lidarr session does
  not keep using an expired access token.
- If Tidal rejects an access token before its clock expiry, the shared OAuth
  handler asks the managed token provider to refresh immediately. The provider
  clears its cached Common session and re-primes from Tidal's persisted OAuth
  state, so future requests use the renewed token.
- Session fields (session id, country code) are recovered from the JWT claims
  (`sid`, `cc`) when the token response omits them.
- If Tidal revokes the refresh token (for example after password changes,
  manual logout, or account-side expiry), automatic renewal cannot recover; run
  the sign-in flow again to issue a new refresh token.

## Region / market

The indexer's **Tidal Market** setting controls the catalog region. It defaults
to `US`; the supported values are `US, UK, DE, FR, CA, AU, JP`
(`src/Tidalarr/Integration/TidalarrSettings.cs`).

## Common auth error

If you reuse a redirect URL (Tidal authorization codes are single-use), the
exchange fails with:

> Authorization code is invalid or expired — paste a fresh redirect URL from a
> new Tidal browser login (the previous code has been used).

**Fix:** start a brand-new browser login and paste the *fresh* redirect URL. See
[Troubleshooting](Troubleshooting.md) for other failure modes.
