Target Framework Rationale

Summary
- Core plugin targets net6.0 to match host ABI and maximize compatibility.
- CLI targets net9.0 to leverage newer tooling and language/runtime features; CLI is not shipped.

Details
Core (src/Tidalarr, net6.0)
- Compatibility with host: Lidarr hosts that load plugins run on .NET 6, so the plugin binary must be compatible.
- Stable ABI: Minimizes loader friction and type-identity issues across MethodImpl/Runtime feature deltas.
- Packaging/ALC: Keeps the packaging closure small and predictable for AssemblyLoadContext.

CLI (TidalCLI, net9.0)
- Developer UX: Modern language/runtime features and better diagnostics.
- Not shipped: The CLI is a test harness and local tool; it is not part of the plugin package.
- CI support: Actions runners have .NET 9 available; we set up dotnet 6 and 9 in CI.

What ships
- Only the net6.0 plugin zip (Lidarr.Plugin.Tidalarr.dll + Common runtime and plugin.json).
- CLI is excluded from packaging and distribution artifacts.

Future Multi-Targeting Plan
- Consider adding net8.0/net9.0 for core once the host runtime advances or we introduce ID-based diagnostics everywhere (already in place).
- Guardrails:
  - Keep public surface area stable across TFMs.
  - Verify packaging closure identical across TFMs.
  - Run smoke tests against a matrix of historical host versions.

Operational Guidance
- Development: Install .NET 6 and 9 SDKs.
- CI: Workflows set up both SDKs for build/test and packaging.
- Consumers: Use only the packaged net6.0 zip in host deployments.

