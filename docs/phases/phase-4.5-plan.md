# Phase 4.5 — Invitations and create-time enrichments

**Status**: signed off — 2026-05-04. Awaiting executor pickup *after* Phase 4 ships.

## Why this phase

Two requirement clusters surfaced after Phase 4 was planned and signed off, both touching the create flow:

1. **Owners should be able to define preset meal options at event-creation time** — instead of having to wait until the event exists and then add them via the manage page.
2. **Owners should be able to invite people by email at event-creation time**, with each invitee receiving a link, the event page showing invited-but-not-ordered status, and the manage page able to nudge unordered invitees.

Both touch `NewEvent.razor` and `EventService.CreateAsync`. Bundling them into one phase keeps the create-flow diff coherent and lets the executor reason about the form's enriched state once. It also means Phase 4 (manage page) stays single-purpose.

## Goal

A creator submitting `/new` can:

- Define preset meal options inline (label + tag flags), persisted with the event.
- Invite a list of attendees by email, persisted as `Invitee` rows; each gets an invitation email with a link to `/e/{slug}?invite={inviteeId}` that pre-fills their email read-only on the public form.
- See those invitees show up on `/e/{slug}` flagged as "no order yet" until they submit, then folded into the regular attendance list.

A manage-token holder on `/e/{slug}/manage?t={token}` can:

- Add or remove invitees post-creation.
- Click "Send reminder" to email all invitees who haven't ordered yet.

## Decisions confirmed at kickoff

Locked at sign-off via the seven questions Wilhelm answered on 2026-05-04.

- **Email parsing on the create form** — comma-separated, deduped server-side (case-insensitive).
- **Invite link** — `/e/{slug}?invite={inviteeId}` (query-string, not a token). Invitee IDs aren't sensitive credentials; they're identifiers for pre-fill convenience.
- **Pre-filled email when arriving via invite link** — read-only. The page reads `?invite=`, looks up the invitee, pre-fills the email input as `readonly`. If the invitee ID doesn't resolve, the input is editable as a regular `/e/{slug}` visit.
- **Email uniqueness per event** — `UNIQUE(EventId, Email)` on `Invitee`. Adding an email that's already an invitee OR already has an attendance prompts "this email is already part of the event" and refuses to create a duplicate.
- **Removing an invitee who has ordered** — UI prompt offers three choices: *(a) remove invitee only* (keeps the attendance), *(b) remove invitee and the matching attendance*, *(c) cancel*. Service supports both (a) and (b) modes.
- **Send-reminders confirmation** — single prompt showing the count of recipients ("send reminder to N people who haven't ordered yet").
- **Invitees can be added both at creation and on the manage page** — same service method (`InviteeService.CreateAsync`); the create-form just pre-bundles a batch.
- **Hard-rule carryover from Phase 4** — interactive Blazor for everything in this phase. No form-post-to-minimal-API. No `IHttpContextAccessor` injection in pages. The Phase 3 form-post seam stays paid-down in Phase 3's existing code; nothing new in this phase repeats it.
- **Email sending** — `IEmailSender` console-stub, same as elsewhere. Real provider lands in Phase 5; Phase 5's scope grows by two bodies (invite-on-creation and remind-unordered) but no infrastructure change.
- **Email matching is case-insensitive** — `Invitee.Email` and `Attendance.Email` both go through the existing lower-casing value converter. Joins on email are then plain `==`.

## Open questions — resolved at sign-off

All seven design questions resolved by Wilhelm 2026-05-04. See "Decisions confirmed at kickoff" above for the rolled-up answers.

### Knock-on from sign-off (sign-off-decision review rule)

Re-checked every Decisions item in `phase-4-plan.md` and `design.md` against the new answers. **One revision**: my earlier-in-session suggestion that meal-options-at-creation fits into Phase 4 was reversed — bundling it with invitations in Phase 4.5 keeps the create-flow change coherent and Phase 4 single-purpose. No other ripples.

The Phase 4 plan is **unchanged** as a result; it stays manage-page-only.

## Prerequisites

- **Phase 4 must ship first.** This phase's manage-page tasks (4.5.9, 4.5.10) extend the manage page Phase 4 builds. The data-layer + create-flow tasks (4.5.1–4.5.8) don't strictly need Phase 4, but executing the phase in order keeps the plan reviewable.
- All 42 tests passing as of plan write (Phase 3 closed, Phase 4 not yet started).

## Task breakdown

