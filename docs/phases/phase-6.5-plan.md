# Phase 6.5 — Events listing / discovery

**Status**: signed off — 2026-05-11. Awaiting executor pickup.

## Why this phase

Acknowledged scope creep, raised by Wilhelm during the Phase 4.5 planning. The motivation:

> "Today the only way for a user to find an event they're part of is to keep the URL. Lose the URL → use the recover flow (for events you own) or you're stuck (for events you've ordered). A discovery list page would give users a way back into events without needing to re-find URLs."

This phase delivers that list page while preserving the v1 no-auth, not-browseable design model.

The central tension: a list page rendered in the browser inherently reveals "what events is this email tied to" — which is exactly the leak the privacy model is trying to prevent. The resolution is a **magic-link flow**: typing an email never renders anything sensitive directly; it sends an email with a short-lived, one-time link that opens the list page. The information only ever reaches the email holder.

## Goal

A user lands on the landing page (or types `/my-events` directly), enters their email, gets a "check your inbox" message. An email arrives with a magic link. Clicking the link opens a page listing every event tied to that email — as owner, attendee, and/or invitee — with a role-appropriate action link per row. Token expires after 1 hour; consumed on first valid use.

After Phase 6.5, the only remaining v1 paths are Phase 7 (deploy) → Phase 8 (polish) → Phase 9 (production launch).

## Decisions confirmed at kickoff

Locked at sign-off so execution stays judgement-free. Hard rules from prior phases carry.

**Hard rules carried**:
- Interactive Blazor for the request form (mutating action — submit triggers email send). The list-rendering page is also interactive Blazor; it reads `?t={token}` from the URL and renders directly (no form post). (Phase 4 rule applies as written.)
- Per-task verification table in the retrospective. (Phase 3 rule.)
- Plan internal-consistency check before lock. (Phase 4 rule.)
- Cowork Phase Exit review non-deferrable. (Phase 4 rule.)
- Best-effort email send: `try/catch` + `LogWarning`; never propagate. (Phase 5 rule.)
- Absolute URLs in email bodies via `EmailSettings:BaseUrl`. (Phase 5 rule.)
- Pure helpers for layered/joined views (the `AttendanceVisibility` / `EventDisplayList` / `ReminderAudience` / `CsvExportBuilder` pattern). The list aggregator follows.

**New decisions for this phase**:

- **Auth model is magic-link via short-lived token**. Mirrors `/recover`'s privacy posture (information goes to the email, not to the typed-in browser) while permitting a browser-rendered list. Captured in §Why this phase.
- **New entity `MyEventsAccessToken`**: `Token` (PK, 32-char URL-safe random), `Email` (lower-cased via value converter), `IssuedAt`, `ExpiresAt`, `ConsumedAt?`. Schema enforces single-use via `ConsumedAt`. Not on `UNIQUE(Email, ...)` — a user can have multiple pending tokens (e.g. requested twice).
- **Token expiry: 1 hour**. Tight enough to limit leaked-link blast radius; long enough that the user has time to switch tabs to their inbox.
- **Token is single-use**. `ConsumedAt` set on first valid use; subsequent visits with the same token render a "this link has expired or been used" page (same generic message regardless of cause — don't leak whether the token was real-but-consumed vs random-bytes).
- **Three roles considered for the email match**: owner (via `Event.OwnerEmail`), attendee (via `Attendance.Email`), invitee (via `Invitee.Email`). All three are joined; the same user can hold multiple roles in one event (e.g. owner + invitee). Per-event row shows a combined role badge.
- **Pure helper `MyEventsListBuilder`**: takes the three lists, returns `IReadOnlyList<MyEventRow>` with `Event`, `Roles` (flags enum), and pre-resolved action URL per row. Same shape as `EventDisplayList` / `ReminderAudience`.
- **Per-row action URL** picks the highest-privilege role:
  - **Owner** → manage URL: `/e/{slug}/manage?t={ManageToken}`
  - **Attendee** (not owner) → view/edit URL: `/e/{slug}?t={EditToken}`
  - **Invitee only** → public URL with invite pre-fill: `/e/{slug}?invite={inviteeId}`
- **Default view: ongoing events first**, past events in a collapsed section below (locked at sign-off; changed from "ongoing only" working assumption). Ongoing: `!IsClosed AND StartsAt > now`. Past: `IsClosed OR StartsAt <= now`. The past-events section renders as `<details>` collapsed by default; the user clicks to expand. Both lists share the same row shape (title, date, role badge, action URL).
- **Email send is fire-and-forget** (locked at sign-off). `RequestAccessAsync` writes the token row, kicks off the email send via `Task.Run` (or `_ = SendAsync(...)`) without awaiting, and returns immediately. The page renders the generic "if your email matches, check your inbox" message at the same speed regardless of whether the email matched anything — closes the timing side-channel that would otherwise let an attacker probe email existence. Send failures still get logged via the `IEmailSender` implementation's own try/catch; we just don't block on them here.
- **Email body** lists the events directly (mirrors `/recover`'s pattern — comprehensive content in the email) **and** carries the magic-link URL. Two redundant paths, same posture: useful in inbox, also clickable into the browser list.
- **Page placement**: new `/my-events` page. Landing-page gets a second link next to "Lost your manage link?" → "See all your events". `/recover` stays separate; it's faster for the specific case of "I lost the manage URL and just want it back" (single step, no browser visit). Phase 8 polish could revisit collapsing them.
- **Email validation** at request time via `EmailAddressAttribute.IsValid` (same pattern as Phase 4.5's `InviteeService`). Rejects malformed input before persisting the token.

## Open questions — resolved at sign-off

All five resolved by Wilhelm 2026-05-11:

- ✅ **"Ongoing" as default + past in collapsed section.** Reversal of the working assumption (which was ongoing-only). Wilhelm: "Yes include a collapsed section for past events." Folded into §Decisions and tasks 6.5.4 + 6.5.7.
- ✅ **Single-use token.** Tighter blast radius if the link leaks.
- ✅ **`/recover` stays separate.** Different use case, different mechanism.
- ✅ **Magic link** (not immediate-render). Consistent with the v1 no-auth identity-via-tokens model.
- ✅ **Fire-and-forget email send.** Closes the timing side-channel. Captured in §Decisions.

### Knock-on from sign-off (sign-off-decision review rule)

Re-checked every other §"Decisions confirmed at kickoff" item against the answers. **One ripple**: the "ongoing only" decision became "ongoing first + past collapsed" — tasks 6.5.4 (`MyEventsListBuilder` now returns all events with an `IsOngoing` flag or category enum, not a filtered slice) and 6.5.7 (`/my-events` page renders ongoing first, past in a `<details>` collapsed block) updated accordingly. Exit criteria amended.

No other items affected. The fire-and-forget addition is a new explicit §Decisions bullet rather than a change to an existing one.

### Adjacent fix (applied 2026-05-11, separate from this phase)

Wilhelm noticed that `/recover` sends one email per matching event — N events = N emails. Inbox-clutter bug. **Fixed by Cowork** as an immediate edit per the bugfix-immediately discipline; `/recover` now sends ONE email with all manage links listed in the body, conditional on count (singular phrasing for N=1, list format for N>1). Not part of Phase 6.5; just adjacent in topic.

## Prerequisites

- Phase 6 closed (it is — review complete, all 107 tests passing).

## Task breakdown

| #      | Task                                                                                                                                | Surface                                                                | Model      | Size | Notes                                                                                                                                                                                                                                |
| ------ | ----------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | ---------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 6.5.1  | `MyEventsAccessToken` entity + EF migration                                                                                          | `Models/MyEventsAccessToken.cs`, `Data/AppDbContext.cs`, `Migrations/*` | **Haiku**  | S    | PK is `Token` (32-char random). `Email` value-converted to lower-case. `IssuedAt`, `ExpiresAt`, `ConsumedAt?`. No FK relationships (it's a transient credential, not tied to a specific event).                                       |
| 6.5.2  | `IMyEventsService` interface + DTOs                                                                                                  | `Services/MyEvents/IMyEventsService.cs`                                | **Haiku**  | S    | Methods: `RequestAccessAsync(email)` → sends email; `ValidateAndConsumeAsync(token)` → returns email or null; `GetEventsForEmailAsync(email)` → returns aggregated rows.                                                              |
| 6.5.3  | `MyEventsService` implementation                                                                                                     | `Services/MyEvents/MyEventsService.cs`                                 | **Sonnet** | M    | `RequestAccessAsync` validates email format, generates token, persists, calls `IEmailSender`. `ValidateAndConsumeAsync` finds row, checks expiry + not-consumed, marks consumed, returns email. Best-effort email try/catch.        |
| 6.5.4  | `MyEventsListBuilder` pure helper                                                                                                    | `Services/MyEvents/MyEventsListBuilder.cs`                             | **Sonnet** | S    | Takes events-owned + attendances + invitees, returns `IReadOnlyList<MyEventRow>` deduped by `EventId`, with combined `Roles` flags, a single best-action URL per row, AND an `IsOngoing` flag (`!Event.IsClosed && Event.StartsAt > now`). Pure; testable without DB. Rows ordered: ongoing first by `StartsAt` ascending, then past by `StartsAt` descending.    |
| 6.5.5  | DI registration                                                                                                                      | `Program.cs`                                                           | **Haiku**  | S    | Scoped `IMyEventsService`.                                                                                                                                                                                                            |
| 6.5.6  | Add `GetByEmailAsync` to `AttendanceService` and `InviteeService` if not present, OR plumb queries directly through `MyEventsService` | services                                                               | **Haiku**  | S    | Existing: `EventService.GetByOwnerEmailAsync` ✓ from Phase 4; `InviteeService` has list-by-event but not list-by-email. Add a small `ListByEmailAsync(email)` on each. Mirror lower-cased compare.                                    |
| 6.5.7  | `MyEvents.razor` page — request form + list view (ongoing + past-collapsed)                                                          | `Pages/MyEvents.razor`                                                 | **Sonnet** | M    | `@page "/my-events"`. Reads `?t=` via `[SupplyParameterFromQuery]`. If `?t=` valid → call `ValidateAndConsumeAsync` + `GetEventsForEmailAsync` + render. **Layout**: ongoing rows visible by default; past rows wrapped in `<details><summary>Past events ({count})</summary>...</details>` collapsed. If `?t=` invalid or absent → render request form with the same generic "check your inbox" success message. |
| 6.5.8  | Landing-page link                                                                                                                    | `Pages/Index.razor`                                                    | **Haiku**  | S    | New line: "Looking for events you're part of? **See all your events**". Links to `/my-events`.                                                                                                                                       |
| 6.5.9  | Email body for the magic link                                                                                                        | inside `MyEventsService.RequestAccessAsync`                            | **Sonnet** | S    | Body lists matching events (title + date in event TZ + role) AND includes the magic-link URL. Both useful from inbox. Absolute URLs via `EmailSettings:BaseUrl`. TZ-aware datetime formatting (Phase 4.5 lesson).                       |
| 6.5.10 | Tests: `MyEventsService` (token lifecycle, expiry, consumption, email validation)                                                    | `tests/.../MyEventsServiceTests.cs`                                    | **Sonnet** | M    | ~8 tests: malformed email throws; valid request persists token + sends; `ValidateAndConsumeAsync` returns email + marks consumed; second use of same token returns null; expired token returns null.                                  |
| 6.5.11 | Tests: `MyEventsListBuilder` pure helper                                                                                             | `tests/.../MyEventsListBuilderTests.cs`                                | **Sonnet** | S    | ~6 tests: owner-only row, attendee-only row, invitee-only row, dual-role same event (combined badge + best-action URL), ongoing filter excludes past/closed, ordering by `StartsAt` ascending.                                       |
| 6.5.12 | `design.md` updates: §3 scope (`/my-events` discovery), §5 (new entity), §6 (new route)                                              | docs                                                                   | **Haiku**  | S    |                                                                                                                                                                                                                                      |
| 6.5.13 | `change_log.md` close entry; retro at bottom of this file (executor + actual model(s); per-task tick table)                          | docs                                                                   | **Haiku**  | S    | Per `process.md`.                                                                                                                                                                                                                    |

**Total**: 13 tasks. Mostly Sonnet (judgement-heavy services + page + tests). Haiku for entity + DI + docs. No Opus — all architectural decisions are committed at sign-off.

Comparable size to Phase 4.5 (15 tasks). Single coherent phase.

## Risks / what might bite

- **Token cleanup**: expired `MyEventsAccessToken` rows accumulate. Not urgent — they're small — but a future janitor job (Hangfire recurring) could prune them. **Mitigation**: not in scope this phase; revisit at Phase 9 alongside the event retention policy (`design.md` §9). Captured.
- **Email body absolute URLs**: same Phase 5 lesson — `EmailSettings:BaseUrl` is the source of truth. If `BaseUrl` is wrong, the magic link goes to the wrong host. Already a known pattern; no new gotcha.
- **Side-channel via email-rendering time**: if the request endpoint takes meaningfully longer when the email matches vs doesn't (because we send an email in the match case), a timing attack could probe email existence. **Mitigation**: send the email in the background (`Task.Run` fire-and-forget) OR always do equivalent work whether or not we found a match. Cheapest: don't await the send before returning to the page. **Captured as a §Decisions item** — confirm the working approach at sign-off.
- **`MyEventsListBuilder` aggregation correctness**: same `EventId` might appear in all three input lists. Deduping by `EventId` is the contract; tests must cover the case explicitly.
- **`?t=` token in browser history**: same posture as the manage-URL token. Single-use mitigates: even if the URL is in history, second use fails.
- **Magic-link email send failure**: `RequestAccessAsync` is best-effort per the Phase 5 rule. User sees "if your email matches, check your inbox" regardless. If the send genuinely failed, user can request again.

## Exit criteria

- A user can type their email at `/my-events`, receive an email with a magic link, click the link, and see events where their email matches as owner / attendee / invitee. **Ongoing events** (`!IsClosed AND StartsAt > now`) render visible by default; **past events** in a collapsed `<details>` block at the bottom.
- Each row shows event title, date in the event's TZ, role badge(s), and a single best-action button.
- Token is single-use: a second visit with the same token renders a generic "link expired or used" page.
- Token expires after 1 hour: a visit after expiry renders the same generic page.
- The request form always shows the same "if your email matches, check your inbox" message — no leak of whether the email matched anything.
- `dotnet build` and `dotnet test` are green; new `MyEventsService` and `MyEventsListBuilder` tests cover the cases listed in §Tasks.
- `design.md` updated.
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern", with the per-task verification rule applied. Findings recorded as a peer subsection in this file's "What actually happened".

## What actually happened

**Executor**: Claude Code (Anthropic).
**Models used**: Sonnet 4.6 throughout. No escalation to Opus needed; all architectural decisions were locked at sign-off.
**Execution date**: 2026-05-12.

### Deviations from plan

- **No deviations.** 13 tasks executed as scoped.
- Test count: 18 new tests (116 → 134). Plan estimated ~14 (8 + 6). Extra tests in `MyEventsServiceTests`: email-lowercasing assertion, `GetEventsForEmailAsync_ReturnsOwnerEvents` end-to-end smoke. Extra tests in `MyEventsListBuilderTests`: `AttendeeAndInvitee_SameEvent_AttendeeUrlWins`, `OngoingFlag_FalseForPastEvent`, `EmptyInputs_ReturnsEmptyList` — net positive.

### Surprises / what to do differently

- **`ShouldHaveFlag` not available for custom `[Flags]` enums in Shouldly** — Shouldly's `ShouldHaveFlag` works on `Enum` type but the extension wasn't resolving for `EventRole`. Workaround: `.HasFlag(EventRole.Owner).ShouldBeTrue()`. Same pattern should be used in future phases for non-BCL flag enums.
- **`file sealed class` test doubles**: `NullEmailSender` and `NullReminderService` are file-scoped in each test file. A shared test-helper file for common doubles would reduce duplication across 5+ test files. Flagged for Phase 8 cleanup.
- **`FirstOrDefaultAsync` ambiguity**: without `using Microsoft.EntityFrameworkCore;`, the compiler finds `System.Linq.AsyncEnumerable.FirstOrDefaultAsync` first and can't infer type args. Fix: use `.ToListAsync()` then in-memory `.First()`, or ensure the EF using is present. Lesson: always add `using Microsoft.EntityFrameworkCore;` in test files that touch `DbSet<T>`.

### Per-task verification

| #       | Task                                                      | Status | Artifact                                                                               |
|---------|-----------------------------------------------------------|--------|----------------------------------------------------------------------------------------|
| 6.5.1   | `MyEventsAccessToken` entity + EF migration               | ✅     | `Models/MyEventsAccessToken.cs`; `Data/AppDbContext.cs` (DbSet + model config); `Migrations/..._AddMyEventsAccessToken.cs` |
| 6.5.2   | `IMyEventsService` + DTOs                                 | ✅     | `Services/MyEvents/IMyEventsService.cs` — `EventRole` flags enum, `MyEventRow` record |
| 6.5.3   | `MyEventsService` implementation                          | ✅     | `Services/MyEvents/MyEventsService.cs`                                                 |
| 6.5.4   | `MyEventsListBuilder` pure helper                         | ✅     | `Services/MyEvents/MyEventsListBuilder.cs`                                             |
| 6.5.5   | DI registration                                           | ✅     | `Program.cs` — `AddScoped<IMyEventsService, MyEventsService>()`                        |
| 6.5.6   | `ListByEmailAsync` on attendance + invitee services       | ✅     | `IAttendanceService` + `AttendanceService`; `IInviteeService` + `InviteeService`       |
| 6.5.7   | `MyEvents.razor` page (request form + list view)          | ✅     | `Pages/MyEvents.razor` — request form, `?t=` list render, collapsed past `<details>`  |
| 6.5.8   | Landing-page link                                         | ✅     | `Pages/Index.razor` — "See all your events" → `/my-events`                            |
| 6.5.9   | Email body for magic link                                 | ✅     | `MyEventsService.SendMagicLinkEmailAsync` + `BuildEmailBody`                          |
| 6.5.10  | `MyEventsServiceTests` (8 tests)                          | ✅     | `tests/.../MyEventsServiceTests.cs` — 8 tests, all passing                            |
| 6.5.11  | `MyEventsListBuilderTests` (10 tests)                     | ✅     | `tests/.../MyEventsListBuilderTests.cs` — 10 tests, all passing                       |
| 6.5.12  | `design.md` updates (§3, §5, §6)                          | ✅     | `docs/design.md` — discovery bullet, entity table, routes table                       |
| 6.5.13  | `change_log.md` close entry + this retro                  | ✅     | `change_log.md` 2026-05-12 entry; this section                                        |

**Test totals**: 134 passing, 0 failing, 0 skipped (up from 116 at Phase 6 close, +18).

### Cowork Phase Exit review

_Pending — Cowork to fill in this subsection per `process.md` §"Phase exit — the two-tool review pattern"._
