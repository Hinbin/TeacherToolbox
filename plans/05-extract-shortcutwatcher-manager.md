# Plan 05 — Extract ShortcutWatcherManager from MainWindow

## Why

`MainWindow.xaml.cs` is **1448 lines** and owns far too many responsibilities:

- ShortcutWatcher process lifecycle (start, monitor, restart with backoff, max-3-attempts cap)
- Named-pipe IPC: connect, listen for keypress messages, dispatch to UI
- Window positioning across multiple monitors (DPI-aware)
- NavigationView wiring + theme updates
- Custom title bar regions

The first two of those — process lifecycle and pipe IPC — are about **half the file** by line count (constructor-side initialization in lines 62-102, then `StartShortcutWatcher` in 552-658, `VerifyPipeConnectionAsync` in 124-203, `ListenForKeyPresses` in 893-1115, plus restart watchdog state scattered throughout: `pipeLock`, `isPipeListenerRunning`, `pipeFailedChecks`, semaphores).

For a single developer maintaining a 500-user app, this is the single biggest barrier to safely changing anything. Every feature change risks touching shortcut-watcher concerns by accident, and the watcher logic itself is impossible to unit-test because it lives in a `Window` subclass.

## Scope

**In:**
- Extract a new service `IShortcutWatcherService` + `ShortcutWatcherManager` implementation that owns:
  - Launching the `ShortcutWatcher.exe` child process.
  - The watchdog timer + restart-with-cap logic.
  - Named-pipe connection and listening loop.
  - Parsing pipe messages into typed events.
- Expose an event (`event EventHandler<ShortcutPressedEventArgs> ShortcutPressed`) that `MainWindow` subscribes to.
- Register the service in DI as a singleton; start it from `App.OnLaunched` (or from `MainWindow` constructor if process lifetime needs to track main window) and stop it on app exit.

**Out:**
- Replacing the keyboard-hook implementation inside `ShortcutWatcher.exe` itself. That stays as-is.
- The window-positioning, theme, and title-bar logic in `MainWindow`. Those are real concerns of `MainWindow` and don't need extracting now.
- Switching IPC mechanism away from named pipes.

## Design

```csharp
namespace TeacherToolbox.Services;

public interface IShortcutWatcherService : IAsyncDisposable
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    bool IsRunning { get; }
    event EventHandler<ShortcutPressedEventArgs> ShortcutPressed;
    event EventHandler<WatcherHealthChangedEventArgs> HealthChanged; // optional, for diagnostics
}

public class ShortcutPressedEventArgs : EventArgs
{
    public int Number { get; init; }   // 0–9
    public DateTimeOffset PressedAt { get; init; }
}
```

Internally `ShortcutWatcherManager`:
- Holds the `Process` instance and a `CancellationTokenSource` for shutdown.
- Has its own watchdog `Timer` (or a `PeriodicTimer` in a background `Task`).
- Maintains the restart-attempt counter and the pipe-connection state.
- Calls `_telemetry.LogWarning(...)` (Plan 03) when restart attempts hit the cap.
- Raises `ShortcutPressed` on the captured `DispatcherQueue` so subscribers can mutate UI safely.

`MainWindow` becomes the subscriber:
```csharp
public MainWindow(..., IShortcutWatcherService shortcutWatcher)
{
    _shortcutWatcher = shortcutWatcher;
    _shortcutWatcher.ShortcutPressed += OnShortcutPressed;
}

private void OnShortcutPressed(object sender, ShortcutPressedEventArgs e)
{
    // Existing code that triggers timers based on number 0–9 lives here.
}
```

## Steps

1. **Read the relevant chunks of `MainWindow.xaml.cs` first**:
   - Lines 62-102 (constructor-side init)
   - Lines 124-203 (`VerifyPipeConnectionAsync`)
   - Lines 205-316 (watchdog)
   - Lines 552-658 (`StartShortcutWatcher`)
   - Lines 893-1115 (`ListenForKeyPresses`)
   - Anywhere `pipeLock`, `isPipeListenerRunning`, `pipeFailedChecks`, or the `_shortcutProcess`-equivalent field is used.
   Catalog the state and the methods that read/write it.

