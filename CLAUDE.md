# CLAUDE.md

Quick orientation for any Claude instance (Code, Cowork, future) starting work in this repo. Don't replace this file with a deep summary — it's the entry point. The substance lives in `docs/`.

## What this is

**Consid Måltid** — a web app for scheduling events and collecting food orders. Intentionally anonymous: no user accounts, no login. Identity is carried by tokens in URLs. Coordination tool, not a SaaS platform.

Stack: ASP.NET Core Blazor Server (.NET 10) · EF Core 10 · SQLite · Hangfire · Docker (Phase 7+).

## Read in this order

1. **`docs/design.md`** — source of truth. Architecture, scope, data model, page structure, identity model.
2. **`docs/roadmap.md`** — phased delivery plan.
3. **`docs/process.md`** — collaboration rhythm, model-selection rubric, sandbox/Windows division of labour.
4. **`docs/phases/`** — per-phase task plans and retrospectives. **The current phase's plan is the contract for the work in flight.**
5. **`change_log.md`** — chronological diary of every meaningful decision (newest first).
6. **`README.md`** — pointer document.

If `design.md` and `change_log.md` ever disagree, `design.md` wins and the change log gets amended.

## Current status

- Phases 0 – 7 complete. App is deployed and live.
- Phase 8 (polish) — pending. Next up.
- Phase 9 (production launch — custom domain, verified email sender, backups, retention) — pending.

## Production environment

- **URL**: https://moeltid.fly.dev
- **Host**: Fly.io (`fly.toml` at repo root). Region `arn` (Stockholm). Auto-stops when idle, auto-starts on traffic — first request after sleep takes ~5-10s.
- **Storage**: persistent Fly volume `moeltid_data` mounted at `/data`. SQLite + (when re-enabled) Hangfire data live here.
- **Deploy**: GitHub Actions on push to `main` → `flyctl deploy --remote-only` using `FLY_API_TOKEN` secret. Manual override: `flyctl deploy` from a machine with `flyctl` installed and `flyctl auth login` done.
- **Logs**: `flyctl logs -a moeltid` for streaming, `flyctl logs -a moeltid --no-tail` for a one-shot dump.
- **SSH into the machine**: `flyctl ssh console -a moeltid` (gets a shell on the running container).
- **Secrets**: `flyctl secrets list -a moeltid` to see what's set; `flyctl secrets set KEY=value -a moeltid` to update. `EmailSettings__ApiKey` and `EmailSettings__BaseUrl` live here.
- **Hangfire / scheduled reminders are disabled in production** (auto-stop = sleeping machine = unreliable scheduled jobs). Re-evaluated in Phase 9 if we move to always-on hosting.

See `docs/design.md` §7 for the full deployment notes.

## After completing a phase

Fill in the "What actually happened" section at the bottom of the phase's `docs/phases/phase-N-plan.md` — executor, actual models used per task, escalations and why, surprises, lessons. Add a dated entry in `change_log.md` summarising the phase. Cowork performs the Phase Exit review pass per `docs/process.md` § "Phase exit" before the phase is truly closed.

## Working rhythm (summary, full version in `process.md`)

- Claude drives, Wilhelm reviews at phase boundaries.
- Phases end at a sign-off checkpoint. Don't roll into the next phase without approval.
- Documentation discipline: `design.md` is the source of truth; `change_log.md` is the diary; per-phase plans hold the task-level breakdown and the retrospective.
- Working assumptions get tagged with a "confirm before Phase N" stamp in `design.md` §9.
- For each non-trivial task, pick a model deliberately (Haiku / Sonnet / Opus per `process.md`) and record the choice in the phase plan.

## What this project is *not*

- A multi-user platform with accounts.
- A long-term relationship app.
- Where to put Microsoft Entra integration in v1 (it's an optional Phase 10).

If a feature suggestion implies "users will log in to manage their things over time", that's a smell — the design intentionally avoids it. Push back or surface it as a §9 question.
