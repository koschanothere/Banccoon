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

Large workflows should become app-guided modal panels rather than permanent top-level tabs. These overlays must feel calm, clear, and hard to misuse.

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

## Phase 0.1: Navigation Simplification

- Combine Preferences and Data into one Settings section.
- Preserve all existing preferences, backup/export, import/restore, and delete-all-local-data options.
- Move navigation tab/rail style controls into Settings only.
- Remove the always-present Rail/Tabs controls from the global header.
- Persist appearance/navigation preferences properly instead of keeping them as shell-only runtime state.
- Goals should be treated as an account in the DB.
- Keep Data functionality inside Settings.
- Keep tab navigation focused on everyday destinations: Dashboard, Transactions, Accounts, and Settings. Scheduled and Statements tabs should go into transaction options. Forecast and Analytics go into dashboard as collapsable fields.
- Remove Statements and Reconciliation as permanent top-level tabs once their guided overlays exist.
- Keep workflow launch buttons where users naturally need them rather than forcing users to hunt for special tabs.

## Phase 1: Shared Workflow Overlay Architecture

- Add a reusable in-app modal/workflow host inside the existing MAUI shell, not a separate OS window.
- Support dimmed background, focused content, close/cancel rules, busy/error states, and simple step transitions.
- Keep overlays reusable for startup setup, statement import review, reconciliation, account editing, category creation, backup restore, and destructive confirmations.
- Keep workflow state in ViewModels instead of embedding one-off logic in `MainPage.xaml`.

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
- Add a plus action with Expense, Income, Transfer, Scheduled, and Statement import options.
- Expense and Income overlays should collect transaction name, account, date, amount, category, and optional assignment to an existing scheduled occurrence.
- Transfer overlay should collect transaction name, amount, date, outgoing account, incoming account or goal, and optional assignment to an existing scheduled occurrence.
- Scheduled should open a schedule manager overlay for viewing and creating scheduled expense, income, and transfer templates.
- Scheduled assignment from Expense, Income, or Transfer creates only an occurrence link to an existing scheduled transaction; it must not create a new scheduled template.
- Statement import should route into the dedicated statement-import workflow.
- Add a first-class `Transaction.Name` field and persist it through SQLite, import/export, statement-created transactions, and tests.
- Persist `PaidScheduledTransactionId` and `PaidScheduledOccurrenceDate`; the model and forecast service already expect them, but SQLite persistence must read and write them.
- Transaction history rows should show name, category or transfer destination, scheduled mark, amount, and account value immediately after the transaction.
- In edit mode, keep name locked, make category a dropdown, make scheduled assignment editable, show a trash action, and allow amount and account-value-after edits.
- Editing account value after a transaction should recalculate that transaction's amount.
- Add multi-select actions for mass category assignment, mass scheduled-occurrence assignment, and mass deletion.

## Phase 3: Bank Statement Import Workflow Redesign

- Replace the Statements tab workflow with a guided overlay launched from blank setup, dashboard actions, or an import command.
- Step 1: pick/read statement and show detected balance, account number, card ending, parser, period, and row count.
- Step 2: confirm account match or create/link an account.
- When creating an account from a statement, default the starting balance from the parsed closing/current balance when available, falling back only if needed.
- Step 3: review pending transaction rows in a compact list that gets shorter as rows are approved or skipped.
- Default uncategorized rows to `Other`, but make category selection/creation fast.
- Add multi-select so multiple rows can be categorized, skipped, or attached to the same scheduled transaction together.
- Keep duplicate warnings visible before approval.
- Preserve local category learning when rows are approved.

## Phase 4: Scheduled Transaction Matching From Imports

- Allow imported rows to be attached to an existing scheduled transaction instead of only a category.
- After choosing a scheduled transaction, ask which scheduled period/occurrence the payment belongs to.
- Record the created transaction as linked to that scheduled occurrence.
- Mark the occurrence as paid so forecasts include or suppress the correct event, including future-dated payments that are already paid.
- Keep the scheduled transaction’s category as the default category for that imported row.
- Add tests for paid occurrence handling, future paid occurrences, and duplicate prevention.

## Phase 5: Guided Reconciliation

- Move reconciliation out of the main navigation and into an app-triggered overlay.
- Trigger reconciliation from dashboard/check-in actions, after statement import, and when the app needs a real balance check.
- Reuse the same “shrinking list” interaction pattern for expected scheduled items: confirm, delay, skip, or attach actual imported/manual transactions.
- Keep actual-balance comparison, grouped spending, and balance adjustment as focused steps in the workflow.
- Keep explicit adjustment transactions for auditability.

## Phase 6: UX Hardening And Expected Overlay Windows

Use focused overlays for:

- first-run setup;
- statement account confirmation;
- statement category/scheduled matching;
- reconciliation/check-in;
- create/edit account;
- create/edit transaction;
- create/edit scheduled transaction;
- create/edit savings goal;
- create category while categorizing;
- backup restore validation;
- delete-all-local-data confirmation;
- possible duplicate transaction review;
- credit-card payoff details.

## Phase 7: Later Product Hardening

- Add a zero-persistence dashboard graph calendar/date selector for arbitrary start and end dates.
- Reset temporary dashboard graph date selection when the user leaves the dashboard or data reloads.
- Keep the graph default as past 7 days plus the saved default forecast period.
- Desktop reminders and notification lifecycle.
- Better list sorting/filtering.
- Richer analytics and category views.
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
