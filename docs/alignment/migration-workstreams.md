# Migration Workstreams & Safeguards

## Workstream Overview
| ID | Scope | Key Tasks | Repos | Regression Safeguards |
| --- | --- | --- | --- | --- |
| WS1 | Shared MSBuild packaging | Add PluginPackaging.targets, manifest props, update both csproj files to import targets | Lidarr.Plugin.Common, Tidalarr, Qobuzarr | Verify dotnet build+uild.ps1 -Deploy produce identical artifact set; add CI check that inspects deploy folder contents. |
| WS2 | Streaming module unification | Introduce StreamingIntegrationModule, refactor Tidal/Qobuz modules to inherit shared base | Lidarr.Plugin.Common, Tidalarr, Qobuzarr | Expand existing DI/unit tests to instantiate modules; add new smoke test loading each assembly via reflection to ensure DryIoC registration still succeeds. |
| WS3 | Settings & quality alignment | Move shared enums/validators to common lib; update plugin settings & UI annotations | Lidarr.Plugin.Common, Tidalarr, Qobuzarr | Run existing validation/unit tests; add new [Theory] matrices covering quality permutations for both plugins. |
| WS4 | CLI/test harness | Publish StreamingCliHost utilities; migrate CLI projects and smoke tests to shared helpers | Lidarr.Plugin.Common, Tidalarr, Qobuzarr | Extend CLI smoke suite to exercise auth + download flows with mocks; ensure dotnet test runs in both repos post-migration. |
| WS5 | Analyzer & warning parity | Ship shared .editorconfig; enable warnings-as-errors in Qobuzarr after code cleanup | Lidarr.Plugin.Common, Tidalarr, Qobuzarr | Add CI gate running dotnet build -warnaserror in both repos; track suppression list centrally to avoid drift. |

## Sequencing & Gates
1. **Preparation (Week 1)**
   - Land WS1 in Lidarr.Plugin.Common with sample host tests.
   - Apply to Tidalarr first; ensure build/test/deploy succeed.
   - Create GitHub workflow to diff deploy folder contents for regressions.
2. **Module & Settings Alignment (Weeks 2-3)**
   - After packaging stabilises, implement WS2 + WS3 in parallel feature branches.
   - Gate merges on updated unit suites (	ests/Tidalarr.Tests, xt/qobuzarr/tests) and new DryIoC smoke tests.
3. **CLI/Test Harmonisation (Week 4)**
   - Roll out WS4 once modules/settings are consistent.
   - Introduce integration test that launches the CLI via dotnet run --project with mocked environment to ensure command wiring intact.
4. **Quality Gates (Week 5)**
   - Enable shared analyzers, remove bespoke warning suppressions, and enforce warnings-as-errors across both plugins.
   - Monitor build pipelines for new warnings; triage or suppress via shared config only.

## Risk Mitigation
- Maintain feature branches per workstream; avoid merging until both plugins and the common library compile/test successfully.
- Record baseline metrics (build time, test duration, plugin size) before each merge to confirm no regressions.
- Coordinate versioning: bump Lidarr.Plugin.Common once per merged workstream and update both plugin dependency pointers in the same PR window.
- Communicate changes via docs: update docs/alignment/ with status, and add release notes to each repo when behaviour changes (e.g., new settings UI).

With these workstreams and safeguards defined, Step 3 of the alignment plan is complete and the implementation phase can begin.

