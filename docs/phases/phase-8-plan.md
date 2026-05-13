# Phase 8 — Polish

**Status**: completed

## Goal

The app should feel intentionally designed, not auto-generated. Phase 8 is a horizontal sweep across every page: responsive layout, empty/error/loading states, visual identity, accessibility, and friendly error pages. No new features. No data-model changes. No migrations.

Exit criteria mirror the roadmap: *the app feels intentionally designed, not auto-generated.*

## What exists today (pre-phase baseline)

- **Stack**: Bootstrap 5 via the legacy Blazor Server template. `site.css` is the stub from `dotnet new blazorserver` (open-iconic, a generic blue `btn-primary`). No custom palette, no logo, no brand.
- **Pages**: `Index`, `NewEvent`, `EventCreated`, `EventPage`, `EditOrder`, `ManageEvent`, `Events`, `Recover`, `ManageRecover`. All functional; none polished.
- **Layout**: `MainLayout.razor` (assumed, from scaffold) + `NavMenu`. Template chrome still present (About link, open-iconic icons, generic nav).
- **Error pages**: `Error.cshtml` is the bare scaffold template. The `blazor-error-ui` div in `App.razor` is the default Blazor error bar.
- **Accessibility**: no deliberate pass has been done.
- **Empty states**: most list/table views are `@if (!items.Any()) { <p class="text-muted">No items yet.</p> }` — functional, not polished.
- **Loading states**: most `OnInitializedAsync` overrides render nothing (or the full scaffold) until data arrives. No loading skeletons.

## Decisions confirmed at kickoff

1. **Visual identity**: Custom palette — main colours: burgundy, raspberry red, light orange; complementary: black, dark purple, greige, beige. Light/dark mode toggle that respects the palette. No external branding guidelines.
2. **Font**: Keep system/Helvetica stack — no web fonts.
3. **Responsive scope**: Best-effort. Don't over-invest; prioritise the order form and the manage table.
4. **Accessibility depth**: Pragmatic pass — keyboard nav + obvious contrast issues on critical paths.
5. **Friendly error pages**: Blazor `NotFound` fragment in `Routes.razor` is the chosen approach; `Error.cshtml` restyled to match the palette.

## Open questions — need Wilhelm's answers at sign-off

1. **Visual identity**: should the app get a custom palette + logo, or stay Bootstrap-default with just cleanup? If custom: any Consid branding guidelines to follow, or free choice?
2. **Font**: keep system/Helvetica stack, or add a Google Font (latency tradeoff on Fly free tier)?
3. **Scope of responsive sweep**: is the mobile experience a hard requirement for Phase 8, or a best-effort?
4. **Accessibility depth**: WCAG 2.1 AA (full audit tool pass) or pragmatic pass (keyboard nav + obvious contrast issues)?
5. **Friendly 404/500**: custom Blazor not-found page vs extending `Error.cshtml`? The app uses Blazor's own not-found routing, so a Blazor `NotFound` fragment in `Routes.razor` is the natural fit.

## Task breakdown

Tasks are sized for a single sitting each. Sonnet for anything requiring design judgement; Haiku for mechanical sweeps.

| #    | Task                                                                                    | Surface                                          | Model      | Size | Notes                                                                                                                               |
| ---- | --------------------------------------------------------------------------------------- | ------------------------------------------------ | ---------- | ---- | ----------------------------------------------------------------------------------------------------------------------------------- |
| 8.1  | Audit all pages: inventory empty states, loading states, and error paths                | read-only pass                                   | **Sonnet** | S    | Produces a checklist used by 8.4–8.6. No code changes.                                                                              |
| 8.2  | Visual identity: custom palette + CSS variables + updated `site.css`                    | `wwwroot/css/site.css`, `App.razor`              | **Sonnet** | M    | Define a small set of CSS custom properties (`--color-primary`, `--color-surface`, etc.). Update Bootstrap overrides. Replace the generic blue. |
| 8.3  | Logo / wordmark: SVG favicon + nav brand mark                                           | `wwwroot/favicon.svg`, `MainLayout.razor`        | **Sonnet** | S    | Simple text-based SVG wordmark is fine. Replace the scaffold `favicon.png`. Remove the template "About" link from the nav.          |
| 8.4  | Responsive layout sweep                                                                 | all `.razor` pages                               | **Sonnet** | M    | Mobile-first check on every page. Primary concerns: `NewEvent` form, `ManageEvent` orders table, `Events` listing.                  |
| 8.5  | Empty states                                                                            | `EventPage`, `ManageEvent`, `Events`             | **Haiku**  | S    | Replace `<p class="text-muted">No items yet.</p>` stubs with styled empty-state messages.                                           |
| 8.6  | Loading states                                                                          | `EventPage`, `ManageEvent`, `Events`, `EditOrder` | **Haiku**  | S    | Add a simple spinner / skeleton while `OnInitializedAsync` is in flight. One shared `<LoadingSpinner>` component to avoid repetition. |
| 8.7  | Friendly not-found and error pages                                                      | `Routes.razor` (`NotFound` fragment), `Error.cshtml` | **Sonnet** | S    | Blazor `NotFound` fragment for unknown routes. `Error.cshtml` restyled to match the app's palette. Both include a link back to `/`. |
| 8.8  | Accessibility pass                                                                      | all `.razor` pages                               | **Sonnet** | M    | Keyboard navigation, `aria-label` on icon-only buttons, `alt` on images, contrast check on the new palette. Focus on the critical paths: create event, submit order, manage event. |
| 8.9  | `blazor-error-ui` styling                                                               | `App.razor`, `site.css`                          | **Haiku**  | S    | Style the default Blazor reconnect/error bar to match the app's palette instead of the scaffold default.                            |
| 8.10 | Nav cleanup                                                                             | `MainLayout.razor`, `NavMenu.razor`              | **Haiku**  | S    | Remove template chrome (open-iconic sidebar, "About" link). Replace with a minimal top-nav or simple header appropriate to the app. |
| 8.11 | `change_log.md` close entry; retro at bottom of this file                               | docs                                             | **Haiku**  | S    | Standard phase-close docs.                                                                                                          |

