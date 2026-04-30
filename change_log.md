# Change Log

Manual, human-curated record of decisions, design changes, and milestones for Consid Måltid. Newest entries first. Update this file whenever a meaningful decision is made or a phase boundary is crossed.

Format: one section per date (or per work session). Within a date, group entries under a short heading.

---

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
