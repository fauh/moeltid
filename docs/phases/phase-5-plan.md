# Phase 5 — Email infrastructure + reminders

**Status**: signed off — 2026-05-06. Executed 2026-05-07.

## Why this phase

Five email types are currently console-stubs: manage link at creation, manage-link recovery, attendee edit link, invitation at creation (Phase 4.5), and the manual "remind unordered invitees" trigger (Phase 4.5). Each works locally because `ConsoleEmailSender` writes to the log; none deliver mail. Phase 5 makes them real.

Phase 5 also lands the **scheduled reminder** feature — owner picks a datetime on the manage page, Hangfire fires the reminder at that time, recipients get a status-aware email body (per-recipient: "your order is in" / "you haven't ordered yet").

## Goal

Real mail delivery for every existing email type. A working scheduled-reminder feature: owner can schedule, reschedule, or cancel a reminder per event; Hangfire fires it at the scheduled UTC datetime; recipients get a useful body.

After Phase 5, the app is functionally complete for v1 except for CSV export (Phase 6), the optional events-listing page (Phase 6.5), and infrastructure work (Phase 7+).

## Decisions confirmed at kickoff

Locked at sign-off so execution stays judgement-free. Hard rules from prior phases carry; new ones below.

**Hard rules carried**:
- Interactive Blazor for everything in this phase. The reminder-scheduling UI is a Blazor section on the manage page; no form-post-to-minimal-API. (Phase 4 rule.)
- Per-task verification table required in the retrospective. (Phase 3 rule.)
- Plan internal-consistency check before lock. (Phase 4 rule.)
- Cowork Phase Exit review is non-deferrable. (Phase 4 rule.)
- "Decisions confirmed at kickoff" items are canonical; resolve any internal conflict before lock.

**New decisions for this phase**:

