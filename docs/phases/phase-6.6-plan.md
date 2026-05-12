# Phase 6.6 — Public event browsing (Phase 6.5 redo)

**Status**: signed off — 2026-05-11. Awaiting executor pickup.

## Why this phase

Phase 6.5 was built, reviewed, marked complete — and was the wrong feature. Wilhelm's original framing throughout was "list all ongoing events" / "browse all events"; the plan kept narrowing it into "magic-link list of events tied to *my* email" because I was protecting the `design.md` §1 "events aren't browseable" line. That line is now reversed: **events are inherently public**. Privacy is an opt-in option per event.

Phase 6.6 is the redo. It does two things: removes the Phase 6.5 magic-link infrastructure cleanly, and ships the actual feature — a public `/events` browse page with a per-event `IsPrivate` opt-out.

This is the first time the project has had to undo a phase's worth of work. Documenting honestly here as a lesson.

## Goal

A visitor lands on `/events` and sees every public event in the system. No auth, no email, no clicks-to-reveal. Each row shows event title, event date in the event's timezone, and an ordered-count. Ordered by `StartsAt` ascending; events that are closed or already started are in a collapsed `<details>` section at the bottom. Owners who want a private event tick a checkbox at creation (or in edit) — those events never appear in `/events` and stay reachable only by direct link.

## Decisions confirmed at kickoff

Locked via the paraphrase-and-confirm pass on 2026-05-11. No open questions to resolve.

- **`Event.IsPrivate`** new `bool` column. **Default `false`** — events are public unless the owner explicitly opts in to privacy. Captured in `Event` entity + migration.
- **`/events` page**: anonymous, no auth, no email, no token. Just `db.Events.Where(e => !e.IsPrivate).Select(...)` plus the ordered-count.
- **Row contents**: title, event date in `Event.TimeZoneId` (via `TimeZoneHelper.ToLocalString`), ordered-count. No owner name. Clicking a row navigates to `/e/{slug}` (the existing public event page).
- **Ordering and layout**: ongoing (`!IsClosed && StartsAt > now`) ordered by `StartsAt` ascending, visible by default. Past (`IsClosed || StartsAt <= now`) ordered by `StartsAt` descending, in a `<details><summary>Past events ({count})</summary>...</details>` collapsed block.
- **No pagination** in this phase. Defer until the list grows past ~50; Phase 8 polish or later.
- **`AttendeeOrdersVisible`** stays as a separate, independent toggle. It controls "do attendees see each other's orders on the event page" — different question from "does this event appear in the browse list." Both toggles live in the create + edit forms.
- **`/recover` stays as-is**, including the one-email fix applied 2026-05-11. Different use case (lost manage URL) from `/events` (discovery).
- **Phase 6.5 infrastructure is fully removed**: `MyEventsAccessToken` entity + drop migration, `MyEventsService` + interface + tests, `MyEventsListBuilder` + tests, `Pages/MyEvents.razor`, the landing-page "See all your events" link, and the related DI registration in `Program.cs`. The `MyEventsListBuilder` pure helper is **not** salvaged — the aggregation-by-role logic doesn't apply to a public browse.
- **Listing query lives in `EventService`**: new method `ListPublicAsync()` returning `IReadOnlyList<EventListRow>` where `EventListRow` is a record of `(Event ev, int OrderedCount, bool IsOngoing)`. SQLite projection: `db.Events.Where(...).Select(e => new EventListRow(e, e.Attendances.Count(), ...))`. The IsOngoing flag is computed in the projection.
- **A small pure helper `PublicEventGrouping`** groups + orders rows for the page. Same testability shape as the prior pure helpers (`AttendanceVisibility`, `EventDisplayList`, `ReminderAudience`, `CsvExportBuilder`). Pure function over `IReadOnlyList<EventListRow>` → `(ongoing, past)` tuple, each side ordered correctly.
- **Hard rules carried**: interactive Blazor (the browse page is read-only Blazor); per-task verification table in the retro; plan internal-consistency check before lock; Cowork Phase Exit review non-deferrable; pure helpers for layered/joined views.
- **`design.md` gets a substantive rewrite of §1 (vision), §3 (scope), §5 (`Event` entity gains `IsPrivate`), §6 (new `/events` route, removal of `/my-events`), and §8 (identity model — clarify that link-only privacy is now opt-in, not the default).** Bundled into one task (6.6.16).

## Open questions

