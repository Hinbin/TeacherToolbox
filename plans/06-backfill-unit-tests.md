# Plan 06 — Backfill unit tests for ViewModels and Settings

> **Status (2026-05-07): mostly done.** Phases A (Models, except `StudentClass`), B (Services, except the `SettingsServiceFactory` singleton check), C (ViewModels — all six), and D (`scripts/test-unit.ps1` wrapper, with `-Coverage` support) are in place. The unit test project (`TeacherToolbox.UnitTests`) is ~2,200 lines across 8 test files. Integration tests have grown alongside in `TeacherToolbox.IntegrationTests` (~1,100 lines, 10 files including a substantial `TestBase`).
>
> **What this plan now is:** a short remaining-work list, plus a check on whether the leftovers are worth doing at all.

## What's left

Three concrete gaps and one open question.

### 1. `StudentClassTests.cs` (done)

`Student` is well-covered. `StudentClass` — which holds the weighted random-selection logic, the no-consecutive-pick guarantee, and day-of-week assignment — has zero unit tests. This is the most consequential model in the app: a regression here means a teacher gets the same kid picked twice in a row in front of a class.

Cover at minimum:
- Weighted selection respects weights over many trials (statistical, not exact).
- No two consecutive picks are identical when class size > 1.
- Single-student class returns that student deterministically.
- Day-of-week assignment is stable for the same date.

`StudentClass.CreateAsync` reads from disk via `Windows.Storage`. Test the in-memory selection logic by constructing a `StudentClass` directly and using `AddStudent` — don't try to mock the file load.

### 2. `SettingsServiceFactoryTests.cs` (done)

The factory in [TeacherToolbox/Services/SettingServiceFactory.cs](../TeacherToolbox/Services/SettingServiceFactory.cs) is a double-checked singleton with both sync and async creation paths. One test each:
- `CreateSync` returns the same instance on repeat calls.
- `CreateAsync` returns the same instance on repeat calls.
- `CreateSync` followed by `CreateAsync` returns the same instance (and vice versa).

The factory currently constructs a real `LocalSettingsService` internally, which writes to `%LocalAppData%`. **Don't add tests that touch that path.** Either (a) refactor the factory to take an `ISettingsService` factory delegate so tests can inject a fake, or (b) skip these tests. Option (a) is ~10 lines; option (b) is also fine — the factory is small enough to read.

### 3. CI workflow (optional)

There is no `.github/workflows/` directory. `scripts/test-unit.ps1` exists and works locally. For a one-developer project shipping to ~500 users, a manual pre-release `./scripts/test-unit.ps1` run is defensible. A GitHub Actions workflow that runs unit tests on push would be nice-to-have, not need-to-have. **Stop and ask before adding one** — it requires self-hosted Windows runners or `windows-latest` minutes, and this isn't on GitHub-hosted CI today.

## What's deliberately *not* on the list

These were in the original plan; on review they're not worth doing for this codebase:

- **Concurrent-save stress tests for `LocalSettingsService`** (100 parallel writes). The settings service is called from UI event handlers and app startup — there is no realistic concurrent-write workload. Existing `_settingsLock` protects the simple case. Skip.
- **Atomic-write / `File.Move` mocking.** Would require either an IO abstraction layer or `System.IO.Abstractions`. Yak-shaving for a single-user desktop app. Skip.
- **More edge cases on `Student` sanitization.** Existing tests cover apostrophe, hyphen, single-char dedup, weight suffix, null/whitespace, invalid punctuation. The doubled-letter case (`"a a apple"`) and dot-stripping (`"John... Smith"`) from the original plan would be *nice* but neither has produced a real bug. Add only if a regression appears.
- **>60% line-coverage target.** Coverage is collected (`./scripts/test-unit.ps1 -Coverage`) but no threshold is enforced anywhere. Setting one in CI would just create busywork. Read the report when something feels under-tested; don't gate on a number.

## Is a different approach warranted?

No. The current split is working:

- **Unit tests** (`TeacherToolbox.UnitTests`) — Models, Services, ViewModels with mocked dependencies. Fast, runs in a vanilla .NET test process, no WinUI dispatcher.
- **Integration tests** (`TeacherToolbox.IntegrationTests`) — FlaUI-driven UI tests against the built `TeacherToolbox.exe`. The substantial `TestBase` (503 lines) handles app launch, settings cleanup, and dialog helpers.

The boundary is clean. Plan 02 removed the WinUI-types-in-VMs problem that originally blocked unit-testing. ViewModels now take service interfaces in their constructors and are testable without any UI scaffolding. There is nothing to redesign here — just fill the two gaps above and move on.

## Acceptance criteria (revised)

- [x] `StudentClassTests.cs` exists with at least the four cases listed.
- [x] Decision made on `SettingsServiceFactoryTests.cs`: implemented with the small refactor.
- [x] `./scripts/test-unit.ps1` still passes.
- [x] No new test writes to `%LocalAppData%\TeacherToolbox\`.

## Stop and ask

- Before refactoring `SettingsServiceFactory` to accept an injected `ISettingsService` factory delegate — confirm the user wants that small DI tweak vs. just skipping the factory tests.
- Before adding a GitHub Actions workflow — confirm CI is wanted and the runner story (Windows minutes, x86 SDK availability) is acceptable.
- If `StudentClass` selection logic turns out to depend on `DateTime.Now` for day-of-week assignment, inject a `Func<DateTime>` rather than freezing the clock globally.
