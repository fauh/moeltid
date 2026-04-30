# Phase 2 — Event creation

**Status**: COMPLETE — 2026-04-30.

## Goal

Anyone can create an event via `/new`, land on a success page showing the manage URL, and navigate to `/e/{slug}` to see the event. Data persists in SQLite. No email yet (Phase 5). No meal options yet (Phase 3).

## Decisions confirmed at kickoff

- **Slug format**: `{kebab-title}-{6-char-random}`, fallback `event-{6-char-random}`. Resolved in `design.md` §9.

## Task breakdown

| # | Task | Surface | Model | Size | Notes |
|---|---|---|---|---|---|
| 2.1 | Delete template cruft | 5 files + `Program.cs` + `NavMenu.razor` | **Haiku** | S | |
| 2.2 | Add NuGet packages | `Moeltid.csproj` | **Haiku** | S | EF Core Sqlite 10.0.7 + Design 10.0.7 |
| 2.3 | `Event` entity | `Models/Event.cs` | **Haiku** | S | |
| 2.4 | `AppDbContext` | `Data/AppDbContext.cs` | **Haiku** | S | Unique index on `Slug`; `OwnerEmail` lowercased via EF value converter |
| 2.5 | Wire EF + services | `Program.cs`, `appsettings.json` | **Haiku** | S | Auto-migrate on startup |
| 2.6 | Token + slug generators | `Services/TokenGenerator.cs`, `Services/SlugGenerator.cs` | **Haiku** | S | |
| 2.7 | EF migration + DB update | shell | **Haiku** | S | Updated `dotnet-ef` global tool from v9 → v10.0.7 first |
| 2.8 | `/new` form + submit handler | `Pages/NewEvent.razor` | **Sonnet** | M | JS interop for TZ; 3-attempt retry on slug collision |
| 2.9 | `/created/{EventId}` success page | `Pages/EventCreated.razor` | **Haiku** | S | |
| 2.10 | `/e/{Slug}` placeholder | `Pages/EventPage.razor` | **Haiku** | S | |
| 2.11 | Update `Index.razor` | `Pages/Index.razor` | **Haiku** | S | |
| 2.12 | Update `design.md` §9 + `change_log.md` | docs | **Haiku** | S | |
| 2.13 | Create this file + commit + push | docs + git | **Haiku** | S | |

## Exit criteria

- [x] `/new` form persists an event to SQLite.
- [x] `/created/{id}` shows manage URL and console-logs the email stub.
- [x] `/e/{slug}` renders event title and description.
- [x] Restarting the app and revisiting the slug still works (data persists).

## What actually happened

Completed 2026-04-30 by Claude Code (Sonnet 4.6) with Wilhelm Ericsson.

**Actual models used**: Sonnet 4.6 throughout (single Claude Code session; no per-task model switching).

