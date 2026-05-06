# Phase 5 — Email infrastructure + reminders

**Status**: signed off — 2026-05-06. Awaiting executor pickup.

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

## Open questions — resolved at sign-off

All five resolved by Wilhelm 2026-05-06:

- ✅ **Email provider**: **Resend**. Cleaner API, modern .NET-friendly, generous free tier. Brevo's free tier is bigger but the API is older and less DX-polished.
- ✅ **Reminder audience**: all email holders (attendees-with-email + invitees-without-attendance). Body varies per recipient — *"you ordered X"* for attendees, *"you haven't submitted yet, deadline is Y"* for invitees-no-order.
- ✅ **One phase, not split.** 18 tasks; executor handles in one session. Pre-cleared to split mid-phase if it sprawls.
- ✅ **Hangfire dashboard**: Development-only via environment check. No production admin route.
- ✅ **Real-email test**: manual smoke during Phase Exit review, using Resend's sandbox / verified-addresses mode.

### Knock-on from sign-off (sign-off-decision review rule)

Re-checked every other §"Decisions confirmed at kickoff" item against the answers. No reasoning chains broken; no items needed updating. The plan's working assumptions all matched the picks.

## Prerequisites

- Phase 4.5 closed (it is — 80 tests passing).
- Wilhelm has (or can sign up for) an account with the chosen email provider — needed for API key + sandbox domain.
- `BaseUrl` for development (e.g. `https://localhost:5001`) decided.

## Task breakdown

