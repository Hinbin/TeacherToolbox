# Plan 04 — Constructor-inject ViewModels into Pages

## Why

DI is configured properly in `App.xaml.cs:29-56`, but Pages and Windows reach into the global service locator via `App.Current.Services.GetRequiredService<T>()` to retrieve their ViewModels and services:

- `Controls/Clock.xaml.cs:52, 66` (theme + ViewModel)
- `Controls/RandomNameGeneratorPage.xaml.cs:23` (ViewModel)
- `Controls/SettingsPage.xaml.cs:19, 23-24` (ViewModel + theme)
- `Controls/ScreenRulerWindow.xaml.cs:33-35` (settings + theme)
- `Controls/TimerWindow.xaml.cs:74-76` (settings + theme)
- `MainWindow.xaml.cs:67-71` (settings + sleep preventer + theme)

This is the **service-locator anti-pattern**. It works at runtime, but it:

1. Hides dependencies — looking at a class signature gives no hint what services it needs.
2. Makes the classes hard to substitute in tests, because there is no way to inject a fake.
3. Couples every Page tightly to `App.Current`, which means the navigation system can't be tested without spinning up the whole app.

WinUI 3 does not yet have built-in DI for `Page` and `Window` constructors (the navigation framework calls the parameterless constructor). The pragmatic workaround is to:
- Keep parameterless constructors so navigation works,
- But do dependency resolution in **one** central spot (e.g., a `ViewModelLocator` or a `Page` factory), and resolve once-per-page-creation rather than scattering `App.Current.Services.GetRequiredService<T>()` calls through code-behind.

## Scope

**In:**
- Add a `ViewModelLocator` (or extend an existing pattern) so each Page exposes `ViewModel` as a property whose value is resolved once, in one place.
- Reduce the per-Page service-locator calls to a single line per Page.
- Change `MainWindow.xaml.cs:67-71` to take services through a constructor where feasible (it's instantiated by `App.OnLaunched`, so we control its construction).

**Out:**
- Replacing the navigation system entirely.
- Plan 05's extraction of `ShortcutWatcherManager` (separate work).

## Approach

WinUI 3 navigation calls `Activator.CreateInstance` on the page type. Two acceptable patterns:

**Pattern A — `ViewModelLocator` resource**
```csharp
public class ViewModelLocator
{
    public ClockViewModel Clock => App.Current.Services.GetRequiredService<ClockViewModel>();
    public SettingsViewModel Settings => App.Current.Services.GetRequiredService<SettingsViewModel>();
    // ... one property per ViewModel
}
```
Register as a resource in `App.xaml`:
```xml
<Application.Resources>
    <local:ViewModelLocator x:Key="Locator" />
</Application.Resources>
```
Then in each Page's XAML:
```xml
<Page DataContext="{Binding Source={StaticResource Locator}, Path=Clock}" ...>
```
This puts all the locator calls in one file. The Pages themselves don't reference `App.Current` at all.

**Pattern B — Factory delegate registered in DI**
```csharp
services.AddSingleton<Func<Type, Page>>(sp => type => (Page)ActivatorUtilities.CreateInstance(sp, type));
```
Then have the navigation code use this delegate to construct Pages with constructor injection.

**Pick Pattern A.** It is simpler, requires no changes to navigation, and the `App.Current.Services` calls live in exactly one file (the Locator), where they are honest about what they are.

## Steps

1. **Create `ViewModels/ViewModelLocator.cs`** with one property per registered ViewModel:
   - `Clock`
   - `Settings`
   - `TimerWindow`
   - `RandomNameGenerator`
   - (any others you find in `App.xaml.cs:52-55`)

2. **Register the locator as a resource** in `App.xaml`:
   ```xml
   <vm:ViewModelLocator x:Key="Locator" />
   ```
   (Add the `vm:` namespace alias.)

3. **For each Page that currently does `ViewModel = App.Current.Services.GetRequiredService<...>()`:**
   - Remove that line from code-behind.
   - In the Page's XAML, set `DataContext="{Binding Source={StaticResource Locator}, Path=Clock}"` (etc).
   - Update bindings inside the Page from `{x:Bind ViewModel.Foo}` (which uses the codebehind property) to `{Binding Foo}` if needed — or keep the codebehind `ViewModel` property as `(MyViewModel)DataContext` getter so existing `{x:Bind}` bindings still work.

   **Note on `x:Bind`:** `x:Bind` requires the property to be on the page, so if you keep `{x:Bind}` you'll keep a thin `public ClockViewModel ViewModel => (ClockViewModel)DataContext;` getter. That's fine — it's no longer reaching into the locator from code.

4. **For services (not ViewModels) used in code-behind** (e.g., `_themeService`, `_settingsService` in `Clock.xaml.cs`, `SettingsPage.xaml.cs`, etc.): these need to stay code-resolved because Pages can't take constructor parameters easily. Move them onto a base class:

   - Create `Controls/AutomatedPage.cs` if the existing one doesn't already do this (it might — check what `AutomatedPage.cs` currently is).
   - Add protected `Services`, `ThemeService`, `SettingsService` properties resolved once on construction.
   - Have Pages inherit from this base. Replace per-page `App.Current.Services.GetRequiredService<...>()` calls with `ThemeService` / `SettingsService` references.

5. **`MainWindow.xaml.cs`** is the easiest case because `App.OnLaunched` constructs it. Change its constructor to accept the three services it needs:
   ```csharp
   public MainWindow(ISettingsService settingsService, ISleepPreventer sleepPreventer, IThemeService themeService)
   ```
   Then in `App.xaml.cs:60`:
   ```csharp
   MainWindow = ActivatorUtilities.CreateInstance<MainWindow>(Services);
   ```
   Remove lines 67-71 of `MainWindow.xaml.cs`.

6. **Audit.** Re-grep for `App\.Current\.Services` across the whole solution. The only file allowed to contain it is `ViewModels/ViewModelLocator.cs` (and the base page class if you went that route). If it appears anywhere else, those calls were missed.

7. **Run the app manually.** Click through every page (Clock, Timer, RNG, Settings, Screen Ruler). Each must render and function. The DataContext binding is the easy thing to silently break.

8. **Run tests.** Existing tests must still pass.

## Acceptance criteria

- [ ] `App.Current.Services` appears in at most two files (`ViewModelLocator.cs` and one base-page class if used) — verified by grep.
- [ ] `MainWindow` takes services via constructor.
- [ ] Every Page still loads correctly when navigated to.
- [ ] No regressions in existing tests.
- [ ] Adding a new ViewModel requires: register in `App.xaml.cs` + add a property to `ViewModelLocator`. Nothing else.

## Stop and ask

- If `x:Bind` bindings in a Page rely on type information that breaks when DataContext is set via XAML resource — sometimes the compiler complains. There are workarounds; ask if blocked.
- If `AutomatedPage.cs` already does something specific. It might already be the base class and have a different purpose.
- If the navigation framework uses a custom mechanism (e.g., `Frame.Navigate(typeof(...))` with a parameter) — Pattern A still works but worth checking.
