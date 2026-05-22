# Teacher Toolbox 0.3 Beta Patch Notes

Compared with stable `v0.2`, the current `master` branch is a large update focused on the timer, register reminders, global shortcuts, screen ruler usability, settings, and app stability.

## Headline Changes

- Added a new **Register Reminder** tool for scheduled register prompts.
- Redesigned the **Timer** picker and timer window, including a circular countdown gauge and interval/custom timer improvements.
- Added timer settings for **finish behaviour** and **timer ring colour**.
- Added **Mock Mode** to the Exam Clock, with controls for pausing, nudging time, and testing time-slice alerts.
- Moved global shortcuts into the main Teacher Toolbox process, removing the separate ShortcutWatcher executable.
- Improved the **Screen Ruler**, including a resizable ruler height and clearer open/close/display controls.
- Reworked the app internally around MVVM and dependency injection to improve testability and reduce UI-related crashes.
- Added crash/error logging, diagnostic log export, and a privacy note explaining what is collected.
- Updated the app from .NET 6 / Windows App SDK 1.6 to .NET 8 / Windows App SDK 2.0.

## What Beta Testers Should Focus On

### Register Reminder

Please test the new Register Reminder page thoroughly:

- Enable and disable reminders from the master switch.
- Set reminder times for different days of the week.
- Try labels, blank labels, weekday-only reminders, and unusual combinations of days.
- Check that reminder toast windows appear at the expected time.
- Test **Snooze** and confirm the reminder returns after the configured snooze duration.
- Test **Done** and confirm the same reminder does not repeatedly reappear.
- Try the reminder sound picker and the test sound button.
- Restart the app and confirm reminder settings are saved.

### Timer

Please spend extra time with the redesigned timer:

- Launch 30 second, 1 minute, 2 minute, 3 minute, 5 minute, and 10 minute timers from the Timer page.
- Use the **Custom** timer and check hours/minutes/seconds entry.
- Use the **Interval** timer, add and remove intervals, and confirm interval numbering and transitions are correct.
- Confirm the circular timer ring counts down smoothly and remains readable at different window sizes.
- Pause and resume timers by clicking/tapping the timer window.
- Check that timer window position and size are restored after closing and reopening.
- Test all timer finish behaviours in Settings:
  - Close timer
  - Count up
  - Stay at zero
- Change the timer ring colour in Settings and confirm new timers use it.
- Confirm timer sounds still play at the right times.

### Exam Clock Mock Mode

Mock Mode is new since `v0.2` and should be treated as a priority test area:

- Turn **Mock Mode** on and off and confirm the clock switches between centre number display and mock controls.
- Use the mock time controls to move the clock forwards and backwards.
- Pause and resume the mock clock.
- Create time slices while Mock Mode is enabled.
- Confirm time-slice alerts trigger when mock time reaches the end of a segment.
- Toggle mock sound on and off and confirm alerts respect the setting.
- Check that time slices can still be created, extended, and removed without overlapping incorrectly.
- Switch between light and dark themes and confirm mock controls, clock hands, and time-slice colours remain readable.

### Global Shortcuts

The shortcut listener has been rewritten and now runs inside Teacher Toolbox:

- Press **F9** from another app and confirm Teacher Toolbox focuses and generates a random name.
- Press **Win+0** and confirm it opens a 30 second timer.
- Press **Win+1** through **Win+9** and confirm they open 1 to 9 minute timers.
- Confirm Win+number shortcuts do not also trigger the Windows taskbar action.
- Try shortcuts after closing timer windows, after navigating between pages, and after leaving the app idle.

### Screen Ruler

The screen ruler has had usability and positioning changes:

- Open and close the ruler from the Screen Ruler page.
- Drag the ruler around the screen.
- Use the resize handle at the bottom to make the ruler taller or shorter.
- Confirm the ruler height and position are remembered.
- If using more than one monitor, test moving the ruler between displays.
- Check that the ruler remains on top of other windows and does not get lost off-screen.

### Random Name Generator

The random name generator was moved to a new MVVM page and received UI polish:

- Confirm existing class lists still load correctly.
- Add and remove classes.
- Switch between classes, including when there are more classes than fit directly in the bottom bar.
- Confirm random selections still work and do not immediately repeat the same pupil.
- Check names with punctuation, hyphens, apostrophes, repeated single-letter words, and trailing numeric weighting.
- Confirm the tip/get-started flow appears sensibly for new or empty setups.

### Exam Clock

The exam clock has also been refactored and had visual/theme fixes:

- Open the exam clock in light and dark themes.
- Check that the clock face, hands, and coloured time segments are visible.
- Add, edit, and remove time slices.
- Adjust the current time through the digital time interaction.
- Resize/reopen the clock and confirm its size is remembered.

### Settings, Theme, and Diagnostics

Settings persistence changed significantly, so regression testing matters:

- Switch between system, light, and dark themes.
- Restart the app and confirm theme, sound, timer behaviour, timer colour, window positions, and reminder settings are retained.
- Use **Send Feedback** and **View Feedback** links.
- Use **Save diagnostic logs** and confirm it creates a shareable log ZIP.
- Check that the About section shows the expected app version.

## Stability and Regression Areas

Please report anything unusual in these areas:

- App startup failures or unusually slow startup.
- Crashes when opening, closing, resizing, or dragging timer/ruler/clock windows.
- Settings not saving or resetting after restart.
- Unexpected focus changes caused by global shortcuts.
- Theme inconsistencies between the main window and pop-out windows.
- Any student/class data not migrating cleanly from the previous stable version.

## Technical Notes

- Version updated to `0.3.0`.
- Target framework updated to `.NET 8.0`.
- Windows App SDK updated to `2.0.1`.
- Build remains x86-only.
- The old standalone `ShortcutWatcher` project has been removed.
- Unit and integration test projects have been reorganised and expanded.
- New local/cloud telemetry services log technical crash details; student names, class names, and user files are not intentionally collected.