| #     | Task                                                                                                                                                      | Surface                                                              | Model      | Size | Notes                                                                                                                                                                                                                              |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- | ---------- | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 5.1   | Pick email provider; create account; get API key; verify a sandbox sender                                                                                 | external + `appsettings.{env}.json`                                  | **Opus**   | S    | Architectural choice; locked at kickoff. Provider's docs walk through verification.                                                                                                                                                |
| 5.2   | `EmailSettings` config record + DI binding + secret handling                                                                                              | `Services/Email/EmailSettings.cs`, `Program.cs`, `Moeltid.csproj`, `appsettings.json` | **Haiku**  | S    | Properties: `BaseUrl`, `FromAddress`, `ApiKey`, `UseRealProvider`. **Dev API key**: `dotnet user-secrets init` (adds a `<UserSecretsId>` GUID to `Moeltid.csproj` — committed) then `dotnet user-secrets set "EmailSettings:ApiKey" "<key>"` (stored at `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` — never in repo). **Prod API key**: host env var `EmailSettings__ApiKey` (double-underscore in env-var name = colon in config key; .NET binds automatically). **Non-secret values** (`BaseUrl`, `FromAddress`, `UseRealProvider`) go in `appsettings.json` and `appsettings.{env}.json`. **Fail-fast at startup**: when `UseRealProvider == true`, verify `ApiKey` is non-empty; throw with a clear message if missing — silent send-time failures are worse than refusing to start. |
| 5.3   | Implement real `IEmailSender` (e.g. `ResendEmailSender`)                                                                                                  | `Services/Email/ResendEmailSender.cs`                                | **Sonnet** | M    | Uses typed `HttpClient` via `AddHttpClient<IEmailSender, ResendEmailSender>`. Bearer auth header. Throws on non-2xx; the caller decides what to do (we wrap in try/catch in 5.6).                                                  |
| 5.4   | DI swap based on env / config flag                                                                                                                        | `Program.cs`                                                         | **Sonnet** | S    | If `EmailSettings:UseRealProvider == true`, register the real sender; else `ConsoleEmailSender`. Convention: dev uses console, prod uses real. Override via env var for staging smoke tests.                                       |
| 5.5   | Add `BaseUrl` prepending to every email-building call site                                                                                                | `EventService` (manage + invite), `AttendanceService` (edit), `Recover.razor`, `InviteeService` (reminders) | **Sonnet** | M    | Inject `IOptions<EmailSettings>` into each service. Replace `$"/e/..."` with `$"{settings.BaseUrl}/e/..."`. Five call sites. Update existing tests if any assert on URL shape.                                                       |
| 5.6   | Wrap real sends in try/catch + warn-log; don't propagate                                                                                                  | each `Send*Async` private method                                     | **Sonnet** | S    | Best-effort delivery. Pays down the Phase 2.5 / 4.5 carry-over. `ConsoleEmailSender` is silent on success and throws nothing, so this only matters once the real provider is in.                                                   |
| 5.7   | Manual smoke test in dev: send each of the five existing email types to a verified test address; verify content + clickable absolute URL                  | (no code change)                                                     | **Sonnet** | M    | Includes attendee edit link, manage link at creation, recovery, invite at creation, manual "remind unordered invitees" trigger.                                                                                                    |
| 5.8   | Add `Hangfire` + `Hangfire.Storage.SQLite` NuGet refs                                                                                                     | `Moeltid.csproj`                                                     | **Haiku**  | S    |                                                                                                                                                                                                                                    |
| 5.9   | Configure Hangfire in `Program.cs`: storage, server, dashboard (Development-only)                                                                          | `Program.cs`                                                         | **Sonnet** | M    | `app.UseHangfireDashboard(...)` only when `app.Environment.IsDevelopment()`. Otherwise no route. Background server starts in all envs.                                                                                             |
| 5.10  | `Reminder` entity + EF migration `AddReminder`                                                                                                            | `Models/Reminder.cs`, `Data/AppDbContext.cs`, `Migrations/*`         | **Haiku**  | S    | Per design §5: `EventId (PK & FK), ScheduledFor (UTC), IsSent, HangfireJobId`. `OnDelete(Cascade)` on the FK so closing or deleting an event clears the reminder row.                                                              |
| 5.11  | `IReminderService` + `ReminderService`                                                                                                                    | `Services/Reminders/*`                                               | **Sonnet** | M    | Methods: `ScheduleAsync(eventId, whenUtc)` (creates/updates row, schedules Hangfire job, stores `HangfireJobId`); `CancelAsync(eventId)` (removes row, cancels Hangfire job); `GetByEventAsync(eventId)`. |
| 5.12  | `ReminderAudience` pure helper                                                                                                                            | `Services/ReminderAudience.cs`                                       | **Sonnet** | S    | Builds the per-recipient list: takes `(attendances, invitees)` → returns `IReadOnlyList<RecipientLine>` where each line has `(Email, Kind, OrderTextOrNull)`. Pure; tested without DB.                                              |
| 5.13  | Reminder Hangfire job entry point                                                                                                                         | `Services/Reminders/ReminderJob.cs`                                  | **Sonnet** | M    | Static or instance method registered with Hangfire. Loads event + audience, builds bodies, calls `IEmailSender` per recipient, marks `Reminder.IsSent = true`. Try/catch per recipient — one bad address doesn't sink the batch.   |
| 5.14  | Manage-page reminder section                                                                                                                              | `Pages/ManageEvent.razor`                                            | **Sonnet** | M    | Datetime picker (defaults to a sensible time before deadline), Save / Cancel buttons, validation: must be after `now`, before `Deadline`, before `StartsAt`. Shows "Reminder scheduled for X (TZ)" status.                          |
| 5.15  | On-close hook: cancel pending reminder                                                                                                                    | `EventService.CloseAsync`                                            | **Haiku**  | S    | Inside `CloseAsync`, after setting `IsClosed = true`, look up the `Reminder` and call `ReminderService.CancelAsync(eventId)` if a Hangfire job exists.                                                                              |
| 5.16  | Tests: `ReminderService` + `ReminderAudience` + `ReminderJob`                                                                                             | `tests/.../*`                                                        | **Sonnet** | M    | ~10 tests. Audience covers attendees-only, invitees-only, mixed, status-aware bodies. ReminderService tests use a fake Hangfire-job-id generator; we don't need real Hangfire in tests.                                            |
| 5.17  | `change_log.md` close entry; retro at bottom of this file (executor + actual model(s); per-task tick table)                                               | docs                                                                 | **Haiku**  | S    | Per `process.md` rules. Phase Exit review subsection added by Cowork.                                                                                                                                                              |
| 5.18  | `design.md` updates: §3 confirms reminder-audience answer; §4 mentions Resend/Brevo; §5 `Reminder` entity already there but verify; §7 deployment plan mentions email provider config | docs                                                                 | **Haiku**  | S    |                                                                                                                                                                                                                                    |