None. The paraphrase-and-confirm pass on 2026-05-11 resolved every relevant decision. Sign-off on this plan is sign-off on the task breakdown only.

## Prerequisites

- Phase 6.5 closed in tracker (it is, but the code shipped is wrong-feature).
- Decision recorded in `design.md` §1: events are public by default (lands in task 6.6.16).

## Task breakdown

### Removal pass

| #      | Task                                                                                          | Surface                                                                              | Model      | Size |
| ------ | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ---------- | ---- |
| 6.6.1  | Delete `Pages/MyEvents.razor` and remove the landing-page "See all your events" link          | `Pages/MyEvents.razor`, `Pages/Index.razor`                                          | **Haiku**  | S    |
| 6.6.2  | Delete `Services/MyEvents/*` (interface, service, list builder)                               | `Services/MyEvents/IMyEventsService.cs`, `MyEventsService.cs`, `MyEventsListBuilder.cs` | **Haiku**  | S    |
| 6.6.3  | Delete `Models/MyEventsAccessToken.cs`; remove DbSet + `OnModelCreating` config; remove DI    | `Models/MyEventsAccessToken.cs`, `Data/AppDbContext.cs`, `Program.cs`                | **Haiku**  | S    |
| 6.6.4  | `DropMyEventsAccessToken` EF migration                                                        | `Migrations/*`                                                                       | **Haiku**  | S    | Generated via `dotnet ef migrations add` after deleting the entity from `AppDbContext`. Verify the Up scrubs the table and indexes; Down can re-create it for completeness or just throw `NotSupportedException` (we're not rolling back).            |
| 6.6.5  | Delete `MyEventsServiceTests.cs` and `MyEventsListBuilderTests.cs`                            | `tests/.../`                                                                          | **Haiku**  | S    |
| 6.6.6  | Verify nothing else in the codebase references the removed types (`Grep` pass)                | (no edits expected — verification only)                                              | **Haiku**  | S    |

### Build pass

| #       | Task                                                                                          | Surface                                                                              | Model      | Size |
| ------- | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ---------- | ---- |
| 6.6.7   | Add `Event.IsPrivate` (bool, default false) + `AddEventIsPrivate` migration                   | `Models/Event.cs`, `Data/AppDbContext.cs`, `Migrations/*`                            | **Haiku**  | S    | Schema-level default `false` so existing rows become public on upgrade (consistent with the new model — events were always implicitly public-by-coincidence; explicit now).                                                                       |
| 6.6.8   | Extend `CreateEventRequest` and `UpdateEventRequest` with `IsPrivate`; propagate through `EventService.CreateAsync` + `UpdateAsync`  | `Services/Events/IEventService.cs`, `EventService.cs`                                | **Sonnet** | S    | Backwards-compat default `false` on the record for safety; both callers pass it explicitly.                                                                                                                                                          |
| 6.6.9   | `EventService.ListPublicAsync()` returns `IReadOnlyList<EventListRow>` (record: `Event ev, int OrderedCount, bool IsOngoing`)                       | `Services/Events/IEventService.cs`, `EventService.cs`                                | **Sonnet** | M    | Single LINQ projection: `db.Events.Where(e => !e.IsPrivate).Select(e => new EventListRow(e, e.Attendances.Count(), !e.IsClosed && e.StartsAt > DateTimeOffset.UtcNow))`. Ordering deferred to the helper (6.6.10) so the SQL stays simple.        |
| 6.6.10  | `PublicEventGrouping` pure helper: `(ongoing, past)` tuple, ordered                            | `Services/Events/PublicEventGrouping.cs`                                             | **Sonnet** | S    | Ongoing rows ordered by `StartsAt` ascending; past rows ordered by `StartsAt` descending. Pure function over `IReadOnlyList<EventListRow>`. Same pattern as `EventDisplayList` / `ReminderAudience`.                                              |
| 6.6.11  | `NewEvent.razor`: "Private event (link-only)" checkbox + form-model field + handler           | `Pages/NewEvent.razor`                                                               | **Sonnet** | S    | Bound to `model.IsPrivate`. Helper text: *"When ticked, this event won't appear in the public browse list. Only people with the direct link can see it."*                                                                                          |
| 6.6.12  | `ManageEvent.razor`: same "Private event" checkbox in the edit-event section                  | `Pages/ManageEvent.razor`                                                            | **Sonnet** | S    | Mirrors the create form. `UpdateEventRequest.IsPrivate` carries through.                                                                                                                                                                            |
| 6.6.13  | New `Pages/Events.razor` at `/events`                                                          | `Pages/Events.razor`                                                                 | **Sonnet** | M    | Loads `ListPublicAsync`, runs through `PublicEventGrouping.Build`, renders ongoing table + `<details>` past block. Each row is a link to `/e/{slug}`. Title, event date (TZ-aware), ordered-count columns.                                          |
| 6.6.14  | Landing-page link to `/events`                                                                 | `Pages/Index.razor`                                                                  | **Haiku**  | S    | "Browse events" link, sibling to the existing "Create an event" CTA and "Lost your manage link?" link.                                                                                                                                              |
| 6.6.15  | Tests: `ListPublicAsync` + `PublicEventGrouping`                                              | `tests/.../EventServiceTests.cs`, `tests/.../PublicEventGroupingTests.cs`            | **Sonnet** | M    | EventService test: private events excluded, ordered count matches, IsOngoing flag correct. PublicEventGrouping tests: ongoing/past split, ordering directions, empty list, all-past, all-ongoing.                                                  |

### Docs

| #      | Task                                                                                          | Surface                                                                              | Model      | Size |
| ------ | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ---------- | ---- |
| 6.6.16 | `design.md` rewrite                                                                            | `docs/design.md`                                                                     | **Sonnet** | M    | §1 vision: "events are public by default" replacing the "not browseable" framing. §3 scope: add browse page, add `IsPrivate` toggle. §5 entity: add `IsPrivate`. §6 routes: add `/events`, remove `/my-events`. §8 identity model: clarify that link-only privacy is now opt-in (private events) rather than universal. |
| 6.6.17 | `change_log.md` entry + retro at bottom of this file (executor + actual model(s); per-task tick table) | docs                                                                                 | **Haiku**  | S    | Per `process.md`. The retro should be honest about Phase 6.5 → 6.6 — the redo is itself a learning artifact.                                                                                                                                       |

**Total**: 17 tasks (6 removal + 9 build + 2 docs). Mid-sized phase; comparable to 4.5 (15) and 6.5 (13). No Opus tasks. Sonnet for the judgement-heavy services + pages + tests; Haiku for the mechanical removals + DI + config + docs.

## Risks / what might bite

- **Migration ordering**: the `DropMyEventsAccessToken` migration must apply before any new code references EF Core's expected schema. Since we run `Database.Migrate()` at startup, all pending migrations apply in order; the drop happens first because it's authored first (timestamps). No drama expected.
- **`<UserSecretsId>` and other Phase 5 secret-handling unaffected**: removal does not touch `EmailSettings` or the Resend wiring.
- **Hangfire jobs scheduled against deleted `MyEventsAccessToken` data**: there are none — Phase 6.5's tokens didn't schedule jobs, they're a passive credential. Safe to drop.
- **`Event.IsPrivate` default `false` semantics on upgrade**: existing events become public. This matches the new model and Wilhelm's "events are inherently public" framing. If we wanted existing events to stay private-by-coincidence (the current behaviour), we'd default to `true` for upgrade. We're not doing that — captured.
- **`ListPublicAsync` count projection**: EF Core translates `e.Attendances.Count()` to a SQL `(SELECT COUNT(*) FROM Attendances WHERE EventId = e.Id)` subquery. Should work fine on SQLite. Tests verify.
- **`/events` performance with growing data**: a Where-Select-ToList of all public events fetches all rows. Fine at any plausible internal-tool scale; pagination becomes a Phase 8 concern if usage grows.
- **The redo itself is the largest single risk**: we built and reviewed Phase 6.5 incorrectly. The paraphrase-and-confirm rule in `process.md` is the institutional answer. The next time a phase plan locks, the executor (or Cowork) restates the user's framing in one sentence at sign-off and gets explicit confirmation.

## Exit criteria

- A visitor at `/events` sees a table of all public events: title, event date (in event TZ), ordered-count. Ordered by `StartsAt` ascending.
- Past events (`IsClosed || StartsAt <= now`) sit in a collapsed `<details>` section at the bottom, ordered by `StartsAt` descending.
- Private events (`IsPrivate == true`) do not appear in the listing at all.
- Owner can tick "Private event (link-only)" on the create form and on the edit form; the value persists and round-trips.
- The Phase 6.5 magic-link infrastructure is entirely gone from the codebase: no references to `MyEventsAccessToken`, `MyEventsService`, `MyEventsListBuilder`, `MyEvents.razor`, or the landing-page "See all your events" link.
- `dotnet build` and `dotnet test` are green; new tests for `ListPublicAsync` and `PublicEventGrouping` pass.
- `design.md` reflects the new model (events public by default, privacy opt-in).
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern" before truly closing.

## What actually happened

**Executor**: Claude Code (Anthropic).
**Models used**: Sonnet 4.6 throughout.
**Execution date**: 2026-05-12.

### Deviations from plan

- **No deviations.** 17 tasks executed as scoped.
- Test count: 9 new tests (116 → 125). Plan estimated ~9 (6 grouping + 3 service). Exact match.
- `ListPublicAsync` uses a correlated subquery `db.Attendances.Count(a => a.EventId == e.Id)` rather than `e.Attendances.Count()` because `Event` has no `Attendances` navigation collection. Functionally identical; SQLite translates both to the same subquery. The plan said `e.Attendances.Count()` as a shorthand — this is the correct implementation of that intent.
- `IsOngoing` flag computed client-side after `ToListAsync()` — same SQLite `DateTimeOffset` limitation established in prior phases; noted in plan §Risks.

### Surprises / what to do differently

- Nothing unexpected. The removal pass was the riskiest part (stale references, migration ordering) but the Grep verification step caught everything cleanly.
- Phase 6.5 → 6.6 redo is the main lesson artifact; documented in `process.md` as the paraphrase-and-confirm rule. No new process surprises in 6.6 execution itself.

### Per-task verification

| #      | Task                                                             | Status | Artifact                                                                          |
|--------|------------------------------------------------------------------|--------|-----------------------------------------------------------------------------------|
| 6.6.1  | Delete `MyEvents.razor` + landing link                           | ✅     | File deleted; `Index.razor` link removed                                          |
| 6.6.2  | Delete `Services/MyEvents/*`                                     | ✅     | All three files + directory deleted                                               |
| 6.6.3  | Delete entity + DbSet config + DI                                | ✅     | `Models/MyEventsAccessToken.cs` deleted; `AppDbContext.cs` + `Program.cs` cleaned |
| 6.6.4  | `DropMyEventsAccessToken` EF migration                           | ✅     | `Migrations/..._DropMyEventsAccessToken.cs`                                       |
| 6.6.5  | Delete test files                                                | ✅     | `MyEventsServiceTests.cs` + `MyEventsListBuilderTests.cs` deleted                 |
| 6.6.6  | Grep verification — no stray references                          | ✅     | Grep found only migration history files; no live code references                  |
| 6.6.7  | `Event.IsPrivate` + `AddEventIsPrivate` migration                | ✅     | `Models/Event.cs`; `Migrations/..._AddEventIsPrivate.cs`                          |
| 6.6.8  | `IsPrivate` in `CreateEventRequest` + `UpdateEventRequest`       | ✅     | `Services/Events/IEventService.cs` — both records updated with default `false`    |
| 6.6.9  | `EventService.ListPublicAsync()`                                 | ✅     | `Services/Events/EventService.cs` — correlated count subquery, client-side IsOngoing |
| 6.6.10 | `PublicEventGrouping` pure helper                                | ✅     | `Services/Events/PublicEventGrouping.cs`                                          |
| 6.6.11 | `NewEvent.razor` "Private event" checkbox                        | ✅     | `Pages/NewEvent.razor` — checkbox + helper text + model field + submit wiring     |
| 6.6.12 | `ManageEvent.razor` "Private event" checkbox                     | ✅     | `Pages/ManageEvent.razor` — checkbox + populate + save wiring + model field       |
| 6.6.13 | `Pages/Events.razor` at `/events`                                | ✅     | `Pages/Events.razor` — ongoing table + collapsed past `<details>`                 |
| 6.6.14 | Landing-page "Browse events" link                                | ✅     | `Pages/Index.razor`                                                               |
| 6.6.15 | Tests                                                            | ✅     | `PublicEventGroupingTests.cs` (6 tests) + 3 new tests in `EventServiceTests.cs`   |
| 6.6.16 | `design.md` rewrite                                              | ✅     | §1 vision, §3 scope, §5 `IsPrivate`, §6 routes, §8 visibility + identity model   |
| 6.6.17 | `change_log.md` + this retro                                     | ✅     | `change_log.md` 2026-05-12 entry; this section                                   |

**Test totals**: 125 passing, 0 failing, 0 skipped.

### Cowork Phase Exit review

_Pending — Cowork to fill in this subsection per `process.md` §"Phase exit — the two-tool review pattern"._