**Deviations from plan:**
- `dotnet-ef` global tool was on v9.0.10; needed updating to v10.0.7 before migrations would work. Added as a sub-step to task 2.7.
- CA1716 warning on `Event` class name (reserved keyword in VB/F#) — suppressed with `#pragma warning disable` since the domain name is mandated by the design and this is a C#-only project.
- CA1805 warning (`IsClosed = false` redundant) — fixed by removing the explicit initialiser.

**Post-plan fixes (caught during review):**
- `Event.StartsAt` (DateTimeOffset) added — the event date/time was missing from the original model. `AddEventStartsAt` migration applied; form, success page, and event page updated. `design.md` §5 updated.
- Datetime picker defaults changed from `00:00` (midnight) to `12:00` (noon) — midnight is not a sensible default for a meal event.
- TZ fallback changed from `Europe/Stockholm` to `UTC` — JS interop already detects the browser's local TZ; the hardcoded country default was wrong for non-Swedish users.
- Branch/PR convention established: `phase-N` branches from Phase 3 onwards, PR per phase. Added to `process.md`.

**Surprises:**
- None significant. The auto-migrate-on-startup pattern worked cleanly; the slug collision retry loop was straightforward.

**Things to do differently:**
- Review the data model against the form fields before writing code — `StartsAt` would have been caught at planning time.
- Establish branch convention before starting, not after.
- The TZ conversion bug (see review findings below) self-masked under single-TZ happy-path testing — read and write shared the same wrong assumption, so a manual round-trip looked correct. Manual smoke-testing should include a deliberate "what if I'm not in UTC" check; the structural answer is the test project landing in Phase 2.5.
- "Phase complete" is best treated as "Code's POV says complete" — exit through a Cowork-side review pass before signing the phase off in the change log. Adopt this as a formal exit step from Phase 3 onwards.

**Post-completion review findings (Cowork-side, 2026-04-30):**

A read-through of the Phase 2 deliverables in Cowork after Code closed the phase surfaced one correctness bug and three smaller issues. Listed in severity order:

- **🔴 TZ input bug** — `model.StartsAt` and `model.Deadline` are local wall-clock `DateTime`s from `<InputDate>`, but the submit handler stores them with `TimeSpan.Zero`, claiming UTC. A user in Stockholm entering "12:00" gets stored as 12:00 UTC (= 14:00 local). Round-trips look correct inside a single Phase-2-only session because the display side also reads the embedded offset without applying any TZ conversion — both sides share the same wrong assumption, so the bug self-masks. Surfaces immediately when Phase 5 reminders fire (wrong wall-clock hour) or when Phase 4's manage page applies `Event.TimeZoneId` per `design.md` §8.
- **🟡 Display side mirrors the same gap** — `EventCreated.razor` and `EventPage.razor` use `.ToString("f")` directly on the `DateTimeOffset`, ignoring `Event.TimeZoneId`. Per design, the manage page should render in owner TZ.
- **🟡 No deadline-vs-StartsAt validation** — defaults seed a sensible deadline (1 day before event) but a user can type a deadline that follows the event start. No server-side rejection.
- **🟡 No unique index on `Event.ManageToken`** — token has ~132 bits of entropy so collisions are theoretical, but a UNIQUE index is one fluent line + one migration command and gives the same retry-on-collision semantics already in place for `Slug`.

Smaller observations also captured during the review (deferred to specific later phases — listed for completeness, not action):

- `IJSRuntime.InvokeAsync<string>("eval", ...)` for TZ detection works but is non-idiomatic and CSP-fragile. Deferred to Phase 8 polish.
- `Console.WriteLine` for the email stub should use `ILogger`. Will be replaced wholesale at Phase 5.
- Slug generator strips Swedish characters (`Måltid` → `m-ltid`). Cosmetic; slugs aren't user-facing strings.
- `.slnx` (new XML solution format) — recent Visual Studio support; confirmed working in Wilhelm's setup, flagged for awareness if collaborators on older tooling join.
- No tests yet. The TZ bug is exactly what a single `EventService.CreateAsync` test would have caught at write time. Phase 2.5 introduces the test project.

The four 🔴/🟡 issues above are tracked as follow-up tasks `2.14`–`2.18` below.

---

## Follow-up tasks — bugfixes (added 2026-04-30 after Cowork review)

Cowork-side review of Phase 2 surfaced one correctness bug and three smaller issues. Listing them here as `2.14`+ to keep the phase plan honest. These run before Phase 2.5; they fix already-shipped code rather than introducing new structure.

| # | Task | Surface | Model | Size | Notes |
|---|---|---|---|---|---|
| 2.14 | **Fix TZ input conversion** in `NewEvent.HandleSubmit` | `Pages/NewEvent.razor` | **Sonnet** | M | `model.StartsAt` and `model.Deadline` are local wall-clock `DateTime`s but are being stored with `TimeSpan.Zero`, claiming UTC. Use `TimeZoneInfo.FindSystemTimeZoneById(model.TimeZoneId)` + `ConvertTimeToUtc` (with `DateTime.SpecifyKind(..., Unspecified)` first). Fall back to `TimeZoneInfo.Utc` if the IANA ID doesn't resolve. |
| 2.15 | **Fix TZ display** for the manage-side success page and the public event page | `Pages/EventCreated.razor`, `Pages/EventPage.razor`, possibly a small shared helper | **Sonnet** | M | `.ToString("f")` reads the embedded offset (UTC) without applying `Event.TimeZoneId`. Add a small helper (component or static method) that takes UTC + IANA → local string. Apply it to `StartsAt`/`Deadline` rendering. (The public page renders in `Event.TimeZoneId` for now; full attendee-browser-TZ rendering can wait until Phase 3.) |
| 2.16 | **Deadline-vs-StartsAt validation** in the form model | `Pages/NewEvent.razor` (`FormModel`) | **Sonnet** | S | Implement `IValidatableObject` or a `[CustomValidation]` attribute. Reject submission when `Deadline >= StartsAt`. Surface message via `ValidationSummary`. |
| 2.17 | **Unique index on `Event.ManageToken`** | `Data/AppDbContext.cs`, new EF migration `AddUniqueIndexOnManageToken` | **Haiku** | S | Defence in depth. Token has 132 bits of entropy, but a UNIQUE index is one fluent line + one migration command. Mirror the slug retry pattern if a collision ever fires. |
| 2.18 | **Doc + change_log update** | `change_log.md`, possibly `design.md` if §5 needs amending | **Haiku** | S | One dated entry summarising the fixes. |

**Exit criteria for the follow-up:**
- [ ] An event scheduled at "12:00 in Stockholm" round-trips as 12:00 in the manage view (was: 12:00 UTC stored, displayed without TZ conversion → coincidentally fine in Phase 2 but wrong everywhere else).
- [ ] Form rejects deadlines that fall after the event start.
- [ ] `Event.ManageToken` has a unique index in the schema.
- [ ] `change_log.md` records the fix-it pass.

**Total**: 5 tasks. Sonnet for the conversion logic (judgement-heavy), Haiku for the index + docs. No Opus needed — these are well-specified fixes.

The `eval`-based TZ detection and the `Console.WriteLine` email stub are flagged but **deferred to Phase 8 (polish)** and **Phase 5 (real email)** respectively. The `IJSRuntime` `eval` works for now and the email stub gets replaced wholesale at Phase 5.
