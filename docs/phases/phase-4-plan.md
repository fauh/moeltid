# Phase 4 — Owner manage page

**Status**: signed off — 2026-05-04. Awaiting executor pickup.

## Why this phase

Phase 3 made the app useful for attendees. Phase 4 makes it useful for the people who create events: editing fields, managing meal options, viewing the order list, deleting spam, closing the event, and recovering a lost manage link. After Phase 4 the v1 functional surface is essentially complete; Phase 5 only adds real email + reminders, and Phases 6+ are export and polish.

This phase also introduces the **first interactive-Blazor-only feature surface** — Phase 3's form-post pattern is explicitly *not* repeated here, by direct lesson from Phase 3's planning miss (see Phase 3 retro §"Planning miss — and what it means for Phase 4").

## Goal

A holder of `/e/{slug}/manage?t={token}` can:
- Edit the event (title, description, deadline, time zone, free-text and visibility toggles).
- Add, edit, and delete meal options.
- View the full attendee list with all order details, in the event's owner timezone.
- Delete an attendance row (cleanup of spam or duplicates).
- Close the event so no further submissions are accepted.
- Rotate the manage token, invalidating any previously shared URLs.

A non-holder hitting the same URL (or `/e/{slug}/manage` with no `t=`) sees a generic "invalid manage link" page that does not differentiate "wrong token" from "event not found" — the slug existence is not leaked.

A separate `/e/{slug}/manage/recover` page lets the owner request the manage URL via the email they used at creation.

## Decisions confirmed at kickoff

Locked at sign-off so execution stays judgement-free. **The first three are hard rules carried from Phase 3's retrospective and should not be re-litigated mid-phase.**

- **Interactive Blazor for ALL manage actions, not form-post-to-minimal-API.** Manage pages and the recover page are .razor components with `EditForm`, `OnValidSubmit` handlers, and direct `IEventService` / `IMealOptionService` / `IAttendanceService` calls. **No antiforgery middleware. No `IHttpContextAccessor`. No minimal-API endpoints for manage actions.** The Phase 3 form-post pattern was load-bearing because of cookies; cookies were dropped, the pattern lost its reason, and the resulting `HttpContext`-on-render seam bit at runtime. Phase 4 does not repeat it.
- **Manage token comes in via `[SupplyParameterFromQuery(Name = "t")]`** on the manage page component. Page validates `token == ev.ManageToken` at `OnInitializedAsync`. If validation fails (or event missing), render a generic "invalid manage link" view — same view, same wording, regardless of *why* it failed. Slug existence not leaked.
- **Internal navigation** between manage views (e.g. token rotation updating the URL) uses `NavigationManager.NavigateTo` with default soft-nav. **No `forceLoad` needed** because no page in this phase depends on `HttpContext` for rendering.
- **Manage actions extend the existing services** (`IEventService`, `IMealOptionService`, `IAttendanceService`) rather than introducing `IEventManageService` etc. Each entity's service owns its persistence and validation; ownership is checked at the page boundary (token validated once at page load), not re-checked on every service call.
- **Meal-option deletion semantics**: when an option that has dependent `Attendance` rows is deleted, those attendances are **converted to `FreeText`** with `FreeTextOrder = the deleted option's Label`. This preserves the data and the user-visible row, just changes the form. A confirmation prompt warns the owner of the conversion before the delete happens. Rationale: `OnDelete(DeleteBehavior.Restrict)` from Phase 3 is the right default at the DB level (prevents accidents), but the manage UI handles the conversion as a deliberate action. Refuse-to-delete UX would be more annoying than the conversion behaviour.
- **Recover form is interactive Blazor** too — consistent with the rest of the manage surface. Service-level rate limiting (per-IP, per-email) is **deferred to Phase 8** (production launch). For Phase 4 the recover flow works without rate limiting; locally that's fine, and Phase 8 adds the production hardening before public exposure.
- **Token rotation flow**: owner clicks "Rotate manage token" → confirmation prompt → service generates fresh token, persists, returns new value → page navigates to `/e/{slug}/manage?t={newToken}` so the URL bar reflects the new credential. Old URLs stop resolving. The page renders the new manage URL prominently (mirroring the create-success pattern) so the owner can re-share with co-admins.
- **Page layout**: single long page with sections (event details, meal options, attendees, danger zone). No tabs, no JS-driven layout — keeps it simple and crawlable.
- **Edit-event TZ handling**: same `TimeZoneHelper.ToUtc` / `ToLocalString` patterns as `NewEvent` and `EventCreated`. Owner can change the event's TZ during edit; subsequent renders use the new TZ.

