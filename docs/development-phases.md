# Banccoon Development Phases

Banccoon is a private, offline-first financial forecasting desktop app. The foundation is now mostly in place: local SQLite data, forecasting, recurrence, import/export, statement parsing, category learning, reconciliation services, savings goals, credit-card projections, and a first MAUI desktop UI are already present.

The roadmap now shifts from “build the primitives” to “make the app guide the user through real financial workflows.”

## Completed Foundation

- Solution split into Core, Infrastructure, App, and Tests.
- Domain models for accounts, categories, transactions, scheduled transactions, savings goals, settings, recurrence, statement imports, reconciliation, and credit-card details.
- Forecasting and recurrence services with test coverage.
- Local SQLite repositories for saved financial data.
- JSON backup/export, validation, merge/replace import, and guarded local-data reset.
- Desktop shell with dashboard, accounts, transactions, statements, scheduled items, goals, forecast, reconciliation, analytics, data, and preferences surfaces.
- Savings-goal reservations and credit-card payoff/obligation calculations.
- Bank statement import foundation with parser registry, Sberbank debit-card parser, pending import batches/rows, duplicate warnings, category suggestions, local category learning, and row approve/skip.
- Basic reconciliation/check-in services and UI for expected scheduled items, actual balance comparison, grouped spending, and balance adjustments.
- Dashboard account-total controls and projected-balance chart.

## Current Product Direction

The app should no longer assume users manually build everything first. On first startup (empty db), Banccoon should guide the user into setup through a focused in-app overlay. The first useful setup paths should be bank-statement import, manual setup, and backup restore from exports.

Large workflows should become app-guided experiences rather than permanent top-level tabs: quick edits are modal overlays, but multi-step flows (statement import, reconciliation) are dedicated full pages, not modals. See `docs/ui-structure-decisions.md` for the full structural spec — it is the source of truth for information architecture and interaction patterns; this file governs implementation phasing only.

The dashboard's headline number is a **"Free to spend" figure** built on the existing `AvailableToSpendService`, extended with a user-set safety-buffer reserve and selectable window modes (rolling days / calendar-aligned / dynamic-until-next-major-payment) — see "Money Model: Free to Spend" in `docs/ui-structure-decisions.md` for the full spec. Not yet assigned to a specific phase below; fold into dashboard-rework phasing when that work is scheduled.

## Implementation Progress

- **Done**: old `MainPage.xaml`/`FinanceDataViewModel.cs`/`ShellViewModel.cs`/`NavigationItemViewModel.cs`/`AppSection.cs` deleted. New `AppShell.xaml` has the real 4-tab `TabBar` (Dashboard/Transactions/Accounts/Settings), each routing to its own page + view model, registered in `MauiProgram.cs`. `App.xaml` now carries the real light/dark color tokens from `docs/visual-design-language.md` (not the old ad-hoc green palette), applied via `AppThemeBinding`.
- **Done**: Dashboard's hero "Free to spend" card + "how it's calculated" disclosure are wired to the real `IAvailableToSpendService`/`IForecastService`/`IAccountRepository`/`ISavingsGoalRepository` — not mock data. The balance/forecast chart reuses the existing `ForecastChartView`/`ForecastChartDrawable` control (it already had current-date marker, projection line, event dots, selected-point callout — genuinely well-built, not part of the old god-object problem) and plots real `ForecastResult.ProjectedBalances`. Small reusable formatting helpers (`Formatting/MoneyFormat.cs`, `AccountNumberFormat.cs`, `DateDisplay.cs`, `DisplayText.cs`) were extracted from the old monolith rather than duplicated.
- **Done**: Accounts page lists real, non-archived accounts from `IAccountRepository` (read-only for now — no create/edit/archive/drag-reorder/primary flag yet).
- **Done**: Settings page has a real, working Light/Dark/System control that persists via `ISettingsRepository` and applies immediately via `Application.Current.UserAppTheme` — this is the single global theme control; no per-screen theme toggle exists (that was a mockup-only convenience, not a design decision).
- **Stub only, not yet built**: Transactions page (just a placeholder — none of the resolve-upcoming widget, filter bar, add menu, select mode, or row content is wired yet). Dashboard's Upcoming/Forecast/Analytics/Goals sections are placeholder cards. Accounts has no create/edit/archive/goal-progress/credit-card-details/primary-account UI yet.
- **Not yet in Core, deliberately deferred rather than improvised**: the safety-buffer reserve and the three free-to-spend window modes (rolling/calendar/dynamic) from `docs/ui-structure-decisions.md` aren't implemented — `AvailableToSpendService` still only does `LowestForecastedBalance − ReservedForSavingsGoals`, and the forecast window is still the existing `ForecastPeriod` day-count enum. These need a deliberate `AppSettings`/SQLite schema decision (new columns, migration) rather than being added silently mid-session.
- The chart currently plots forecast-only points (today forward); historical points derived from persisted transactions (the old code had this) are not wired back in yet.
- Verified: `dotnet build src/Banccoon.App/Banccoon.App.csproj` succeeds with 0 warnings/errors, and all 86 existing tests in `Banccoon.Tests` still pass (they only touch Core/Infrastructure, untouched by this work). Not yet verified: actually running the Windows app and looking at it — that needs a human (or a Windows GUI-capable tool) to check, it wasn't possible to visually verify from this environment.

