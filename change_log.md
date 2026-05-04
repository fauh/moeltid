# Change Log

Manual, human-curated record of decisions, design changes, and milestones for Consid Måltid. Newest entries first. Update this file whenever a meaningful decision is made or a phase boundary is crossed.

Format: one section per date (or per work session). Within a date, group entries under a short heading.

---

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
