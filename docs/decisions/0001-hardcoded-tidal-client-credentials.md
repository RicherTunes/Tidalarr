# ADR-0001: Hardcoded Tidal Client Credentials and Master Decryption Key

**Status:** Accepted

---

## Context

Tidalarr integrates with the Tidal streaming service using OAuth2 and AES-CTR stream decryption.
Three categories of values are embedded as compile-time constants:

1. **OAuth PKCE credentials** (`TidalConstants.CLIENT_ID_PKCE`, `TidalConstants.CLIENT_SECRET_PKCE`)
   used for the device-authorisation flow that most third-party clients rely on.
2. **Legacy OAuth client ID** (`TidalConstants.CLIENT_ID`) together with the Android redirect URI
   (`TidalConstants.REDIRECT_URI`), used as a fallback and for token refresh.
3. **Stream decryption master key** (`TidalStreamDecryptor.MasterKeyBase64`) used to derive
   per-track AES keys from Tidal's `securityToken` values.

A naive security scan flags all five values as potential secrets.  The purpose of this record is to
document why they are *not* secrets in the cryptographic sense and why hardcoding is the correct
approach.

---

## Decision

Embed the five values as source-level constants.  Do **not** load them from environment variables,
configuration files, or a secret store.

### Rationale

#### 1. These are not confidential credentials

The Tidal Android application ships these values in its APK.  APKs are ordinary ZIP archives; the
constants are recoverable via standard `dex2jar` / `jadx` decompilation tools with no specialist
knowledge.  They have been publicly documented and replicated in open-source projects since at least
2018, including:

- **TidalSharp** (the upstream C# library that Tidalarr wraps):
  `CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU"` and related values appear verbatim — see
  <https://github.com/TidalSharp> and related forks.
- **python-tidal / tidalapi**: the same `CLIENT_ID` (`zU4XHVVkc2tDPo4t`) has been present in the
  Python ecosystem for years.
- **tidal-dl**, **RedSea**, **Chimera**: each independently extracted and published the same values.

A GitHub code search for the string `6BDSRdpK9hqEBTgU` returns dozens of independent repositories
across multiple languages, confirming that these values are effectively public knowledge.

#### 2. Rotation is not a realistic mitigation

Tidal does not rotate these credentials on a per-consumer basis; they are embedded in every copy of
the official Android client that has shipped for the relevant version range.  If Tidal were to
invalidate them server-side, every third-party client — and every user of the official Android app
on that version — would simultaneously lose access.  Historically this has not happened, and the
ecosystem assumption is that these values are stable.

#### 3. Per-instance secrets would not improve security

If the values were moved to a secret store or environment variable, the benefit would be purely
cosmetic: any attacker who can intercept network traffic or inspect the running process can already
observe the OAuth flow.  The actual security boundary is Tidal's server-side rate-limiting,
per-session token revocation, and PKCE code-verifier binding — none of which depend on the client ID
being secret.

#### 4. Consistency with upstream TidalSharp

Tidalarr delegates the Tidal protocol implementation to TidalSharp (a git submodule).  Diverging
from TidalSharp's constants would introduce maintenance overhead and the risk of protocol
mismatches.  Keeping them identical and co-located in `TidalConstants.cs` makes future updates
trivial to audit.

---

## Consequences

- **Positive:** No secret-management infrastructure is required; contributors can build and run the
  project without provisioning credentials.
- **Positive:** The codebase stays in sync with TidalSharp's assumptions without a translation
  layer.
- **Negative:** Static-analysis tools (gitleaks, truffleHog) will flag these values as potential
  secrets.  The gitleaks allowlist in `.gitleaks.toml` is updated to suppress false positives for
  these specific constants (see the `[allowlist]` section referencing this ADR).
- **Neutral:** If Tidal ever does rotate these values, a one-line change to `TidalConstants.cs` and
  `TidalStreamDecryptor.cs` is sufficient to adopt the new values.

---

## References

1. TidalSharp upstream source (constants mirrored here):
   <https://github.com/search?q=6BDSRdpK9hqEBTgU&type=code>
2. python-tidal / tidalapi — same CLIENT_ID in use since 2018:
   <https://github.com/tamland/python-tidal>
3. PKCE RFC (explains why client_id alone is not a secret in PKCE flows):
   <https://www.rfc-editor.org/rfc/rfc7636>
4. Tidal Android APK decompilation guides (widely available; search "tidal apk decompile client_id").
