# Roadmap

Phased plan from zero to deployed. Each phase ends with a review checkpoint — no rolling on to the next phase without sign-off. Tick boxes as we go.

Phases are sized so any one of them is a comfortable sitting. If a phase grows, split it.

---

## Phase 0 — Repo and docs (current)
- [x] README, change_log, design doc, roadmap, .gitignore.
- [x] First-pass design.md and decisions.
- [ ] Wilhelm reviews the rewritten design (post-anonymity pivot) and signs off.
- [ ] Decisions written into design.md and change_log.md.

**Exit criteria**: design.md §9 has no unanswered questions blocking Phase 2.

---

## Phase 1 — Scaffold (local only)
- [ ] `dotnet new blazorserver -n ConsidMaltid -f net10.0` in agreed layout.
- [ ] Add `.editorconfig` and `Directory.Build.props` (nullable enabled, latest LangVersion).
- [ ] Smoke test: `dotnet run`, browse the URL.
- [ ] `git init`, baseline commit, scaffold commit.
- [ ] (Optional) push to GitHub if Wilhelm wants a remote now.

**Exit criteria**: app runs locally; version-controlled.

**Note**: Hosting, Docker, and remote deployment are deferred to Phase 7 to keep early iteration fast.

---

## Phase 2 — Event creation
- [ ] `Event` entity + EF migration (UTC `Deadline`, `TimeZoneId`, `Slug`, `OwnerName`, `OwnerEmail`, `ManageToken`, visibility/free-text toggles).
- [ ] Slug generator: `{kebab-title}-{6-char-random}`, with fallback for empty/long titles.
- [ ] `ManageToken` generator: 22-char URL-safe random.
- [ ] `/new` create-event form (Blazor component): title, description, deadline (date/time picker), owner name, owner email, time zone (auto-detected from browser, editable), visibility toggle, free-text toggle.
- [ ] Submit handler: persist event, generate tokens, redirect to `/created/{eventId}`.
- [ ] `/created/{eventId}` success page: display the manage URL prominently with a "save this — it's your only way back in" note. Console-log the would-be email body (real send lands in Phase 5).
- [ ] `/e/{slug}` placeholder page (just renders title/description for now — full UI lands in Phase 3).

**Exit criteria**: anyone can create an event; the success page returns a working manage URL; the slug works in `/e/{slug}`.

**Follow-up bugfix tasks** added after Cowork review of Phase 2 — see `docs/phases/phase-2-plan.md` "Follow-up tasks" section. Cover: TZ-aware datetime conversion (input + display), deadline-vs-StartsAt validation, unique index on `ManageToken`. Run before Phase 2.5.

---

## Phase 2.5 — Service layer and tests
Introduces a thin service layer between Razor pages and the DB so Phase 3+ doesn't accumulate DbContext access scattered through `.razor` files, and so we have unit/integration tests for everything that does business logic.
- [ ] `IEventService` + `EventService` extracted from current direct-DbContext access in pages.
- [x] `tests/Moeltid.Tests/` xUnit project; SQLite in-memory fixture; Shouldly.
- [x] Tests for `TokenGenerator`, `SlugGenerator`, `EventService`.
- [x] Razor pages refactored to use the service interfaces only.
- [x] `IEmailSender` stub (`ConsoleEmailSender`) introduced now to make the Phase 5 swap a one-liner.

**Exit criteria**: `dotnet test` runs and passes; pages no longer reference `AppDbContext` directly; service-layer pattern documented in `design.md` §4.

See `docs/phases/phase-2.5-plan.md` for the task breakdown.

---

## Phase 3 — Attendee signup and meal ordering
- [x] `MealOption` and `Attendance` entities + migration. `MealTag` flags enum.
- [x] `/e/{slug}` public event page: shows event details, attendee form, list of existing attendees/orders if `AttendeeOrdersVisible`.
- [x] Attendee form: name, optional email, **free-text order field (primary path)** OR pick a preset meal option.
- [x] Submit handler: persist `Attendance`, generate `EditToken`, redirect to `/e/{slug}?t={editToken}`, console-log would-be edit email. (URL-only; no cookie — simplified at sign-off.)
- [x] `/e/{slug}/edit-order` page: read token from `?t=`, allow edit / withdraw before close.
- [x] Wire the per-event visibility toggle: when off, attendees see only their own row; when on, everyone sees everything.
- [x] Free-text path verified as the lowest-friction one (one input, submit).

**Exit criteria**: end-to-end ordering works for any attendee; they can edit/withdraw via URL token.

---

