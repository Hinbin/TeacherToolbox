# Privacy and Data Collection

This document explains what data is collected by Teacher Toolbox to help improve the application and fix crashes.

## Telemetry and Crash Reporting

We use a secure Google Apps Script (which saves to a private Google Sheet) and local file logging to capture application crashes and errors. This allows us to notice and fix problems that might occur during lessons.

### What is captured:
- **Exception Details:** Type of error, error message, and the stack trace (the line of code where the error occurred).
- **Application Context:** The part of the app that was being used when the error occurred (e.g., "Timer", "Random Name Generator").
- **App Version:** Which version of Teacher Toolbox you are running (e.g., "0.3.0").
- **Operating System:** Windows version (e.g., "Windows 10.0.19041").
- **Basic System Info:** CPU architecture (e.g., X64) and memory usage at the time of the crash.

### What is NOT captured:
- **Student Data:** We NEVER capture student names, class names, or any data from your student lists.
- **Personal Information:** We do not collect your name, email address, or any other identifying information.
- **User Files:** We do not access or upload any files from your computer, except for the log files when you manually choose to "Save diagnostic logs".
- **Settings:** Your specific application settings are not captured unless they are directly related to a crash.

## Diagnostic Logs

If you experience an issue, you can use the **"Save diagnostic logs"** button in the Settings page. This will create a ZIP file containing the recent error logs from your computer. You can then choose to share this ZIP file with the developer to help diagnose a problem.

These logs only contain the technical information listed above.

## Data Retention

- **Local Logs:** Stored on your computer in `%LocalAppData%\TeacherToolbox\logs\`. We keep the last 7 days of logs.
- **Crash Reports (Google Sheets):** Exception reports sent to the school's Google Sheet are retained according to the school's data management policies.
