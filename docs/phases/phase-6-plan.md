# Phase 6 — CSV export

**Status**: signed off — 2026-05-07. Awaiting executor pickup.

## Why this phase

The original use case in `design.md` is: *"After events, a summary of the events orders can be exported for ease of taking care of accounting."* Phase 6 delivers that — a downloadable CSV containing the event's orders, generated from the manage page.

Small, focused phase. No new entities, no new services beyond the exporter itself, no new infrastructure. After Phase 6, the only remaining v1-core piece is Phase 6.5 (events listing — deferred enhancement), then it's polish, deploy, and launch.

## Goal

A manage-token holder on `/e/{slug}/manage` can click a "Download orders CSV" button and get a file named `event-{slug}-orders-{yyyy-MM-dd}.csv` containing one row per attendee plus, optionally, one row per still-unordered invitee. UTF-8 with BOM (so Excel opens it cleanly). Dates rendered in the event's owner timezone with the IANA TZ name alongside.

## Decisions confirmed at kickoff

Locked at sign-off so execution stays judgement-free. Hard rules from prior phases carry; new ones below.

**Hard rules carried**:
- Interactive Blazor for ALL manage actions; no form-post-to-minimal-API. (Phase 4 rule.) **See `§Open questions` for the export-download nuance — the spirit of this rule may permit a GET endpoint for download even though the letter says no.**
- Per-task verification table in the retrospective. (Phase 3 rule.)
- Plan internal-consistency check before lock. (Phase 4 rule.)
- Cowork Phase Exit review non-deferrable. (Phase 4 rule.)
- Pure helpers for layered/joined views (the `AttendanceVisibility` / `EventDisplayList` / `ReminderAudience` pattern). The CSV exporter is a transformer over collections; same pattern.

**New decisions for this phase**:

- **CSV library**: `CsvHelper` (de-facto standard in .NET; handles quoting, escaping, encoding correctly out of the box). Hand-rolling is tempting for "just commas" but breaks on free-text orders containing commas, quotes, or newlines.
- **File-name format**: `event-{slug}-orders-{yyyy-MM-dd}.csv`. The date is when the export was generated, in the event's timezone.
- **Encoding**: **UTF-8 with BOM**. Excel on Windows misreads BOM-less UTF-8 CSVs as Windows-1252; the BOM is the single-line fix.
- **Columns** (order matters — owner's accounting flow):
  1. `Name`
  2. `Email`
  3. `OrderType` (`PresetOption` / `FreeText` / `NoOrderYet`)
  4. `OptionLabel` (empty when `OrderType != PresetOption`)
  5. `FreeTextOrder` (empty when `OrderType != FreeText`)
  6. `Tags` (comma-separated flag names; empty when `OrderType != PresetOption` or option has no tags)
  7. `SubmittedAt_OwnerTZ` (e.g. `2026-05-10 13:24 Europe/Stockholm`; empty for NoOrderYet rows)
  8. `SubmittedAt_UTC` (e.g. `2026-05-10 11:24Z`; empty for NoOrderYet rows)
- **Tokens excluded.** No `EditToken`, no `ManageToken`, no `InviteeId` in the CSV. The export is for accounting; leaked CSVs shouldn't leak access credentials. Captured as a §Risk.
- **Audience**: attendees (all of them, regardless of email) PLUS invitees who haven't ordered. Anonymous attendees (no email) get a row with empty `Email`. NoOrderYet rows have only `Name = email` (we don't have a name for unfulfilled invitees), `Email`, and `OrderType = NoOrderYet`; the rest are empty. *See §Open questions — whether to include NoOrderYet rows at all.*
- **`CsvExportBuilder` is a pure helper** (no DB, no I/O). Takes `Event` + `IReadOnlyList<Attendance>` + `IReadOnlyList<Invitee>`, returns the CSV text. Mirrors the established pattern; trivially testable with golden-file or per-row assertions.
- **Download mechanism**: **minimal-API GET endpoint** `GET /e/{slug}/manage/orders.csv?t={token}` (locked at sign-off). Read-only GET endpoints are **exempt** from the Phase 4 "no minimal-API for manage actions" rule because they have none of the concerns that rule was about (no antiforgery, no cookies, no `HttpContext`-on-render seam). The rule applies to **mutating** manage actions; downloads are not mutating. This Phase 6 plan is the precedent; future plans that need a read-only manage endpoint can reference it.
- **Post-close download prompt** (scope addition at sign-off): the manage-page close-section transitions to a "download the CSV" prompt after the close action succeeds. See §Open questions for the design rationale.