**Total**: 11 tasks. Skews Sonnet (visual/design judgement calls) with a few mechanical Haiku tasks.

## Risks / what might bite

- **Bootstrap coupling**: the app uses Bootstrap 5 classes throughout. Custom CSS that fights Bootstrap specificity can cause surprises. The safest approach is CSS variables that override Bootstrap's own custom properties (it exposes `--bs-primary`, `--bs-body-*`, etc.) rather than fighting class selectors.
- **Responsive tables**: `ManageEvent`'s orders table will not collapse gracefully on mobile without deliberate handling. Either a `table-responsive` wrapper + horizontal scroll, or a card-per-row layout below the breakpoint.
- **SVG favicon cross-browser**: `favicon.svg` has good but not universal support. Keep `favicon.png` as a fallback.
- **Loading states in Blazor Server**: `OnInitializedAsync` runs server-side on prerender; the loading flicker may not be visible in prerender mode. Loading state is more relevant for the interactive re-renders (e.g. after an action). Worth noting in the retro if spinners are invisible in practice.
- **Scope creep**: "polish" is an open-ended word. The scope boundary is: anything that makes the app *feel* finished without adding new data, endpoints, or business logic. Animations, complex transitions, and marketing copy are out.

## Exit criteria

- Every page passes a visual spot-check: no template chrome, consistent palette, logo/wordmark visible.
- Forms and tables are usable on a 375px-wide screen (iPhone SE viewport).
- Unknown routes render a friendly not-found page, not a blank Blazor fallback.
- Keyboard navigation reaches every interactive element on the critical paths.
- `dotnet build` and `dotnet test` are green.
- Deployed to Fly.io and smoke-tested on the live URL.
- Cowork phase-exit review pass per `process.md` before the phase is truly closed.

## What actually happened

All 11 planned tasks were completed (two were folded into adjacent tasks and marked skipped rather than as separate steps). One unplanned task — order deadline enforcement — was added and completed before the polish work began.

**What went smoothly:**
- CSS custom properties + Bootstrap `--bs-*` override strategy worked cleanly. No specificity conflicts, and the dark-mode toggle needed only a `[data-theme="dark"]` block on top of the same variable names.
- Replacing the scaffold sidebar with a top-nav was a one-file change to `MainLayout.razor` and a rewrite of `NavMenu.razor`. The scoped CSS files were cleared without any side effects.
- `LoadingSpinner` as a shared component paid off immediately — four pages benefited with one small component.
- The `table-responsive` wrapper was the entire responsive work needed; all forms were already single-column and fine on mobile.

**What was harder than expected:**
- `site.css` required a full rewrite rather than an incremental edit because the scaffold styles were entangled (e.g. the `.btn-primary` override hardcoded hex values). Starting fresh was cleaner.
- The `color-mix()` function used for subtle tinted surfaces requires a reasonably modern browser; this is acceptable given the audience but worth noting.

**What was cut / descoped:**
- No Google fonts (kept system stack as decided at kickoff).
- No WCAG 2.1 AA full audit — pragmatic pass only (keyboard nav + contrast on critical paths).
- Responsive work stayed best-effort: `table-responsive` wrappers on all tables, no card-per-row rewrites.

**Risks that materialised:**
- None of the listed risks bit in practice. Bootstrap specificity was avoided entirely via `--bs-*` variables. SVG favicon is served with PNG fallback. Blazor Server prerender means loading spinners are not visible on first load — noted but acceptable; they fire on interactive re-renders.