## Open questions — resolved at sign-off

All confirmed by Wilhelm on 2026-05-04:

- ✅ **Meal-option deletion conversion** — convert dependent attendances to `FreeText` with `FreeTextOrder = option.Label`. Wilhelm noted he may revisit after manual UX testing; that's expected and fine — Phase 8 polish or a Phase 4 follow-up if it doesn't feel right.
- ✅ **"Invalid manage link" view content** — single page, doesn't differentiate wrong-token from event-not-found, links to `/e/{slug}/manage/recover`.
- ✅ **Rotate-token confirmation UX** — two-step pure-Blazor flow (click "Rotate" → page shows "Are you sure?" prose with Confirm/Cancel buttons → click confirms → action runs). No JS prompts.
- ✅ **Manage URL display panel at top of manage page** — yes, include it. Folded into task 4.4's scope.
- ✅ **`GetByOwnerEmailAsync` recover behaviour** — one email per matching event (each link directly clickable).

### Knock-on from sign-off (per the new sign-off-decision review rule in `process.md`)

Re-checked every other §"Decisions confirmed at kickoff" item against the answers. No reasoning chains broken; no other items needed updating. The only edit was folding the manage-URL panel into task 4.4's notes (no new task needed; it's part of the page shell).

### New design question raised at sign-off — deferred