## Phase 0: Navigation Simplification

- Remove the account edit panel
- Make account fields editable when the user enters editing mode and selects an account to edit.
- Show per-account edit actions only in edit mode or as clear row actions.
- Format money and other numeric amounts with three-digit separators in display and sensible numeric parsing in inputs.
- Format account numbers (and in their input fields) in four-digit groups.
- Mask account numbers by default everywhere outside an active edit/reveal context.
- Add a deliberate reveal/hide action for full account numbers (open/closed eye icon).
- Avoid showing full account numbers in status text, summaries, dashboard cards, and import match messages.
- Make the dashboard graph default to the past 7 days of account-balance history plus the saved default forecast period.
- Draw a clear current-time marker between historical account changes and future projected balances.
- Derive historical dashboard graph points from persisted transactions and dashboard-included accounts, including account totals after each relevant day or transaction.
- Expose `Account.IncludeInDashboardTotals` (already exists in Core) as a toggle in account editing — e.g. for a savings account that shouldn't count toward the dashboard total.
- Add a primary-account concept (new: `AppSettings.PrimaryAccountId` or `Account.IsPrimary`), settable from account editing. Used as the default pre-selected account in Expense/Income/Transfer overlays.

## Phase 0.1: Navigation Simplification

- Combine Preferences and Data into one Settings section.
- Preserve all existing preferences, backup/export, import/restore, and delete-all-local-data options.
- Move navigation tab/rail style controls into Settings only.
- Remove the always-present Rail/Tabs controls from the global header.
- Persist appearance/navigation preferences properly instead of keeping them as shell-only runtime state.
- Goals should be treated as an account in the DB.
- Keep Data functionality inside Settings.
- Keep tab navigation focused on everyday destinations: Dashboard, Transactions, Accounts, and Settings. Scheduled-*template* management (create/edit recurring rules) moves into Settings, not into Transactions. Statement import and reconciliation become full-page flows launched from Transactions, not tabs. Forecast and Analytics go into dashboard as collapsable fields, in order Upcoming → Forecast → Analytics → Goals (customizable in Settings).
- Remove Statements and Reconciliation as permanent top-level tabs once their guided full-page flows exist.
- Keep workflow launch buttons where users naturally need them rather than forcing users to hunt for special tabs.

## Phase 1: Shared Workflow Overlay And Full-Page Flow Architecture

