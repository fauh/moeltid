# Consid Måltid

A web app for scheduling events and managing food orders for them. Sign up to events, place a meal order (preset option or free text), and let event owners export the results for accounting.

## Status

Phase 0 — repo and design docs only. No code yet. Local-only iteration through Phase 6; hosting and deployment land at Phase 7.

## Where to look

- [`docs/design.md`](docs/design.md) — the design document. Source of truth. Updated as decisions are made.
- [`docs/roadmap.md`](docs/roadmap.md) — phased plan from zero to deployed.
- [`docs/process.md`](docs/process.md) — how Claude and Wilhelm collaborate, and how Claude picks which model to use for each task.
- [`docs/phases/`](docs/phases/) — per-phase task breakdowns and retrospectives.
- [`change_log.md`](change_log.md) — manual log of decisions and milestones.

## Repo

`https://github.com/fauh/moeltid.git`

## Stack (planned)

ASP.NET Core Blazor Server (.NET 10) · EF Core 10 · SQLite · Hangfire · Docker · Render or Fly.io.

**No auth.** The app is intentionally accountless — manage access is via emailed token URLs. See `docs/design.md` §8 for the full identity model.

See `docs/design.md` §4 for full stack rationale.
