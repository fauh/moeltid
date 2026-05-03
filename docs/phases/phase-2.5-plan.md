# Phase 2.5 — Service layer and tests

**Status**: COMPLETE — 2026-05-01.

## Why this phase exists

Phase 2 shipped Razor pages that call `AppDbContext` directly. That was the right speed for one entity and no business logic. Phase 3 onwards will add collision-retry on `EditToken`, attendee uniqueness rules, "is the event closed" guards, and email-stub side effects — all of which are awkward to put inside `.razor` files and impossible to test without reaching into ASP.NET Core's component infrastructure.

This phase introduces a thin service layer between Razor pages and the DB, plus a tests project. Once it lands:
- Razor pages stay declarative — they `@inject IEventService` and call typed methods.
- Business logic and DB access live in services that are trivially unit/integration testable.
- Swapping SQLite for PostgreSQL later means changing one connection string and any provider-specific bits in services; pages don't care.
- Phase 3+ services follow the same pattern, so we don't accumulate three different ways of accessing data.

## Goal

A working `IEventService` + `EventService` with the existing Razor pages refactored to use it, and a `tests/Moeltid.Tests/` xUnit project with passing tests for `TokenGenerator`, `SlugGenerator`, and `EventService`.

## Decisions confirmed at kickoff

- **Pattern**: service classes (not repository, not mediator). Each entity gets a service. Pages depend on `I{Entity}Service` interfaces; DI binds them to concrete implementations.
- **Test framework**: **xUnit**. Modern .NET standard; clean async; `[Fact]` / `[Theory]` are intuitive.
- **Test DB**: **SQLite in-memory** (`Data Source=:memory:` with `OpenAsync` to keep the connection alive for the test's lifetime). Higher fidelity than EF's in-memory provider; still fast.
- **Assertions**: **FluentAssertions** for readable test assertions.
- **Project layout**: `tests/Moeltid.Tests/Moeltid.Tests.csproj`, registered in `Moeltid.slnx`.

## Prerequisites

- [ ] Phase 2 follow-up bugfixes (`2.14`–`2.18`) merged. Refactoring on top of correct code keeps the diff small and the review scope clear.

## Task breakdown

| # | Task | Surface | Model | Size | Notes |
|---|---|---|---|---|---|
| 2.5.1 | Define `IEventService` interface | `Services/Events/IEventService.cs` | **Haiku** | S | Methods: `CreateAsync(CreateEventRequest)`, `GetByIdAsync(Guid)`, `GetBySlugAsync(string)`. Define `CreateEventRequest` record with the inputs. |
| 2.5.2 | Implement `EventService` | `Services/Events/EventService.cs` | **Sonnet** | M | Owns slug + token generation, the 3-attempt retry on slug collision (and now ManageToken collision), TZ → UTC conversion, the email-stub `Console.WriteLine`. Constructor takes `AppDbContext`, `SlugGenerator`, `TokenGenerator`, `ILogger<EventService>`. |
| 2.5.3 | Refactor `NewEvent.razor` to call `IEventService` | `Pages/NewEvent.razor` | **Sonnet** | S | Page becomes thin: build `CreateEventRequest`, call service, navigate. Submit handler shrinks to ~15 lines. |
| 2.5.4 | Refactor `EventCreated.razor` and `EventPage.razor` | `Pages/EventCreated.razor`, `Pages/EventPage.razor` | **Haiku** | S | Replace direct DbContext calls with `IEventService.GetByIdAsync` / `.GetBySlugAsync`. |
| 2.5.5 | DI registration for `IEventService` | `Program.cs` | **Haiku** | S | `builder.Services.AddScoped<IEventService, EventService>();`. |
| 2.5.6 | Create `tests/Moeltid.Tests/` xUnit project | `tests/Moeltid.Tests/**`, `Moeltid.slnx` | **Sonnet** | M | `dotnet new xunit -f net10.0`, add to `slnx`, reference the web project, add NuGet packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.EntityFrameworkCore.Sqlite`, `FluentAssertions`. |
| 2.5.7 | SQLite in-memory test fixture | `tests/Moeltid.Tests/Infrastructure/InMemoryDatabaseFixture.cs` | **Sonnet** | M | Helper or `IAsyncLifetime` fixture: opens an in-memory SQLite connection, configures `DbContextOptions<AppDbContext>`, runs migrations, exposes a `CreateDbContext()` method. Disposes the connection at end of test class. |
| 2.5.8 | Tests for `TokenGenerator` | `tests/Moeltid.Tests/Services/TokenGeneratorTests.cs` | **Haiku** | S | Length, URL-safety (no `+/=`), reasonable randomness (e.g. 1000 iterations all distinct). |
| 2.5.9 | Tests for `SlugGenerator` | `tests/Moeltid.Tests/Services/SlugGeneratorTests.cs` | **Sonnet** | S | Kebab-cases ASCII, truncates over `MaxTitleLength`, handles empty/whitespace titles (fallback `event-...`), preserves the suffix shape, behaviour on Swedish characters captured (currently strips them — captured as a known limitation rather than a test failure). |
| 2.5.10 | Tests for `EventService` | `tests/Moeltid.Tests/Services/EventServiceTests.cs` | **Sonnet** | M | `CreateAsync` persists with correct fields and lower-cased email; `GetByIdAsync` returns null for unknown id; `GetBySlugAsync` is case-sensitive (or insensitive — pin the behaviour); slug-collision retry succeeds within 3 attempts; TZ conversion produces correct UTC for non-UTC inputs. |
| 2.5.11 | Update `design.md`, `process.md`, `change_log.md`, retro | docs | **Haiku** | S | `design.md` §4 gets a small "Service layer" subsection. `process.md` documents test conventions (xUnit, SQLite in-memory, FluentAssertions). `change_log.md` records phase closure. |

**Total**: 11 tasks. Mostly Sonnet (judgement-heavy refactor + test design), some Haiku (DI registration, doc updates).

## Open questions for this phase

- [x] **Exceptions vs `Result<T>`** — confirmed: throw exceptions for now. Introduce `Result<T>` only if exceptions are overloaded for control flow.
- [x] **`IEmailSender` stub** — confirmed: introduce `IEmailSender` + `ConsoleEmailSender` now. Makes Phase 5 a one-DI-line swap.
- [x] **Test naming convention** — confirmed: `MethodName_Scenario_ExpectedOutcome` (Microsoft house style).

## Risks / what might bite

- **`.slnx` + xUnit project registration** — newer solution format; if `dotnet sln add` doesn't update `.slnx` cleanly, manual XML edit. Captured in 2.5.6.
- **EF in-memory vs SQLite in-memory** — moving from one to the other later is painful; making the call now (SQLite in-memory) means we get higher fidelity from the start, at minor perf cost.
- **`AppDbContext` is `Scoped` but `IEventService` will also be `Scoped`** — each request gets a fresh DbContext + a fresh service. Tests don't have a request, so the fixture explicitly creates contexts. Captured in 2.5.7.
- **Refactor scope creep** — temptation to introduce repositories, AutoMapper, MediatR, etc. while the diff is open. Don't. The phase is "thin services + tests"; anything else is a separate phase.

## Exit criteria

- `dotnet build` clean.
- `dotnet test` runs the new project; all tests pass.
- Razor pages no longer reference `AppDbContext` directly — only services.
- `tests/Moeltid.Tests/` exists with the test categories above.
- `design.md` §4 mentions the service-layer pattern; `process.md` documents test conventions.

## What actually happened

Completed 2026-04-30 (service layer + tests) and 2026-05-01 (Shouldly migration) by GitHub Copilot with Wilhelm Ericsson.

**Actual models used**: GitHub Copilot (VS-integrated) for all tasks in this session — Copilot picked up the project mid-stream from the prior Claude Code session and completed the remaining work.

**Deviations from plan:**
- Tasks 2.5.1–2.5.10 were already complete when this session opened (done in the preceding Claude Code session). The remaining task was 2.5.11 (retrospective + doc updates) plus the Shouldly migration below.
- **FluentAssertions replaced with Shouldly.** The phase plan specified FluentAssertions; that library changed to a commercial-license model (free for non-commercial only). Shouldly (MIT) is a direct substitute with equivalent readability. All 22 tests were ported in the same session as this retrospective. `process.md` and future phase plans updated to reflect the swap.
- **`design.md` §4 "Service layer" subsection** was already present, having been written during the earlier Code session.

**Surprises:**
- FluentAssertions v8 emits a license-warning banner to test output on every run. Caught during the hand-off review — straightforward swap to Shouldly before closing the phase.

**Things to do differently:**
- Nothing significant. Copilot's VS-integrated tooling (run tests, replace in files) made the Shouldly migration mechanical and verifiable in one pass.

**Post-completion review findings (Cowork-side, retroactive 2026-05-01):**

Read-through performed in Cowork after phase close: services (`IEventService` / `EventService` / `IEmailSender` / `ConsoleEmailSender` / `TimeZoneHelper`), DI wiring in `Program.cs`, the `AddUniqueIndexOnManageToken` migration, the `InMemoryDatabaseFixture`, all three test files, the three refactored Razor pages. No 🔴 correctness bugs. Architecture and test infrastructure are sound. A handful of 🟡s worth addressing before the related code grows in Phase 3+.

🟡 **Email send is inside the `EventService.CreateAsync` success path**, after `SaveChangesAsync`. A throw from `IEmailSender.SendAsync` would surface to the page as "Something went wrong creating the event" *after* the event has already persisted — the user would retry and create a duplicate. With `ConsoleEmailSender` this is quiescent; Phase 5's real provider will need the email send wrapped in try/catch + warn-log so it's best-effort. **Phase 5 prerequisite.**

🟡 **`TimeZoneHelper.ToUtc` and `ToLocalString` swallow all exceptions and silently fall back.** Same shape as the original Phase 2 bug pattern — silent wrong-UTC if anything throws. Browser-detected IANA IDs are always valid, so this is defensive rather than urgent. Recommended fix: narrow to `TimeZoneNotFoundException` / `InvalidTimeZoneException` and log a warning when the fallback path runs. Cheap; do opportunistically.

🟡 **`EventCreated.razor` doesn't handle the not-found case.** If `EventService.GetByIdAsync(EventId)` returns null, `ev` stays null and the page shows "Loading…" forever. `EventPage.razor` has the right pattern with a `notFound` flag; `EventCreated` should mirror it. Edge case (only reachable post-create), but a stuck "Loading…" state is confusing. ✅ **FIXED 2026-05-01 (Cowork via file edit)** — `notFound` flag mirroring `EventPage.razor` pattern. Triggered the new "Bugfix discipline" section in `process.md`.

🟡 **`IsUniqueConstraintViolation` in `EventService` is SQLite-specific** — checks `ex.InnerException.Message.Contains("UNIQUE")`. PostgreSQL and SQL Server emit different messages. Not a problem today; flagged for the eventual PostgreSQL migration. Either abstract via `EFCore.Exceptions` or branch by provider. **Phase 7 / future-DB prerequisite.**

🟡 **`InMemoryDatabaseFixture` shares one in-memory SQLite instance across all tests in the class** (xUnit `IClassFixture<>` semantics). Current tests are isolation-safe (assert on returned values, not DB counts), but as the test class grows, count-based or unique-constraint-sensitive tests will be flaky. Worth a comment in the fixture documenting the shared-DB lifetime, and a convention in test-naming or test setup that any test which mutates state should be aware of it.

🟢 **No direct unit tests for `TimeZoneHelper`** — exercised indirectly via the Stockholm-summer test in `EventServiceTests`. Direct tests would catch fallback edge cases. Phase 3 cleanup.

🟢 **No test for the slug-collision retry actually firing** — would need a test seam (interface for `SlugGenerator` so tests can inject a deterministic stub that returns a colliding slug N times). Add when convenient.

🟢 **No test for manage-link email body assembly** — `NullEmailSender` swallows it. A `RecordingEmailSender` test double could verify the manage URL appears in the body. Phase 5 will cover this when a real provider arrives.

🟢 **`EventService.GetByIdAsync` uses `FindAsync(...).AsTask()`** — `.AsTask()` always allocates a `Task` even on cache hits. Marginal; making it an `async` method is cleaner. Phase 3+ cleanup.

🟢 **Typo**: `CreateAsync_UtcTimeZone_StoresStaratsAtAsUtc` → `StoresStartsAtAsUtc`. Cosmetic.

**No follow-up tasks raised.** None of the 🟡s are blockers for Phase 3. They map cleanly to specific later phases as prerequisites or opportunistic fixes.

**Process note**: this review was performed retroactively, *after* the phase was marked COMPLETE — exactly the situation `process.md` "Phase exit — the two-tool review pattern" was added to prevent. The pattern formally takes effect from Phase 3 onwards. Phase 2.5's retroactive review is the catch-up, not a precedent. The phase is now genuinely closed.