**Total**: 18 tasks. Mostly Sonnet (judgement-heavy services + page work). One **Opus** call for provider choice (architectural; locked at kickoff). Several Haiku for mechanical config + entity + migration work.

If we split into 5a (5.1–5.7, ~7 tasks) and 5b (5.8–5.18, ~11 tasks), the 5a half ships real email and URL absolutification on its own — usable independent of the reminder feature. See §"Open questions".

## Risks / what might bite

- **Domain verification with the email provider**: Resend / Brevo both require some DNS work for production custom domains. For dev we use the provider's sandbox/test domain to bypass this. Phase 7 / 9 (deploy + production launch) is when we wire up real DNS. Captured: we use sandbox in 5.7's smoke test.
- **`BaseUrl` mismatch in dev**: typing `localhost:5001` vs `127.0.0.1:5001` — emails that go to a real inbox click to the URL we configured. Easy to misconfigure. Mitigation: a single source-of-truth in `appsettings.{env}.json`.
- **Hangfire job persistence across restarts**: SQLite-backed Hangfire serializes job state to the DB. Restarts pick up scheduled jobs correctly. But the `HangfireJobId` we stored on `Reminder` must match the live job — if anyone manually deletes Hangfire's tables, we lose the link. Captured.
- **Time-zone drift between scheduled-time and event TZ**: `Reminder.ScheduledFor` is UTC. The manage UI lets the owner pick a datetime in the *event's* TZ (consistent with `NewEvent.razor`'s pattern). Conversion done by `TimeZoneHelper.ToUtc`. Same gotcha as Phase 2's TZ bug — the test should specifically cover Stockholm-summer round-trip.
- **Reminder audience join correctness**: `ReminderAudience` joins attendances + invitees by email (both lower-cased via value converters). Edge case: an attendee with no email (the "anonymous attendee" path from Phase 3 — `Email` is optional) is excluded from reminder recipients. Captured; documented in the audience builder.
- **Best-effort send swallows real errors**: by design (the carry-over from Phase 2.5 review), but we lose visibility. Mitigation: `LogWarning` includes the recipient + the exception type/message. If the provider returns 4xx (config/auth issue) we want loud logs.
- **Phase 5 grew — split if it sprawls**: the executor can split mid-phase per `process.md`. The 5a / 5b split is pre-cleared if needed.

## Exit criteria

- All five existing email types (manage at create, recovery, edit-link, invite at create, manual reminder) deliver real email when run with `EmailSettings:UseRealProvider == true`.
- Email URLs in body are absolute and clickable.
- Owner can schedule one reminder per event on the manage page; UI prevents post-deadline scheduling. Reminder entity persists. Hangfire job is scheduled and survives restart.
- When the reminder fires, every email-holder gets a status-aware body (attendees: "you ordered X"; invitees-no-order: "submit by X").
- Closing the event cancels any pending reminder Hangfire job.
- `dotnet build` and `dotnet test` are green; new tests cover `ReminderService`, `ReminderAudience`, and the on-close cancellation hook.
- Manual smoke test of all five email types passes (recipient, subject, absolute URL, body content all sane). Recorded in the retro.
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern", with the per-task verification rule applied. Findings recorded as a peer subsection in this file's "What actually happened".

## What actually happened

_To be filled in at phase end. Per `process.md`, must include: the executor (Claude Code, GitHub Copilot, or other), the actual model(s) run, deviations from plan, surprises, what to do differently, and an explicit per-task tick against the table above with each tick pointing at the specific code/test artifact. The Cowork-side Phase Exit review goes here as a peer subsection._
