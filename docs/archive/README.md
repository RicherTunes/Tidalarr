# docs/archive

This directory holds historical documents that have been superseded by newer, actively maintained
equivalents.  Files here are **read-only references** — they are preserved for audit trail and
context but should not be edited or cited as current guidance.

## Contents

| File | Superseded by | Notes |
|------|---------------|-------|
| `TECH-DEBT-INVENTORY.md` | `docs/TECH_DEBT_BACKLOG.md` | Tracked tech debt from the initial v1.0 development when Tidalarr was a direct TidalSharp port. The architecture has since been rewritten using clean components and the shared library. |
| `TIDALARR_ARCHITECTURE_PLAN.md` | CLAUDE.md / implemented codebase | Initial planning-phase architecture doc; implementation diverged (net8.0, submodule-based shared lib). |
| `TIDALARR_CLEAN_ARCHITECTURE_PLAN.md` | CLAUDE.md / implemented codebase | Planning-phase clean-architecture spec; actual codebase has evolved beyond this plan. |
| `TIDALARR_FINAL_IMPLEMENTATION_PLAN.md` | CLAUDE.md / implemented codebase | One of several iteration plans created during development; superseded by V2/V3. |
| `TIDALARR_FINAL_IMPLEMENTATION_PLAN_V3.md` | CLAUDE.md / implemented codebase | Last iteration of the pre-implementation plan; the live codebase is the authoritative reference. |
| `TIDALARR_HARDENED_FINAL_PLAN.md` | CLAUDE.md / implemented codebase | Hardened variant of the final implementation plan; superseded by implemented code. |
| `TIDALARR_IMPLEMENTATION_PLAN_V2.md` | CLAUDE.md / implemented codebase | Early "direct port" plan that was superseded by the clean-architecture approach. |
| `TIDALARR_IMPLEMENTATION_PLAN_V2_EXTENDED.md` | CLAUDE.md / implemented codebase | Extended version of V2 iteration plan; superseded. |
| `TIDALARR_SHARED_LIBRARY_INTEGRATION_PLAN.md` | `docs/ITERATION_3_SHARED_LIBRARY_IMPROVEMENTS.md` | Shared library integration has been completed; plan is historical context only. |
| `TIDALARR_VALIDATED_IMPLEMENTATION_PLAN.md` | CLAUDE.md / implemented codebase | Feasibility-analysis-phase plan; superseded by implementation. |

## Policy

- Do **not** link to archived files from active documentation except to say "this was superseded by X".
- Do **not** update archived files; open a PR against the active replacement instead.
- To archive a new file: move it here with `git mv`, add a row to the table above, and update the
  active replacement to note the archival.
