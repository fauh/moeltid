# Consid Måltid — Design Document

> Living document. Update as decisions are made. Pair every meaningful change here with a `change_log.md` entry on the same date.

## 1. Vision

A web app for scheduling events and collecting food orders. Anyone with the link can sign up to an event and submit a meal order — preset option or free text. The event creator gets a manage link by email and uses it to edit the event, see all orders, schedule a reminder, and export the order list for accounting.

Free-text orders are the primary, must-be-best-supported workflow. Preset options exist but are secondary.

The app is intentionally accountless. No registration, no passwords, no admin role. Identity is carried by tokens in URLs and emails. This is a coordination tool — closer to Doodle than to a SaaS platform.

## 2. Roles

There are no user accounts. Two informal roles, defined entirely by what URL someone holds:

| Role | What they hold | What they can do |
|---|---|---|
| **Event creator** ("owner") | A manage URL containing the event's `ManageToken`, received by email at creation; re-requestable. | Edit the event, view all orders, schedule a reminder, close the event, export CSV, share the manage URL with co-admins. |
| **Attendee** | The public event URL `/e/{slug}`. | Sign up, submit a meal order, edit their own order (cookie or email link). |

Multiple co-administrators are supported by the simple expedient of sharing the manage URL. There is no `User` entity anywhere in the system.

## 3. Scope — v1

One cohesive scope. No prototype/hardening split. The app is publicly usable from the moment it deploys.

In:
- **Anonymous event creation.** Creator provides title, description, deadline, owner name, owner email, optional preset meal options, free-text toggle, and an "attendee orders visible" toggle (default on).
- A `ManageToken` is generated and emailed to the owner. The success screen also displays the manage URL once. The token can be re-requested by entering the owner email — same token returned, so links shared with co-admins keep working.
- **Public event URL** `/e/{slug}`. Attendees enter their name (and optional email), pick a preset meal option or write a free-text order, submit.
- A cookie remembers the attendee on this device so they can edit/withdraw their order. If they provided email, an edit link is also emailed.
- Meal options carry tags: drink, fish, vegetarian (lacto-ovo), vegan.
- **Invitations**: at event creation (and on the manage page), the owner provides a comma-separated list of emails. Each invitee gets an emailed link `/e/{slug}?invite={inviteeId}` that pre-fills their email read-only on the order form. Invited-but-not-ordered rows show on the event page flagged "no order yet" (subject to `AttendeeOrdersVisible`). The manage page shows invitee status and has a "Send reminders" action for unordered invitees.
- **Owner manage page**: edit fields, manage meal options, manage invitees (add/remove with three-option prompt when invitee has ordered), schedule one reminder, close the event (no further changes), view all orders, export CSV, optionally rotate the manage token.
- **Per-event "attendee orders visible" toggle**: when on (default), the public page shows all attendee names and orders; when off, attendees see only their own row. Owner sees everything regardless.
- **One reminder per event.** Hangfire fires it; an email goes to attendees who provided email.
- All datetimes stored UTC. `Event.TimeZoneId` is the owner's; manage page renders in that TZ. Public page renders in the visiting browser's TZ.
- Basic responsive layout.

Out (deferred):
- User accounts of any kind.
- Microsoft Entra integration. (Could be added as an additive layer if Consid wants creators to be identifiable internally.)
- xlsx export — CSV is good enough for v1.
- Multiple reminders per event.
- Per-event configurable multi-order policy.
- Real-time admin browse / dashboards.
- Payments.
- Multi-language UI.
- Mobile native app.

## 4. Tech stack and rationale

| Layer | Choice | Why |
|---|---|---|
| Framework | **ASP.NET Core Blazor Server on .NET 10** | C# end-to-end. Server-rendered UI with SignalR-driven reactivity — no separate Web API layer. Familiar to a C# backend dev. .NET 10 is the current LTS (Nov 2025). |
| ORM | **EF Core 10** | Standard. Migrations via `dotnet ef`. |
| Database | **SQLite** for v1. Migration path to PostgreSQL if traffic grows. | Single file, zero ops. EF Core supports both with the same code. |
| Auth | **None.** Manage access via emailed token in URL; attendee identity via cookie + optional email. | The app is intentionally accountless. See §8. |
| Background jobs | **Hangfire** | For the single per-event reminder. SQLite-backed storage. |
| Email | **Resend** (locked at Phase 5). | Critical: the manage link arrives by email. Dev uses `ConsoleEmailSender`; prod uses `ResendEmailSender` behind `IEmailSender`. API key via `dotnet user-secrets` in dev, host env var `EmailSettings__ApiKey` in production. `EmailSettings:BaseUrl` carries the absolute URL base for all email links. |
| Container | **Docker** | Portability. Linux containers from `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`. |
| Host | **Render** or **Fly.io** free tier — picked at Phase 1. | Both accept Dockerfiles, both have free tiers. Replaceable later. |
| CI/CD | **GitHub Actions** | Standard. Build, test, push image, deploy. |

