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
