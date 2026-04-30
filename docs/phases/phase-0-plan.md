# Phase 0 — Repo and docs (retrospective)

**Status**: COMPLETE on 2026-04-30.

This phase is documented retroactively — there was no `phase-0-plan.md` written up front, because the per-phase planning convention itself was introduced during Phase 0. From Phase 1 onward, every phase gets a plan written before code.

## Goal (as it became clear)

Establish a shared mental model of the project — what we're building, how we'll build it, how we'll work together — and produce the documentation scaffolding to support all of it. No code in this phase.

## What actually happened

The phase took several major turns. In order:

1. **Initial framing.** Wilhelm asked for a 0-to-deploy plan. Stack pitched: Blazor WebAssembly on GitHub Pages — on the strength of "C# developer who wants GitHub Pages." That recommendation was made before I knew what the app actually did.
2. **First pivot — GitHub Pages eliminated by features.** Wilhelm shared the feature description (events, food orders, scheduled reminders, exports) and it was immediately clear the app needs a runtime. Pivoted to ASP.NET Core Blazor Server + Docker + free-tier host.
3. **Round-1 design questions answered.** Company free-text, invite-only registration, one reminder per event, CSV export, per-user time zones, link-based event privacy with auto-signup invites, one order per user per event.
4. **Knock-on changes.** Those answers introduced an `Invite` entity, an `Admin` super-user role, time-zone storage on user, and several new routes. Phase 2 grew quietly.
5. **First descope — auth and admin pushed into a "hardening phase".** Wilhelm pushed back on building auth in early v1. I proposed a stub-identity model (cookie + dev user-switcher) for prototype phases with real auth landing in a single dedicated Phase 7.
6. **Process discipline added.** Wilhelm asked for two layers of meta-documentation: per-task model selection (Opus / Sonnet / Haiku) and per-phase task breakdowns reviewed before execution. Added `docs/process.md` and the `docs/phases/` folder.
7. **Major pivot — accountless, no auth at all.** Wilhelm articulated the principle: "simplicity is KEY for this project. both from a usability point of view and to minimise any pitfalls when developing this. Adding security does add a layer of complexity and vulnerability." Pivoted to a fully anonymous per-event model — manage URL emailed to the owner, attendee identity via cookie + optional email. Removed `User`, `Invite`, `Admin`, the entire hardening phase. Bumped to .NET 10.
8. **Deployment deferred.** Wilhelm pushed deployment to Phase 7 in favour of rapid local iteration. Phase 1 was slimmed from 16 tasks to 9.

Six documentation files exist at phase end: `README.md`, `change_log.md`, `docs/design.md`, `docs/roadmap.md`, `docs/process.md`, `docs/phases/phase-1-plan.md` — plus this one.

## What I'd ranked wrong in hindsight

- **Recommending GitHub Pages without first asking what the app does.** I jumped from "C# dev, wants GitHub Pages" straight to a stack recommendation. I should have asked about features first. The lesson generalises: "what does it do" before "where does it run".
- **Underestimating how much auth complexity costs.** I cycled through three auth designs (full Identity in v1 → stub-then-harden → accountless). The accountless model was always the right answer for this use case. I should have proposed it earlier. Wilhelm's "simplicity is KEY" intervention saved meaningful work.
- **Phase 2 grew silently.** When I labelled a single phase "auth, invites, registration", I was underweighting how much that actually is. The accountless pivot made it moot, but the lesson stands — if a phase title contains three nouns, it's probably two phases.
- **`/admin/events` and the admin role drifted into v1 unexamined.** Once an admin role existed for "browse all events", it pulled in admin bootstrap, admin policies, an admin route, an admin UI question. None of that was justified by an actual user need; it just followed from the role existing. Lesson: question every role's actual cost.

## What I think we got right

- **The three-file documentation pattern** (`design.md` = source of truth, `change_log.md` = diary, `phases/` = plans + retros). Already paying off — the change log makes the evolution legible, and `design.md` always reflects "where we are now". The discipline of paired updates ("never one without the other") is what makes it work.
- **Working-assumption tagging with confirm-before-Phase-N stamps.** Stops silent drift. Forces explicit revisit moments.
- **Resisting the temptation to start coding too early.** Several times during Phase 0, the right move was to write more docs and less code. The doc investment will guide every later phase.
- **The .NET 10 bump.** The ecosystem has six months of maturity since release; starting on a pre-LTS would have been false economy.
- **Naming the entity layer to survive a pivot.** Renaming `User` → `AppUser` was deliberate so a later `: IdentityUser` swap would be mechanical. The accountless pivot deleted the entity entirely, but the naming principle generalises — don't name things to match their framework superclass.
- **Aggressive simplification when invited.** Each time Wilhelm pushed back ("descope the admin", "push deploy to Phase 7", "go fully anonymous"), the right response was to lean into it harder than asked, and check whether the simplification justified itself end-to-end. It always did.

## Things I'm watching for in later phases

- **Free-text orders staying the primary path.** Wilhelm has flagged this twice. The risk is over-engineering the meal-option sub-feature in Phase 3; the test will be whether the free-text flow remains one-input-one-submit.
- **Email being the awkward part of pure-local iteration.** Phase 5 has to answer this. Three reasonable options when we get there: keep console-logging through Phase 6; run a local catcher (Mailpit/MailHog); or use a real provider like Resend with a sandbox domain. Flagging early so the answer doesn't ambush us.
- **The model-selection rubric being honest, not performative.** The risk is "I picked Sonnet for this task" becomes a checkbox. Retros should call out where I escalated, where I shouldn't have, and where the rubric was wrong.
- **Phase boundaries being honest, not slipping.** Each phase is sized to be a comfortable sitting. If a phase sprawls, the right move is split, not grind.
- **The "future, optional account layer" (Phase 10) temptation.** It exists as an escape valve. The test at every later phase is whether v1 really works without it. If I catch myself reasoning "well, when we add accounts...", that's a smell.
- **Docs aging silently.** As code arrives, it's tempting to update only the code. Discipline: every meaningful PR-equivalent revisits whether `design.md` still tells the truth.

## Numbers

- Files written or rewritten: 7.
- Major design pivots: 3 (GitHub Pages → server runtime; auth-in-v1 → hardening phase → accountless; .NET 8 → .NET 10).
- Stack recommendations I made and walked back: 1 (Blazor WASM / GitHub Pages).
- Phases removed: 1 (the original Phase 7 "Hardening").
- Open questions resolved: 14.
- Open questions still active: 6 (all relevant to Phase 3 or later).
- Tasks created: 12. Tasks deleted: 1.

## What "complete" means

Phase 0 closes with these conditions met:
- All scope decisions captured in `design.md` and reflected in `roadmap.md`.
- Working assumptions tagged for confirm-by phase.
- The collaboration model documented (`process.md`).
- A concrete plan exists for the next phase (`phase-1-plan.md`).
- No half-decided design issues blocking Phase 1.

All true at sign-off.
