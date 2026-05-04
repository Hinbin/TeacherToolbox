# Plan 06 — Backfill unit tests for ViewModels and Settings

## Why

Coverage today is one file: `ClockViewModelTests.cs` (in the unit-test project). The unmerged `clock-viewmodel-test-fixes.patch` (deleted in Plan 01) shows that previous test attempts hit a wall on WinUI types in ViewModels. Plan 02 removes that obstacle. With a 500-teacher user base and one developer, regression detection has to come from automated tests — manual testing won't scale.

The goal is not 100% coverage. It's **enough tests on the load-bearing logic that a regression is caught before release**. That means:

- ViewModels that contain non-trivial logic (random selection, time-slice calculation, settings persistence).
- The `LocalSettingsService` (file IO, atomic writes, deserialization fallback, sync vs async paths).
- The `Student` / `StudentClass` weighting and selection logic mentioned in CLAUDE.md.

## Prerequisites

- **Plan 01** (cleanup) — single, clearly-named test project.
- **Plan 02** (decouple ViewModels) — required, otherwise `SolidColorBrush` will block any ViewModel construction in tests.
- **Plan 04** (constructor injection) — strongly recommended. ViewModels are easier to test when their dependencies are explicit constructor parameters.

## Scope

**In:**
- ViewModel tests for: `ClockViewModel`, `TimerWindowViewModel`, `RandomNameGeneratorViewModel`, `SettingsViewModel`, `IntervalTimerViewModel`, `TimerPageViewModel`.
- Service tests for: `LocalSettingsService`, `SettingsServiceFactory`.
- Model tests for: `Student` (punctuation stripping, weight extraction), `StudentClass` (weighted selection, no-consecutive-pick guarantee, day-of-week assignment).

**Out:**
- UI/integration tests (those live in `TeacherToolbox.IntegrationTests/` and are out of scope here).
- Tests for `MainWindow` or any code that requires a running WinUI dispatcher.
- Tests for `ShortcutWatcher.exe` internals (Plan 05 covers a possible test there).

## Approach

- **Framework:** NUnit (already in the project).
- **Mocking:** Moq (already in the project).
- **File-IO tests:** use a temp directory per test (`Path.Combine(Path.GetTempPath(), $"ttb-test-{Guid.NewGuid()}")`); clean up in `[TearDown]`.
- **Determinism for randomness:** ViewModels using `Random` should accept an `IRandom` (or `Func<int, int, int>`) interface so tests can pin the values. If they don't today, refactor minimally: introduce a constructor overload that accepts a seed. Do not add a feature-flag system.
- **Async tests:** use NUnit's async-friendly attributes; don't `.Result` on tasks.
- **Test naming:** `MethodUnderTest_Scenario_ExpectedOutcome`. Existing tests follow this pattern.

## Steps

### Phase A — Models (highest leverage, lowest risk)

1. **`StudentTests.cs`** — verify:
   - `"O'Brien"` keeps the apostrophe.
   - `"Mary-Jane"` keeps the hyphen.
   - `"John... Smith"` strips dots.
   - `"Alice 3"` extracts a weight of 3.
   - `"a a apple"` removes the doubled `"a"`.
   - Empty / null / whitespace input handled.

2. **`StudentClassTests.cs`** — verify:
   - Weighted selection respects weights (over many trials).
   - No two consecutive picks are identical (when class size > 1).
   - Day-of-week assignment is stable for the same date.
   - Large class (e.g., 30 students) selection completes in <50ms.

### Phase B — Services

3. **`LocalSettingsServiceTests.cs`** (a stub already exists at `TeacherToolbox.UnitTests/Services/LocalSettingsServiceTests.cs` — extend it):
   - `SaveSettingAsync` followed by `ReadSettingAsync<T>` round-trips a string, an int, a complex object.
   - Concurrent `SaveSettingAsync` calls don't corrupt the file (run 100 in parallel, then verify the file deserializes).
   - Atomic write: kill the process between temp-file creation and rename — the original file should be unchanged. (Simulate by mocking `File.Move`.)
   - Corrupted JSON on disk → `LoadSettings` falls back to defaults instead of throwing.
   - `InitializeSync` produces the same result as `LoadSettings` for the same file.

4. **`SettingsServiceFactoryTests.cs`** — verify the factory returns the same instance on subsequent calls (singleton semantics).

### Phase C — ViewModels (depends on Plan 02)

5. **`ClockViewModelTests.cs`** — replace the existing brittle tests:
   - Time-slice add/remove/extend.
   - Slice doesn't cross hour boundary (the existing test `ExtendTimeSlice_PreventsCrossingHourBoundaryOverlap_SpecificScenario` should be kept).
   - Adding at the same position doesn't create a duplicate.
   - `HandColor` updates when `IThemeService.IsDarkTheme` changes.
   - Construction succeeds with mocked `ITimerService` and `IThemeService` — **no `SolidColorBrush` setup required** (this is the test that proves Plan 02 worked).

6. **`RandomNameGeneratorViewModelTests.cs`**:
   - Pick command calls into the underlying class.
   - Refusing-list / "skip-this-name" feature works.
   - Empty class → command is disabled or no-ops gracefully.
   - F9 hotkey path (if observable from the VM) updates the displayed name.

7. **`SettingsViewModelTests.cs`**:
   - Theme change updates `IThemeService` and persists via `ISettingsService`.
   - Centre-number save flows through to `ISettingsService.SaveSettingAsync`.
   - Loading a settings page reads existing values from `ISettingsService`.

8. **`TimerWindowViewModelTests.cs`**:
   - Countdown reaches zero → state transitions correctly.
   - Pause/resume works.
   - Sound-on-finish flag is honored (mock the sound service).
   - `TimerTextColor` flips to red in the last 10s (or whatever the existing rule is).

9. **`IntervalTimerViewModelTests.cs`** and **`TimerPageViewModelTests.cs`** — read these classes first, then write tests for whatever logic they own. If the class is mostly a thin wrapper around `TimerWindowViewModel`, one or two smoke tests is enough.

### Phase D — CI hookup

10. **Confirm the test project runs from CI**, if there is one. If not, add a one-line `dotnet test` command to whatever build script exists, or note in `CLAUDE.md` that the developer should run it before each release.

## Acceptance criteria

- [ ] At least one test file exists for every public ViewModel and every Service.
- [ ] `dotnet test` passes locally and runs in <30 seconds.
- [ ] No test relies on a real WinUI dispatcher; all run in a vanilla .NET test process.
- [ ] No test writes to `%LocalAppData%\TeacherToolbox\` (only to per-test temp directories that get cleaned up).
- [ ] All tests pass on a clean machine with no special setup.
- [ ] Coverage of ViewModel and Service code is reasonable — aim for >60% line coverage on those folders. Don't bend the design just to chase percentage.

## Stop and ask

- If a ViewModel turns out to depend on a singleton or a static (e.g., `DateTime.Now`) in a way that prevents deterministic testing — the right fix is usually an injected `Func<DateTime>`/`IDateTimeProvider`. Confirm before refactoring.
- If randomness inside a ViewModel is impossible to seed without a refactor that goes beyond a constructor parameter, ask before going wider.
- If a Service's file-IO is genuinely hard to mock (e.g., does interop directly to Win32). Some IO can stay covered by integration tests instead.
- If existing tests start failing for reasons unrelated to your changes — that may indicate a flake, but it may also indicate Plan 02 left something half-done. Don't paper over it; investigate.
