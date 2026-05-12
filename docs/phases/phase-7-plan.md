# Phase 7 — Deploy infrastructure

(Plan owner: Cowork. Status: **signed off — 2026-05-12**. Last edit 2026-05-12.)

## Why this phase

The app has reached feature-completeness for v1 core (Phases 0–6.6: event creation, ordering, manage, invitations, email, CSV export, public browse). Until it's on the internet, no real Consid colleague can use it. Phase 7 is purely about deploying the existing app behind a stable URL — no new product features. Phase 8 polishes what we have; Phase 9 hardens for production launch.

## Goal

A live URL serves the app. Every `git push` to `main` builds, tests, and redeploys automatically. The full ordering flow (create event → submit order → view in `/events` → manage from the owner link) works end-to-end on the deployed instance. Hangfire is actively disabled in the deployed configuration — scheduled reminders are explicitly out of scope for Phase 7 and revisited when we upgrade hosting in Phase 9. Email runs through Resend's test mode (`onboarding@resend.dev` sender) until domain verification, also in Phase 9.

## Decisions confirmed at kickoff

(per AskUserQuestion exchange 2026-05-12, before plan was drafted)

- **Host: Render Free tier.** Trade-off accepted: 15-min idle spin-down causes a slow first request after sleep, but the app is internal-use enough that this is fine for v1. Cost stays at $0 until we explicitly choose to upgrade. Rationale: Fly.io was on the table for keeping Hangfire alive within free credit, but the "disable Hangfire entirely for the POC" decision (below) removes that constraint, making Render's simpler UX the better fit.
- **Hangfire actively disabled in deployed config.** A `Reminders:Enabled` config flag (default `true` in development, `false` in `appsettings.Production.json`) gates Hangfire DI registration, the dashboard, the `IRecurringJobManager`/`IBackgroundJobClient` services, and `IReminderService` (which we swap to a `NullReminderService` no-op when disabled). The reminder section in `ManageEvent.razor` is hidden when reminders are disabled. Phase 9 re-enables when we upgrade to a tier that stays warm.
- **Hangfire dashboard stays Development-only.** No production-side basic-auth or IP allowlist needed — the dashboard simply isn't registered when reminders are off, so the `/hangfire` route returns 404.
- **GitHub Actions auto-deploys on push to `main`.** Build + test runs on every push to any branch; deploy job is gated to `main`.
- **Custom domain deferred to Phase 9.** Phase 7 uses Render's free `*.onrender.com` subdomain.
- **Resend in test mode** with `onboarding@resend.dev` sender. Real domain verification deferred to Phase 9.

## Open questions

(none blocking the plan; flagged for sign-off)

