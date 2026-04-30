# Phase 2.5 — Service layer and tests

**Status**: draft. Awaiting Wilhelm sign-off before any task is executed.

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

- [ ] **Should `IEventService` be the only abstraction, or should we also introduce a `Result<T>` / `OneOf<>` pattern for failures (collision exhausted, validation, etc.)?** *Working assumption*: throw exceptions for now; introduce `Result<T>` only if we find ourselves overloading exceptions for control flow. **Confirm at kickoff.**
- [ ] **Should `EventService` own the email-stub `Console.WriteLine`, or should we introduce an `IEmailSender` stub now in anticipation of Phase 5?** *Working assumption*: introduce `IEmailSender` with a `ConsoleEmailSender` implementation now — it's a 30-line investment that lets Phase 5 swap in a real provider with one DI line. **Confirm at kickoff.**
- [ ] **Test naming convention** — `MethodName_Scenario_ExpectedOutcome` (Microsoft house style) or `Should_ExpectedOutcome_When_Scenario` (BDD style)? *Working assumption*: Microsoft house style for consistency with .NET ecosystem norms. **Confirm at kickoff.**

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

_To be filled in at phase end._
