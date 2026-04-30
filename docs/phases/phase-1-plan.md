# Phase 1 — Scaffold (local only)

**Status**: draft. Awaiting Wilhelm sign-off before any task is executed.

## Goal

A working Blazor Server (.NET 10) project on disk that runs locally with `dotnet run`, with version control set up and pushed to the prepared GitHub remote.

Hosting, Docker, and remote deployment are explicitly **deferred to Phase 7** to keep early iteration fast. The point of this phase is to get to a state where every later phase is just "open the project, write code, run, see it".

## Decisions confirmed at kickoff

- **Project name**: `Moeltid` (matching the GitHub repo `moeltid`). The user-facing brand stays "Consid Måltid" — that's separate from the code namespace.
- **Repo layout**: `Moeltid.sln` at repo root, `src/Moeltid/Moeltid.csproj` for the web project. Room for `tests/Moeltid.Tests/` later.
- **GitHub remote**: `https://github.com/fauh/moeltid.git` — already prepared by Wilhelm.

## Prerequisites — Wilhelm to confirm before kickoff

- [ ] **.NET 10 SDK** installed locally on Wilhelm's Windows machine (`dotnet --version` ≥ 10.0).
- [ ] `git` installed locally.
- [ ] Confirmation that **`C:\Workspace\consid\claude\consid_maltid`** is the repo root.
- [x] GitHub remote prepared: `https://github.com/fauh/moeltid.git`.

(Removed from prerequisites: Docker, Render/Fly.io account — those move to Phase 7.)

## Task breakdown

Tasks are atomic enough to review individually. The "Model" column is the *planned* model — recorded for learning. Sizes: **S** = a few minutes, **M** = a single sitting.

| #     | Task                                                                              | Surface                                | Model      | Size | Notes                                                                                          |
| ----- | --------------------------------------------------------------------------------- | -------------------------------------- | ---------- | ---- | ---------------------------------------------------------------------------------------------- |
| 1.1   | Verify prerequisites (`dotnet --version`, `git --version`)                        | shell only                             | **Haiku**  | S    | Two checks. Escalate to Sonnet if anything's missing and needs install guidance.               |
| 1.2   | Decide repo layout (project at root vs `src/Moeltid/`)                       | decision only — recorded in `design.md` | **Sonnet** | S    | Working recommendation: `src/Moeltid/Moeltid.csproj` with `Moeltid.sln` at repo root. |
| 1.3   | `git init` and first commit (current docs)                                        | `.git/`, existing files                | **Haiku**  | S    | Baseline. `main` as default branch.                                                            |
| 1.4   | `dotnet new blazorserver -n Moeltid -f net10.0` per agreed layout            | `src/Moeltid/**`, `*.sln`         | **Haiku**  | S    | Mechanical. `-f net10.0` pins the framework.                                                   |
| 1.5   | Add `.editorconfig` and `Directory.Build.props` (nullable, latest LangVersion, warnings-as-errors off) | `.editorconfig`, `Directory.Build.props` | **Haiku**  | S    | Standard .NET defaults.                                                                        |
| 1.6   | Smoke test locally: `dotnet run`, browse `https://localhost:{port}`               | shell only                             | **Haiku**  | S    | Confirm the default Blazor Server template renders. Escalate to Sonnet if it errors.           |
| 1.7   | Commit the scaffold                                                               | `.git/`                                | **Haiku**  | S    | One commit per logical step keeps history clean.                                               |
| 1.8   | Push to GitHub remote (`https://github.com/fauh/moeltid.git`)                     | `.git/`                                | **Haiku**  | S    | Wilhelm runs the push from his machine (Claude can't auth to GitHub from the sandbox). Claude prepares the commits and the `git remote add origin` line. |
| 1.9   | Phase retrospective — fill in "What actually happened"                            | this file                              | **Sonnet** | S    | Captures actual models used, surprises, lessons.                                               |

**Total**: 9 tasks (8 if no GitHub remote yet). Almost all Haiku — this phase is mechanical setup. One Sonnet call for the layout decision.

## Open questions for this phase

None outstanding — layout decided, remote confirmed, .NET 10 the agreed framework.

## Risks / what might bite

- **.NET 10 toolchain availability**: SDK should be installed and on `PATH`. If a stale .NET install is shadowing 10, `dotnet --version` will reveal it.
- **Default ports / HTTPS dev cert**: first run sometimes prompts for the dev cert (`dotnet dev-certs https --trust`). Captured in 1.6.
- **Solution file naming and location**: small choice, but affects every subsequent IDE open. Captured in 1.2.

(Removed risks from previous version: host selection, first-deploy failure, Docker base image cadence, free-tier limits — all deferred to Phase 7.)

## Sandbox limitation discovered during execution

When Claude tried to run `git init` from the Linux sandbox against the Windows-mounted workspace, the resulting `.git/config` file came back filled with null bytes when re-read from the sandbox side, breaking subsequent git commands. The Windows-side view of the file showed partially valid content. The sandbox could not delete the broken `.git/` folder (`Operation not permitted`).

**Conclusion**: the sandbox can write text files into the workspace folder via the file tools (`Write`/`Edit`), but cannot reliably run git or other tools that perform structured filesystem operations against that mount. Tools like `dotnet new`, `git init`, `git commit` must be run on Wilhelm's Windows side.

**Process implication for later phases**: all Phase-N execution that involves running build tools, scaffolding generators, or version-control operations against the workspace runs on Wilhelm's Windows machine. Claude can:
- write source and config files (Razor, C#, JSON, YAML, etc.) directly via `Write`/`Edit`,
- propose exact commands for Wilhelm to run,
- read the results once they're on disk,
- reason about failures and propose fixes.

This is also captured in `process.md`. The retrospective at the bottom of each phase plan should note any tasks that were re-routed to Wilhelm because of this.

## Cleanup needed before kicking off

The broken `.git/` folder from the failed `git init` is sitting in the workspace root. Wilhelm needs to delete it on Windows before running a clean `git init`:

```powershell
cd C:\Workspace\consid\claude\consid_maltid
Remove-Item -Recurse -Force .git
```

## Exit criteria

(Mirrors the roadmap.) `dotnet run` starts the app locally and the default Blazor Server template renders in the browser. The repo has version-controlled history of everything created so far. No deploy required.

## What actually happened

_To be filled in at phase end. Record: actual models used per task, escalations and why, surprises (good and bad), things to do differently next phase._