- **Disk size** for the persistent `/data` volume. Working assumption: **1 GB** (Render's free tier max, comfortably more than SQLite will use for v1; Hangfire's storage is moot since it's disabled).
- **`EmailSettings:BaseUrl`** for prod will be set to the Render subdomain once the service is provisioned (between tasks 7.17 and 7.18). Will be set as a Render env var, not committed to the repo.
- **Reminder UI cleanup**: when `Reminders:Enabled = false`, should `Reminder.cs` model + `Reminders` DbSet + the `AddReminder` migration also be removed from the deployed schema, or kept dormant so re-enabling in Phase 9 is a one-flag flip? **Working assumption**: keep dormant — schema diffs add Phase 9 friction and offer no Phase 7 benefit.

## Prerequisites

- A Render account linked to Wilhelm's GitHub. (Free; create if not already.)
- A Resend account with an API key. (Already set up per Phase 5.)
- `phase-7` branch created off `main` (covered by task 7.1).

## Task breakdown

### Prep
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.1  | Create `phase-7` branch from `main`                                                   | git                                    | **Haiku**  | S    |

### Containerize
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.2  | Multi-stage `Dockerfile`: SDK 10 build → `aspnet:10.0-alpine` runtime, non-root user, chown `/data` for SQLite write access, expose 8080 | `Dockerfile` (new)                     | **Sonnet** | M    |
| 7.3  | `.dockerignore`: exclude `bin/`, `obj/`, `*.db`, `.git/`, `tests/`, `.claude/`, `.vs/` | `.dockerignore` (new)                  | **Haiku**  | S    |
| 7.4  | Add `/health` endpoint via `AddHealthChecks` + `MapHealthChecks("/health")`           | `Program.cs`                           | **Haiku**  | S    |
| 7.5  | Set `ASPNETCORE_URLS=http://+:8080` env in the Dockerfile (don't hardcode in Program.cs) | `Dockerfile`                         | **Haiku**  | S    |
| 7.6  | Local `docker build` + `docker run` smoke test: container starts, `Database.Migrate()` succeeds against `/data` mount, `/health` returns 200, `/events` renders | (verification) | **Haiku**  | S    |

### Disable Hangfire / Reminders in prod
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.7  | New `RemindersSettings` record with `Enabled: bool` (default `true`); bind from `Reminders:` section | `Services/Reminders/RemindersSettings.cs` (new) + `Program.cs` | **Sonnet** | S |
| 7.8  | In `Program.cs`, wrap `AddHangfire`/`AddHangfireServer`/`UseHangfireDashboard` (and the Hangfire NuGet-driven `IBackgroundJobClient` services) in `if (remindersSettings.Enabled)` | `Program.cs` | **Sonnet** | M |
| 7.9  | New `NullReminderService : IReminderService` — `ScheduleAsync` returns a dummy `Reminder`-or-throws-`NotSupportedException` (decide at task time), `CancelAsync` no-ops, `GetByEventAsync` returns `null`. Register conditional on the flag | `Services/Reminders/NullReminderService.cs` (new) + `Program.cs` | **Sonnet** | S |
| 7.10 | Hide the reminder-schedule section in `ManageEvent.razor` when `Reminders.Enabled == false` (inject `IOptions<RemindersSettings>`, gate the relevant `<div>` with `@if (RemindersEnabled)`) | `Pages/ManageEvent.razor` | **Sonnet** | S |
| 7.11 | Set `"Reminders": { "Enabled": false }` in `appsettings.Production.json` (create the file if absent) | `appsettings.Production.json` | **Haiku** | S |
| 7.12 | Tests: `NullReminderServiceTests` covering all three method shapes; existing `ReminderServiceTests` continue to pass under the enabled path | `tests/Moeltid.Tests/Services/NullReminderServiceTests.cs` (new) | **Sonnet** | S |

### Host config
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.13 | `render.yaml` blueprint: web service from Docker, persistent disk mounted at `/data` (1 GB), healthcheck path `/health`, auto-deploy off (we trigger via GH Actions), env-var stubs for `ASPNETCORE_ENVIRONMENT=Production`, `EmailSettings__*` | `render.yaml` (new) | **Sonnet** | M |
| 7.14 | Connection string respects a `DATA_DIR` env var (default `.` for dev so existing dev DB keeps working; prod sets to `/data`). `Program.cs` reads it and builds `Data Source={DATA_DIR}/moeltid.db` | `Program.cs` + `appsettings.json` | **Sonnet** | S |

### CI/CD
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.15 | `.github/workflows/ci.yml`: triggers on push and PR; runs `dotnet restore` → `dotnet build` (Release) → `dotnet test` (Release) | `.github/workflows/ci.yml` (new) | **Sonnet** | S |
| 7.16 | `.github/workflows/deploy.yml`: triggers on push to `main`; depends on the CI workflow's success; calls Render Deploy Hook URL (stored in GH Actions secret `RENDER_DEPLOY_HOOK`) | `.github/workflows/deploy.yml` (new) | **Sonnet** | S |

### First deploy (Wilhelm-driven; Cowork prepares + walks through)
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.17 | Provision the Render service from the blueprint; set env vars (`EmailSettings__ApiKey`, `EmailSettings__BaseUrl=<the_render_url>`, `EmailSettings__UseRealProvider=true`); copy the Deploy Hook URL into the GH Actions secret | Render dashboard + GitHub repo settings | **Sonnet** | M |
| 7.18 | First deploy: merge the phase-7 branch to `main`, watch the GH Actions deploy job + Render build log, confirm `https://*.onrender.com/` serves the app and `/health` returns 200 | (verification) | **Sonnet** | M |
| 7.19 | Smoke-test the full ordering flow on the live URL: create event with `wilhelm.ericsson@consid.se` → confirm manage email arrives → submit an order via another browser → see it in `/events` → use the manage URL to mark closed | (verification) | **Sonnet** | M |

### Docs
| #    | Task                                                                                  | Surface                                | Model    | Size |
| ---- | ------------------------------------------------------------------------------------- | -------------------------------------- | -------- | ---- |
| 7.20 | Update `design.md` §7 with actual host, URL, decision rationale (Hangfire-disabled note + cold-start trade-off) | `docs/design.md` | **Sonnet** | S |
| 7.21 | Update `CLAUDE.md` with prod environment quick-reference: deployed URL, where to find logs, how to redeploy manually | `CLAUDE.md` | **Sonnet** | S |
| 7.22 | Phase-close entry in `change_log.md` summarising the deploy + the host/Hangfire decisions | `change_log.md` | **Haiku** | S |

**Total**: 22 tasks.

## Risks / what might bite

- **First-deploy SQLite permissions**: the non-root container user may not have write access to the `/data` mount on first boot. Mitigation in 7.2 (chown the volume path in the Dockerfile); verify in 7.6 against a local volume mount.
- **Render free tier 15-min idle spin-down**: first request after a long idle gap takes ~30s. Acceptable for v1 internal use; possible Phase 8 polish to add a loading state to the landing page.
- **Resend test-mode sender deliverability**: emails from `onboarding@resend.dev` may land in spam for non-Resend-owned recipient domains. Already noted as a Phase 9 follow-up; Phase 7 doesn't change senders.
- **GH Actions secret leakage**: the Render Deploy Hook URL is a write-token. It must live in GH Actions secrets and never appear in workflow logs (mask with `::add-mask::` if echoed for debugging).
- **Migration race on first deploy**: `Database.Migrate()` runs at startup. With Render free running a single instance there's no real race, but worth noting for Phase 9 scale-out.
- **CSRF cookie security in prod**: with HTTPS terminated at Render's edge, antiforgery cookies must be `Secure`+`HttpOnly`. ASP.NET Core defaults are correct behind HTTPS, but verify in 7.19 via browser DevTools.
- **Disabled-reminders UI regression**: hiding the reminder section in `ManageEvent.razor` shouldn't break the page's other sections (close-event, rotate-token, manage meal options, manage invitees, orders table, CSV export, send-reminder-to-non-ordered-invitees). The "send reminder to non-ordered" action is a one-shot email send via `IEmailSender`, *not* a Hangfire scheduled job — confirm in 7.10 that it's left intact.

## Exit criteria

- A reachable `https://<service>.onrender.com` URL serves the live app.
- `git push` to `main` redeploys automatically — verified by watching at least one end-to-end deploy.
- Full ordering flow works on the deployed instance: create event → manage email arrives → submit order (separate browser/incognito) → order appears in `/events` → manage URL works.
- Hangfire is *not* running in production: `/hangfire` dashboard returns 404; reminder section is hidden on the manage page; no Hangfire-related lines in startup logs.
- `design.md` §7, `CLAUDE.md`, and `change_log.md` reflect the deployed reality.
- Cowork has performed the Phase Exit review pass per `process.md` §"Phase exit", with the navigation-reachability rule applied (re-read every page reachable from /events and from any manage-page link).

## What actually happened

(filled in by the executor at phase close — per `docs/process.md` §"Phase decomposition")

### Deviations from plan

### Surprises / what to do differently

### Per-task verification

### Cowork Phase Exit review

### Follow-up tasks