| #      | Task                                                                                                                                                   | Surface                                                                          | Model      | Size | Notes                                                                                                                                                                                                                                                |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------- | ---------- | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4.5.1  | `Invitee` entity + EF migration                                                                                                                        | `Models/Invitee.cs`, new migration                                               | **Haiku**  | S    | Fields: `Id, EventId, Email (lower-cased value converter), InvitedAt`. Unique index on `(EventId, Email)`. FK to `Event` with `OnDelete(Cascade)` (deleting an event deletes its invitees).                                                          |
| 4.5.2  | `IInviteeService` + `InviteeService` + DI                                                                                                              | `Services/Invitees/*`, `Program.cs`                                              | **Sonnet** | M    | Methods: `CreateAsync(eventId, email)`, `CreateBatchAsync(eventId, IEnumerable<string> emails)`, `ListByEventAsync`, `ListUnorderedByEventAsync` (joins against `Attendance` by email), `DeleteAsync(id, alsoDeleteMatchingAttendance: bool)`, `GetByIdAsync`. Dedup + lower-case + uniqueness validation in `CreateBatchAsync`. |
| 4.5.3  | Extend `EventService.CreateAsync` to accept meal options + invitees, persist transactionally                                                            | `Services/Events/EventService.cs`, `IEventService.cs`                            | **Sonnet** | M    | Add to `CreateEventRequest` two optional collections: `MealOptions: IEnumerable<MealOptionDraft>` and `InviteeEmails: IEnumerable<string>`. New `MealOptionDraft(string Label, MealTag Tags)` record. Service inserts event + options + invitees in a single `SaveChangesAsync`. Existing 4-tests-on-create-flow still pass with empty defaults. |
| 4.5.4  | `EventDisplayList` pure helper                                                                                                                         | `Services/EventDisplayList.cs`                                                   | **Sonnet** | S    | Static method that takes `IEnumerable<Attendance> attendances` and `IEnumerable<Invitee> invitees`, returns a unified list with a "kind" marker (Ordered / NoOrderYet). The "no order yet" set = invitees whose email doesn't match any attendance email for that event. Visibility-toggle layering happens in the page (using `AttendanceVisibility.Apply` over the unified list, with invitee rows treated by their email-matched attendance ID where applicable). |
| 4.5.5  | Tests: `EventDisplayList`                                                                                                                              | `tests/.../EventDisplayListTests.cs`                                             | **Haiku**  | S    | 4-5 tests: ordered-only, invited-only, mixed (some matched, some not), case-insensitive email matching, empty inputs.                                                                                                                              |
| 4.5.6  | Extend `NewEvent.razor`: add meal-options section + invite-emails textarea + submit handler                                                            | `Pages/NewEvent.razor`                                                           | **Sonnet** | L    | Reuses the meal-options UI pattern Phase 4 builds for the manage page (extract a small Razor component if helpful). Invite-emails: comma-separated textarea with placeholder `alice@example.com, bob@example.com`. Submit handler parses, dedupes, builds `CreateEventRequest`, calls service. Server-side validation surfaces duplicates ("alice@example.com is already invited") if any cross-collision is found at submit. |
| 4.5.7  | Extend `EventPage.razor`: read `?invite=`, pre-fill readonly email; render unified attendance + invitee list                                           | `Pages/EventPage.razor`                                                          | **Sonnet** | M    | New `[SupplyParameterFromQuery(Name = "invite")]` Guid? property. On init, if non-null, look up `Invitee`; if found and matches event, pre-fill the email field as `readonly`. The orders/attendance table now uses `EventDisplayList` to include invited-no-order rows with a "no order yet" badge. Visibility toggle still applies. |
| 4.5.8  | Invite-email stub on creation                                                                                                                          | `EventService.CreateAsync`                                                       | **Haiku**  | S    | After persisting invitees, loop and call `IEmailSender.SendAsync` per invitee with the invite URL `{baseUrl}/e/{slug}?invite={inviteeId}`. Body mirrors the manage-link email shape. **Note**: same relative-URL gotcha that's been flagged for Phase 5 — `ConsoleEmailSender` logs whatever's passed, but the real-email work in Phase 5 owns making URLs absolute. |
| 4.5.9  | Manage page: Invitees section (list, add, delete with prompt)                                                                                          | `Pages/ManageEvent.razor`                                                        | **Sonnet** | M    | New section under "Meal options". List of invitees with status (ordered / no order yet). Add-invitee form (single email; server validates uniqueness). Delete button opens a small confirmation: if invitee has matching attendance, three-option prompt (keep order / remove both / cancel); if no attendance, single-option confirm.                |
| 4.5.10 | Manage page: "Send reminder to invitees who haven't ordered" button                                                                                    | `Pages/ManageEvent.razor`                                                        | **Sonnet** | S    | Button shows the count: *"Remind N people who haven't ordered yet"*. Click → confirmation prose + Confirm/Cancel. Confirm calls a new `IInviteeService.SendRemindersAsync(eventId)` (or just enumerates `ListUnorderedByEventAsync` and calls `IEmailSender.SendAsync` per invitee — service-side is the cleaner home).                              |
| 4.5.11 | Tests: `InviteeService`                                                                                                                                | `tests/.../InviteeServiceTests.cs`                                               | **Sonnet** | M    | ~8 tests: `CreateAsync` lower-cases email; `CreateBatchAsync` dedupes within the batch and against existing rows; uniqueness conflict throws or returns a structured error; `ListUnorderedByEventAsync` correctly joins (case-insensitive); `DeleteAsync(false)` keeps attendance; `DeleteAsync(true)` removes both (transactionally); `GetByIdAsync` happy + null. |
| 4.5.12 | Tests: extended `EventServiceTests` for create-with-options + create-with-invitees                                                                     | `tests/.../EventServiceTests.cs`                                                 | **Sonnet** | M    | ~5 new tests: `CreateAsync` persists supplied meal options; persists supplied invitees with lower-cased emails; transactional rollback on any inner error; default empty collections still work; existing tests remain green.                       |
| 4.5.13 | Tests: send-reminders behaviour                                                                                                                        | `tests/.../InviteeServiceTests.cs`                                               | **Sonnet** | S    | 2-3 tests using a `RecordingEmailSender` test double: `SendRemindersAsync` calls `SendAsync` once per unordered invitee; doesn't call for invitees with orders; counts match `ListUnorderedByEventAsync`.                                           |
| 4.5.14 | `design.md` updates: §3 scope adds invitations + meal-options-at-creation; §5 adds `Invitee` entity; §6 adds the `?invite=` query parameter behaviour; §8 reaffirms no-auth model | `docs/design.md`                                                                  | **Haiku**  | S    | Doc-only.                                                                                                                                                                                                                                            |
| 4.5.15 | `change_log.md` close entry; retro at bottom of this file (executor + actual model(s); per-task tick table)                                            | docs                                                                             | **Haiku**  | S    | Use the new per-task verification format from `process.md`.                                                                                                                                                                                          |