**The entire `Banccoon.App` project (Views and ViewModels) is being rebuilt from scratch, not refactored.** `MainPage.xaml` (1,510 lines, every screen in one file) and `FinanceDataViewModel.cs` (5,311 lines, every screen's logic in one class) are discarded outright once their replacements exist — nothing from the old UI layer carries forward. `Banccoon.Core` and `Banccoon.Infrastructure` are unaffected; the financial/domain logic there stays as-is. Every file in the new App layer stays small and single-purpose (one view, one view model, one component per file) — see the standing "keep files small" rule; do not let any new file grow into a second god-object.

- Add a reusable in-app modal/workflow host inside the existing MAUI shell, not a separate OS window, for quick-edit overlays (account editing, category creation, create/edit transaction, create/edit scheduled template, create/edit savings goal, backup restore validation, destructive confirmations).
- Add a separate reusable full-page flow host/pattern for multi-step workflows that need real screen space: statement import and reconciliation. Same busy/error/step-transition support as the overlay host, but navigated to rather than dimmed-background-modal.
- Support dimmed background, focused content, close/cancel rules, busy/error states, and simple step transitions in the overlay host; confirm-before-discard on cancel only when there's unsaved progress.
- Startup setup remains a blocking full-screen overlay (not the same as the statement-import/reconciliation full pages) — see Phase 2.
- One view model per feature/screen from the start (e.g. `DashboardViewModel`, `TransactionsViewModel`, `AccountsViewModel`, `SettingsViewModel`, plus overlay-specific ones) — never a shared catch-all view model.

## Phase 2: Blank-State Startup And Guided Setup

- Detect a genuinely blank local dataset after startup load.
- Show a setup overlay before the normal dashboard workflow.
- Offer setup choices: import a bank statement, set up manually, and restore a Banccoon backup.
- Route bank-statement setup into the statement import workflow.
- Route manual setup into account creation without exposing the full Accounts edit UI.
- Route backup restore into the existing import/restore services with validation first.

## Phase 2.5: Transaction Screen Redesign

- Rebuild Transactions as a history-first screen after the shared overlay host exists.
- Remove permanent create/edit transaction panels once their overlay replacements are available.
- Add a plus action with **Expense, Income, and Transfer only**. Statement import gets its own separate header button next to the plus action, not a menu item inside it.
- Expense and Income overlays should collect transaction name, account, date, amount, category, and optional assignment to an existing scheduled occurrence.
- Transfer overlay should collect transaction name, amount, date, outgoing account, incoming account or goal, and optional assignment to an existing scheduled occurrence.
- Scheduled *template* creation/editing (recurring rules) is **not** reachable from the plus action; it moves to Settings (see Phase 0.1).
- Scheduled assignment from Expense, Income, or Transfer creates only an occurrence link to an existing scheduled transaction; it must not create a new scheduled template.
- Statement import should route into the dedicated statement-import full-page workflow.
- Add a first-class `Transaction.Name` field and persist it through SQLite, import/export, statement-created transactions, and tests.
- Persist `PaidScheduledTransactionId` and `PaidScheduledOccurrenceDate`; the model and forecast service already expect them, but SQLite persistence must read and write them.
- Transaction history rows show name, category (or transfer destination), and amount, with **account balance-after in gray directly under the amount** and a **small scheduled-mark icon** on the row — all visible without expanding or opening edit.
- In edit mode, keep name locked, make category a dropdown, make scheduled assignment editable, show a trash action, and allow amount and account-value-after edits.
- Editing account value after a transaction should recalculate that transaction's amount.
- Add multi-select actions via an explicit "Select" mode toggle button (not hover-checkboxes or long-press): mass category assignment, mass scheduled-occurrence assignment, and mass deletion.
- Add a collapsed filter bar (account, category, date range, type) above the list that expands on demand.
- Add a persistent "resolve upcoming" widget pinned at the top of the screen: one row per due/overdue scheduled item with Mark Paid / Skip / Delay actions. Mark Paid pre-fills a transaction from the template; nothing here is auto-dismissed — it persists until the user resolves it.
- Add a category-management icon in the header (next to the filter control) opening a manage-categories overlay (rename, merge, delete, recolor, reorder). Category *creation* stays inline wherever a category is picked (transaction entry, statement import row, scheduled assignment) — this is deliberately not part of Settings.
- Add a manual reconciliation "check-in" action in the header, grouped with the filter and category-management controls.

## Phase 3: Bank Statement Import Workflow Redesign

- Replace the Statements tab workflow with a guided **full page**, not a modal overlay, launched from blank setup or the Transactions header import button. **Not** offered as a dashboard action/entry point.
- Step 1: pick/read statement and show detected balance, account number, card ending, parser, period, and row count.
- Step 2: confirm account match or create/link an account.
- When creating an account from a statement, default the starting balance from the parsed closing/current balance when available, falling back only if needed.
- Step 3: review pending transaction rows in a compact list that gets shorter as rows are approved or skipped.
- Default uncategorized rows to `Other`, but make category selection/creation fast.
- Add multi-select so multiple rows can be categorized, skipped, or attached to the same scheduled transaction together.
- Add a bulk "approve all matching category X" action that surfaces the matched rows for review before committing them, rather than silently mass-approving.
- Keep duplicate warnings visible before approval.
- Preserve local category learning when rows are approved.
- Parser resolution order: try the configured default parser → fall back to auto-detection across `IStatementParserRegistry.AvailableParsers` (already built) → on total failure, show a clear "format not recognized" message with a path to flag it for a new parser.
- **High-priority backlog: bank parser coverage.** Sberbank exists. Tinkoff and Alphabank are next, confirmed must-have — each needs a real statement sample from an actual account holder before the parser can be built/verified.

## Phase 4: Scheduled Transaction Matching From Imports

- Allow imported rows to be attached to an existing scheduled transaction instead of only a category.
- After choosing a scheduled transaction, ask which scheduled period/occurrence the payment belongs to.
- Record the created transaction as linked to that scheduled occurrence.
- Mark the occurrence as paid so forecasts include or suppress the correct event, including future-dated payments that are already paid.
- Keep the scheduled transaction’s category as the default category for that imported row.
- Add tests for paid occurrence handling, future paid occurrences, and duplicate prevention.

## Phase 5: Guided Reconciliation

- Move reconciliation out of the main navigation and into a guided **full page**, not a modal overlay.
- Trigger reconciliation automatically right after statement import, plus a manual "check-in" action in the Transactions header, available anytime.
- Reuse the same "shrinking list" interaction pattern used by the Transactions "resolve upcoming" widget for expected scheduled items: confirm, delay, skip, or attach actual imported/manual transactions — one consistent interaction language across the app.
- Keep actual-balance comparison, grouped spending, and balance adjustment as focused steps in the workflow.
- Keep explicit adjustment transactions for auditability.

## Phase 6: UX Hardening And Expected Overlay Windows

Use focused **modal overlays** for:

- first-run setup (blocking full-screen overlay, not skippable);
- create/edit account;
- create/edit transaction;
- create/edit scheduled transaction template;
- create/edit savings goal;
- create category while categorizing;
- manage categories (rename/merge/delete/recolor/reorder);
- backup restore validation;
- delete-all-local-data confirmation (extra deliberate confirmation step, irreversible);
- possible duplicate transaction review;
- credit-card details (utilization, min payment, payoff estimate).

Statement account confirmation, statement category/scheduled matching, and reconciliation/check-in are **not** separate overlay windows — they are steps within the Phase 3 and Phase 5 full-page flows.

## Phase 7: Later Product Hardening

Note: the dashboard custom date range and the Analytics scope below are core decisions (see `docs/ui-structure-decisions.md`), not optional nice-to-haves — they're listed here only because their *implementation* can happen after the core navigation/overlay work lands, not because the spec is vague or deferred. The Transactions filter bar is core to Phase 2.5, not a Phase 7 item — it's listed there now, not here.

- Add a zero-persistence dashboard graph calendar/date selector for arbitrary start and end dates, on top of the default 7-days-back-plus-forecast-period range.
- Reset temporary dashboard graph date selection when the user leaves the dashboard or data reloads.
- Keep the graph default as past 7 days plus the saved default forecast period.
- Desktop reminders and notification lifecycle.
- Analytics: multi-period category trend (line/bar over last N months), auto-surfaced top movers, drill-down from a category into its filtered transactions, and income-vs-expense breakdown — independent date range on Analytics, defaulting to current month.
- Error presentation and diagnostics.
- Database migration diagnostics, including clear failure messages for schema upgrades.
- Database backup before risky operations.
- Schema migration and backup/restore verification for new transaction fields.
- Ledger and audit consistency checks for balance-after-transaction history, transfer edits, bulk deletion, and imported or scheduled occurrence links.
- Accessibility, keyboard navigation, focus handling, and responsive-layout hardening for overlay workflows.
- Diagnostics for statement parsing/import failures and destructive operations.
- Optional encryption, OCR, or bank sync research, always disabled by default.

## Test Plan Priorities

- Dashboard graph tests for the 7-day historical default, forecast continuation, current-time marker, arbitrary temporary ranges, and reset-on-leave behavior.
- Transaction persistence tests for `Name`, scheduled occurrence fields, import/export round trips, and old database migration defaults.
- Forecast tests confirming paid scheduled occurrences remain suppressed after app reload.
- Transaction UI/ViewModel tests for overlay type selection, scheduled occurrence assignment, transfer to account or goal, balance-after amount recalculation, and bulk edit/delete behavior.
- Regression tests for statement import routing and statement-created transaction names.
