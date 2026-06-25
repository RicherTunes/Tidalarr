Target Framework Rationale

Summary

- Core plugin targets net8.0 to match host ABI and maximize compatibility.
- CLI targets net8.0 for consistency with the core and to avoid complexity; CLI is not shipped.

Details
Core (src/Tidalarr, net8.0)

- Compatibility with host: Lidarr hosts that load plugins run on .NET 8, so the plugin binary must be compatible.
- Stable ABI: Minimizes loader friction and type-identity issues across MethodImpl/Runtime feature deltas.
- Packaging/ALC: Keeps the packaging closure small and predictable for AssemblyLoadContext.

IMPORTANT: Never use net6.0 images (pr-plugins-2.x tags). Loading a net8.0 plugin into a net6.0 host causes System.Runtime assembly load failures and Lidarr crash-loops.

CLI (TidalCLI, net8.0)

- Developer UX: Modern language/runtime features and better diagnostics.
- Not shipped: The CLI is a test harness and local tool; it is not part of the plugin package.
- CI support: Actions runners have .NET 8 available; we set up dotnet 8 in CI.

What ships

- Only the net8.0 plugin zip (Lidarr.Plugin.Tidalarr.dll + plugin.json). Common/Abstractions are ILRepack-merged and internalized into the plugin DLL — not shipped as separate runtime assemblies.
- CLI is excluded from packaging and distribution artifacts.

Future Multi-Targeting Plan

- Consider adding a newer TFM for CLI and core only after the Lidarr plugins-branch host runtime advances.
- Guardrails:
  - Keep public surface area stable across TFMs.
  - Verify packaging closure identical across TFMs.
  - Run smoke tests against a matrix of historical host versions.

Operational Guidance

- Development: Install .NET 8 SDK.
- CI: Workflows set up .NET 8 for build/test and packaging.
- Consumers: Use only the packaged net8.0 zip in host deployments.
- Docker image: `ghcr.io/hotio/lidarr:nightly-3.1.3.4970` (net8).