**Total**: 15 tasks. Mostly Sonnet (judgement-heavy services + page changes + tests). One **L** (4.5.6 — `NewEvent.razor` is the densest UI surface in this phase, two new sections plus parsing logic). No Opus — all decisions are committed.

This is bigger than Phase 4 (14) but smaller than Phase 3 (17). Splitting into 4.5a (data + create + display) and 4.5b (manage extensions + reminders) is *possible* — and clean — but adds a phase boundary for relatively coherent work. Recommendation: keep as one phase.

## Risks / what might bite

- **Invitee email matching is case-insensitive via value converters.** Both `Invitee.Email` and `Attendance.Email` are lower-cased on save. Joins after that are simple `==`. If for any reason a row escapes the converter (e.g., raw SQL insert in tests), the join silently misses. Captured: tests should always go through `CreateAsync` paths, not direct DbContext seeding, so the converter fires.
- **Duplicate detection on the create form when multiple emails are submitted at once.** Server-side: parse, dedupe within batch, then check each against existing invitees and attendances. If conflicts exist, return validation errors and re-render the form with the duplicates highlighted. Don't half-create the event. Captured in 4.5.6 + 4.5.3.
- **Transactional rollback in `EventService.CreateAsync`** — if invitee creation fails after the event is persisted, roll back. EF's `SaveChangesAsync` wraps the whole `Add` set in a single transaction by default, so this works as long as everything is added before `SaveChangesAsync` is called. Captured in 4.5.3.
- **The `?invite=` query parameter and the `?t=` (edit token) query parameter coexist on `/e/{slug}`.** When both are present, `?t=` wins for "show your existing order banner"; `?invite=` is informational only. Captured in 4.5.7.
- **Removing an invitee who has ordered** — the "remove both" path needs to be transactional (delete the invitee row + delete the attendance row in a single SaveChanges). Captured in 4.5.2.
- **Phase 5 grows by two new email types** (invite-on-creation, remind-unordered). No infrastructure change, just two more bodies. Worth noting in Phase 5's plan when it's written.

## Exit criteria

- Owner can submit `/new` with a list of meal options and a list of invitee emails; the event page shows them all with the right state.
- Invitee clicks `/e/{slug}?invite={id}` → form has their email pre-filled and read-only → they submit an order → their row flips from "no order yet" to a normal attendance row.
- Owner on the manage page can add/remove invitees, and the remove flow correctly handles the "has ordered" branch.
- Owner clicks "Send reminders" → only invitees with no matching attendance receive an email (stubbed).
- `dotnet build` and `dotnet test` are green; new tests cover each new path.
- `design.md` updated for the new entity, route param, and §3 scope additions.
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern", **with the per-task verification rule applied**. Findings recorded as a peer subsection in this file's "What actually happened".

## What actually happened