## Open questions — resolved at sign-off

All four resolved by Wilhelm 2026-05-07:

- ✅ **Download mechanism**: **minimal-API GET endpoint** `GET /e/{slug}/manage/orders.csv?t={token}`. Plain `<a href="..." download>` on the manage page. Wilhelm's framing: "simpler is always desirable" — the Blazor + JS + base64 round-trip alternative has more moving parts. The Phase 4 "no minimal-API for manage actions" rule applies to **mutating** actions (form posts, cookies, antiforgery, HttpContext seam); read-only GETs have none of those concerns and are exempt. This Phase 6 plan is the precedent.
- ✅ **NoOrderYet rows included**. Wilhelm: "a more complete picture is always better than a less complete picture." Empty order columns clearly mark them.
- ✅ **CsvHelper** (not hand-roll). RFC 4180 quoting is non-trivial; the dependency is worth it.
- ✅ **File name**: `event-{slug}-orders-{yyyy-MM-dd}.csv`, date in event's owner TZ.

### Scope addition agreed at sign-off

**Post-close download prompt.** When an event is closed via the manage page, the close-section in the danger zone transitions to a new state: *"Event closed. Don't forget to download the orders CSV for your records."* with inline **[Download orders CSV]** and **[Dismiss]** buttons. Persists until the owner acts. The regular Orders-section download button stays as before, so the same download is reachable from two places.

Why this over renaming "Close event" to "Close and export": close is one-way and destructive (no re-open in v1); bundling it with a download in one click conflates two distinct actions. Why this over a modal popup: inline state-changes are part of the page; popups are easy to dismiss without reading.

Folded into task 6.5's scope.

### Knock-on from sign-off (sign-off-decision review rule)

Re-checked every other §"Decisions confirmed at kickoff" item against the answers and the scope addition. No reasoning chains broken. The "GET endpoint exemption" is captured both in §Decisions (locked text) and in the Open Questions resolution above, so future executors reading either section see the same answer.

## Prerequisites

- Phase 5 closed (it is, smoke test passed 2026-05-07).
- All 96 tests passing as of plan writing.

## Task breakdown

