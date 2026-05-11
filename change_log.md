# Change Log

Manual, human-curated record of decisions, design changes, and milestones for Consid Måltid. Newest entries first. Update this file whenever a meaningful decision is made or a phase boundary is crossed.

Format: one section per date (or per work session). Within a date, group entries under a short heading.

---

## 2026-05-11 — Phase 6 complete

**CSV export** delivered. Manage-token holders can download `event-{slug}-orders-{date}.csv` from the manage page at any time; closing an event surfaces a one-time prompt to grab the CSV before moving on.

**What landed:**
- `CsvHelper` 33.1.0 added. `CsvExportBuilder` pure static helper (same pattern as `ReminderAudience`) takes `Event` + `IReadOnlyList<Attendance>` + `IReadOnlyList<Invitee>` and returns a UTF-8-with-BOM `byte[]`. Columns: Name, Email, OrderType, OptionLabel, FreeTextOrder, Tags, SubmittedAt_OwnerTZ, SubmittedAt_UTC. NoOrderYet rows for invitees who haven't ordered. Tokens excluded.
- 11 `CsvExportBuilderTests` — BOM assertion, FreeText/PresetOption columns, multi-tag serialization, commas+quotes round-trip, anonymous attendee, NoOrderYet, no-doubling of invitees who ordered, token exclusion, Stockholm TZ offset.
- Minimal-API GET endpoint `GET /e/{slug}/manage/orders.csv?t={token}`. Returns 404 for bad token or unknown slug. Registered in `Program.cs`. Exempt from Phase 4 "no minimal-API for manage actions" rule (read-only; no antiforgery/cookie/HttpContext concerns).
- `ManageEvent.razor`: Orders-section flex header with conditional download anchor (disabled-look when zero attendees and invitees); post-close prompt with inline **[Download]** and **[Dismiss]** on `ev.IsClosed && !postCloseDownloadDismissed`.
- `design.md` §3 updated with CSV export bullet.

**Stats**: 107 tests passing (+11 from Phase 5's 96). Build: 0 errors, 45 warnings (all pre-existing).

## 2026-05-07 — Phase 6 plan signed off + scope addition

Wilhelm reviewed and signed off all four open questions; all matched the working assumptions or chose the simpler option:

- **Download mechanism**: minimal-API GET endpoint `GET /e/{slug}/manage/orders.csv?t={token}`. Cleaner than Blazor + JS + base64.
- **Audience**: include NoOrderYet rows ("a more complete picture is always better").
- **CsvHelper** (not hand-roll).
- **File name**: `event-{slug}-orders-{yyyy-MM-dd}.csv`.

**Read-only GET endpoint exemption locked.** The Phase 4 hard rule "no minimal-API for manage actions" applies to mutating actions (form posts, cookies, antiforgery, HttpContext seam). Read-only GETs have none of those concerns and are exempt. The Phase 6 plan's §Decisions captures this as the precedent — future plans needing a read-only manage endpoint can reference it. No edit to `process.md` needed (task 6.7 dropped — original plan's conditional clarification task is unnecessary now that the precedent is set in plan §Decisions text).

**Scope addition** at sign-off: when an event is closed via the manage page, the close-section transitions to a *"Event closed. Don't forget to download the orders CSV"* prompt with inline **[Download]** and **[Dismiss]** buttons. Folded into task 6.5's scope. Rationale: close is one-way and destructive — separating from export is cleaner than coupling them in a "Close and export" button-rename; inline state-change is more visible than a modal popup but less intrusive.

Sign-off-decision review rule applied: no ripples; the four picks + scope addition don't break any other Decisions item.

Phase 6 plan locked. **7 tasks** (down from 8 — the conditional `process.md` clarification dropped). Awaiting executor pickup.

## 2026-05-07 — Phase 6 plan written

`docs/phases/phase-6-plan.md` drafted: 8 tasks covering `CsvHelper` integration, a `CsvExportBuilder` pure helper (same pattern as `AttendanceVisibility` / `EventDisplayList` / `ReminderAudience`), tests covering the messy CSV edge cases (free-text with commas/quotes/newlines, tag flag combinations, BOM, anonymous attendees), and a download mechanism on the manage page.

**Locked decisions**:
- `CsvHelper` for RFC 4180 quoting.
- UTF-8 with BOM for Excel compatibility.
- Columns: Name, Email, OrderType, OptionLabel, FreeTextOrder, Tags, SubmittedAt_OwnerTZ, SubmittedAt_UTC.
- Tokens (edit / manage / invitee IDs) **excluded** from the export — leaked CSVs shouldn't leak access credentials.
- Anonymous attendees included with empty Email.

