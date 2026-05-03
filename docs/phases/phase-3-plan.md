# Phase 3 — Attendee signup and meal ordering

**Status**: signed off — 2026-05-01. Awaiting executor pickup.

This is the first phase that runs under the formalised "Phase exit — the two-tool review pattern" from `process.md` — i.e. Cowork performs a review pass before the phase is truly closed.

## Why this phase

Phase 2 + 2.5 produced an event-creation flow with no way for anyone other than the creator to interact with it. Phase 3 is what makes the app actually do something: attendees with the link can sign up and submit a meal order; they can return later via a saved URL or emailed link to update or withdraw it. This is the central use case — the rest of the app exists to support this flow.

## Goal

Anyone with a `/e/{slug}` URL can:
- See the event details,
- Submit a meal order (free-text or preset option),
- See other attendees' orders when `Event.AttendeeOrdersVisible` is on,
- Return later via a saved `/e/{slug}?t={token}` URL or emailed edit link to update or withdraw their order.

Owner-side meal-option management lands in Phase 4. For Phase 3, options are seeded via test fixtures only.

## Decisions confirmed at kickoff

Locked at sign-off so execution stays judgement-free.

- **Order submission uses a static `<form method="post">` posting to a minimal-API endpoint** (`POST /e/{slug}/order`), not interactive Blazor. Reasoning: keeps state stateless on the server side, avoids the SignalR-circuit / `HttpContext.Response` mismatch, and stays simple to test directly. The page itself remains Blazor-rendered — only the submit roundtrip is a plain form post. The same pattern continues for edit and withdraw in this phase, and for Phase 4's manage actions.
- **Anti-forgery tokens** are required on the form and validated in the endpoint. Standard ASP.NET Core middleware.
- **No cookies. The `EditToken` lives in the URL only.** After submit, the endpoint redirects to `/e/{slug}?t={editToken}`. This URL is bookmarkable and is the attendee's "save this — it's your way back" path, mirroring Phase 2's manage-link pattern. If the attendee provided an email, the same URL is also emailed. The success-redirect page renders a prominent "save this URL" call-out, the same way `EventCreated.razor` does for the manage link.
- **Visibility-toggle semantics with URL tokens**:
  - `Event.AttendeeOrdersVisible == true` → all attendees and orders shown to everyone with the public link.
  - `false` + URL has a valid `?t=` → show only the matched attendee's own row (their own banner) plus the form for new submissions.
  - `false` + no `?t=` → show no attendee data; just the form. Owner sees everything via the manage page (Phase 4).