### Why not GitHub Pages
GitHub Pages only serves static assets. The app needs a runtime for DB, scheduled jobs, and server-side rendering. Docker keeps us portable — anywhere that runs a Linux container can run this app.

### Why Blazor Server over Blazor WebAssembly
- No download of the .NET runtime to the browser → faster first paint.
- Server holds DB connection, simpler mental model for a backend dev.
- No separate Web API needed.
- Data layer and services are reusable if we ever want WASM later.

### Why SQLite first
- File-based, no separate DB process to operate.
- EF Core swaps to PostgreSQL with one connection-string and one provider-package change.
- Dev setup is `dotnet run` and you have a working database.

### Service layer
Business logic lives in service classes, not Razor pages. Each aggregate (currently just `Event`) has an interface (`IEventService`) and a concrete implementation (`EventService`) in `src/Moeltid/Services/Events/`. Razor pages depend only on the interface via DI.

Rationale: collision-retry, uniqueness checks, "is the event closed" guards, and email-stub side effects are awkward to keep in `.razor` files and untestable without bootstrapping ASP.NET Core component infrastructure. A thin service layer allows straightforward xUnit integration tests.

`IEmailSender` / `ConsoleEmailSender` stub is introduced at Phase 2.5; Phase 5 swaps in a real provider with one DI registration change.

## 5. Data model

```
Event
  Id                        Guid           PK
  Slug                      string         unique, URL-safe (e.g. "friday-lunch-x7q9")
  Title                     string
  Description               string         nullable
  StartsAt                  DateTimeOffset (UTC)
  Deadline                  DateTimeOffset (UTC)
  TimeZoneId                string         IANA — owner's TZ at creation
  AllowFreeText             bool           default true
  AttendeeOrdersVisible     bool           default true
  IsClosed                  bool           default false
  OwnerName                 string
  OwnerEmail                string         lower-cased
  ManageToken               string         random, URL-safe, ~22 chars; carried in /e/{slug}/manage?t={token}
  CreatedAt                 DateTimeOffset

MealOption
  Id            Guid           PK
  EventId       Guid           FK -> Event
  Label         string
  Tags          MealTag flags  (None | Drink | Fish | Vegetarian | Vegan)

Attendance
  Id              Guid           PK
  EventId         Guid           FK -> Event
  Name            string         attendee-typed
  Email           string?        optional; for emailed edit link + reminder
  EditToken       string         random, URL-safe; used in cookie + emailed edit link
  OrderType       enum           (PresetOption | FreeText)
  MealOptionId    Guid?          FK -> MealOption (when OrderType = PresetOption)
  FreeTextOrder   string?        (when OrderType = FreeText)
  SubmittedAt     DateTimeOffset (UTC)

Reminder
  EventId         Guid           PK & FK -> Event   (one reminder per event)
  ScheduledFor    DateTimeOffset (UTC)
  IsSent          bool           default false
  HangfireJobId   string?

Invitee  (added 2026-05-04 in Phase 4.5)
  Id            Guid           PK
  EventId       Guid           FK -> Event (cascade-delete)
  Email         string         lower-cased via value converter
  InvitedAt     DateTimeOffset (UTC)
  UNIQUE(EventId, Email)
```

Notes:
- **No `User` table. No `Invite` table. No roles.** The whole identity layer is replaced by tokens.
- The flag-based `MealTag` lets a single option be e.g. "drink + vegan" without a join table.
- `ManageToken` and `EditToken` are stored as plaintext so re-request returns the same token (preserving links shared with co-admins). The risk: a server compromise lets attackers edit some events. Bounded and reversible. Optional **rotation** button on the manage page replaces the token, invalidating any previously shared URLs. We can move to encrypt-at-rest later if the risk profile changes.
- Datetimes are stored as UTC throughout. Conversion to display time zone happens at the rendering boundary using `TimeZoneInfo`.