2. **Create `Services/IShortcutWatcherService.cs` and `Services/ShortcutWatcherManager.cs`** with the design above. Move the relevant code into the manager. Replace direct `Debug.WriteLine` in error paths with `ITelemetryService` calls (depends on Plan 03 — if that's not done yet, leave `Debug.WriteLine` for now).

3. **Define `ShortcutPressedEventArgs`** and the parsing logic that today turns pipe bytes into "number N pressed". Keep the wire format identical — `ShortcutWatcher.exe` is unchanged.

4. **Register in DI** at `App.xaml.cs:29-56`:
   ```csharp
   services.AddSingleton<IShortcutWatcherService, ShortcutWatcherManager>();
   ```

5. **Start the service** in `App.OnLaunched` after MainWindow is constructed, or in MainWindow's constructor (depends on whether the watcher should run while MainWindow is closed — current behavior ties it to MainWindow lifetime, so start it in MainWindow constructor and call `StopAsync` in `Closed` event).

6. **Subscribe to the event in `MainWindow`**, in place of the inline pipe-reading code. The handler dispatches to whatever the existing inline code did (showing a timer with N seconds).

7. **Delete the now-unused fields and methods from `MainWindow.xaml.cs`**: `pipeLock`, `isPipeListenerRunning`, `pipeFailedChecks`, the watchdog timer field, `StartShortcutWatcher`, `VerifyPipeConnectionAsync`, `ListenForKeyPresses`, etc. Be careful — some of these might be referenced from XAML (unlikely for these private members, but check).

8. **Manual smoke test:**
   - App launches.
   - `ShortcutWatcher.exe` shows up as a child process in Task Manager.
   - Win+1 through Win+9 trigger timers.
   - Win+0 triggers the 30-second timer.
   - Kill `ShortcutWatcher.exe` from Task Manager. Within ~30 seconds it should be restarted automatically. After 3 kills in quick succession, no further restart attempts should be made (and a telemetry warning should be logged).
   - App close: `ShortcutWatcher.exe` should also exit.

9. **Verify line count.** `MainWindow.xaml.cs` should drop by roughly 400-500 lines. If it didn't, something was left behind.

10. **Add a basic unit test** for `ShortcutWatcherManager` if feasible — at minimum, a test that asserts `ShortcutPressed` is raised when bytes matching the wire format are written to a test pipe. This is a stretch goal; if the pipe code is hard to test, defer to Plan 06.

## Acceptance criteria

- [ ] `IShortcutWatcherService` exists and is registered in DI.
- [ ] `MainWindow.xaml.cs` no longer contains any reference to `Process`, named pipes, or the watchdog timer for the watcher.
- [ ] `MainWindow.xaml.cs` line count drops by >300 lines.
- [ ] All existing keyboard shortcuts (Win+0..9) still work.
- [ ] Killing the watcher process triggers a restart within 30s, with a 3-attempt cap.
- [ ] App exit cleanly terminates the watcher (no orphan process — verify in Task Manager).
- [ ] Existing tests still pass.

## Stop and ask

- If the pipe protocol isn't a clean line-based or length-prefixed format and instead relies on ordering side-effects with the watchdog. The extraction should preserve behavior, but a tangled protocol may need a small rewrite.
- If `MainWindow` accesses watcher state from XAML event handlers in non-obvious ways (e.g., a button that says "restart shortcuts"). Those need re-wiring through the new service.
- If the existing restart logic has subtle behavior (e.g., backoff calculated from kernel time, hardcoded paths to `ShortcutWatcher.exe` resolved relative to the install location) that breaks when moved out of `MainWindow` — those must be carried over verbatim.
