# Plan 02 — Decouple ViewModels from WinUI types

## Why

The ViewModels expose WinUI-specific types (`SolidColorBrush`, `ElementTheme`) directly. This is the root cause of the failed test attempts captured in `clock-viewmodel-test-fixes.patch` — `SolidColorBrush` cannot be constructed in a plain unit-test process because it requires a live WinUI dispatcher. Today's tests work around this by injecting `null` and checking the ViewModel "handles it gracefully" — but that just hides the coupling and makes ViewModels harder to test, not easier.

The fix is to make ViewModels expose theme-aware data in a UI-neutral form (e.g., a `Color` struct, an enum, or a string) and let the XAML layer convert it to a brush via a value converter or binding expression. That way ViewModels are pure C# logic that any test runner can construct.

Concrete leakage observed:

- `ClockViewModel.cs:47` — `private SolidColorBrush _handColorBrush;`
- `ClockViewModel.cs:117` — `public SolidColorBrush HandColorBrush { get; }`
- `ClockViewModel.cs:350` — `HandColorBrush = new SolidColorBrush(isDarkTheme ? Colors.White : Colors.Black);`
- `TimerWindowViewModel.cs:53-54, 114, 120, 185, 452-465, 501-507, 559-570` — heavy `SolidColorBrush` use including `TimerTextColor` and `TrailBrush` properties.
- `RandomNameGeneratorViewModel.cs:8` — uses `Microsoft.UI.Xaml`.
- `SettingsViewModel.cs:7` — uses `Microsoft.UI.Xaml`.

## Scope

**In:**
- Replace `SolidColorBrush` properties on `ClockViewModel` and `TimerWindowViewModel` with UI-neutral equivalents.
- Add a value converter (or use existing `Microsoft.UI.Xaml.Media.SolidColorBrush` from XAML) to convert the new property type to a brush in the bound XAML.
- Audit the other two ViewModels (`RandomNameGeneratorViewModel`, `SettingsViewModel`) and remove `Microsoft.UI.Xaml` usings if they aren't actually needed; if they are, document why or refactor.

**Out:**
- Plan 06 will add tests that exercise the now-decoupled ViewModels.
- Removing all WinUI dependencies — `IThemeService` itself can stay coupled to WinUI; only ViewModels are in scope.
- `IntervalTimerViewModel` and `TimerPageViewModel` unless inspection reveals the same problem.

## Approach (recommended)

For each color/brush property on a ViewModel:

1. Replace the `SolidColorBrush` property with a `Windows.UI.Color` (a struct — safe to construct in tests) or an enum/string identifier the converter understands.
2. In the XAML that binds to that property, change the binding to go through a `ColorToBrushConverter` (create one in `TeacherToolbox/Converters/` if it doesn't exist).
3. Update any code in the ViewModel that *consumes* the brush (rare) to consume the new type instead.

Example shape — adapt to the actual code:

```csharp
// Before
public SolidColorBrush HandColorBrush { get; private set; }

// After
public Windows.UI.Color HandColor { get; private set; }
```

```xml
<!-- Before -->
<Path Fill="{x:Bind ViewModel.HandColorBrush, Mode=OneWay}" />

<!-- After -->
<Path Fill="{x:Bind ViewModel.HandColor, Mode=OneWay, Converter={StaticResource ColorToBrushConverter}}" />
```

If a value converter approach is impractical for a specific binding (e.g., a binding inside a template that doesn't have access to a converter), prefer `IValueConverter` registered as an app resource over keeping the brush on the ViewModel.

## Steps

1. **Audit.** Grep for `SolidColorBrush|Microsoft\.UI` in `TeacherToolbox/ViewModels/*.cs`. Catalog every public property and every internal field that uses these types. Confirm the list above is complete.

2. **Add `ColorToBrushConverter`** in `TeacherToolbox/Converters/` (check whether one already exists first) — a simple `IValueConverter` that turns `Windows.UI.Color` into `SolidColorBrush`. Register it in `App.xaml` resources or the relevant page resource dictionary so XAML can reference it as `{StaticResource ColorToBrushConverter}`.

3. **Refactor `ClockViewModel`.**
   - Change `_handColorBrush`/`HandColorBrush` to `_handColor`/`HandColor` of type `Windows.UI.Color`.
   - Update any setters/initializers (line 350 area).
   - Update the bound XAML in `Controls/Clock.xaml` to use the converter.

4. **Refactor `TimerWindowViewModel`.**
   - Apply the same pattern to `TimerTextColor` and `TrailBrush` (rename `TrailBrush` to `TrailColor`).
   - Update bindings in `Controls/TimerWindow.xaml`.
   - Note: line 569-570 has a cast `purpleBrush as SolidColorBrush` — that suggests pulling a brush from a resource dictionary. After refactoring, this should pull a `Color` instead, or be removed if unused.

5. **Audit `RandomNameGeneratorViewModel.cs:8` and `SettingsViewModel.cs:7`.** Determine whether the `Microsoft.UI.Xaml` using is actually necessary. If a single property uses `Visibility`, that is also UI-bound and should be replaced with `bool` + a `BoolToVisibilityConverter`. If the using is unused, remove it.

6. **Build and run.** `dotnet build` must succeed. Manually run the app and verify the clock hand and timer text/trail colors still render correctly in both light and dark themes.

7. **Run existing tests.** They should still pass. The brittle `_mockThemeService.Setup(t => t.GetHandColorBrush()).Returns(...)` lines may need updating to the new contract — make the change.

## Acceptance criteria

- [ ] No `using Microsoft.UI.Xaml*` in any file under `TeacherToolbox/ViewModels/`.
- [ ] No `SolidColorBrush` references in any file under `TeacherToolbox/ViewModels/`.
- [ ] App still launches; clock and timer windows render colors correctly in light + dark themes.
- [ ] Existing unit tests still pass.
- [ ] A `Windows.UI.Color`-typed property can be constructed in a unit test without any WinUI dispatcher (verify by writing one throwaway `[Test]` that does `var vm = new ClockViewModel(...)` — this should succeed where it previously didn't).

## Stop and ask

- If a binding uses the brush in a way that the converter approach can't replicate (e.g., as part of a `Storyboard` animation targeting `SolidColorBrush.Color`). Storyboards animating brushes need different handling.
- If `IThemeService` exposes brushes back to ViewModels — the service interface may also need adjusting. Decide jointly whether the service returns `Color` vs `SolidColorBrush`.
- If the audit reveals more leakage in `IntervalTimerViewModel` / `TimerPageViewModel` than expected (e.g., `Visibility`, `GridLength`, `Thickness`). Those are also UI types but their replacement may be larger than this plan covers.
