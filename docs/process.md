# Process — how we work

This document is the meta-layer: how Claude and Wilhelm collaborate on Consid Måltid, how Claude picks which model to use for each task, and how decisions get recorded. It exists so we can revisit *why* something was done a particular way — and learn from it — months later. Update when the process itself evolves.

## Working rhythm

- **Claude drives, Wilhelm reviews at checkpoints.** Each phase ends at a sign-off boundary. Claude does not roll into the next phase without approval.
- **No silent decisions.** Any meaningful choice — about scope, architecture, library, model selection — gets logged in `change_log.md` and (where relevant) `design.md`. The change log is the diary; the design doc is the source of truth.
- **Living docs.** `design.md` and `roadmap.md` are revised as the project learns. The change log preserves the history of those revisions.
- **Working assumptions are explicit.** When Claude makes a call without confirmation, it's flagged as a "working assumption" in `design.md` §9 with a confirm-before-Phase-N stamp.

## Branch and PR convention

All feature work happens on a dedicated branch; `main` is the stable, reviewed base.

- **Branch naming**: `phase-N` for phase work (e.g. `phase-3`), `fix/short-description` for hotfixes, `chore/short-description` for non-functional changes.
- **One PR per phase** (or per meaningful logical unit). Open the PR when the phase is ready for review; merge after Wilhelm signs off.
- **`main` is never committed to directly** for new work. Hotfixes that are truly trivial (single-line, no risk) may be committed directly with agreement.
- Claude prepares the branch, commits, and opens the PR. Wilhelm reviews and merges (or asks Claude to address feedback first).

This convention was adopted from Phase 3 onwards (Phase 0–2 committed directly to `main` before the convention existed).

## Model selection

Claude has three models available, each suited to different work. Each task has a planned model, picked deliberately and recorded in the per-phase plan.

| Model | Strengths | Best for |
|---|---|---|
| **Opus** (most capable) | Deep reasoning, architectural decisions, debugging tricky issues, code review, multi-file refactoring with cross-cutting implications. Slowest, most expensive. | Anything where getting it wrong is expensive: stack choices, security boundaries, debugging mysteries, reviewing the final diff. |
| **Sonnet** (workhorse) | Standard coding, well-specified features, writing tests, applying patterns, documentation, CI/CD config. Fast, capable. | Most feature work — entity classes, services, Razor components, EF migrations once the model is decided, tests. |
| **Haiku** (fast & cheap) | Mechanical, well-specified, single-file tasks; verifying things; running commands. | Scaffolding (`dotnet new`), boilerplate config (`.editorconfig`), running build/test, simple grep/lint fixes, summaries. |

### Selection rubric

For each task, in order:

1. Could a competent intern do this with the spec in front of them, with no judgement calls? → start with **Haiku**.
2. Does this require pattern-matching, judgement, or non-trivial code generation, but the design is settled? → **Sonnet**.
3. Does the task require designing the approach, or could a wrong call here be hard to undo? → **Opus**.

When in doubt, escalate one level. A Haiku failure that Sonnet would have caught is more expensive than just running Sonnet first.

### Escalation triggers

If a task started on a smaller model:
- gets stuck twice in a row,
- starts making a security-relevant decision,
- begins touching more than 2–3 files in non-mechanical ways, or
- feels like it's fighting the spec,

→ escalate to the next tier and note it in the phase plan.

### Documenting model choices

Each phase plan (in `docs/phases/phase-N-plan.md`) lists the *planned* model per task. At phase end, the *actual* model used and any escalations are recorded in the same file under "What actually happened". Over time, this catalog is how we learn what kinds of work need which level of capability — and where our intuitions are wrong.

## Phase decomposition

Before any code is written for a phase, Claude writes `docs/phases/phase-N-plan.md` containing:

1. **Goal** — what "done" looks like (mirrors the roadmap exit criteria).
2. **Prerequisites** — anything Wilhelm needs to confirm or set up first.
3. **Task list** — phase broken into discrete, reviewable tasks. Each task has:
   - A short description
   - Affected files / surfaces
   - Planned model
   - Rough size (S / M / L) for visibility
   - Notes on dependencies or risks
4. **Open questions** — anything needing Wilhelm's call before work starts.
5. **Risks / what might bite** — known unknowns; honest about where we expect friction.

Wilhelm reviews and signs off (or pushes back) before any code is written. The plan is the contract for the phase.

After the phase, the same file gets a **"What actually happened"** section: actual models used, surprises, what we'd do differently. This is the learning tool.

## Documenting decisions — three files, three purposes

| File | Purpose | Edit frequency |
|---|---|---|
| `docs/design.md` | Current source of truth — architecture, scope, data model, page structure, auth strategy | Whenever a decision changes the design |
| `change_log.md` | Chronological diary of decisions and milestones | Every meaningful decision or phase boundary |
| `docs/phases/phase-N-plan.md` | Per-phase task plan + retrospective | Once at phase start, again at phase end |

If `design.md` and `change_log.md` ever conflict, `design.md` is canonical and the change log gets amended.

## Sandbox vs Windows — division of labour

Claude runs in a Linux sandbox that mounts Wilhelm's Windows workspace folder. The mount is good enough for reading and writing text files (via the file tools `Read` / `Write` / `Edit`), but **structured tooling like `git`, `dotnet`, `npm`, etc. cannot be run reliably from the sandbox against the mount**. Files written by such tools from the sandbox can come back as null-bytes or be undeletable, due to filesystem coherency limits.

Practical division of labour:

| Type of work | Where it runs |
|---|---|
| Writing / editing text files (source, config, docs) | Claude, via `Write` / `Edit` |
| Reading file content | Claude, via `Read` |
| Running `dotnet`, `git`, `npm`, etc. | Wilhelm, on Windows |
| Reviewing tool output, debugging | Both — Claude proposes, Wilhelm executes, both look at the result |

When a phase needs commands run, Claude provides them as copy-pasteable PowerShell / cmd snippets in chat (and ideally in the phase plan), then waits for Wilhelm to report back. Phase retrospectives should note any tasks that re-routed because of this.

This was discovered during Phase 1 (a `git init` from the sandbox left a broken `.git/` folder that had to be deleted from Windows). Documented to spare us repeating the experiment.

## When this file changes

Update `process.md` if:
- The model lineup changes (new model added or removed).
- The selection rubric proves wrong in practice and needs adjustment.
- The task / review rhythm changes.
- A new documentation file is introduced.

Each change to this file gets its own entry in `change_log.md`.