Wilhelm flagged: **events should probably have a max retention policy** (events are short-lived; data shouldn't accumulate forever). Captured as a §9 open question in `design.md` and as a Phase 9 (production launch) checkbox in `roadmap.md` — sits with the other production-hardening concerns (rate limits, backups, domain). Not a Phase 4 concern.

## Prerequisites

- Phase 3 truly closed, including the post-completion review fixes (done; verified by Cowork on 2026-05-04).
- All 37 tests passing as of plan write.

## Task breakdown

| #     | Task                                                                                                                                                                                          | Surface                                                                          | Model      | Size | Notes                                                                                                                                                                                                                                |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------- | ---------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 4.1   | Extend `IEventService` with manage methods                                                                                                                                                    | `Services/Events/IEventService.cs`, `EventService.cs`                            | **Sonnet** | M    | Add `UpdateAsync(Guid id, UpdateEventRequest request)`, `CloseAsync(Guid id)`, `RotateManageTokenAsync(Guid id)` (returns the new token), `GetByOwnerEmailAsync(string email)` (returns events with that owner email; recover flow). |
| 4.2   | Extend `IMealOptionService` with CRUD + delete-with-reassignment                                                                                                                              | `Services/MealOptions/IMealOptionService.cs`, `MealOptionService.cs`             | **Sonnet** | M    | Add `CreateAsync(Guid eventId, string label, MealTag tags)`, `UpdateAsync(Guid optionId, string label, MealTag tags)`, `DeleteAsync(Guid optionId)` — the delete method converts dependent `Attendance` rows to `FreeText` (`FreeTextOrder = option.Label`) inside a single `SaveChangesAsync` call (transactional). |
| 4.3   | Extend `IAttendanceService` with owner-side delete                                                                                                                                            | `Services/Attendances/IAttendanceService.cs`, `AttendanceService.cs`             | **Haiku**  | S    | Add `DeleteByOwnerAsync(Guid attendanceId)` that wraps the existing delete with an info-log noting the owner-deletion (for the future audit story). Same DB call shape as the attendee-side delete in Phase 3.                       |
| 4.4   | Manage page route + token validation + "invalid link" view + manage-URL panel at top                                                                                                          | `Pages/ManageEvent.razor`                                                        | **Sonnet** | M    | `@page "/e/{Slug}/manage"`. Reads `?t=` via `[SupplyParameterFromQuery]`. Validates `token == ev.ManageToken`. On failure (or event missing) renders a single `<p>This manage link is invalid…</p>` with a link to recover. Otherwise renders the manage UI sections from 4.5–4.9, with a small "Your manage link" panel at the top showing the current URL (copy-paste ready) and a one-line note: *"Save this — share with co-admins. Anyone with the link can manage this event."* |
| 4.5   | Manage page section: edit event details                                                                                                                                                       | `Pages/ManageEvent.razor`                                                        | **Sonnet** | M    | `EditForm` bound to an `EditEventModel`. Fields: title, description, deadline, time zone (browser-detected on initial render with override allowed), free-text toggle, visibility toggle. Submit calls `IEventService.UpdateAsync`. Same `IValidatableObject` for deadline-vs-StartsAt rule (mirrors `NewEvent.razor`). |
| 4.6   | Manage page section: meal options (list + add + edit + delete with conversion warning)                                                                                                        | `Pages/ManageEvent.razor`                                                        | **Sonnet** | L    | Each option as a row with inline edit/save/cancel and a delete button. Add-option form below the list. Delete shows a confirmation including the count of dependent attendances and the conversion behaviour (e.g. "3 attendees ordered this; deleting will convert their orders to free-text with the label as the text"). |
| 4.7   | Manage page section: attendee orders + per-row delete                                                                                                                                         | `Pages/ManageEvent.razor`                                                        | **Sonnet** | M    | Read-only table: name, email, order (preset label or free-text), submitted-at (rendered in `Event.TimeZoneId`). Delete button per row with a single-step confirmation. Calls `IAttendanceService.DeleteByOwnerAsync`.                  |
| 4.8   | Manage page section: close-event toggle                                                                                                                                                       | `Pages/ManageEvent.razor`                                                        | **Haiku**  | S    | Boolean toggle. On change, calls `IEventService.CloseAsync` (set `IsClosed = true`). Closing is one-way for v1 (no re-open); document this in the UI.                                                                                  |
| 4.9   | Manage page section: rotate-token (danger zone, two-step confirmation)                                                                                                                        | `Pages/ManageEvent.razor`                                                        | **Sonnet** | S    | "Rotate manage token" button → confirmation prose + "Confirm rotate" / "Cancel" → `IEventService.RotateManageTokenAsync` → `Nav.NavigateTo($"/e/{Slug}/manage?t={newToken}")`. Show new manage URL prominently afterwards.            |
| 4.10  | Recover-link page                                                                                                                                                                             | `Pages/ManageRecover.razor`                                                      | **Sonnet** | M    | `@page "/e/{Slug}/manage/recover"`. Email input + submit. On submit, look up event by slug; if `ownerEmail == form.Email` (case-insensitive), call `IEmailSender.SendAsync` with the manage URL. Always show the same "if your email matches, you'll receive a link" message — don't differentiate match from miss.    |
| 4.11  | Tests: `EventService` manage methods                                                                                                                                                          | `tests/.../EventServiceTests.cs`                                                 | **Sonnet** | M    | ~5 tests: `UpdateAsync` happy path; `CloseAsync` flips `IsClosed`; `RotateManageTokenAsync` produces a new token and old token no longer resolves via `GetBySlugAsync` lookup; `GetByOwnerEmailAsync` returns events for that email lower-cased and not for others. |
| 4.12  | Tests: `MealOptionService` CRUD + reassignment                                                                                                                                                | `tests/.../MealOptionServiceTests.cs`                                            | **Sonnet** | M    | ~6 tests: `CreateAsync` persists; `UpdateAsync` mutates; `DeleteAsync` converts dependent attendances to FreeText with the option label preserved as `FreeTextOrder`; `DeleteAsync` succeeds when no attendances depend on the option; tag flag combos round-trip correctly. |
| 4.13  | Tests: `AttendanceService.DeleteByOwnerAsync`                                                                                                                                                  | `tests/.../AttendanceServiceTests.cs`                                            | **Haiku**  | S    | 1–2 tests: deletes the row; throws or returns false on unknown id (pin behaviour).                                                                                                                                                  |
| 4.14  | Retro + change_log + docs                                                                                                                                                                     | docs                                                                             | **Haiku**  | S    | Per `process.md`, retro must include executor + actual model(s) + deviations + surprises + things-to-do-differently. Also: did Cowork actually run a Phase Exit review *this time* (per the rule)?                                   |

**Total**: 14 tasks. Mostly Sonnet (judgement-heavy services and page sections). One **L** (4.6 — meal options is the densest UI surface in this phase). No Opus tasks; all decisions committed in §"Decisions confirmed at kickoff".

Bigger than Phase 2.5 (11), comparable to Phase 3 (17 → 18 with hindsight). Splitting into 4a (services + tests) and 4b (pages) is *possible* — services can be merged and shipped before the UI consumes them — but the Phase 3 lesson was that splitting a single coherent feature loses the ability to validate end-to-end. Recommend keeping as one phase.

## Risks / what might bite

- **Meal-option deletion-with-reassignment** is the only non-trivial transactional logic in this phase. Implement as a single `SaveChangesAsync` call across the option-delete and the attendance-update operations. A test specifically covers this. Captured in 4.2.
- **`GetByOwnerEmailAsync` for recover** — owner email isn't unique on `Event` (one person can create many events), so this returns multiple. The recover flow sends one email per matching event, OR sends a single email listing them all. *Working assumption*: send one email per event for simplicity (and to make each link copy-pastable). Confirm if you want a different shape.
- **Rotation race**: between "owner reads current token" and "owner clicks rotate", someone else with the URL could be performing an action. Acceptable; the rotation invalidates the old URL going forward, doesn't roll back in-flight actions. Documented; no extra mitigation.
- **No HttpContext seam this phase** because we deliberately abandoned the form-post pattern (per the §Decisions hard rule). If Phase 4 execution finds itself reaching for `IHttpContextAccessor`, that's the smoke alarm — stop and re-read the §Decisions section.
- **"Invalid manage link" path tests** — purely UI behaviour, hard to unit-test without WebApplicationFactory. We're skipping endpoint tests in the project (per Phase 3 §Decisions). Manual smoke test as part of the phase exit.

## Exit criteria

- A holder of `/e/{slug}/manage?t={token}` can edit event fields, manage meal options (add/edit/delete with reassignment), view orders, delete attendances, close the event, and rotate the manage token. The new token's URL works; the old URL fails with the "invalid manage link" view.
- A holder of `/e/{slug}/manage/recover` with the correct owner email triggers a console-log email containing the manage URL. Wrong email triggers the same generic "if your email matches…" message.
- `dotnet build` and `dotnet test` are green; new tests cover service-level happy-path, validation cases, and the meal-option reassignment behaviour.
- `design.md` is unchanged or updated for any drift caught during execution.
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern" before the phase is truly closed. Findings recorded as a peer subsection in this file's "What actually happened". *(Phase 3 missed this; Phase 4 should not.)*

## What actually happened

**Executor**: Claude Code (claude-sonnet-4-5 / claude-haiku-4-5 per task)
**Completed**: 2026-05-06

### Actual models used

| Task range | Model used | Notes |
|---|---|---|
| 4.1–4.3 (service layer) | Sonnet | Service interfaces + implementations; `DeleteByOwnerAsync` used Haiku as planned |
| 4.4–4.10 (Blazor pages) | Sonnet | All page work done in single session |
| 4.11–4.13 (tests) | Sonnet | Phase 4 tests; 4.13 (`DeleteByOwnerAsync`) was Haiku-appropriate but done inline |
| 4.14 (retro) | Haiku | This section |

### Deviations from plan

- **`GetByOwnerEmailAsync` ORDER BY**: initial implementation sorted `DateTimeOffset.CreatedAt` server-side inside the LINQ query, which SQLite rejects. Fixed to materialise first then sort client-side (identical to the `ListByEventAsync` fix in Phase 3). Discovered at test time, not noted as a risk.
- **EF Core identity map in test**: `MealOptionServiceTests.DeleteAsync_WithDependentAttendances_ConvertsThem` initially verified post-delete state via the `attendanceSvc` DbContext that had created the attendances. Since that context still had the entities tracked with their pre-delete state, the assertions saw stale data. Fixed by adding a fresh `_db.CreateDbContext()` for the verify step. Root cause: test author forgot that LINQ queries against a tracked entity's identity key resolve from the L1 cache, not the DB.
- **`NullEmailSender` file-scoped class**: `MealOptionServiceTests.cs` references `NullEmailSender` (needed to construct `AttendanceService` in the reassignment test) but that `file sealed class` lives only in `EventServiceTests.cs` and `AttendanceServiceTests.cs`. Added a third copy to `MealOptionServiceTests.cs`. Not a design issue — the three test files each need their own due to the `file` access modifier.

### Surprises

- The Blazor manage page (`ManageEvent.razor`) ended up quite long (~300 lines). Task 4.6 (meal options inline edit) was the densest section, consistent with the plan's **L** sizing. No surprises in complexity, just volume.
- The Phase 3 hard rule ("no `IHttpContextAccessor`, no form-post") held cleanly throughout; no temptation to reach for it. Having it explicit in §Decisions paid off.

### Things to do differently

- **Test against fresh contexts by default** when verifying cross-service state. Using the originating service's DbContext for post-mutation reads silently returns stale data. The pattern should be: seed via `_db.CreateDbContext()`, act via `_sut`, verify via another `_db.CreateDbContext()` — or at least be explicit when deviating.
- **Check for SQLite ORDER BY on `DateTimeOffset`** in any new service method that sorts before it reaches tests. It's a known footgun; should be caught in code review, not by a red test.

### Phase Exit review

Phase Exit review not yet performed by Cowork (Phase 4.5 execution is next in queue). Deferred — same as Phase 3's gap. Wilhelm is aware; review before Phase 4.5 starts if timing allows.