| #     | Task                                                                                                                       | Surface                                                                          | Model      | Size | Notes                                                                                                                                                                                                                                |
| ----- | -------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------- | ---------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 6.1   | Add `CsvHelper` NuGet ref                                                                                                  | `Moeltid.csproj`                                                                 | **Haiku**  | S    | Latest stable.                                                                                                                                                                                                                       |
| 6.2   | `CsvExportBuilder` pure helper                                                                                             | `Services/Exports/CsvExportBuilder.cs`                                           | **Sonnet** | M    | Takes `Event`, `IReadOnlyList<Attendance>` (with `MealOption` included), `IReadOnlyList<Invitee>`. Returns `byte[]` (UTF-8 with BOM). Builds rows per the locked column order. Uses `CsvHelper` for proper quoting.                  |
| 6.3   | Tests for `CsvExportBuilder`                                                                                               | `tests/.../CsvExportBuilderTests.cs`                                             | **Sonnet** | M    | ~8 tests: header row, FreeText with comma in body, PresetOption with tags, NoOrderYet rows, empty attendances + empty invitees, anonymous attendee (no email), tag-flag serialization, BOM presence at byte 0.                       |
| 6.4   | Minimal-API GET endpoint `GET /e/{slug}/manage/orders.csv?t={token}`                                                       | `Endpoints/ExportEndpoints.cs` (new) + `Program.cs` (registration)                | **Sonnet** | M    | Validates `?t=` against `Event.ManageToken`. Returns `Results.NotFound()` for invalid token or missing event (same generic 404 as the Phase 4 invalid-manage-link view — don't leak slug existence). On success: calls `CsvExportBuilder`, returns `Results.File(bytes, "text/csv", filename)`. |
| 6.5   | Wire download button into `ManageEvent.razor` + post-close download prompt                                                 | `Pages/ManageEvent.razor`                                                        | **Sonnet** | M    | Two surfaces. (a) **Orders section**: "Download orders CSV" button (plain `<a href="..." download>`); disabled-look when there are zero attendees AND zero invitees. (b) **Danger zone after close**: when `confirmClose` succeeds and `ev.IsClosed` becomes true, the close-section state transitions to *"Event closed. Don't forget to download the orders CSV for your records."* with inline **[Download orders CSV]** and **[Dismiss]** buttons. New page state field `postCloseDownloadDismissed` controls dismissal. Persists across re-renders until clicked or dismissed. |
| 6.6   | `design.md` updates: §3 confirms CSV export delivered; §6 page table mentions the export endpoint                          | docs                                                                             | **Haiku**  | S    |                                                                                                                                                                                                                                      |
| 6.7   | `change_log.md` close entry; retro at bottom of this file (executor + actual model(s); per-task tick table)                | docs                                                                             | **Haiku**  | S    | Per `process.md`.                                                                                                                                                                                                                    |

**Total**: 7 tasks (one task removed at sign-off — the conditional `process.md` clarification isn't needed; the §Decisions text in this plan stands as the precedent). Small phase by design. Two Sonnet (`CsvExportBuilder` + its tests, the endpoint), one Sonnet for the manage-page wiring + post-close prompt, the rest Haiku.

## Risks / what might bite

- **Free-text order content** — commas, quotes, newlines, Unicode. `CsvHelper` handles RFC 4180 quoting correctly. Tests must include at least one row with each of these.
- **Tags as a flags enum** — `MealTag.Vegetarian | MealTag.Drink` → `.ToString()` gives `"Vegetarian, Drink"` which already contains a comma. CsvHelper will quote the cell, so it round-trips. Tests must include a multi-flag row to confirm.
- **Excel and BOM** — without the BOM, Excel opens UTF-8 CSV as Windows-1252 and mangles `ö`/`å` etc. Test asserts BOM presence (`0xEF 0xBB 0xBF`) at byte 0.
- **Tokens in URLs** — by Decision, the CSV body excludes `EditToken`, `ManageToken`, `InviteeId`. Tests confirm none of these strings appear in the output. (Mitigation for a leaked-CSV scenario.)
- **Anonymous attendees** (no email) — covered: empty `Email` column. Tests include one such row.
- **Empty exports** — zero attendees + zero invitees. Decision: button disabled in UI; the builder still works and produces a header-only CSV when called directly.
- **File name and slugs with special characters** — slug format from Phase 2 is `[a-z0-9-]+` plus a 6-char random suffix; already filename-safe. No escaping needed.

## Exit criteria

- A manage-token holder can click "Download orders CSV" and get a CSV file with the columns listed in §Decisions, in the order listed.
- BOM is present at byte 0; the file opens cleanly in Excel without mojibake.
- Free-text orders with commas, quotes, and newlines round-trip correctly (Excel shows them in a single cell).
- Tags flag combinations serialize as readable lists (e.g. `"Vegetarian, Drink"`).
- Tokens (edit / manage / invitee IDs) do not appear in the export.
- `dotnet build` and `dotnet test` are green; new `CsvExportBuilderTests` cover the edge cases listed in §Risks.
- `design.md` updated to mark the export feature delivered.
- **Cowork performs a Phase Exit review pass** per `process.md` §"Phase exit — the two-tool review pattern", with the per-task verification rule applied. Findings recorded as a peer subsection in this file's "What actually happened".

## What actually happened

_To be filled in at phase end. Per `process.md`, must include: the executor (Claude Code, GitHub Copilot, or other), the actual model(s) run, deviations from plan, surprises, what to do differently, and an explicit per-task tick against the table above with each tick pointing at the specific code/test artifact. The Cowork-side Phase Exit review goes here as a peer subsection._
