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

After the phase, the same file gets a **"What actually happened"** section: **the executor used** (Claude Code, GitHub Copilot, other), the actual model(s) run, deviations from plan, surprises, what we'd do differently. This is the learning tool. See §"Executors" for why naming the executor matters.

## Phase exit — the two-tool review pattern

A phase isn't closed when Code says "done." Phases close after a **Cowork-side review pass** of the actual deliverables — pages, services, migrations, tests — against the phase plan, the design doc, and the change log.

The pattern that emerged from Phase 2:

1. Code executes the phase, fills in **"What actually happened"** in the phase plan, and stops at the phase boundary.
2. Cowork does a read-through: actual code + actual tests + retrospective + any earlier code the new feature now touches. Looks for: correctness gaps that masked themselves under happy-path testing, drift between code and `design.md`, missing validation, missing tests, naming or pattern inconsistencies.
3. Findings land in the phase plan as a **"Post-completion review findings"** subsection inside "What actually happened" — peer to Code's retro, never overwrites it. Severity-marked (🔴 / 🟡 / 🟢).
4. Anything actionable becomes a **follow-up task** numbered as `N.X` continuing the phase (e.g. `2.14`–`2.18` after Phase 2). Bugfix passes run on the same phase before the next phase starts; they fix already-shipped code rather than introducing new structure.
5. Once follow-up tasks merge, the phase is truly closed. `change_log.md` gets one entry summarising Code's close *and* the Cowork review pass.

Why the two-tool pattern:
- Code is closer to the code while writing it, but is also more likely to test the happy path it just authored. A read-through with fresh eyes against the design contract catches different things.
- The retrospective is a more honest learning artifact when Code's view and Cowork's view both appear, in chronological order, rather than being merged into a single voice.
- Bugfix passes that ride on the same phase don't pollute the next phase's diff.

Adopted from Phase 3 onwards. Phase 2 is where the pattern was learned; its retrospective retroactively reflects the structure.

## Bugfix discipline — clean working software over rapid progress

The repo is the contract Code and Copilot read to understand the project. Bugs sitting in the repo teach the next executor the wrong patterns. Therefore:

