# Context Loading Policy

This policy reduces AI startup context without deleting project information.
Canonical documents remain authoritative; summaries are navigation aids only.

## Non-negotiable rules

1. Read `docs/context/context-manifest.yaml` before selecting project files.
2. Read `docs/context/project-brief.md` and `docs/context/review-index.md` first for planning or review work.
3. Read a target GDD in full when reviewing or implementing that system.
4. Read only directly referenced dependency sections unless the workflow explicitly requires a cross-system audit.
5. Never load `production/session-logs/**`, `production/session-state/**`, or `.claude/agent-memory/**` by default.
6. Historical logs are read only when the user requests history, audit reconstruction, or a missing decision cannot be recovered from the current source.
7. Never replace a canonical GDD, registry entry, formula, acceptance criterion, or review decision with a summary.

## Source classes

| Class | Default read mode | Meaning |
|---|---|---|
| `canonical` | Full when in scope | Current GDDs, registry, source code, and pinned engine references. |
| `navigation` | Summary first | Project brief, systems index, and review index. Use them to choose the smallest relevant canonical set. |
| `history` | On demand | Review history, consistency history, session logs, and archived state. |
| `local` | Never by default | Agent memory, local settings, dumps, and machine-specific state. |

## Workflow profiles

### `planning`

Load the project brief, systems index, review index, current stage/review mode,
the target GDD, its direct dependencies, and the relevant registry entries.
Do not load raw session logs or full review histories.

### `target-review`

Load the target GDD in full, the systems-index row, direct dependency sections,
the latest review summary, and registry entries whose source points to the target.
Use the full review log only when a finding cannot be traced from the latest summary.

### `cross-system-audit`

Load all current canonical GDDs and the entity registry. Exclude review-history
logs and session logs unless the audit explicitly investigates historical drift.

### `historical-audit`

This is the only profile that may load archived review/session material. Scope it
to a date, system, or decision before reading files.

## Freshness and fallback

Every derived summary must name its source files and review date. If a summary is
older than a referenced canonical file, incomplete, or missing a required ID,
discard it for that task and read the canonical source instead.

## Integrity promise

Context reduction means selective loading and navigation summaries. It does not
mean lossy rewriting of canonical documents. Original history remains available
on disk and is never deleted by this policy.