## Phase 4 — Owner manage page
- [ ] `/e/{slug}/manage` page, gated on `?t={ManageToken}` matching `Event.ManageToken`. 401-style page if missing/invalid.
- [ ] Edit event fields (title, description, deadline, toggles).
- [ ] Manage meal options (add / edit / remove with tag selection).
- [ ] Orders view: table of all attendances with name / email / order / submitted-at (rendered in `Event.TimeZoneId`).
- [ ] Owner can delete an attendance (cleanup of spam / duplicates).
- [ ] Close-event toggle.
- [ ] Rotate-token button: regenerates `ManageToken`, invalidates the old URL, shows the new URL once.
- [ ] `/e/{slug}/manage/recover` form: enter owner email; if matches, re-send the manage URL via console-log (real email lands in Phase 5). Per-IP and per-email rate limiting.

**Exit criteria**: an owner can fully manage their event; lost manage URL can be recovered via email; rotation works.

---

## Phase 5 — Email and reminders
- [ ] Pick an email provider (Resend vs Brevo); add config; verify with a test domain.
- [ ] Replace console-log stubs with real sends: manage link at creation, manage-link recovery, attendee edit link (when email provided).
- [ ] Add Hangfire + SQLite storage.
- [ ] Owner can schedule **one** reminder per event on the manage page (datetime picker; UI prevents scheduling after deadline).
- [ ] Reminder send: emails attendees who provided email, with a "you have/haven't ordered yet" line.

**Exit criteria**: scheduled reminder fires; manage and edit links land in real inboxes.

---

## Phase 6 — CSV export
- [ ] CSV export button on the manage orders view.
- [ ] Columns: attendee name, email, order type, option label / free text, tags, submitted-at (in owner TZ + UTC).
- [ ] xlsx is out of scope for v1 — revisit later.

**Exit criteria**: owner downloads a clean accounting-ready CSV.

---

## Phase 7 — Deploy infrastructure
This is where hosting and deployment land — close to launch, when the app is stable enough to be worth deploying. Splits cleanly from Phase 8 (production launch) so that "we have a host that runs the app" and "we have a polished public service" are two reviewable steps.
- [ ] Multi-stage `Dockerfile` (SDK 10 build → `aspnet:10.0-alpine` runtime, non-root user, healthcheck endpoint, correct `ASPNETCORE_URLS` binding).
- [ ] `.dockerignore`.
- [ ] Test `docker build` and `docker run` locally — confirm the container serves the app.
- [ ] **Decide host**: Render vs Fly.io. Tradeoff documented in `change_log.md`.
- [ ] Host config in repo (`render.yaml` or `fly.toml`). Persistent volume for the SQLite + Hangfire data.
- [ ] GitHub Actions workflow: build + test on push, deploy on `main`.
- [ ] First deploy: push, watch the deploy, get the (private) URL. Smoke-test the full ordering flow on the deployed instance.
- [ ] Update `design.md` §7 and `change_log.md` with the deploy URL and host choice.

**Exit criteria**: a reachable URL serves the live app; `git push` to `main` redeploys; full ordering flow works on the deployed instance.

---

## Phase 8 — Polish
- [ ] Responsive layout sweep (phone-friendly).
- [ ] Empty / error / loading states for every page.
- [ ] Visual identity: logo, colours, typography.
- [ ] Accessibility pass (keyboard, alt text, contrast).
- [ ] Friendly 404 / 500 pages.

**Exit criteria**: the app feels intentionally designed, not auto-generated.

---

## Phase 9 — Production launch
- [ ] Custom domain.
- [ ] Email domain verified (SPF / DKIM).
- [ ] Backup plan in place (nightly SQLite snapshot to a remote bucket).
- [ ] Light per-IP rate limits on event creation, order submission, manage-link recovery.
- [ ] `robots.txt`, sitemap if relevant.
- [ ] Privacy / terms pages — minimum viable.
- [ ] Production secrets via the host's secret store, not in repo.
- [ ] Test full flow as a brand-new visitor on a fresh device.

**Exit criteria**: ready to be used by real Consid colleagues.

---

## Phase 10 — Future, optional: account layer
Only if there's a real need (e.g., "I want a list of events I've created across devices"). This is purely additive; the v1 design must work without it.
- [ ] `User` entity, registration, login.
- [ ] Optional Microsoft Entra integration via OpenID Connect.
- [ ] `Event.OwnerUserId` (nullable FK) — when set, treat the user as the owner regardless of token possession.
- [ ] "My events" page for logged-in users.

Not committed. Listed so we don't paint ourselves into a corner in v1.