- 🔴 **Bugs** — incorrect behaviour, edge cases producing wrong output, UI states that get stuck (e.g. `EventCreated.razor`'s "Loading…" forever before its Phase 2.5 review fix) — are fixed **as soon as identified**, not batched into a later pass. This applies whether the bug surfaces mid-phase, during a Cowork review, or at any other time.
- 🟡 **Smells / improvements** — defensive issues, naming, missing tests, performance nits — can be batched into a follow-up pass before the next phase starts, or deferred to a specific later phase if they don't actively cause problems and aren't likely to mislead the next executor.
- 🟢 **Cosmetic / minor** items can be deferred indefinitely.

When Cowork can apply a fix safely via the file tools (small, well-understood changes — null checks, missing handlers, doc comments, pattern mirrors of other pages), it should: that's faster than queueing for the next executor and avoids leaving the bug in the repo where it might shape the next executor's reading. When the fix needs build/test verification or is structurally non-trivial, Cowork drafts it as a task; the executor applies it.

**Working principle (per Wilhelm, 2026-05-01): clean working software is better than rapid progress.** This is the same root as the "simplicity is KEY" project principle from `design.md`: less code in fewer places means fewer bugs and faster reasoning.

## Documenting decisions — three files, three purposes

| File | Purpose | Edit frequency |
|---|---|---|
| `docs/design.md` | Current source of truth — architecture, scope, data model, page structure, auth strategy | Whenever a decision changes the design |
| `change_log.md` | Chronological diary of decisions and milestones | Every meaningful decision or phase boundary |
| `docs/phases/phase-N-plan.md` | Per-phase task plan + retrospective | Once at phase start, again at phase end |

If `design.md` and `change_log.md` ever conflict, `design.md` is canonical and the change log gets amended.

## Testing conventions

Tests live in `tests/Moeltid.Tests/`. The stack is:

| Concern | Choice | Why |
|---|---|---|
| Framework | **xUnit** | Standard for .NET; fits the phase plan's testing approach. |
| Assertions | **Shouldly** | MIT-licensed; readable failure messages; direct swap for FluentAssertions. (FluentAssertions v8 changed to a commercial-license model mid-project — replaced at Phase 2.5 close.) |
| DB for service tests | **SQLite in-memory** (`Data Source=:memory:`) | Higher fidelity than EF in-memory provider (enforces constraints, runs migrations); negligible perf cost. One `SqliteConnection` kept open per test class via `InMemoryDatabaseFixture : IAsyncLifetime`. |

Test naming follows **Microsoft house style**: `MethodName_Scenario_ExpectedOutcome` — e.g. `CreateAsync_ValidRequest_LowerCasesOwnerEmail`. The `CA1707` analyzer warning (underscores in member names) is expected for test files; suppress at the project level if needed.

Tests for each service class live in `tests/Moeltid.Tests/Services/`. Infrastructure helpers (fixtures) live in `tests/Moeltid.Tests/Infrastructure/`.

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

## Executors — Claude Code, GitHub Copilot, or other

The execution side of this project (running `dotnet`, `git`, building, testing) lives outside Cowork on Wilhelm's machine. The **default executor is Claude Code** — it fits the project's process discipline neatly: it can pick a model per task per §"Model selection", and it reads `CLAUDE.md` automatically on entry to orient itself.

**GitHub Copilot is a valid fallback.** This first happened during Phase 2.5, when the Claude Code session hit usage limits mid-phase. Copilot opened the same workspace, read the same `phase-N-plan.md`, executed the remaining tasks, filled in the same retrospective, and closed the phase. Other IDE-embedded executors (Cursor, Aider, etc.) work the same way for the same reason: the docs are the contract, not any specific runtime.

When to switch executors:

- **Claude Code session hits a usage / token limit** mid-phase. The cleanest catch-up is to open Copilot in the same workspace and continue from the in-progress task in the phase plan.
- A specific task benefits from Copilot's IDE integration (run-tests-on-save loops, "go to definition" while reasoning, etc.).
- Wilhelm prefers IDE-embedded chat for a particular session.

What stays constant across executors:

- The phase plan in `docs/phases/phase-N-plan.md` is the contract — every executor reads it as their first step.
- **Every phase retrospective must name the executor and the actual model(s) run.** Required fields: executor, model(s), deviations, surprises, what to do differently. Without these the retrospective doesn't function as a learning artifact — six months from now we need to be able to look back and tell who/what produced a given outcome.
- **Every Cowork-side review subsection (per §"Phase exit") must reference the executor whose work is being reviewed.** Different executors have different blind spots and tendencies; recording who shipped the code helps the next review know what to look for and lets us calibrate the executor-selection rubric over time.
- The change log gets a phase-close entry from whoever closed the phase, naming the executor.
- Cowork still owns the Phase Exit review pass per §"Phase exit — the two-tool review pattern", regardless of who executed.

What may differ:

- **Per-task model selection.** Claude Code can run Haiku / Sonnet / Opus per task per the rubric. GitHub Copilot runs a single model per session (currently Sonnet-class). When Copilot executes a phase, the "Model" column in the plan is informational rather than directive — note the actual model in the retrospective.
- **Tooling quirks.** Copilot has shipped license-warning banners on third-party libraries (this is how the FluentAssertions → Shouldly swap got noticed in Phase 2.5). Different executors will surface different things; the retrospective is where those serendipities get recorded.

## When this file changes

Update `process.md` if:
- The model lineup changes (new model added or removed).
- The selection rubric proves wrong in practice and needs adjustment.
- The task / review rhythm changes.
- A new documentation file is introduced.

Each change to this file gets its own entry in `change_log.md`.
