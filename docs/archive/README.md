# docs/archive

This directory holds historical documents that have been superseded by newer, actively maintained
equivalents.  Files here are **read-only references** — they are preserved for audit trail and
context but should not be edited or cited as current guidance.

## Contents

| File | Superseded by | Notes |
|------|---------------|-------|
| `TECH-DEBT-INVENTORY.md` | `docs/TECH_DEBT_BACKLOG.md` | Tracked tech debt from the initial v1.0 development when Tidalarr was a direct TidalSharp port.  The architecture has since been rewritten using clean components and the shared library. |

## Policy

- Do **not** link to archived files from active documentation except to say "this was superseded by X".
- Do **not** update archived files; open a PR against the active replacement instead.
- To archive a new file: move it here with `git mv`, add a row to the table above, and update the
  active replacement to note the archival.
