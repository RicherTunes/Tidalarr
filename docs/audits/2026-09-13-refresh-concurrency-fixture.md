# Tidal refresh concurrency fixture audit

Date: 2026-09-13  
Branch: test/refresh-concurrency-barriers-20260913  
Base: 7c9e4bc9ea0802d118f1c47a67e9a7a54fed8289

## Finding

The retained push failure in
.omc/state/tech-debt-retirement/h1-pin-wave-20260913/job-102067-independent.log
used a 50 ms delay as its overlap proof. That permits the first refresh to
finish before all callers enter the managed provider, so the observed
new-access-2 values do not establish a duplicate overlapping refresh.

## Fixture correction

The managed-provider test now starts one public refresh, waits for the fake's
actual underlying call, captures seven follower tasks while that call is
blocked, asserts one provider call and incomplete public tasks, then releases
and joins all work. It also asserts the re-primed manager generation. A
sequential control proves a completed flight does not permanently suppress a
later explicit refresh.

The OAuth lifecycle test uses the real Common OAuthDelegatingHandler and
TidalOAuthService. Two original requests are observed with old-access before
either 401 is released. A single-use refresh HTTP fake is gated while the
first refresh is pending; the delayed second 401 must reuse new-access, with
one refresh request and the rotated refresh token persisted.

All event gates use RunContinuationsAsynchronously. Finally blocks release
every gate and join outstanding tasks. No production code, API, retry policy,
or token assertion was changed. The historical failure remains retained and
is not relabeled as resolved by this fixture-only candidate.

## Validation

The candidate is test-only plus this changelog and audit. Focused serialized
Release results and raw receipts are recorded beside this document after the
authorized run. No live Tidal credentials or external API calls are used.