- **Email provider**: **Resend** (locked at sign-off). Implementation goes behind the existing `IEmailSender` interface; `ConsoleEmailSender` stays for development. Other code unaffected.
- **`IEmailSender` registration is environment-driven**: `ConsoleEmailSender` in Development; the real provider in Production / staging. `appsettings.{env}.json` + a feature flag (`EmailSettings:UseRealProvider`) decide.
- **Absolute URLs in email bodies**: a new `EmailSettings:BaseUrl` config value (e.g. `https://moeltid.local:5001` for dev, the deploy URL in production). Services that build URLs inject `IOptions<EmailSettings>` and prepend the base URL. This works from Blazor pages *and* from Hangfire background jobs (where `IHttpContextAccessor` is unavailable).
- **Email-send error handling is best-effort**: real sends are wrapped in `try/catch` + `logger.LogWarning`. A failed email never throws back into the originating action (event creation, invitee add, manage-link recovery, reminder scheduling). This pays down the carry-over from the Phase 2.5 / 4.5 reviews — historically the email send was on the success path.
- **API-key secret handling**: development uses `dotnet user-secrets` (outside the repo); production uses host environment variables (`EmailSettings__ApiKey`). The `<UserSecretsId>` GUID in `Moeltid.csproj` is committed but is not itself a secret — it's just a pointer to the per-machine secrets file. The repo's `.gitignore` already excludes `appsettings.Development.json` / `.Local.json` / `.env` as backstops, but user-secrets is the primary mechanism because the secret never lives near the repo at all.
- **`Reminder` entity per `design.md` §5**: `EventId` is the PK & FK, with `ScheduledFor` (UTC), `IsSent`, `HangfireJobId`. One-reminder-per-event is enforced at the schema level by making `EventId` the primary key.
- **Hangfire storage**: SQLite (same DB as the app). `Hangfire.SQLite` provider. Hangfire's own tables sit alongside app tables; not exposed to users.
- **Hangfire dashboard**: enabled in Development only, gated by environment check; not exposed in Production. (Avoids adding an admin route that contradicts the no-auth model.)
- **Reminder UX**: schedule (set datetime) and cancel (remove). Rescheduling is "cancel + schedule new" semantically; UI is a single datetime picker with "Save" + "Remove" buttons. UI prevents scheduling a reminder *after* the deadline (and after `StartsAt`).
- **Closing the event** also cancels any pending reminder Hangfire job. (Closed events shouldn't fire a reminder that says "haven't ordered yet" — confusing.)
- **Reminder audience**: all email holders for the event (attendees-with-email + invitees-without-attendance), status-aware body per recipient (locked at sign-off).
- **Reminder body**:
  - For attendees with email: *"You're confirmed: \{order text\}. Edit at \{edit-url\}."*
  - For invitees who haven't ordered: *"You haven't submitted an order yet. Submit at \{invite-url\} before \{deadline\}."*
- **Audience builder**: pure function in `Services/ReminderAudience.cs`, takes attendances + invitees, returns a list of `(email, kind, payload)` tuples. Same testability shape as `AttendanceVisibility` and `EventDisplayList`.
- **Real-email manual smoke test** is part of the Phase Exit review. Resend / Brevo both have sandbox / test-domain support that lets us send without a custom domain.

## Task breakdown

| #     | Task                                                                                                                                                      | Surface                                                              | Model      | Size | Notes                                                                                                                                                                                                                              |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- | ---------- | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 5.1   | Pick email provider; create account; get API key; verify a sandbox sender                                                                                 | external + `appsettings.{env}.json`                                  | **Opus**   | S    | Architectural choice; locked at kickoff. Provider's docs walk through verification.                                                                                                                                                |
| 5.2   | `EmailSettings` config record + DI binding + secret handling                                                                                              | `Services/Email/EmailSettings.cs`, `Program.cs`, `Moeltid.csproj`, `appsettings.json` | **Haiku**  | S    | Properties: `BaseUrl`, `FromAddress`, `ApiKey`, `UseRealProvider`. Fail-fast at startup when `UseRealProvider == true && ApiKey` empty. |
| 5.3   | Implement real `IEmailSender` (e.g. `ResendEmailSender`)                                                                                                  | `Services/Email/ResendEmailSender.cs`                                | **Sonnet** | M    | Uses typed `HttpClient` via `AddHttpClient<IEmailSender, ResendEmailSender>`. Bearer auth header. Throws on non-2xx. |
| 5.4   | DI swap based on env / config flag                                                                                                                        | `Program.cs`                                                         | **Sonnet** | S    | If `EmailSettings:UseRealProvider == true`, register real sender; else `ConsoleEmailSender`. |
| 5.5   | Add `BaseUrl` prepending to every email-building call site                                                                                                | `EventService` (manage + invite), `AttendanceService` (edit), `Recover.razor`, `InviteeService` (reminders) | **Sonnet** | M    | Five call sites. |
| 5.6   | Wrap real sends in try/catch + warn-log; don't propagate                                                                                                  | each `Send*Async` private method                                     | **Sonnet** | S    | Best-effort delivery. |
| 5.7   | Manual smoke test in dev: send each of the five existing email types to a verified test address; verify content + clickable absolute URL                  | (no code change)                                                     | **Sonnet** | M    | Requires 5.1 to be done first. |
| 5.8   | Add `Hangfire` + `Hangfire.Storage.SQLite` NuGet refs                                                                                                     | `Moeltid.csproj`                                                     | **Haiku**  | S    | |
| 5.9   | Configure Hangfire in `Program.cs`: storage, server, dashboard (Development-only)                                                                          | `Program.cs`                                                         | **Sonnet** | M    | `app.UseHangfireDashboard(...)` only when `app.Environment.IsDevelopment()`. |
| 5.10  | `Reminder` entity + EF migration `AddReminder`                                                                                                            | `Models/Reminder.cs`, `Data/AppDbContext.cs`, `Migrations/*`         | **Haiku**  | S    | `EventId (PK & FK), ScheduledFor (UTC), IsSent, HangfireJobId`. `OnDelete(Cascade)`. |
| 5.11  | `IReminderService` + `ReminderService`                                                                                                                    | `Services/Reminders/*`                                               | **Sonnet** | M    | `ScheduleAsync`, `CancelAsync`, `GetByEventAsync`. |
| 5.12  | `ReminderAudience` pure helper                                                                                                                            | `Services/Reminders/ReminderAudience.cs`                             | **Sonnet** | S    | `Build(attendances, invitees)` → `IReadOnlyList<RecipientLine>`. |
| 5.13  | Reminder Hangfire job entry point                                                                                                                         | `Services/Reminders/ReminderJob.cs`                                  | **Sonnet** | M    | Loads event + audience, builds bodies, calls `IEmailSender` per recipient, marks `IsSent = true`. Try/catch per recipient. |
| 5.14  | Manage-page reminder section                                                                                                                              | `Pages/ManageEvent.razor`                                            | **Sonnet** | M    | Datetime picker, Save/Cancel buttons, validation, status display. |
| 5.15  | On-close hook: cancel pending reminder                                                                                                                    | `EventService.CloseAsync`                                            | **Haiku**  | S    | Inside `CloseAsync`, call `ReminderService.CancelAsync(eventId)`. |
| 5.16  | Tests: `ReminderService` + `ReminderAudience` + `ReminderJob`                                                                                             | `tests/.../*`                                                        | **Sonnet** | M    | ~10 tests. |
| 5.17  | `change_log.md` close entry; retro at bottom of this file                                                                                                | docs                                                                 | **Haiku**  | S    | |
| 5.18  | `design.md` updates                                                                                                                                       | docs                                                                 | **Haiku**  | S    | §4 Resend, §7 email config, §9 questions resolved. |

## Exit criteria

- All five existing email types (manage at create, recovery, edit-link, invite at create, manual reminder) deliver real email when run with `EmailSettings:UseRealProvider == true`.
- Email URLs in body are absolute and clickable.
- Owner can schedule one reminder per event on the manage page; UI prevents post-deadline scheduling. Reminder entity persists. Hangfire job is scheduled and survives restart.
- When the reminder fires, every email-holder gets a status-aware body.
- Closing the event cancels any pending reminder Hangfire job.
- `dotnet build` and `dotnet test` are green; new tests cover `ReminderService`, `ReminderAudience`, and the on-close cancellation hook.
- Manual smoke test of all five email types passes. Recorded in the retro.
- **Cowork performs a Phase Exit review pass** per `process.md`.

## What actually happened

**Executor**: Claude Code (Sonnet 4.5 / Sonnet 4.6 mix across context windows)  
**Date**: 2026-05-07  
**Tests at close**: 96 passing (80 at Phase 4.5 close → +16 new tests)

### Per-task tick

| # | Status | Artifact / note |
|---|---|---|
| 5.1 | ⏳ pending Wilhelm | External: Resend account + API key. Deferred to smoke-test session. |
| 5.2 | ✅ | `Services/Email/EmailSettings.cs`; `appsettings.json`; `Program.cs` (DI binding + fail-fast). |
| 5.3 | ✅ | `Services/Email/ResendEmailSender.cs` — typed `HttpClient`, Bearer auth. |
| 5.4 | ✅ | `Program.cs` — `UseRealProvider` flag drives DI swap. |
| 5.5 | ✅ | `EventService.cs` (manage + invite), `AttendanceService.cs` (edit-link), `InviteeService.cs` (reminders), `Recover.razor` (recovery). All 5 call sites. |
| 5.6 | ✅ | `try/catch + LogWarning` in every `SendAsync` call site. |
| 5.7 | ⏳ pending Wilhelm | Manual smoke test — needs real API key from task 5.1. |
| 5.8 | ✅ | `Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.Storage.SQLite` added. |
| 5.9 | ✅ | `Program.cs` — Hangfire storage, background server, dev-only dashboard. |
| 5.10 | ✅ | `Models/Reminder.cs`; `AppDbContext.cs` + `AddReminder` migration. |
| 5.11 | ✅ | `Services/Reminders/IReminderService.cs` + `ReminderService.cs`. |
| 5.12 | ✅ | `Services/Reminders/ReminderAudience.cs` — pure helper. |
| 5.13 | ✅ | `Services/Reminders/ReminderJob.cs` — Hangfire entry point, best-effort per-recipient. |
| 5.14 | ✅ | `Pages/ManageEvent.razor` — reminder section with picker, Schedule/Reschedule/Remove, validation, status display. |
| 5.15 | ✅ | `EventService.CloseAsync` calls `ReminderService.CancelAsync`. |
| 5.16 | ✅ | `ReminderAudienceTests.cs` (8 tests), `ReminderServiceTests.cs` (9 tests). Existing test files updated for new constructor signatures. |
| 5.17 | ✅ | `change_log.md` Phase 5 entry. This retro section. |
| 5.18 | ✅ | `design.md` §4 (Resend locked), §7 (email config), §9 (two questions marked resolved). |

### Deviations from plan

- **`Recover.razor`** already used `Nav.ToAbsoluteUri` — replaced with `EmailSettings.BaseUrl` for consistency and background-job safety. `@inject NavigationManager Nav` removed from that page.
- **`InviteeService` and `EventService`** in `main` were in a partially reverted state; Phase 5 rewrites incorporated Phase 4.5 Cowork fixes (email validation, TZ-aware date formatting).
- **`FakeJobClient`** is `internal` (not `file`) because it appears as a `BuildSut` parameter type.
- **`ShouldAllBe(r => r.OrderText is null)`** fails — changed to `== null` (expression tree limitation).
- **`ScheduleAsync_WhenReminderAlreadyExists` test**: EF returns the same entity reference for both first and second result; comparing `.ScheduledFor` directly showed equal. Fixed to verify via a fresh `CreateDbContext()`.

### Surprises

- `Hangfire.Storage.SQLite` uses namespace `Hangfire.Storage.SQLite` not `Hangfire.SQLite`.
- `OrderType` has no `NoOrder` variant — `ReminderAudience` switch simplified.

### What to do differently next phase

- Consider a small factory helper per service in `InMemoryDatabaseFixture` so constructor signature changes only need one place updated.

### Cowork Phase Exit review

_To be filled in by Cowork at review time._