## 6. Page structure

| Route | Who | Purpose |
|---|---|---|
| `/` | anyone | Landing. CTA to create an event. |
| `/new` | anyone | Create-event form: title, description, deadline, owner name, owner email, time zone (defaulted from browser), meal options, free-text toggle, visibility toggle. |
| `/created/{eventId}` | creator (one-shot, post-create) | Success screen showing the manage URL plainly + a note that it was emailed. "Bookmark this URL" prompt. |
| `/e/{slug}` | anyone with the link | Public event page: attendee signup, meal ordering. Shows others' orders if `AttendeeOrdersVisible`. Optional `?invite={inviteeId}` query param pre-fills the email field read-only when arriving via an emailed invite link. |
| `/e/{slug}/edit-order` | attendee with edit token (cookie or `?t=` from email) | Edit or withdraw your own order. |
| `/e/{slug}/manage` | manage-token holder (`?t=` in URL) | Owner manage page. Token validated on every request. |
| `/recover` | anyone | Top-level form to request manage link(s) emailed to the owner. Looks up by owner email; one email per matching event. Discoverable from the landing page. Rate-limited per IP and per email (Phase 8). |
| `/e/{slug}/manage/recover` | (legacy) | Redirect stub to `/recover` since 2026-05-06. Kept for inbound bookmarks; safe to delete once those are confirmed dead. |

## 7. Deployment plan

Deployment is deferred to **Phase 7** — early iteration runs locally only via `dotnet run`. Rationale: rapid feature iteration is more important than infrastructure during the prototype phases, and any host/CI choices made now would likely be reworked once we know the real shape of the app. The plan below is the *target* for Phase 7, not Phase 1.

- **Build**: `Dockerfile` in repo root, multi-stage build (SDK 10 → ASP.NET runtime 10 on Alpine).
- **Runtime image**: `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`.
- **Host**: Render or Fly.io free tier — picked at Phase 7. Persistent volume for the SQLite file and Hangfire data.
- **Email**: **Resend** (locked Phase 5). Domain verification needed before production launch. Dev and staging use Resend's sandbox / verified-address mode. `EmailSettings:UseRealProvider` feature flag controls the DI swap. `EmailSettings:BaseUrl` must be set to the deployment URL in production so email links point to the right host.
- **CI/CD**: GitHub Actions on push to `main` — build, test, push image, trigger deploy. Set up at Phase 7.
- **Domain**: TBD. Use the host's free subdomain initially.
- **Backups**: nightly cron in the container that copies the SQLite file to a remote bucket. Decision deferred to Phase 9.

## 8. Identity model (no accounts)

The app uses no authentication system. Identity is carried by tokens in URLs and (optionally) cookies.

### Manage access
- At event creation, a `ManageToken` is generated (cryptographically random, URL-safe, ~22 chars).
- The manage URL `/e/{slug}/manage?t={token}` is shown on the create-success screen and emailed to the owner.
- **Anyone with the URL has full owner rights.** Co-administrators are supported by sharing the URL.
- **Lost the URL?** Visit `/e/{slug}/manage/recover`, enter the owner email; if it matches, the same link is re-emailed. Rate-limited.
- **Optional rotation**: a "rotate manage token" button on the manage page generates a fresh token and invalidates the old URL. Useful if the URL was leaked or a co-admin should lose access.

### Attendee identity
- When an attendee submits an order, an `EditToken` is generated and:
  - stored in a per-event cookie (keyed by `eventId`) so the same browser can edit later,
  - emailed to the attendee if they provided an email, in the form of an edit URL.
- No account. The attendee is whoever holds the edit token.
- Lost cookie + no email → submit a fresh order. Owner can clean up duplicates.

### Why no auth
- Use case is short-lived coordination — a meal order, an event two weeks out. No long-term account relationship.
- Removes a substantial security and complexity surface: no registration, no password reset, no session fixation, no account enumeration, no invite flow, no admin role, no Identity tables.
- If accounts are ever wanted (e.g. for "my events" history across devices), they can be added as an additive layer linking events/attendances by email — non-disruptive change.