**Four open questions** for sign-off:
1. Download mechanism — minimal-API GET endpoint (Cowork's lean: spirit of the Phase 4 rule allows read-only GETs) vs Blazor + JS interop?
2. Include NoOrderYet rows (invitees who didn't order)? Lean yes.
3. `CsvHelper` vs hand-roll? Lean CsvHelper.
4. File name format. Lean `event-{slug}-orders-{yyyy-MM-dd}.csv`.

Phase 6 plan awaiting sign-off.

## 2026-05-07 — Phase 5 truly closed (smoke test passed)

Wilhelm ran the smoke test with the Resend API key configured via user-secrets. Reminders deliver as intended (status-aware bodies for attendees vs invitees-no-order); recover links deliver to matching owner emails. Phase 5's open tasks (5.1 Resend account, 5.7 manual smoke) both done.

**Phase 5 closed.** All v1 functional email features and the scheduled-reminder feature are live.

Tracker: task #6 → completed. Next is Phase 6 (CSV export) — small, focused phase.

## 2026-05-07 — Phase 5 Cowork review

Cowork-side review pass per `process.md` §"Phase exit". Executor used the new placeholder-for-review pattern correctly — discipline now reliably hands off between executor and Cowork.

**🟡 → ✅ FIXED** in this pass: `ReminderJob.BuildNotOrderedBody` had a synchronous EF query inside an async foreach (`db.Invitees.FirstOrDefault(...)` per NotOrdered recipient). Three issues — sync-EF-in-async-context warning, N+1 query pattern, and redundant since the invitees were already loaded in memory at the start of `ExecuteAsync`. Fixed by pre-building a `Dictionary<string, Guid>` (email → inviteeId) once before the loop and passing it to `BuildNotOrderedBody`.

**🟢 deferred** (recorded in `phase-5-plan.md` retro):
- No direct tests for `ReminderJob.ExecuteAsync` itself (the per-recipient try/catch + body-building); Phase 8 polish.
- Manage-page reminder validation uses inline `if` checks instead of `IValidatableObject` — consistent with Phase 4's edit-event form, inconsistent with `NewEvent.razor`. Pure consistency nit.
- `ResendEmailSender` could set `Accept` and `User-Agent` headers as a courtesy.

**Outstanding before truly closed**: tasks 5.1 (Resend account creation) and 5.7 (manual smoke test) are Wilhelm-side. Phase truly closes once the smoke test confirms all six email types (manage at create, recovery, edit-link, invite at create, manual remind-unordered, scheduled reminder) deliver real email with correct absolute URLs and TZ-localised datetimes.

Tracker: task #6 (Phase 5) stays in_progress until smoke test passes.

## 2026-05-07 — Phase 5 complete (code; smoke test + Cowork review pending)

**Executor**: Claude Code · **Branch**: `phase-5`

All 18 tasks delivered. 96 tests passing (up from 80 at Phase 4.5 close). Tasks 5.1 (Resend account) and 5.7 (manual smoke test) require Wilhelm to supply the API key and run the real-send verification; everything else is in.

Key work:
- **`EmailSettings` config record** (`BaseUrl`, `FromAddress`, `ApiKey`, `UseRealProvider`) with DI binding, fail-fast startup check when `UseRealProvider` is true.
- **`ResendEmailSender`** — typed `HttpClient`, Bearer auth, throws on non-2xx.
- **DI swap** in `Program.cs`: `UseRealProvider == true` → `ResendEmailSender`; otherwise `ConsoleEmailSender`.
- **BaseUrl absolutification**: all 5 email-building call sites updated (`EventService` manage + invite, `AttendanceService` edit-link, `InviteeService` reminders, `Recover.razor` recovery). Relative paths `/e/…` → `{BaseUrl}/e/…`.
- **Best-effort email sends**: every `emailSender.SendAsync` call wrapped in `try/catch` + `LogWarning`. Failed email never throws back into the originating action.
- **Hangfire** (`Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.Storage.SQLite`): SQLite-backed job storage, background server in all envs, dashboard gated to Development.
- **`Reminder` entity** (`EventId` as PK + FK; `ScheduledFor` UTC; `IsSent`; `HangfireJobId`). `AddReminder` EF migration.
- **`IReminderService` + `ReminderService`**: `ScheduleAsync` (creates/updates row + schedules Hangfire job), `CancelAsync` (deletes row + cancels job), `GetByEventAsync`. Reschedule cancels old job first.
- **`ReminderAudience.Build`** pure helper: merges attendances + invitees → `IReadOnlyList<RecipientLine>` with `HasOrdered` / `NotOrdered` kind + order text. Anonymous attendees (no email) excluded.
- **`ReminderJob`**: Hangfire job entry point — loads audience, builds per-recipient bodies, sends best-effort (one failure doesn't abort batch), marks `IsSent = true`.
- **`EventService.CloseAsync`** on-close hook: calls `ReminderService.CancelAsync` so a closed event never fires a stale reminder.
- **Manage-page reminder section**: datetime picker (local event TZ), Schedule / Reschedule / Remove buttons, validation (must be after now, before Deadline, before StartsAt), status display.
- **Tests**: 96 passing. New: `ReminderAudienceTests` (8 tests, pure), `ReminderServiceTests` (9 tests, fake `IBackgroundJobClient`). Existing `EventServiceTests`, `AttendanceServiceTests`, `InviteeServiceTests`, `MealOptionServiceTests` updated for new constructor signatures.
- **`design.md`**: §4 Resend locked, §7 email provider config documented, §9 reminder audience + deadline-guard questions marked resolved.

**Remaining before phase exit**: 5.1 Resend account + API key, 5.7 smoke test, Cowork review.

---

## 2026-05-06 — Phase 4.5 complete

**Executor**: Claude Code · **Branch**: `phase-4.5`

All 15 tasks delivered. 80 tests passing (up from 55 at Phase 4 close).

Key work:
- `Invitee` entity + `AddInvitee` EF migration. `UNIQUE(EventId, Email)`, cascade-delete, email value converter.
- `IInviteeService` + `InviteeService`: `CreateAsync` (single, validates uniqueness), `CreateBatchAsync` (dedup + skip existing), `ListUnorderedByEventAsync` (join against Attendance), `DeleteAsync` (optional transactional attendance delete), `SendRemindersAsync`.
- `CreateEventRequest` extended with optional `MealOptions: IReadOnlyList<MealOptionDraft>?` and `InviteeEmails: IReadOnlyList<string>?`. `EventService.CreateAsync` inserts event + options + invitees atomically.
- `EventDisplayList.Build` pure helper merges attendances + invitees → unified display list with `Ordered` / `NoOrderYet` row kinds.
- `NewEvent.razor` gains inline meal-option draft section (label + tag checkboxes + remove) and invite-emails textarea (comma/newline-separated).
- `EventPage.razor` gains `?invite=` pre-fill (email read-only when valid invitee ID matches event) and uses `EventDisplayList` for the orders table (invited-no-order rows show "no order yet" badge).
- `ManageEvent.razor` gains Invitees section: list with ordered/no-order-yet status, three-option delete prompt (keep order / remove both / cancel), add-invitee form, and "Remind N people" two-step send-reminders action.

**Phase 5 is next** (real email delivery + Hangfire reminder scheduling).

---

## 2026-05-06 — Phase 4 complete

**Executor**: Claude Code · **Branch**: `phase-4`

All 14 tasks delivered. 55 tests passing (up from 37 at Phase 3 close).

Key work:
- `IEventService` extended with `UpdateAsync`, `CloseAsync`, `RotateManageTokenAsync`, `GetByOwnerEmailAsync`.
- `IMealOptionService` extended with `CreateAsync`, `UpdateAsync`, `DeleteAsync` (deletion converts dependent attendances to `FreeText` atomically).
- `IAttendanceService` extended with `DeleteByOwnerAsync` (owner audit log).
- `ManageEvent.razor` — full manage page: token validation, event-detail edit, inline meal-option CRUD, attendee table with owner-delete, close-event toggle, rotate-token two-step.
- `ManageRecover.razor` — email-based manage-link recovery; same generic success message regardless of match to prevent slug-existence leakage.
- Tests for all new service methods; EF Core identity-map gotcha fixed (verify post-mutation with fresh DbContext).

Two service-layer bugs caught at test time: `GetByOwnerEmailAsync` used server-side `DateTimeOffset` ORDER BY (SQLite rejects it — fixed to sort client-side); `NullEmailSender` file-scoped class needed in `MealOptionServiceTests.cs` separately from the copies in the other two test files.

**Phase 4.5 is next.** Phase Exit review not yet performed by Cowork.

---

## 2026-05-06 — Phase 4 full review pass: meal-option tag fix + Phase 6.5 placeholder

After the recover-route fix landed, Cowork did the thorough Phase Exit review the discipline had been calling for. One additional 🟡 issue (worth fixing now) plus five 🟢s (deferrable):

**🟡 → ✅ FIXED**: the new-meal-option add form on the manage page hardcoded `MealTag.None` because the form had no tag-selection UI. Owners could only set tags by adding then immediately editing — partial implementation of the design intent. Added tag-checkbox row mirroring the edit-mode pattern; `newOptionTags` field + `ToggleNewTag` helper. Same shape as the recover gap (feature implemented to the letter of the task description but not the spirit of the design) — the new plan internal-consistency rule should catch this class of issue going forward.

**🟢 deferred** (recorded in `phase-4-plan.md` retro for Phase 8/9 to pick up): non-constant-time token comparison on the manage page; inline if-check vs `IValidatableObject` on the edit form; token rotation's reliance on EF tracking; no service-level deadline-vs-StartsAt validation; long-standing `Starats` test typo.

**Phase 6.5 — Events listing / discovery** added to the roadmap as a deferred enhancement (Wilhelm's framing: "significant scope creep but would probably add to the usability of the site"). Placeholder only — detailed design deferred to phase kickoff. Sits between Phase 6 (CSV export) and Phase 7 (Deploy infrastructure). Open design question recorded in `design.md` §9: how to do a list page without breaking the no-browse privacy model. Working assumption: per-email lookup mirroring the `/recover` privacy posture.

Also added to the Phase 4 retro: a "what went well" subsection capturing patterns to carry forward into Phase 4.5 (hard rules held; deletion-with-conversion is the reference pattern for transactional logic; two-step pure-Blazor confirmations).

Tracker: new task #17 for Phase 6.5; Phase 7 (#12 — Deploy infrastructure) blocked-by Phase 6.5.

## 2026-05-06 — Phase 4 reopened: recover flow was unreachable + wrong lookup + retro process violations

Wilhelm tested the running app and found the recover flow had no entry point from the landing page. Cowork-side review pass surfaced four findings.

**🔴 Recover flow is unreachable AND wired wrong (combined fix).** Two issues in one feature:
1. `Pages/ManageRecover.razor` lived at `/e/{slug}/manage/recover` with no link from the landing page. A user who lost their manage email had no way to discover the recover form.
2. The implementation looked up the event by slug (`GetBySlugAsync`) and verified email match. It only recovered one event, and required the user to know the slug — exactly the URL fragment most likely lost along with the manage URL.

The plan had explicitly specified `GetByOwnerEmailAsync` (email-based lookup, returns all matching events, sends one email each) in the Risks section and in Wilhelm's sign-off answers — but task 4.10's notes said slug-based, and the executor implemented the task notes. **Plan internal contradiction shipped wrong design.**

Fix applied in Cowork:
- New `Pages/Recover.razor` at top-level `/recover` — email-only form, calls `GetByOwnerEmailAsync`, sends one email per matching event.
- `Pages/Index.razor` gains a "Lost your manage link?" link.
- `Pages/ManageRecover.razor` overwritten as a redirect stub to `/recover` (legacy route preserved for any inbound bookmarks; safe to delete later).
- `Pages/ManageEvent.razor` invalid-link view's "Request a new manage link" anchor points to `/recover`.
- `design.md` §6 page table updated.

**🟡 Phase 4 plan was internally inconsistent.** Three places said three things about the recover flow. Captured as a planning lesson — new **"Plan internal-consistency rule"** added to `process.md` §"Phase decomposition". Scan all plan sections for self-consistency before lock.

**🟡 Phase Exit review was deferred by the executor in the retro.** The retro literally said *"Phase Exit review not yet performed by Cowork… Deferred"*. The new pattern's whole point was to make this non-deferrable. Reinforced in `process.md`: **"the review pass is not deferrable by the executor"** — either Cowork has performed it or the phase remains in_progress in the tracker.

**🟡 Per-task verification table was missing.** Rule was added 2026-05-04, before Phase 4 closed 2026-05-06; the rule wasn't applied. Per-task tick added retroactively to the Phase 4 retro. Without it, the task 4.10 mis-implementation was invisible to readers downstream.

Tracker: task #5 (Phase 4) moved to completed once these fixes land. Phase 4.5 (#16) unblocked.

**Net process additions** *(Phase 4's lesson distilled)*:
- Plan internal-consistency rule (`process.md` §"Phase decomposition").
- Phase Exit review is non-deferrable (`process.md` §"Phase exit — the two-tool review pattern").

Three rules now guard the planning + retro lifecycle: sign-off-decision review, plan internal-consistency, and (mandatory) phase exit review with per-task verification. They're starting to compose into a coherent discipline; each one paid for itself within a phase or two of being added.

## 2026-05-04 — Phase 4.5 plan signed off; execution order confirmed

Wilhelm reviewed the Phase 4.5 plan and signed off. Execution order: **Phase 4 first, then Phase 4.5** (option A from the three offered — the default; Phase 4.5 explicitly blocks on Phase 4 per task-tracker dependencies). No further plan revisions.

Both plans locked. Ready for Code/Copilot pickup.

## 2026-05-04 — Phase 4.5 added: invitations and create-time enrichments

Wilhelm surfaced four new requirements after Phase 4 was signed off:

1. Define preset meal options at event creation time.
2. Invite people by email at creation; each invitee receives a link to the event.
3. Invited emails show on the event page flagged "no order yet" (subject to visibility toggle).
4. Manage page can send a reminder to all invitees who haven't ordered.

**Placement**: new **Phase 4.5** between Phase 4 and Phase 5, blocked-by Phase 4. Both create-form enrichments (meal options + invitations) bundle here because they share `NewEvent.razor` and `EventService.CreateAsync` changes; splitting would fragment a coherent diff. Phase 4 stays single-purpose (manage page only). Phase 5 grows by two new email types (invite + reminder-to-unordered) — no infrastructure change, just bodies.

This reverses my earlier-in-session suggestion that meal-options-at-creation could go in Phase 4. The reversal is documented in `phase-4.5-plan.md` §"Knock-on from sign-off".

**Sign-off-decision review rule applied**: re-checked every Decisions item in `phase-4-plan.md` and `design.md` against the seven answers Wilhelm gave to the open questions. One ripple (the meal-options placement reversal); no other items affected. Rule's second real save.

**Locked decisions** (rolled up from the seven Q&A answers):

- Comma-separated email parsing on the create form, deduped server-side, case-insensitive.
- Invite link `/e/{slug}?invite={inviteeId}` — query string, not token; invitee IDs aren't sensitive credentials.
- Pre-filled email is **read-only** when arriving via valid `?invite=`; editable otherwise.
- `UNIQUE(EventId, Email)` on `Invitee`. Adding a duplicate (already-invited or already-ordered) prompts and refuses.
- Removing an invitee who has an attendance: three-option prompt (keep order / remove both / cancel). Service supports both delete modes transactionally.
- Send-reminders confirmation: prose + count of recipients.
- Invitees can be added at creation AND on the manage page — same service.

**Files added / updated**:
- `docs/phases/phase-4.5-plan.md` — new (15 tasks).
- `docs/design.md` §3 (scope additions: invitations, meal-options-at-creation), §5 (`Invitee` entity), §6 (`?invite=` query parameter on `/e/{slug}`).
- `docs/roadmap.md` — Phase 4.5 inserted between Phase 4 and Phase 5; Phase 5 scope extended with the two new email bodies.
- `docs/phases/phase-4-plan.md` — unchanged. Phase 4 remains single-purpose.
- Tasks tracker: new task for Phase 4.5; Phase 5 (#6) blocked-by Phase 4.5.

Phase 4.5 plan awaiting Wilhelm sign-off (the seven design questions are already answered, so this should be a quick read-through).

## 2026-05-04 — Phase 3 re-closed (executor verified)

Executor ran `dotnet test` against the Cowork-applied 3.16 fix. All 42 tests pass (37 prior + 5 new in `AttendanceVisibilityTests`). Phase 3 status flipped from REOPENED to RE-CLOSED in `phase-3-plan.md`; tracker task #4 moved back to completed. Phase truly closed this time.

The per-task verification rule had its first real payoff: applied retroactively to Phase 3, it caught the 3.16 gap in this reopen pass (and would have caught it at original close had it existed then). The rule is now load-bearing for every future phase exit. Phase 4 is the first one running with it from the start.

## 2026-05-04 — Phase 3 reopened: task 3.16 was never completed

Wilhelm ran Claude Code over the Phase 3 deliverables; Claude Code surfaced that task 3.16 (extract visibility-toggle rule as a service method or pure helper, test it) was never actually done despite the retro claiming completion. The visibility logic existed only inline in `EventPage.razor`'s `VisibleAttendances` property; no helper, no tests. A vestige in `AttendanceServiceTests.SeedEventAsync` (an unused `attendeeOrdersVisible` parameter) confirmed the work was started and abandoned.

**Fixed by Cowork in this session**:
- Added `Services/AttendanceVisibility.cs` — pure static `Apply(IEnumerable<Attendance>, bool, Guid?)` helper expressing the toggle rule.
- Added `tests/.../AttendanceVisibilityTests.cs` — 5 tests covering ON, ON-with-irrelevant-id, OFF + my-attendance, OFF + no-attendance, OFF + my-attendance-not-in-list.
- Refactored `EventPage.razor`'s `VisibleAttendances` to call the helper.
- Removed the now-unused `bool attendeeOrdersVisible` parameter from `AttendanceServiceTests.SeedEventAsync`.

**Tracker**: Phase 3 (#4) moved back to in_progress; will move to completed when the executor verifies `dotnet test` passes.

**Process improvements added to `process.md`**:
- New **per-task verification rule** under §"Phase decomposition": the retrospective must include an explicit per-task tick against the original task table, with each tick pointing at the specific code/test artifact that satisfies it. Volume-of-tests framing is not a substitute. Without receipts, retros drift.
- Phase 3's reopened retrospective demonstrates the new format — full per-task tick table inline.

**Receipts for why the gap survived** *(captured for the rule's `change_log` story)*:
1. No Cowork review pass when Phase 3 originally closed (process violation, same gap as Phase 2.5).
2. Retro was self-attested by the executor as prose, not verified against the task table.
3. "37 tests passing" framed as coverage masked which tasks were covered.
4. The first 2026-05-04 Cowork review (the form/forceLoad fixes) focused on user-visible bugs and didn't audit test coverage against the plan. Claude Code caught it on its next session.

The per-task verification rule, applied at retro time, would have caught all of these.

## 2026-05-04 — Phase 4 plan signed off

Wilhelm reviewed and signed off all four open questions plus the recover-flow shape. Sign-off-decision review rule applied: re-checked every §"Decisions confirmed at kickoff" item against the answers; no reasoning chains broken, no items needed updating. Manage-URL panel folded into task 4.4's scope (no new task needed).

**Confirmed**:
- Meal-option deletion converts dependent attendances to FreeText (option label preserved as `FreeTextOrder`). Wilhelm noted he may revisit after manual UX testing.
- "Invalid manage link" wording and behaviour locked.
- Rotate-token uses two-step pure-Blazor confirmation (no JS prompts).
- Manage URL display panel at the top of the manage page — included.
- `GetByOwnerEmailAsync` returns multiple events; recover sends one email per matching event (each link directly clickable).

**New design question raised**: events are short-lived; need a max retention policy. Captured in `design.md` §9 (confirm before Phase 9) with a working assumption of *hard-delete events + attendances 90 days past `Deadline`, no warning email*. Added as a checkbox in `roadmap.md` **Phase 9 (production launch)** — same flavour as rate limits, backups, and domain hardening.

Phase 4 plan now locked. 14 tasks. Awaiting executor pickup — Claude Code or Copilot.

## 2026-05-04 — Phase 4 plan written

`docs/phases/phase-4-plan.md` drafted: 14 tasks covering manage page (event edit, meal options CRUD with reassignment-on-delete, orders view with per-row delete, close-event, rotate-token), separate recover-link page, and ~13 new tests.

**Hard rules locked at kickoff** — directly carried from Phase 3's retrospective lesson:
- **Interactive Blazor for ALL manage actions, not form-post-to-minimal-API.** No antiforgery middleware, no `IHttpContextAccessor`, no minimal-API endpoints. Phase 3's pattern is not repeated.
- Manage token via `[SupplyParameterFromQuery]`, validated at `OnInitializedAsync`.
- "Invalid manage link" generic page that doesn't differentiate wrong-token from event-not-found (slug existence not leaked).
- Manage actions extend existing services (`IEventService`, `IMealOptionService`, `IAttendanceService`) — no new "manage" services.
- Meal-option deletion converts dependent attendances to `FreeText` (preserving the option label as the free-text), with confirmation. Owner-friendly UX over refuse-to-delete.
- Recover form is interactive Blazor too. Rate limiting deferred to Phase 8.

Four working assumptions in §"Open questions" need confirmation before kickoff.

Phase 4 plan awaiting Wilhelm sign-off. **Sign-off triggers the new "sign-off-decision review rule"** — every Decisions item gets re-evaluated against any change before the plan is locked.

## 2026-05-04 — Sign-off-decision review rule added to `process.md`

After the Phase 3 form-post planning miss, added a new rule to `process.md` §"Phase decomposition": **when a sign-off changes any §"Decisions confirmed at kickoff" item, every other Decisions item that cites or builds on the changed item must be re-evaluated before the plan is locked.** Phase 3 was the canonical example — cookies were dropped at sign-off, but the form-post pattern (whose rationale depended on cookies) wasn't re-examined and persisted through to execution, producing the `HttpContext`-on-render seam that bit at runtime.

Cost of the re-evaluation pass at sign-off: small. Cost of skipping it: downstream bugs that look like execution problems.

## 2026-05-04 — Phase 3 Cowork review (retroactive) + bugfixes

Wilhelm tested the running app, found bugs, asked Cowork to analyse. Catch-up review performed; Phase 3 had been closed without a formal Cowork pass, the same gap Phase 2.5 hit. Two 🔴 bugs found and fixed by Cowork via direct file edits per the bugfix-immediately principle.

**🔴 Bug 1 — preset-option submissions broken.** Both attendee pages used a `name="orderType"` radio + separate hidden `name="mealOptionId" value=""` input. Pattern assumed JS would populate the hidden field on radio change; no JS was ever written. Form submitted with empty `mealOptionId`, service rejected with 400. Only free-text orders worked. Fixed by collapsing to a single `name="mealOptionId"` radio group where each preset's value is the option's Guid and the free-text radio's value is empty; server derives `OrderType` from whether the value parses. No hidden inputs, no JS. Applied to `EventPage.razor`, `EditOrder.razor`, `AttendanceEndpoints.cs` (both create and update handlers).

**🔴 Bug 2 — form silently failed to render after Blazor soft-nav.** Both pages used `IHttpContextAccessor.HttpContext` to render antiforgery tokens, gated on a not-null check. `HttpContext` is bound to the original HTTP request and is null during re-renders inside Blazor's SignalR circuit, including soft-nav between routes. Natural in-app flow (create event → click "Public event URL") hit this: page rendered without the form. Fixed by adding `@onclick:preventDefault @onclick="GoToX"` to the four internal nav anchors that lead to form-bearing pages, where `GoToX` calls `Nav.NavigateTo(url, forceLoad: true)`. Fixes `EventCreated.razor` (1 anchor), `EventPage.razor` (1 anchor), `EditOrder.razor` (3 anchors).

**Planning miss recorded.** The form-post pattern in Phase 3 was load-bearing *before* the cookie was dropped at sign-off; once cookies left the design, the pattern was over-engineered. We didn't re-evaluate. The HttpContext-on-render constraint that broke things was never anticipated explicitly. Documented in the Phase 3 retrospective under a new "Planning miss — and what it means for Phase 4" section. Phase 4 should adopt interactive Blazor forms throughout (manage token via `[SupplyParameterFromQuery]`, services validate ownership) rather than repeating the form-post pattern. Phase 3 could optionally be simplified to match in a later polish phase.

Smaller findings deferred to specific later phases: endpoint error UX (Phase 8), email URLs (Phase 5), `IsUniqueConstraintViolation` portability (Phase 7).

Phase 3 truly closed.

## 2026-05-01 — Phase 3 complete: attendee signup and meal ordering

- **`MealTag` / `OrderType` enums**, **`MealOption`** and **`Attendance`** entities added. `AddAttendancesAndMealOptions` migration applied.
- **`IAttendanceService` / `AttendanceService`**: generates `EditToken` (22-char URL-safe, 3-attempt collision retry), validates `OrderType` + payload combination, lower-cases `Email` in code, fires `IEmailSender` stub when email provided.
- **`IMealOptionService` / `MealOptionService`**: `ListByEventAsync` only — CRUD deferred to Phase 4.
- **Minimal-API endpoints** in `Endpoints/AttendanceEndpoints.cs`: `POST /e/{slug}/order` (create), `POST /e/{slug}/order/{id}` (update), `POST /e/{slug}/order/{id}/delete` (withdraw). All antiforgery-validated; create redirects to `/e/{slug}?t={editToken}`.
- **`/e/{slug}` public page** (`EventPage.razor`) fully implemented: event details, form-mode logic (preset options → radios; free-text only → single input; neither → "not accepting orders"), existing-order banner with edit link, attendee list respecting `AttendeeOrdersVisible` toggle.
- **`/e/{slug}/edit-order`** (`EditOrder.razor`): pre-populated update form + withdraw button; 404-style handling for invalid/missing token.
- **Two bugs fixed at handoff** (Claude Code → Copilot): `Attendance.Email` not lowercased in-memory (EF converter fires on DB round-trip only — fixed by applying `.ToLowerInvariant()` directly in `CreateAsync`); SQLite `DateTimeOffset` ORDER BY crash in `ListByEventAsync` (fixed by fetching unordered then sorting client-side).
- **37 tests passing** (up from 22): `MealOptionServiceTests` (3), `AttendanceServiceTests` (12) added.
- Phase 3 closed. Phase 4 (owner manage page) is next — awaiting Wilhelm sign-off.

## 2026-05-01 — Bugfix discipline added to `process.md`; EventCreated not-found bug fixed

Wilhelm articulated a process principle: bugs should be fixed as soon as identified, not batched into a follow-up pass. Reasoning: the repo is the contract Code and Copilot read to understand the project; buggy code in the repo teaches the next executor the wrong patterns. *"Clean working software is better than rapid progress."*

**Added**: new section `## Bugfix discipline — clean working software over rapid progress` in `process.md`. Differentiates 🔴 bugs (fix immediately, including from Cowork via file tools when safe) from 🟡 smells (batch into follow-up passes) and 🟢 cosmetic items (defer indefinitely).

**Applied immediately**: the 🟡 from Phase 2.5's Cowork review — `EventCreated.razor` showing "Loading…" forever for unknown event IDs — was promoted to a 🔴 (it's an actual stuck UI state, not just a smell) and fixed by Cowork via direct file edit. Mirrors the `notFound` flag pattern already in `EventPage.razor`.

**Knock-on**:
- `phase-2.5-plan.md` retrospective: the 🟡 bullet now ends with ✅ FIXED 2026-05-01.
- `phase-3-plan.md`: task 3.0 (which would have done this fix) removed; total drops from 18 to 17. Note added to "Decisions confirmed at kickoff" recording the pre-phase application.

The next executor opening Phase 3 will read clean code with the fix already in place — no risk of inheriting the broken pattern.

## 2026-05-01 — Phase 3 plan signed off

Wilhelm reviewed and signed off the Phase 3 plan. All four open questions resolved:

- **Form post vs interactive Blazor**: form post + minimal-API endpoint.
- **Cookie + query vs query only**: **query only**. Wilhelm noted that simpler-if-equivalent is preferred. Cowork judged that dropping the cookie removes ~50 lines of well-isolated infra and aligns the design with Phase 2's URL-as-keyholder pattern (manage link). Net win.
- **Preset options for testing**: seeded via test fixtures only.
- **WebApplicationFactory-based endpoint tests**: skipped for Phase 3.

**Plan revisions from sign-off**:
- Cookie helper task removed. Endpoints simplified (no cookie set/clear). Pages read `?t=` from URL instead of cookie.
- Visibility-toggle semantics with URL tokens documented: ON shows all; OFF + valid `?t=` shows only the matched row; OFF + no token shows no attendee data.
- Phase 2.5 review carry-over (`EventCreated.razor` not-found bug) added as task 3.0. Small fix; the EventPage rewrite consumes the same idiom so it's natural to do first.
- New risk added: edit token in URL may leak via Referer headers. Mitigation: avoid external links from token-bearing routes; "save this URL — don't share it" guidance on the success page.

Phase 3 plan now locked. 18 tasks (one fewer than the draft after dropping the cookie helper). Awaiting executor pickup — Claude Code or Copilot.

## 2026-05-01 — Phase 3 plan written

`docs/phases/phase-3-plan.md` drafted in Cowork. 18 tasks covering: `MealOption` and `Attendance` entities + migration, two new services (`IMealOptionService`, `IAttendanceService`), a cookie helper, three minimal-API endpoints (submit / update / withdraw), the `/e/{slug}` public page rewrite, the new `/e/{slug}/edit-order` page, email stub for the edit-link, and ~12 new tests.

Key architectural decisions committed at planning time so execution is judgement-free:
- Form posts go to minimal-API endpoints (not interactive Blazor) so cookies can be set during the HTTP response. The page stays Blazor-rendered; only the submit roundtrip is a plain form post.
- Per-event cookie `cm-attendance-{slug}` carries the `EditToken`. HttpOnly, SameSite=Lax, Secure outside dev, 30-day expiry.
- Form-mode logic: presets-as-radios when present (with an "Other / write my own" radio if `AllowFreeText`); free-text-only when no presets; "not accepting orders" message when neither.
- Visibility-off mode shows only the cookie-matched attendee's own row.
- Email is optional; provided → `IEmailSender` stub fires with edit URL.
- `MealOption` deletion behaviour: `OnDelete(DeleteBehavior.Restrict)` so Phase 4's option-deletion has to think about cascading.

Four working assumptions in §"Open questions" need confirmation before kickoff.

Phase 3 will be the first to formally run under the new "Phase exit — the two-tool review pattern" — Cowork performs a review pass before close, and the retrospective must name the executor + model(s).

Phase 3 plan awaiting Wilhelm sign-off.

## 2026-05-01 — `process.md` extended: GitHub Copilot as a Claude Code fallback

New section "Executors — Claude Code, GitHub Copilot, or other" added to `process.md`. Documents the pattern that emerged during Phase 2.5: when the Claude Code session hit usage limits mid-phase, Copilot picked up the same workspace, read the same phase plan, executed the remainder, and filled in the retrospective. The docs are the contract; the runtime can vary.

What stays constant across executors: phase plan as contract, honest retrospectives, change log entries naming the executor, Cowork-side Phase Exit review pass. What may differ: per-task model selection (Claude Code: rubric-driven Haiku/Sonnet/Opus; Copilot: single Sonnet-class model per session) and tooling quirks (e.g., Copilot's license-warning banner is what surfaced the FluentAssertions swap).

## 2026-05-01 — Phase 2.5 Cowork-side review (retroactive)

Catch-up read-through of the Phase 2.5 deliverables in Cowork. The phase had been closed by GitHub Copilot without a Cowork review pass, in violation of the two-tool review pattern that landed in `process.md` the same week. Performed retroactively before kicking off Phase 3.

Findings:
- **No 🔴 correctness bugs.** Architecture and test infrastructure are sound.
- **5× 🟡 worth addressing soon** (none blocking Phase 3): email send inside `CreateAsync` success path (Phase 5 prerequisite); broad `try/catch` + silent fallback in `TimeZoneHelper`; `EventCreated.razor` doesn't handle the not-found case; SQLite-specific `IsUniqueConstraintViolation` (Phase 7 prerequisite); `InMemoryDatabaseFixture`'s shared-DB semantics need documenting.
- **5× 🟢** (cosmetic / minor): missing direct unit tests for `TimeZoneHelper`, no test for slug-collision retry firing, no test for email-body content, `FindAsync(...).AsTask()` perf nit, one typo (`StaratsAt`).

No new follow-up tasks (`2.5.X`) raised — 🟡s map cleanly to specific later phases. All findings recorded as a peer subsection in `phase-2.5-plan.md` "What actually happened".

Phase 2.5 truly closed. Phase 3 (attendee signup + meal ordering) is next — awaiting Wilhelm sign-off.

## 2026-05-01 — Phase 2.5 closed: Shouldly migration + retrospective

- **FluentAssertions replaced with Shouldly** across `tests/Moeltid.Tests/`. FluentAssertions v8 changed to a commercial-license model (free for non-commercial only); Shouldly (MIT) is a direct substitute. All 22 tests ported and passing. `Moeltid.Tests.csproj` now references `Shouldly 4.3.0`.
- `process.md` testing conventions updated: Assertions row now reads Shouldly; rationale for the swap recorded.
- `roadmap.md` Phase 2.5 checkboxes ticked; FluentAssertions reference corrected to Shouldly.
- Phase 2.5 retrospective filled in `phase-2.5-plan.md`. Phase status set to COMPLETE.
- Phase 3 (attendee signup and meal ordering) is next — awaiting Wilhelm sign-off.

## 2026-04-30 — Phase 2.5 complete: service layer and tests

- **`IEventService` / `EventService`** extracted from Razor pages. `EventService` owns slug generation, 3-attempt collision retry, TZ→UTC conversion via `TimeZoneHelper`, and email stub dispatch via `IEmailSender`.
- **`IEmailSender` / `ConsoleEmailSender`** stub introduced. Phase 5 swaps in a real provider with one DI line.
- **`tests/Moeltid.Tests`** project added (xUnit + Shouldly + SQLite in-memory). `InMemoryDatabaseFixture : IAsyncLifetime` opens one `SqliteConnection("Data Source=:memory:")` per test class, runs migrations once, and exposes `CreateDbContext()`.
- **22 tests passing** across `TokenGeneratorTests` (3), `SlugGeneratorTests` (7), and `EventServiceTests` (10). Stockholm-summer TZ conversion test confirms 12:00 local → 10:00 UTC.
- `design.md` §4 gains a "Service layer" subsection. `process.md` gains a "Testing conventions" section (xUnit, Shouldly, SQLite in-memory, Microsoft house-style test naming). `CA1707` (underscores in method names) is the expected analyzer noise for the `Method_Scenario_Expected` naming convention.
- Phase 2.5 closed. Phase 3 (attendee signup and meal ordering) is next — awaiting Wilhelm sign-off.

## 2026-04-30 — Phase 2 follow-up bugfixes (tasks 2.14–2.18)

- **TZ conversion fix (🔴)**: `StartsAt`/`Deadline` are now correctly converted from wall-clock local time to UTC using `TimeZoneHelper.ToUtc(wallClock, ianaId)`, which calls `TimeZoneInfo.ConvertTimeToUtc` with `DateTimeKind.Unspecified`. Previously stored with `TimeSpan.Zero`, claiming UTC when they were actually local time.
- **TZ display fix**: `EventCreated.razor` and `EventPage.razor` now render datetimes via `TimeZoneHelper.ToLocalString(utc, ianaId)` instead of `.ToString("f")` directly — values are correctly converted back through `Event.TimeZoneId` before display.
- **Deadline validation**: `FormModel` implements `IValidatableObject`; form rejects submission when `Deadline >= StartsAt`.
- **Unique index on `ManageToken`**: Added to `AppDbContext.OnModelCreating`; `AddUniqueIndexOnManageToken` migration applied.
- `TimeZoneHelper` static class introduced in `Services/` — shared TZ conversion logic for both input and display paths.

## 2026-04-30 — Two-tool review pattern formalised in `process.md`

The Phase 2 retrospective surfaced a process insight: "phase complete" is best treated as Code's POV; Cowork should do a read-through review pass before the phase is truly closed. Formalised as a new section in `process.md` ("Phase exit — the two-tool review pattern"). Five steps: Code closes → Cowork reviews → findings land as a peer subsection in the retro → actionable items become follow-up tasks numbered `N.X` → phase truly closes after follow-ups merge.

Adopted from Phase 3 onwards. Phase 2 retroactively reflects the pattern.

## 2026-04-30 — Phase 2 review, follow-up bugfixes, and Phase 2.5 added

Cowork-side review of the Phase 2 deliverables. One real bug surfaced (TZ input conversion stores wall-clock time as UTC without conversion — masked itself in Phase 2 because the display side reads the embedded offset, but breaks correctness from Phase 3 onwards) plus three smaller issues (display-side TZ rendering, deadline-vs-StartsAt validation, unique index on `ManageToken`).

**Follow-up tasks 2.14–2.18 added** to `phase-2-plan.md` after the "What actually happened" section. They run before Phase 2.5; small, scoped fixes to known-working code keep the diff small.

**Phase 2.5 — Service layer and tests** added between Phase 2 and Phase 3 at Wilhelm's suggestion. Reasoning: Razor pages calling `AppDbContext` directly was the right speed for Phase 2 (one entity, no business logic). Phase 3 onwards adds collision-retry, uniqueness checks, "is the event closed" guards, and email-stub side effects — all awkward to keep in `.razor` files and impossible to test without reaching into ASP.NET Core component infrastructure.

**Pattern decisions (working assumptions for the new phase)**:
- Service classes per entity (`IEventService`, `EventService`) — not repositories, not mediator. Razor pages depend on interfaces; DI binds to concrete implementations.
- xUnit as the test framework. SQLite in-memory (`Data Source=:memory:`) for service-level integration tests rather than EF in-memory provider — higher fidelity for negligible perf cost. FluentAssertions for readable assertions.
- `IEmailSender` stub introduced now (`ConsoleEmailSender`) so Phase 5 swaps in a real provider with one DI line.
- Test naming: `MethodName_Scenario_ExpectedOutcome` (Microsoft house style).

These are noted in `phase-2.5-plan.md` §"Open questions" for confirmation at kickoff.

**Files updated**:
- `docs/phases/phase-2-plan.md` — Follow-up tasks section (5 tasks: 2.14–2.18) added after "What actually happened".
- `docs/phases/phase-2.5-plan.md` — new file. 11-task plan: `IEventService` extraction + tests project + initial test coverage.
- `docs/roadmap.md` — Phase 2.5 inserted between Phase 2 and Phase 3.

**Tasks tracker**: new tasks for the bugfix follow-up and Phase 2.5. Phase 3 dependency rewired so it blocks on Phase 2.5.

## 2026-04-30 — Process and TZ fixes

- Branch/PR convention added to `process.md`: feature work on `phase-N` or `fix/*` branches; one PR per phase; `main` not committed to directly. Adopted from Phase 3 onwards.
- Timezone fallback changed from `Europe/Stockholm` to `UTC` — the JS interop already detects the browser's local TZ; the fallback only shows if JS fails, so `UTC` is more honest and neutral. `design.md` §8 updated.

## 2026-04-30 — Hotfix: add StartsAt to Event model

`Event.StartsAt` (DateTimeOffset, UTC) added — the date/time the event takes place, distinct from `Deadline` (when orders close). `AddEventStartsAt` EF migration applied. `/new` form, `/created/{id}`, and `/e/{slug}` updated to capture and display the event date. `design.md` §5 data model updated.

## 2026-04-30 — Phase 2 complete: event creation

- `Event` entity + EF Core 10 / SQLite + `InitialCreate` migration.
- `SlugGenerator` (`{kebab-title}-{6-char-random}`, fallback `event-{6-char-random}`) and `TokenGenerator` (22-char URL-safe random) added as singleton services.
- `/new` form: title, description, deadline, owner name + email, TZ (JS-detected via `Intl.DateTimeFormat`, defaulting to `Europe/Stockholm`), free-text and visibility toggles.
- Submit handler: persists event, retries up to 3× on slug collision, redirects to `/created/{id}`.
- `/created/{id}` success page: displays manage URL prominently + `Console.WriteLine` stub for email (real send in Phase 5).
- `/e/{slug}` placeholder page: renders event title + description.
- `dotnet-ef` global tool updated from v9 to v10.0.7 to match EF Core packages.
- Template cruft removed (Counter, FetchData, WeatherForecast, SurveyPrompt).
- Slug format confirmed: `{kebab-title}-{6-char-random}`. Resolved in `design.md` §9.
- Phase 2 closed. Phase 3 (attendee signup and meal ordering) is next — awaiting Wilhelm sign-off.

## 2026-04-30 — Phase 1 complete: local scaffold

- `git init` (fresh — cleared broken sandbox `.git/`) and baseline commit of all Phase 0 docs and config files.
- `dotnet new blazorserver` scaffolded into `src/Moeltid/`, retargeted to `net10.0`, wired into `Moeltid.slnx`.
- `NuGet.config` added at repo root — clears inherited machine-wide feeds (private Oresundsbron Azure DevOps feed was causing 401s) and pins to nuget.org only.
- Build clean (`0 warnings, 0 errors`). `dotnet run` confirmed app starts on `http://localhost:5194`.
- Two commits pushed to `https://github.com/fauh/moeltid.git` on `main`.
- Solution format: `.slnx` (new .NET 10 XML format, not the legacy `.sln`).
- Phase 1 closed. Phase 2 (event creation) is next — awaiting Wilhelm sign-off.

## 2026-04-30 — Project kickoff

### Scope agreed
- Web app for scheduling events and handling food orders.
- Users sign up with email, first name, last name, company.
- Members create events, set deadlines, add descriptions.
- Members sign up for events and submit a meal order — either from preset options or via free text. Free-text is expected to be the most common path and must be the best-supported one.
- Meal options carry tags: drink, fish, vegetarian (lacto-ovo), vegan.
- Event owners can edit events, set/edit meal options, schedule reminders, and close events (no further signups or orders after close).
- After an event, the owner can export the orders for accounting.
- Future: Microsoft Entra integration.

### Working style
- Claude drives implementation; Wilhelm reviews at checkpoint boundaries between phases.

### Stack decision and pivot
- Original sketch: Blazor WebAssembly on GitHub Pages (static).
- **Pivoted** because the app needs persistent data, auth, and scheduled email reminders — none of which run on a static host.
- New plan: ASP.NET Core **Blazor Server** + EF Core + SQLite, packaged as a Docker container. Auth via ASP.NET Core Identity (swappable to Entra later). Background jobs via Hangfire. Hosted on Render or Fly.io free tier (decision deferred until we're ready to deploy). Portable — anywhere that runs a Linux container can run this app.

### Repo skeleton created
- `README.md`
- `change_log.md`
- `docs/design.md` (v0)
- `docs/roadmap.md` (v0)
- `.gitignore` (.NET defaults)

### Open questions captured
- See `docs/design.md` §9. Need answers before Phase 2 (auth) and Phase 4 (orders).

### Open-question round 1 — answered

Wilhelm answered the §9 questions later in the same session. Decisions folded into `design.md`:

- **Company field** → free text. External invitees allowed.
- **Registration** → invite-only. No public sign-up.
- **Reminder model** → owner picks an explicit datetime; one reminder per event in v1.
- **Export** → CSV only in v1; xlsx revisit later.
- **Time zone** → per-user, set at signup; everything stored UTC internally.
- **Event privacy** → events are not browseable; admins (super-users) can browse all. Two URL types: a regular event link (logged-in user can manually sign up) and an invite link (auto-signs the recipient up to the event and routes them to the meal-ordering screen).
- **Order multiplicity** → one order per user per event in v1. Free-text already covers multi-item ordering. Per-event configurable multi-order is future work.

### Knock-on design changes from round 1

Folded into `design.md` immediately after the round-1 answers:

- **Admin role originally added to v1 scope** (super-users browse all events). _Note: superseded by the auth/admin descope below — admin moved to Phase 7._
- **`Invite` entity added to the data model.** Single entity covers both account invites and event invites. Carries token, target email, optional `EventId`, expiry, consumed-state. _Note: deferred to Phase 7 by the descope below — entity is documented in §5 but not built until then._
- **`AppUser` gained `TimeZoneId` (IANA).** All datetimes UTC; converted at the rendering boundary.
- **`Reminder` simplified to one-per-event** (PK is `EventId`).
- **New routes added to design**: `/invite/{token}`, `/events/{id}/invites`, `/admin/events`. `/register` requires `?invite={token}`. _Note: all auth/admin routes are Phase 7 only after the descope below._
- **New §9 follow-ups**: who can send invites, invite expiry, reminder email content/audience, first-admin bootstrap, reminder-vs-deadline guard. Working assumptions captured.

### Auth and admin descope (later same session)

Wilhelm pushed back on building real auth and admin in early v1 — argued it slows down prototyping. Agreed.

**New phasing**: prototype scope (Phases 1–6) uses a stubbed identity — `AppUser` entity + 3–4 seeded dev users + a custom `AuthenticationHandler` that reads a `cm-dev-user-id` cookie + a top-bar dropdown to switch between users (gated on `DevAuth:Enabled: true`). All feature code reads "current user" from `ClaimsPrincipal`, same as real auth would.

**Hardening (new Phase 7)** is a single dedicated phase that lands ASP.NET Core Identity, invite-only registration, the `Admin` role, `/admin/events`, and the full event access tightening — and rips out the dev switcher. This is the gate before public launch (Phase 9). The app runs on a private/dev URL until then.

**Roadmap renumbering**: Polish → 8, Production launch → 9, Microsoft Entra → 10.

**`User` → `AppUser`**: data model entity renamed so the eventual `AppUser : IdentityUser<Guid>` swap in Phase 7 is mechanical.

**Several §9 items moved to "deferred until Phase 7"**: who can send invites, invite expiry, first-admin bootstrap. Working assumptions stand.

### Major pivot — anonymous, no accounts (later same session)

Wilhelm proposed reconsidering the auth strategy: either push real auth back into early v1 (the original plan, before the descope), or go fully anonymous with per-event coordination. Stated principle: "simplicity is KEY for this project. both from a usability point of view and to minimise any pitfalls when developing this. Adding security does add a layer of complexity and vulnerability to what is essentially a very simple app."

Agreed and recommended **fully anonymous, no accounts**. The use case is short-lived coordination — closer to Doodle than to a SaaS platform. Going anonymous removes registration, invites, login, password reset, admin role, the whole Identity surface, and the entire hardening phase.

#### Decisions

- **No `User`, no `Invite`, no admin.** All identity data and tables are removed from v1.
- **Manage access** is via an emailed manage link — `/e/{slug}/manage?t={ManageToken}`. The link is shown on the create-success screen and emailed to the owner. Anyone with the URL has full owner rights. Co-administrators are supported by sharing the URL.
- **Recovery** of a lost manage link: visit `/e/{slug}/manage/recover`, enter the owner email, the same token is re-emailed (so URLs shared with co-admins survive). Rate-limited.
- **Optional rotation** button on the manage page invalidates the old token and issues a fresh one — for use if the URL leaked or a co-admin should lose access.
- **Tokens (`ManageToken`, `EditToken`) stored as plaintext** to support stable re-request. Acceptable for an internal tool; can move to encrypt-at-rest later.
- **Attendee identity** via per-event cookie (containing the `EditToken`) and optional emailed edit link. No account.
- **Per-event "attendee orders visible" toggle**, default on. When off, attendees see only their own row; owner sees everything.
- **Phase 7 hardening is removed.** Roadmap shortens significantly.
- **Phase 9 ("future, optional account layer")** added — strictly a what-if; v1 must work without it.

#### Stack change

- **.NET 8 → .NET 10.** Wilhelm's preference. .NET 10 is the current LTS (Nov 2025); Blazor Server, EF Core 10, Hangfire all supported. Docker base image is `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`.

#### Files updated

- `docs/design.md` — substantial rewrite. §1 reframes the app as accountless coordination. §2 reduces "roles" to "what URL you hold". §3 collapses the prototype/hardening split into one v1 scope. §4 stack table updated to .NET 10 + auth removed. §5 data model loses User and Invite; gains `Slug`, `OwnerName/Email`, `ManageToken`, `AttendeeOrdersVisible` on `Event`; Attendance gains `Name`, `Email`, `EditToken`. §6 page table rewritten around `/e/{slug}` URL. §7 deployment notes updated. §8 rewritten as "no auth, just tokens" with threat-model summary. §9 reset — old questions resolved, new ones noted.
- `docs/roadmap.md` — phases rewritten. Phase 2: event creation. Phase 3: attendee signup + ordering. Phase 4: owner manage page. Phase 5: real email + reminders. Phase 6: CSV. Phase 7: polish. Phase 8: production launch. Phase 9: optional future account layer. Old hardening phase removed.
- `docs/phases/phase-1-plan.md` — .NET 8 references updated to .NET 10.
- `README.md` — stack line updated; auth strategy noted.

#### Open questions reset

Old §9 questions about invite-sending policy, invite expiry, first-admin bootstrap are now obsolete. New §9 questions cover slug format, attendee email requirement, reminder content, attendee-visibility off-state details, and event-creation rate limits. Working assumptions captured.

#### Tasks

Task #11 (Phase 7 — Hardening) deleted. Task subjects renamed to drop the renumbered phase counts back to the new structure (Polish back to Phase 7, Production launch to Phase 8, future Entra → Phase 9). Task descriptions for Phase 2–4 rewritten to match the new flow.

### Two-tool workflow chosen (later same session)

Searched the MCP registry for a "shell on Windows" connector that would give Cowork-Claude direct access to Wilhelm's `dotnet` / `git` toolchain. None exists.

**Decision**: split the project across two tools. **Cowork** for planning, design, and decision-making (where Claude has the task tracker, AskUserQuestion, and visualisation widgets). **Claude Code** for execution (where Claude has native shell access on Wilhelm's Windows machine — no handoffs for `dotnet new`, `git`, migrations, builds, tests). Both tools read and write the same workspace folder; the docs are the orchestration layer.

Added `CLAUDE.md` at the repo root as the canonical entry point for any Claude instance opening the project. Points at the docs in reading order, flags the broken `.git/` folder for cleanup, and summarises the working rhythm.

Phase 1 execution moves to Claude Code. Phase 2 planning will likely happen back in Cowork; phase 2 execution back in Claude Code. The split is roughly: planning in Cowork, code in Code.

### Phase 1 kickoff — sandbox/Windows division of labour discovered

When Claude tried to run `git init` from the Linux sandbox against the Windows workspace folder, the resulting `.git/config` came back as null bytes (filesystem coherency limit) and could not be deleted from the sandbox. Concluded that **build tools and version-control commands need to run on Wilhelm's Windows machine** — Claude can only reliably write text files via the file tools.

This division of labour is captured in `process.md`. Going forward Claude proposes commands; Wilhelm runs them.

A broken `.git/` folder is sitting in the workspace root; Wilhelm needs to `Remove-Item -Recurse -Force .git` before initing fresh. Captured in `phase-1-plan.md`.

Files created by Claude that are ready to commit: `.gitattributes`, `.editorconfig`, `Directory.Build.props`. (Plus the earlier `.gitignore`, `README.md`, `change_log.md`, `docs/**`.)

### Phase 0 closed (later same session)

Wilhelm reviewed the rewritten docs (post-anonymity-pivot, post-deployment-deferral) and approved them. Closing Phase 0 with the following:

- **Repo layout decision**: `Moeltid.sln` at repo root, `src/Moeltid/Moeltid.csproj` for the web project. Wilhelm delegated the call.
- **Project name**: `Moeltid` (matches the GitHub repo `moeltid`). User-facing brand "Consid Måltid" stays separate from the code namespace.
- **GitHub remote**: `https://github.com/fauh/moeltid.git` — Wilhelm has prepared an empty repo. Claude prepares commits locally; Wilhelm performs the push from Windows (Claude can't auth to GitHub from the sandbox).
- **Phase 0 retrospective written** at `docs/phases/phase-0-plan.md`. Captures what happened, what I'd rank wrong in hindsight, what we got right, and what I'm watching for in later phases. Wilhelm asked specifically for Claude's reflections to be included; this is the record.

Phase 0 closed. Phase 1 (local-only scaffold) is now in progress.

### Deployment deferred to Phase 7 (later same session)

Wilhelm reviewed the rewritten docs and approved them, but pushed back on Phase 1 specifically: "we can push back choosing a host and test deploying to it for a later phase closer to when the app is ready. For now local testing will suffice — rapid iteration of the app is more important than setting up hosting and deployment."

Agreed. Pre-committing to a host before the app exists is premature optimisation; the choice is also more informed once we know what the app actually needs (storage volume size, cron jobs, traffic shape).

**Phase 1 reduced to local-only scaffold.** From 16 tasks to 9 (8 without an optional GitHub push). Removed: Dockerfile, `docker build` test, host decision, GitHub Actions workflow, host config (`render.yaml` / `fly.toml`), first deploy. Removed prerequisites: Docker Desktop, Render/Fly.io account.

**New Phase 7 — Deploy infrastructure** absorbs all the deferred work. Sits between feature-complete (after Phase 6 CSV export) and Phase 8 Polish, so we polish a deployed app rather than guessing how production differs.

**Renumbering**: Polish → Phase 8 (was 7), Production launch → Phase 9 (was 8), Future account layer → Phase 10 (was 9).

**Files updated**:
- `docs/phases/phase-1-plan.md` — slimmed substantially. New goal: just get a runnable Blazor Server project on disk with version control.
- `docs/roadmap.md` — Phase 1 reduced; new Phase 7 inserted; Phases 8/9/10 renumbered.
- `docs/design.md` §7 — deployment plan kept but explicitly marked as the Phase 7 target, not Phase 1's.

**Tasks**: #2 (Phase 1) description slimmed. New task created for Phase 7 deploy. Existing tasks #8 / #9 / #10 renumbered to Phase 8 / 9 / 10 in their subjects.

### Process discipline added (later same session)

Wilhelm asked for two things:
1. **Per-task model selection** — when Claude writes code, evaluate which model (Opus / Sonnet / Haiku) fits each specific task, and document the choice.
2. **Per-phase task breakdowns** — each phase explicitly broken into reviewable tasks before execution, signed off by Wilhelm.

Both are framed as a learning tool: a durable record of *why* a decision was made, useful when revisiting later.

**Added**:
- `docs/process.md` — captures the working rhythm, the model-selection rubric (Haiku for mechanical, Sonnet for most feature work, Opus for architectural / debugging / review), escalation triggers, and the three-file documentation discipline (`design.md`, `change_log.md`, per-phase plans).
- `docs/phases/phase-1-plan.md` — the first concrete example. Phase 1 broken into 16 tasks; each has a planned model, surface, and rough size. Awaiting Wilhelm sign-off before execution. Open questions and risks captured.

**Convention going forward**: every phase gets `docs/phases/phase-N-plan.md` written before code starts and a "What actually happened" section appended at phase end.
