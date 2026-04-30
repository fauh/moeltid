# CLAUDE.md

Quick orientation for any Claude instance (Code, Cowork, future) starting work in this repo. Don't replace this file with a deep summary — it's the entry point. The substance lives in `docs/`.

## What this is

**Consid Måltid** — a web app for scheduling events and collecting food orders. Intentionally anonymous: no user accounts, no login. Identity is carried by tokens in URLs. Coordination tool, not a SaaS platform.

Stack: ASP.NET Core Blazor Server (.NET 10) · EF Core 10 · SQLite · Hangfire · Docker (Phase 7+).

## Read in this order

1. **`docs/design.md`** — source of truth. Architecture, scope, data model, page structure, identity model.
2. **`docs/roadmap.md`** — phased delivery plan.
3. **`docs/process.md`** — collaboration rhythm, model-selection rubric, sandbox/Windows division of labour.
4. **`docs/phases/`** — per-phase task plans and retrospectives. **The current phase's plan is the contract for the work in flight.**
5. **`change_log.md`** — chronological diary of every meaningful decision (newest first).
6. **`README.md`** — pointer document.

If `design.md` and `change_log.md` ever disagree, `design.md` wins and the change log gets amended.

## Current status

- Phase 0 (planning) — **complete**. See `docs/phases/phase-0-plan.md` for the retrospective.
- Phase 1 (local-only scaffold) — **in progress**. See `docs/phases/phase-1-plan.md`.

## Notes specifically for Claude Code

The Cowork session that did Phase 0 ran in a Linux sandbox that couldn't reliably do filesystem-structured operations (git, dotnet) against the Windows-mounted workspace. So:

- A **broken `.git/` folder** is sitting in the workspace root from a failed `git init`. **Delete it before initing fresh**:
  ```powershell
  Remove-Item -Recurse -Force .git
  ```
- Three pre-init config files are already on disk and good to commit: `.gitattributes`, `.editorconfig`, `Directory.Build.props` — plus the existing `.gitignore`, `README.md`, `change_log.md`, and the `docs/` tree.
- Phase 1 tasks 1.1, 1.3–1.4, 1.6–1.9 from `phase-1-plan.md` were the ones that needed Windows shell access. With Claude Code's native shell, you can do them in sequence without any handoff.

## After completing Phase 1

Fill in the "What actually happened" section at the bottom of `phase-1-plan.md` — actual models used per task, escalations and why, surprises, lessons. Add a dated entry in `change_log.md` summarising the phase. Mark task #2 complete in whatever task tracker the parent runtime uses (Cowork has one; Code may have its own).

## Working rhythm (summary, full version in `process.md`)

- Claude drives, Wilhelm reviews at phase boundaries.
- Phases end at a sign-off checkpoint. Don't roll into the next phase without approval.
- Documentation discipline: `design.md` is the source of truth; `change_log.md` is the diary; per-phase plans hold the task-level breakdown and the retrospective.
- Working assumptions get tagged with a "confirm before Phase N" stamp in `design.md` §9.
- For each non-trivial task, pick a model deliberately (Haiku / Sonnet / Opus per `process.md`) and record the choice in the phase plan.

## What this project is *not*

- A multi-user platform with accounts.
- A long-term relationship app.
- Where to put Microsoft Entra integration in v1 (it's an optional Phase 10).

If a feature suggestion implies "users will log in to manage their things over time", that's a smell — the design intentionally avoids it. Push back or surface it as a §9 question.
