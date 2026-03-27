Target Framework Rationale

Summary
- Core plugin targets net8.0 to match the Lidarr plugins-branch host ABI (.NET 8).
- CLI is not currently shipped and may target a newer TFM for developer convenience.

Details
Core (src/Tidalarr, net8.0)
- Compatibility with host: The Lidarr plugins branch (pr-plugins-3.x) runs on .NET 8. The plugin binary MUST target net8.0.
- Stable ABI: Minimizes loader friction and type-identity issues across MethodImpl/Runtime feature deltas.
- Packaging/ALC: Keeps the packaging closure small and predictable for AssemblyLoadContext.

IMPORTANT: Never use net6.0 images (pr-plugins-2.x tags). Loading a net8.0 plugin into a net6.0 host causes System.Runtime assembly load failures and Lidarr crash-loops.

What ships
- Only the net8.0 plugin zip (Lidarr.Plugin.Tidalarr.dll + plugin.json).
- CLI is excluded from packaging and distribution artifacts.

Operational Guidance
- Development: Install .NET 8 SDK.
- CI: Workflows set up the SDK for build/test and packaging.
- Consumers: Use only the packaged net8.0 zip in host deployments.
- Docker image: `ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913` (net8).