- **Attendee email is optional.** If provided, `IEmailSender` fires with the edit URL (real send arrives in Phase 5; for now it's the console stub).
- **Form-mode logic**:
  - Event has preset `MealOption`s → show them as radio buttons. If `AllowFreeText` is also true, append an "Other / write my own" radio with a free-text input.
  - No preset options and `AllowFreeText` → just the free-text input (the primary path for most events).
  - Neither preset options nor `AllowFreeText` → "this event isn't accepting orders yet" message. Edge case for misconfigured events.
- **One-order-per-attendee enforcement** is best-effort. Strict uniqueness on `(EventId, Name)` is rejected (names aren't authoritative). An attendee can in principle submit twice from different sessions; the owner cleans up via Phase 4's manage page if it happens.
- **`Attendance.Email` is lower-cased on save** via the same EF value-converter pattern as `Event.OwnerEmail`.
- **`MealOption` deletion behaviour**: declare `OnDelete(DeleteBehavior.Restrict)` on `Attendance.MealOptionId` so Phase 4's option-deletion has to think about cascading vs nulling.
- ~~**EventCreated.razor not-found fix** included as task 3.0~~ — carry-over from Phase 2.5's review (🟡). **Applied 2026-05-01 by Cowork via direct file edit** before Phase 3 kickoff, per the new "Bugfix discipline" section in `process.md` (bugs get fixed as soon as identified, not queued). Removed from this phase's task list.

## Open questions — resolved at sign-off

All four resolved by Wilhelm on 2026-05-01:

- ✅ **Form post vs interactive Blazor for submit** — form post + minimal-API endpoint.
- ✅ **Edit URL: cookie + query, or query only** — query only. Wilhelm noted "if it significantly reduces complexity, only the query param is good enough." Cowork judged the cookie removed ~50 lines of well-isolated infra plus aligned the design with Phase 2's URL-as-keyholder pattern; complexity reduction was deemed significant. URL-only it is.
- ✅ **Preset meal options seeded via test fixtures** — yes, no Phase-3-only owner UI.
- ✅ **WebApplicationFactory-based endpoint integration tests** — skipped for Phase 3.

## Prerequisites

- Phase 2.5 closed (done; all 22 existing tests passing as of plan sign-off). Cowork-side review of Phase 2.5 also complete; 🟡s deferred to specific later phases per the retrospective.

## Task breakdown

| #    | Task                                                                                                                                                                                          | Surface                                                                       | Model      | Size | Notes                                                                                                                                                                                                                              |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- | ---------- | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 3.1  | `MealTag` flags enum + `OrderType` enum                                                                                                                                                       | `Models/MealTag.cs`, `Models/OrderType.cs`                                    | **Haiku**  | S    | `MealTag = None / Drink / Fish / Vegetarian / Vegan` (flags). `OrderType = PresetOption / FreeText`.                                                                                                                               |
| 3.2  | `MealOption` entity                                                                                                                                                                           | `Models/MealOption.cs`                                                        | **Haiku**  | S    | `Id, EventId, Label, Tags`. Navigation back to `Event`.                                                                                                                                                                            |
| 3.3  | `Attendance` entity                                                                                                                                                                           | `Models/Attendance.cs`                                                        | **Haiku**  | S    | Per `design.md` §5: `Id, EventId, Name, Email?, EditToken, OrderType, MealOptionId?, FreeTextOrder?, SubmittedAt`.                                                                                                                 |
| 3.4  | `AppDbContext` updates: DbSets + navigations + indexes                                                                                                                                        | `Data/AppDbContext.cs`                                                        | **Haiku**  | S    | Unique index on `Attendance.EditToken`; non-unique on `Attendance.EventId` and `MealOption.EventId`. Lower-case value converter on `Attendance.Email`. `OnDelete(DeleteBehavior.Restrict)` on `Attendance.MealOptionId`.            |
| 3.5  | EF migration `AddAttendancesAndMealOptions`                                                                                                                                                   | `Migrations/*`                                                                | **Haiku**  | S    | Generated via `dotnet ef migrations add`. Verify schema matches the plan before applying.                                                                                                                                          |
| 3.6  | `IMealOptionService` + `MealOptionService`                                                                                                                                                    | `Services/MealOptions/*`                                                      | **Haiku**  | S    | Single method `ListByEventAsync(Guid eventId)`. CRUD lands in Phase 4.                                                                                                                                                             |
| 3.7  | `IAttendanceService` interface + `CreateAttendanceRequest` / `UpdateAttendanceRequest` records                                                                                                | `Services/Attendances/IAttendanceService.cs`                                  | **Haiku**  | S    | Methods: `CreateAsync`, `GetByEditTokenAsync`, `UpdateAsync`, `DeleteAsync`, `ListByEventAsync`.                                                                                                                                   |
| 3.8  | `AttendanceService` implementation                                                                                                                                                            | `Services/Attendances/AttendanceService.cs`                                   | **Sonnet** | M    | Generates `EditToken`, persists, validates the OrderType + payload combination, sends email stub when `Email` provided. Mirrors `EventService` patterns (logging, exception shape, retry-on-collision for `EditToken` uniqueness). |
| 3.9  | DI registration for the two new services                                                                                                                                                      | `Program.cs`                                                                  | **Haiku**  | S    | `AddScoped` for both.                                                                                                                                                                                                              |
| 3.10 | Anti-forgery configured + minimal-API endpoints `POST /e/{slug}/order`, `POST /e/{slug}/order/{attendanceId}`, `POST /e/{slug}/order/{attendanceId}/delete`                                   | `Program.cs` (or a new `Endpoints/AttendanceEndpoints.cs`)                    | **Sonnet** | M    | Each handler: validate antiforgery, parse form, call service, redirect. Create-success redirects to `/e/{slug}?t={editToken}`. Update-success redirects to the same. Delete-success redirects to `/e/{slug}` (no token).            |
| 3.11 | `/e/{slug}` public page: load event + options + attendees, render form-mode logic, render attendee list per visibility toggle, render existing-order banner when URL has a valid `?t=`        | `Pages/EventPage.razor`                                                       | **Sonnet** | M    | Replaces the Phase 2 placeholder. Form posts to `POST /e/{slug}/order`. Reads `?t=` from query string via the route's `[Parameter, SupplyParameterFromQuery]` attribute or the navigation manager.                                  |
| 3.12 | `/e/{slug}/edit-order` page: read `?t=` from URL, look up attendance, render pre-populated form + withdraw button                                                                             | `Pages/EditOrder.razor`                                                       | **Sonnet** | M    | Form posts to the update / delete endpoints. Includes a "back to event" link. 404-style handling if `?t=` missing or invalid.                                                                                                       |
| 3.13 | Email stub: `AttendanceService.CreateAsync` calls `IEmailSender.SendAsync` when `Email` is provided                                                                                           | inside `AttendanceService`                                                    | **Haiku**  | S    | Body contains the absolute edit URL (`/e/{slug}?t={editToken}`). Mirrors the manage-link email pattern.                                                                                                                             |
| 3.14 | Tests: `MealOptionServiceTests`                                                                                                                                                               | `tests/.../MealOptionServiceTests.cs`                                         | **Haiku**  | S    | 1–2 tests: `ListByEventAsync` returns options for the right event; empty for unknown.                                                                                                                                              |
| 3.15 | Tests: `AttendanceServiceTests`                                                                                                                                                               | `tests/.../AttendanceServiceTests.cs`                                         | **Sonnet** | M    | ~10 tests: create with FreeText, create with PresetOption, email lower-cased, GetByEditTokenAsync wrong-token returns null, UpdateAsync respects token, DeleteAsync respects token, ListByEventAsync returns event's attendances, validation rejects mismatched OrderType + payload combos. |
| 3.16 | Visibility-toggle behaviour test                                                                                                                                                              | `tests/.../AttendanceServiceTests.cs`                                         | **Sonnet** | S    | Express the rule as a service method or pure helper, test it. Avoid Razor-page tests for now.                                                                                                                                      |
| 3.17 | `change_log.md` close entry; retro at bottom of this file (executor + actual model(s) per `process.md`)                                                                                       | docs                                                                          | **Haiku**  | S    | If anything in `design.md` drifted during execution, fix it.                                                                                                                                                                       |

**Total**: 17 tasks (was 18 in the post-sign-off draft; task 3.0 was removed once the EventCreated fix was applied pre-phase per the new bugfix-discipline rule). Sonnet for the judgement-heavy services, pages, endpoints, and tests; Haiku for entities, enums, DI, and docs. No Opus tasks — every architectural decision is committed in §"Decisions confirmed at kickoff".

This is bigger than Phase 2 (16) or Phase 2.5 (11). Splitting into 3a (data + services + tests) and 3b (pages + endpoints + email stub) is *possible* but loses the ability to validate the data model end-to-end mid-phase. Recommendation: keep as one phase; if it sprawls in execution, the executor can split mid-phase and document in the retro.

## Risks / what might bite

- **Antiforgery in the legacy `dotnet new blazorserver` template** — middleware isn't enabled by default for endpoints. May need explicit `app.UseAntiforgery()` and `[FromForm]` attributes. Captured in 3.10.
- **Validation seam between FormModel and service** — same concern flagged in Phase 2.5's review. For Phase 3 the service must check the OrderType + payload combination is valid (`OrderType=FreeText` requires non-empty `FreeTextOrder`; `OrderType=PresetOption` requires `MealOptionId` non-null and pointing at an option that belongs to the event). Captured in 3.8.
- **Test isolation in `AttendanceServiceTests`** — Phase 2.5's review flagged the shared-DB semantics of `IClassFixture<InMemoryDatabaseFixture>`. As tests accumulate, count-based assertions will flake. Convention: assert on returned values; document the convention in the fixture if not already.
- **Edit-token leakage** — the edit URL is sensitive. Don't log it; don't include it in error responses; don't put it in the page source for someone else's order. The visibility toggle keeps this contained on the public page; the edit page never renders other attendees' tokens.
- **`MealOptionId` referential integrity** — `OnDelete(DeleteBehavior.Restrict)` chosen so Phase 4's option-deletion has to make an explicit choice. Captured in 3.4.
- **Query-string `?t=` in browser history / referrer headers** — the edit token will appear in URL bars and may leak via Referer headers if the page links to external sites. Mitigations: avoid external links from `/e/{slug}?t=` and `/e/{slug}/edit-order?t=`; or set a `Referrer-Policy: same-origin` meta tag globally. Worth a small note on the success page's "save this URL" guidance ("don't share this URL — anyone with it can edit your order"). Captured in this section, addressed in 3.11/3.12 prose.

## Exit criteria

- An attendee with a `/e/{slug}` URL can submit a free-text order; the success redirect URL contains `?t={editToken}`; the page renders the existing-order banner.
- An attendee can update or withdraw their order via `/e/{slug}/edit-order?t={editToken}`.
- When `Event.AttendeeOrdersVisible == true`, all attendees see all orders. When false + valid `?t=`, only the matched row is visible. When false + no `?t=`, no attendee data is rendered. Owner-side visibility lands in Phase 4 and is unaffected.
- An attendee who provides an email gets an `IEmailSender` log entry containing their edit URL.
- `dotnet build` and `dotnet test` are green; new tests cover service-level happy-path and validation cases.
- `design.md` is unchanged (already aligned at plan-write time) or updated for any drift caught during execution.
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern" before the phase is truly closed. Findings recorded as a peer subsection in this file's "What actually happened".

## What actually happened

_To be filled in at phase end. Per `process.md`, must include: the executor (Claude Code, GitHub Copilot, or other), the actual model(s) run, deviations from plan, surprises, and what to do differently._
