# Architectural Improvement Plans

These plans were drafted by Opus after an architectural review of Teacher Toolbox. They are intended to be executed one at a time by Sonnet.

## Context for the implementer

- **Project**: WinUI 3 / .NET 8.0 desktop app for ~500 teachers across 30 schools.
- **Single developer.** Code must stay readable and changes should be small enough to review without help.
- **Two top-line constraints**: must be auto-testable, must be easy to add/remove features.
- Read [CLAUDE.md](../CLAUDE.md) at the project root for build commands and architecture overview before starting any plan.

## Order

Plans are numbered. The numbering reflects suggested execution order, not strict dependency.

| # | Plan | Effort | Depends on |
|---|------|--------|------------|
| 01 | [Cleanup: stray patch and dead test project](01-cleanup-stale-artifacts.md) | XS | — |
| 02 | [Decouple ViewModels from WinUI types](02-decouple-viewmodels-from-winui.md) | M | 01 |
| 03 | [Add crash telemetry](03-add-crash-telemetry.md) | S | — |
| 04 | [Constructor-inject ViewModels into Pages](04-constructor-inject-viewmodels.md) | S | — |
| 05 | [Extract ShortcutWatcherManager from MainWindow](05-extract-shortcutwatcher-manager.md) | L | — |
| 06 | [Backfill unit tests for ViewModels and Settings](06-backfill-unit-tests.md) | M | 02, ideally 04 |

`01` should run first — it removes confusing artifacts that will trip up later work.
`02` is a prerequisite for `06` because ViewModels currently expose `SolidColorBrush`, which can't be constructed in unit tests.
`03`, `04`, `05` are independent of each other and of the others.

## How each plan is structured

Each plan contains:
- **Why** — the motivation, in one paragraph.
- **Scope** — what's in and what's explicitly out.
- **Steps** — ordered, with file paths and line numbers where useful.
- **Acceptance criteria** — how to know it's done.
- **Stop and ask** — situations where the implementer should pause for the human, not improvise.

Always run a build (`dotnet build TeacherToolbox/TeacherToolbox.csproj -p:Platform=x86`) and the existing test suite before declaring a plan done.