### Time zones
Every datetime in the database is UTC. `Event.TimeZoneId` (IANA) is captured at creation from the browser via JS interop (`Intl.DateTimeFormat().resolvedOptions().timeZone`), falling back to `UTC` if detection fails. The manage page renders datetimes in this owner TZ. The public event page renders in the visiting attendee's browser TZ. Linux containers need IANA tzdata available — Microsoft's .NET base images ship it; we'll verify in Phase 1.

### Threat model summary
| Threat | Mitigation |
|---|---|
| Manage URL leaked publicly | Rotate token from manage page. |
| Manage URL guessing | 22-char random token (~131 bits entropy). |
| Spam orders on a public event | Owner can delete orders, close the event. Light per-IP rate-limit on order submission. |
| Spam event creation | Light per-IP rate-limit on event creation (Phase 8). |
| Manage-link recovery abuse | Rate-limit the recovery form per IP and per target email. |
| Server-DB compromise | Tokens are plaintext. Acceptable for v1; revisit if data sensitivity grows. |

## 9. Open questions

### Resolved 2026-04-30
- **Identity model**: anonymous, no accounts. Manage via emailed link; recover via owner email. Attendee identity via cookie + optional email.
- **Manage-token recovery**: stable token returned on re-request (so shared URLs survive). Optional rotation button on manage page.
- **Attendee visibility**: per-event toggle, default on.
- **.NET version**: .NET 10 (LTS, Nov 2025).
- **Reminder model**: owner picks an explicit datetime; one reminder per event.
- **Export format**: CSV only in v1; xlsx revisit later.
- **Order multiplicity**: one order per attendee per event; multi-item needs go in free text.
- **Invitations** *(2026-05-04)*: at event creation time and on the manage page, the owner can list emails of people to invite. Each invitee gets an emailed link `/e/{slug}?invite={inviteeId}` that pre-fills their email read-only on the public form. Invited-but-not-ordered rows show on the event page (subject to the visibility toggle) flagged "no order yet". Manage page has a "Send reminders" action for the unordered subset.
- **Meal options at creation**: owner can define preset meal options (label + tag flags) inline on the create-event form. Same options can also be managed on the manage page later.

### Still open
- [x] **Slug format** — confirmed 2026-04-30: `{kebab-title}-{6-char-random}`, falling back to `event-{6-char-random}` for empty/long titles.
- [ ] **Attendee email — required?** *Working assumption*: optional. Required would enable cross-device edits for everyone but lose anonymous quick-order. **Confirm before Phase 3.**
- [x] **Reminder email content & audience** — confirmed 2026-05-07 (Phase 5): all email holders (attendees with email + invitees without an order). Body is status-aware: attendees get "you ordered X"; invitees-no-order get "submit by deadline". Implemented in `ReminderAudience.Build`.
- [x] **Reminder vs. deadline guard** — confirmed 2026-05-07 (Phase 5): yes. Manage-page UI validates: must be after `now`, before `Deadline`, before `StartsAt`.
- [ ] **Event-creation rate-limit** — *working assumption*: 10 events / hour / IP. **Confirm before Phase 8.**
- [ ] **Public-page attendee visibility default** — confirmed default on, but should we display *names only* or *names + orders* when the toggle is off? *Working assumption*: names only (so an attendee can see how many people are coming without seeing what they're eating). Could also be "nothing visible to other attendees". **Confirm before Phase 3.**
- [ ] **Event retention policy** *(raised at Phase 4 sign-off, 2026-05-04)* — events are short-lived by design; data shouldn't accumulate forever. Open questions: how long after the deadline does an event live? what's deleted at retention end (the whole event including attendances, or just orders)? does the owner get notified before deletion? *Working assumption*: hard-delete the event and its attendances 90 days after `Deadline`, no warning email. Possibly a warning email at 75 days if email-sending exists by then. **Confirm before Phase 9.** Lives with the other production-hardening work (rate limits, backups, domain) in Phase 9.
- [ ] **Events listing / discovery** *(raised 2026-05-06)* — placeholder for a Phase 6.5 enhancement that gives users a way to find events they're part of without keeping the URLs. Tension: the v1 model says events aren't browseable. *Working assumption*: per-email lookup page (typed email returns events with that owner email; doesn't differentiate match from miss; same privacy posture as `/recover`). Open: owner events only vs attendee events too; what counts as "ongoing"; whether to require an email-confirmation step before showing the list. **Confirm at Phase 6.5 kickoff.**

## 10. Roadmap pointer

See `docs/roadmap.md` for the phased delivery plan. Each phase ends at a review checkpoint.