**Executor**: Claude Code (Sonnet for service/UI tasks; inline for Haiku-sized tasks)
**Completed**: 2026-05-06

### Per-task verification

| # | ✓/✗ | Artifact |
|---|---|---|
| 4.5.1 | ✅ | `Models/Invitee.cs`; `AppDbContext.cs` Invitees DbSet + composite unique index; migration `AddInvitee` |
| 4.5.2 | ✅ | `Services/Invitees/IInviteeService.cs` + `InviteeService.cs`; DI in `Program.cs` |
| 4.5.3 | ✅ | `IEventService.cs` — `MealOptionDraft` record; `CreateEventRequest` now has optional `MealOptions` + `InviteeEmails`; `EventService.CreateAsync` inserts all in single `SaveChangesAsync` |
| 4.5.4 | ✅ | `Services/EventDisplayList.cs` — `DisplayRow` + `EventDisplayList.Build` |
| 4.5.5 | ✅ | `EventDisplayListTests.cs` — 6 tests covering ordered-only, invited-only, mixed, case-insensitive match, empty inputs, attendee-with-no-email |
| 4.5.6 | ✅ | `Pages/NewEvent.razor` — meal-options section (inline draft list + tag checkboxes); invite-emails textarea |
| 4.5.7 | ✅ | `Pages/EventPage.razor` — `[SupplyParameterFromQuery] Guid? InviteId`; invite pre-fill; `EventDisplayList.Build` for unified display |
| 4.5.8 | ✅ | `EventService.SendInviteEmailAsync` called per-invitee after `SaveChangesAsync` in `CreateAsync` |
| 4.5.9 | ✅ | `Pages/ManageEvent.razor` — Invitees section: list with ordered/no-order-yet badges; three-option delete prompt; add-invitee form |
| 4.5.10 | ✅ | `Pages/ManageEvent.razor` — "Remind N people" button → confirm → `InviteeService.SendRemindersAsync` |
| 4.5.11 | ✅ | `InviteeServiceTests.cs` — 14 tests covering all methods |
| 4.5.12 | ✅ | `EventServiceTests.cs` — 4 new tests: create-with-options, create-with-invitees (lower-cased), dedup, empty-extras |
| 4.5.13 | ✅ | `InviteeServiceTests.cs` — `SendRemindersAsync_CallsSendOncePerUnorderedInvitee` + `_AllOrdered_SendsNothing` using `RecordingEmailSender` test double |
| 4.5.14 | ✅ | `docs/design.md` §3 invitations + manage-page bullets added; §5 `Invitee` entity already present; §6 `?invite=` note already present |
| 4.5.15 | ✅ | This section; `change_log.md` entry |

**Total tests**: 80 passing (up from 55 at Phase 4 close).

### Actual models used

All tasks done within a single Claude Code session. Haiku-sized tasks (4.5.1, 4.5.4, 4.5.5, 4.5.8, 4.5.14, 4.5.15) handled inline by Sonnet — no model switch taken.

### Deviations from plan

- **`EventDisplayList` receives `VisibleAttendances` not raw `attendances`** in `EventPage.razor`. This is correct per the plan's note about visibility-toggle layering, though the plan's wording of 4.5.4 described `AttendanceVisibility.Apply` as happening in the page "over the unified list". The implementation applies visibility *before* passing to `EventDisplayList.Build`, which is functionally equivalent and simpler.
- **`CreateBatchAsync` skips (not throws on) existing entries**. The plan said "idempotent — safe at event-creation time" and "don't half-create the event". The skip-and-continue approach is the correct read of those two notes together: at creation time there are no existing rows anyway; on the manage-page, single-email `CreateAsync` throws. Consistent with intent.
- **Reminders button shows only when there are unordered invitees**. When `unorderedCount == 0`, the button is hidden entirely rather than disabled — slightly better UX than the plan's description, which was silent on this case.

### Surprises

- No SQLite `DateTimeOffset` ORDER BY surprises this phase — all new `ListBy*` methods sort client-side from the start (lesson carried from Phase 4).
- `EventPage.razor` — the `VisibleAttendances` computed property (used for the old attendance-only table) needed to feed `EventDisplayList.Build` with the already-filtered slice. The `displayRows` field therefore replaces the old `VisibleAttendances` direct loop entirely.

### Things to do differently

- Spell out in the plan whether `CreateBatchAsync` skips or throws on collisions. "Idempotent" and "don't half-create" are both in the plan but point at slightly different behaviours; one note won. A single clear sentence would have saved interpretation.
- Extract the meal-options add/edit row into a Razor component if the manage page keeps growing. At ~550 lines it's still readable but getting close.

### Phase Exit review

Pending Cowork review pass per process.md §"Phase exit — the two-tool review pattern". This retro will be updated when the review lands.
