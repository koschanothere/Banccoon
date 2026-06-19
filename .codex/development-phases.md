# Banccoon Development Phases

This document defines the development phases for Banccoon, a private offline-first financial forecasting desktop application built with .NET MAUI for Windows first, with future portability to other platforms.

The project should start with the smallest usable forecasting product, then expand toward reconciliation, richer account modeling, portability, and optional future integrations.

## Active Branch Plan: Phase 7

Branch: `codex/phase-7-workflow-completion-category-filtering`

This branch completes the most important UI data wiring before product hardening. Hardening depends on users being able to exercise real workflows, so the app should first let users enter saved data, see forecasts from SQLite, reconcile reality, and control local data from the desktop UI.

### Planned Deliverables

- Swap Phase 7 and Phase 8 so UI data wiring comes before desktop product hardening.
- Add a documented logo asset folder for Banccoon marks and mascot variants.
- Load accounts, transactions, scheduled transactions, goals, and settings from local SQLite on app startup.
- Add basic create/delete UI for accounts, transactions, scheduled items, and savings goals.
- Add selected-account balance updates and credit-card payoff calculation controls.
- Recalculate dashboard and forecast outputs from saved data.
- Persist default currency, default forecast period, and reminder frequency from Preferences.
- Filter income and expense category choices to match the selected transaction type.
- Add a Reconciliation screen for expected scheduled item review, actual-balance comparison, grouped spending, and balance adjustments.
- Add a Data screen for JSON backup/export, validation, merge/replace import, and a guarded delete-all-local-data workflow.

### Scope Boundaries

- This phase favors usable saved workflows over final visual polish.
- Detailed analytics, polished dialogs, and native file-picker polish can follow once the main data screens are alive.
- Desktop notifications remain in product hardening because reliable reminders require app lifecycle/tray behavior, not just saved reminder settings.
- Appearance preferences beyond the existing runtime navigation/power-user toggles still need schema support before persistence.

## Active Branch Plan: Phase 6

Branch: `codex/phase-6-goals-credit-cards-organization`

This branch implements the core savings-goal and credit-card obligation behavior. The first slice stays mostly in Core services so the calculations can be trusted before the later UI data-wiring phase builds full account and goal editors.

### Planned Deliverables

- Add savings-goal allocation models and service.
- Reduce available-to-spend by reserved savings-goal amounts without changing projected balances.
- Add credit-card payment projection from planned payments or minimum-payment fallback.
- Include projected credit-card payments in the forecast timeline and upcoming obligations.
- Add a credit-card payoff planner using a user-chosen payment amount.
- Add a manual monthly finance-charge input for power users instead of guessing card interest rules.
- Add a payoff ViewModel surface that recalculates immediately when the chosen payment amount changes.
- Add focused tests for goal reservations, card payment projections, payoff timing, and forecast integration.

### Scope Boundaries

- No automatic interest inference yet; promotional periods and purchase-specific rates require bank/API data or careful manual entry.
- Manual interest/finance charges can be represented in payoff planning now and as a future transaction/category workflow later.
- No full goals or credit-card editor screens yet; real UI data entry belongs to Phase 7 UI Data Wiring.
- No automatic balance mutation when a card payment is forecast; actual payments should still enter through transactions/reconciliation.

## Active Branch Plan: Phase 5

Branch: `codex/phase-5-reconciliation-check-in`

This branch starts the reconciliation and weekly check-in system. The first slice focuses on testable workflow logic rather than full UI screens: expected scheduled event review, forecast-versus-reality comparison, grouped spending creation, and balance-adjustment transactions.

### Planned Deliverables

- Add check-in session models.
- Add expected transaction review decisions: pending, confirmed, delayed, and cancelled.
- Add reconciliation result models for expected balance, actual balance, and difference.
- Add grouped spending entry model and service.
- Add balance adjustment model and service.
- Add focused unit tests for check-in discovery, reconciliation math, grouped spending, and adjustment creation.

### Scope Boundaries

- No full check-in UI yet.
- No notification scheduling yet.
- No automatic account mutation yet; balance adjustment produces explicit transaction records for traceability.
- Confirm/delay/cancel persistence wiring can follow in the UI data-wiring phase.

## Active Branch Plan: Phase 4

Branch: `codex/phase-4-import-export-backup-restore`

This branch starts data portability. Banccoon should be able to produce a versioned, human-readable JSON export containing the user's financial data, validate imports before applying them, merge or replace local data, and create/restore backup files without any cloud service.

### Planned Deliverables

- Add versioned export envelope and data transfer models.
- Add import modes for validation, merge, and replace.
- Add export validation for format version and broken references.
- Add repository-backed export/import services.
- Add JSON file backup/restore service.
- Add tests for export contents, import validation, replace import, merge import, and round-trip restore.
- Add default currency selection to Preferences.

### Scope Boundaries

- Export files are plain JSON for this slice.
- Compressed backup archives can be added later once the schema stabilizes.
- No cloud backup or sync.
- No full file picker UI yet; services are testable first and UI wiring follows.

## Active Branch Plan: UI Shell And Customization Foundation

Branch: `codex/ui-shell-navigation-customization`

This branch starts a dedicated UI shell phase. Banccoon should feel semi-professional, friendly, modern, and calm rather than like a raw technical prototype. The first UI pass should create the app frame users will live in: navigation, dashboard structure, theme tokens, and preference hooks.

### Design Direction

- Calm financial clarity inspired by products like Zenmoney, without copying their UI.
- Friendly desktop-first layout with generous spacing, clear numbers, and soft contrast.
- First-class room for future Banccoon branding and mascot/logo placement.
- Main areas should be visible as stable destinations: Dashboard, Accounts, Transactions, Scheduled, Forecast, Analytics, and Preferences.
- Navigation should support both a visible left rail and a compact focus mode.
- Long-term customization should include light/dark mode, highlight/accent colors, dashboard layout preferences, analytics preferences, and navigation style.

### Planned Deliverables

- Add UI preference models for theme mode, accent color, and navigation style.
- Add a shell ViewModel that tracks the selected section and navigation preferences.
- Replace the placeholder dashboard with a real desktop app shell.
- Add placeholder content surfaces for the main app areas.
- Establish theme resources for light, calm, modern UI styling.
- Keep real CRUD/data-entry wiring for the next Phase 2 UI continuation.

### Scope Boundaries

- This is not the final polished UI.
- No logo/mascot assets yet; the shell should reserve space for them.
- No full accounts or transaction forms yet.
- No advanced custom theme editor yet.
- The layout should be ready for future light/dark and accent customization.

## Active Branch Plan: Phase 3

Branch: `codex/phase-3-recurrence-editor`

This branch starts the user-friendly recurrence layer. The stored recurrence data remains structured, while Core and App gain the services and ViewModels needed to present rules as natural-language choices such as "Every week on Monday" or "Every month on the last day".

### Planned Deliverables

- Add recurrence validation service in Core.
- Add recurrence description service in Core.
- Keep `RecurrenceRule` as the durable source of truth.
- Add a MAUI recurrence editor ViewModel with selectable frequencies, intervals, weekdays, and monthly modes.
- Add a reusable recurrence editor control for future scheduled-transaction screens.
- Add optional technical recurrence syntax for power users.
- Default to the friendly sentence-style UI, with an expandable advanced syntax area.
- Provide syntax examples and validation messages instead of exposing raw storage details.
- Mark all power-user UI elements with a shared power-user tag so preferences can show or hide them globally.
- Register recurrence editor services in DI.
- Add focused tests for validation and natural-language descriptions.

### Phase 3 Scope Boundaries

- No full scheduled-transaction CRUD screen yet.
- No full iCalendar/RFC 5545 implementation yet.
- No database migration changes unless the structured rule model changes.
- No localization yet; descriptions are English for the first product slice.

## Active Branch Plan: Phase 2

Branch: `codex/phase-2-local-persistence`

This branch starts local persistence. The aim is to make Banccoon capable of storing and loading real user data from a per-user local SQLite database while keeping repository interfaces in Core and database details in Infrastructure.

### Planned Deliverables

- Add repository hygiene with `.gitignore`.
- Add SQLite package reference to `Banccoon.Infrastructure`.
- Add a database path provider for per-user local storage.
- Add schema initialization for Phase 2 tables.
- Implement repository round-trips for accounts, categories, scheduled transactions, transactions, savings goals, and settings.
- Add tests using temporary SQLite database files.
- Keep MAUI screens minimal until repository behavior is verified.

### Phase 2 Scope Boundaries

- No cloud storage.
- No bank sync.
- No import/export yet.
- No advanced UI polish yet.
- No encrypted database yet, though the file-based design should allow that later.

## Active Branch Plan: Phase 0 And Phase 1

Branch: `codex/phase-0-1-foundation-forecasting`

This branch implements the first engineering slice of Banccoon: a clean solution skeleton plus a testable forecasting and recurrence core. The .NET SDK is not currently visible on this machine, so project files and source are created directly and build/test verification must be completed once an SDK is installed or added to `PATH`.

### Planned Deliverables

- Create `Banccoon.sln` with Core, Infrastructure, App, and Tests projects.
- Keep business logic in `Banccoon.Core`.
- Add placeholder Infrastructure and App projects that can compile once the SDK is available.
- Implement domain models for accounts, transactions, scheduled transactions, categories, goals, settings, and recurrence.
- Implement recurrence occurrence generation for daily, weekly, monthly, month-end, and yearly rules.
- Implement forecast generation for 7, 30, 60, and 90 day periods.
- Calculate current balance, forecasted ending balance, lowest forecasted balance, upcoming obligations, and available to spend.
- Add focused unit tests for recurrence edge cases and forecast calculations.

### Phase 0/1 Scope Boundaries

- No SQLite implementation yet.
- No MAUI UI polish yet.
- No reconciliation workflow yet.
- No import/export implementation yet.
- Credit card and savings-goal forecast extensions remain in later phases, though the models can exist early.

## Phase 0: Repository And Product Foundation

### Goals

- Establish the solution structure.
- Keep business logic independent from the MAUI UI.
- Create a clean foundation for offline-first local data.
- Document core architectural decisions before feature work expands.

### Components To Build

- `Banccoon.sln`
- Core class library.
- Infrastructure class library.
- MAUI app project.
- Unit test project.
- Shared domain abstractions.
- Dependency injection setup.

### Data Models Required

- `Account`
- `Category`
- `Transaction`
- `ScheduledTransaction`
- `SavingsGoal`
- `AppSettings`
- `RecurrenceRule`

### Services Required

- Repository abstractions.
- Clock/date abstraction for testability.
- Application settings abstraction.
- Initial app startup/bootstrap service.

### ViewModels Required

- `ShellViewModel`
- `DashboardViewModel`

### Screens Required

- App shell.
- Empty dashboard.
- Basic navigation frame.

### Testing Requirements

- Project compilation.
- Smoke tests for dependency injection registration.
- Core model validation tests where useful.

### Exit Criteria

- Solution builds.
- Tests run.
- App starts on Windows.
- Core, infrastructure, UI, and tests are separated.

## Phase 1: Forecasting Core MVP

### Goals

- Build the first useful version of the financial forecast engine.
- Support accounts and scheduled income/expense events.
- Calculate projected balances for 7, 30, 60, and 90 days.

### Components To Build

- Forecast engine.
- Basic recurrence engine.
- Forecast event generation.
- Balance projection model.
- Available-to-spend calculation.

### Data Models Required

- `ForecastRequest`
- `ForecastResult`
- `ForecastEvent`
- `ForecastPeriod`
- `ProjectedBalancePoint`
- `UpcomingObligation`

### Services Required

- `IForecastService`
- `IRecurrenceService`
- `IScheduledTransactionProjectionService`
- `IAccountBalanceService`

### ViewModels Required

- `DashboardViewModel`
- `ForecastViewModel`

### Screens Required

- Dashboard.
- Forecast timeline.

### Testing Requirements

- Forecast includes scheduled income.
- Forecast includes scheduled expenses.
- Events are applied in chronological order.
- Lowest forecasted balance is calculated correctly.
- Available-to-spend calculation is predictable and documented.
- Forecast periods produce expected date ranges.

### Exit Criteria

- The app can answer: current balance, upcoming obligations, available to spend, forecasted balance, and lowest projected balance.

## Phase 2: Local Persistence And Basic Management Screens

### Goals

- Persist user data locally.
- Allow users to create and maintain core records without editing files.

### Components To Build

- SQLite storage.
- Database migrations.
- Repository implementations.
- Account management.
- Category management.
- Scheduled transaction management.

### Data Models Required

- Persistence entities for accounts, categories, transactions, scheduled transactions, settings, and recurrence rules.
- Mapping between persistence entities and domain models.

### Services Required

- `IAccountRepository`
- `ICategoryRepository`
- `ITransactionRepository`
- `IScheduledTransactionRepository`
- `ISettingsRepository`
- `IDatabaseInitializer`

### ViewModels Required

- `AccountsViewModel`
- `AccountEditorViewModel`
- `ScheduledTransactionsViewModel`
- `ScheduledTransactionEditorViewModel`
- `CategoryPickerViewModel`

### Screens Required

- Accounts screen.
- Account editor.
- Scheduled transactions screen.
- Scheduled transaction editor.
- Category creation flow.

### Testing Requirements

- Repository tests using a test database.
- Database migration tests.
- Save/load round-trip tests.
- Optional category behavior tests.

### Exit Criteria

- Users can create accounts and scheduled transactions.
- Forecasts are generated from persisted local data.
- Data remains available after restarting the app.

## Phase 2.5: UI Shell And Design System

### Goals

- Make Banccoon feel like a real desktop app before deeper feature screens are added.
- Establish the main navigation structure.
- Define visual style, spacing, theme resources, and reusable layout patterns.
- Provide a home for future branding, including the Banccoon logo and mascot.

### Components To Build

- Desktop app shell.
- Navigation rail with collapsible focus mode.
- Optional top-tab navigation mode later.
- Theme resource dictionaries.
- Reusable dashboard stat cards and list sections.
- Empty-state patterns.
- Preference model for navigation and appearance.

### ViewModels Required

- `ShellViewModel`
- `NavigationItemViewModel`
- `DashboardViewModel` updates
- `PreferencesViewModel` foundation

### Screens Required

- Dashboard
- Accounts placeholder
- Transactions placeholder
- Scheduled placeholder
- Forecast placeholder
- Analytics placeholder
- Preferences placeholder

### Testing Requirements

- Build verification for MAUI shell.
- Unit tests for UI preference defaults where practical.
- Manual run check for navigation behavior.

### Exit Criteria

- The app opens into a semi-professional, friendly shell.
- Users can switch between the major app sections.
- Navigation can be shown or collapsed for focus.
- The UI has a clear direction for future customization.

## Phase 3: Recurrence Editor And Advanced Recurrence Support

### Goals

- Make recurrence rules flexible enough for real financial obligations.
- Provide a friendly natural-language-style editor.

### Components To Build

- Structured recurrence rule model.
- Human-readable recurrence descriptions.
- Natural-language-style recurrence editor.
- Advanced/custom recurrence option.

### Data Models Required

- `RecurrenceFrequency`
- `RecurrenceInterval`
- `MonthlyRecurrenceMode`
- `DayOfWeekSelection`
- `RecurrenceEndCondition`

### Services Required

- `IRecurrenceDescriptionService`
- `IRecurrenceValidationService`

### ViewModels Required

- `RecurrenceEditorViewModel`

### Screens Required

- Reusable recurrence editor control or dialog.

### Testing Requirements

- Every day.
- Every N days.
- Every week.
- Every N weeks.
- Every month on day N.
- Last day of month.
- Every year.
- Leap year behavior.
- Month-end behavior.

### Exit Criteria

- Users can create recurring income and expenses without seeing technical recurrence syntax by default.
- Power users can expand an advanced section to view or edit technical recurrence syntax with examples.
- Power-user controls can be globally hidden or shown from preferences.

## Phase 4: Import, Export, Backup, And Restore

### Goals

- Make data ownership and portability a first-class feature.
- Allow users to move complete financial history between devices without cloud sync.

### Components To Build

- Versioned JSON export format.
- Full data export.
- Import validation.
- Merge import.
- Replace import.
- Backup creation.
- Backup restoration.

### Data Models Required

- `ExportEnvelope`
- `ExportMetadata`
- `ExportData`
- `ImportValidationResult`
- `ImportConflict`
- `ImportMode`

### Services Required

- `IExportService`
- `IImportService`
- `IBackupService`
- `IExportValidator`
- `IImportConflictResolver`

### ViewModels Required

- `ImportExportViewModel`
- `ImportReviewViewModel`

### Screens Required

- Import/export screen.
- Import validation review.
- Backup/restore actions.

### Testing Requirements

- Export contains all required entities.
- Export includes app version, export format version, and timestamp.
- Import rejects incompatible files.
- Import validates references.
- Merge mode preserves existing compatible data.
- Replace mode replaces existing data safely.
- Export/import round-trip restores full financial history.

### Exit Criteria

- A user can export a single portable file, install the app elsewhere later, import the file, and continue with the same data.

## Phase 5: Reconciliation And Weekly Check-In

### Goals

- Make the app useful for weekly or periodic updates.
- Help users compare forecasted balances to reality without forcing detailed transaction entry.

### Components To Build

- Check-in workflow.
- Expected scheduled transaction review.
- Confirm, delay, or cancel expected events.
- Actual balance entry.
- Forecast-versus-reality comparison.
- Grouped spending entry.
- Balance adjustment option.

### Data Models Required

- `CheckInSession`
- `ExpectedTransactionReview`
- `ReconciliationResult`
- `GroupedSpendingEntry`
- `BalanceAdjustment`

### Services Required

- `ICheckInService`
- `IReconciliationService`
- `IGroupedSpendingService`
- `IBalanceAdjustmentService`

### ViewModels Required

- `CheckInViewModel`
- `ExpectedTransactionsReviewViewModel`
- `ReconciliationViewModel`
- `GroupedSpendingViewModel`

### Screens Required

- Reconciliation screen.
- Check-in workflow.
- Grouped spending entry dialog.
- Balance adjustment confirmation.

### Testing Requirements

- Expected transactions are discovered for the check-in period.
- Confirmed events create or mark transactions correctly.
- Delayed events update next expected dates correctly.
- Cancelled events do not affect the actual balance.
- Reality-versus-forecast difference is calculated correctly.
- Grouped spending reduces balance correctly.

### Exit Criteria

- A user can update the app once per week and reconcile reality against the forecast in a guided workflow.

## Phase 6: Savings Goals And Credit Card Obligations

### Goals

- Improve the accuracy of available-to-spend calculations.
- Include common financial obligations that affect cash availability.

### Components To Build

- Savings goal management.
- Goal reservation calculation.
- Credit card account details.
- Credit card payment forecasting.
- Minimum and planned payment handling.
- User-chosen payoff planning.
- Manual finance-charge modeling for cards where interest cannot be inferred safely.

### Data Models Required

- `CreditCardDetails`
- `CreditCardPaymentProjection`
- `CreditCardPayoffPlan`
- `CreditCardPayoffMonth`
- `SavingsGoalAllocation`
- `AvailableToSpendBreakdown`

### Services Required

- `ISavingsGoalAllocationService`
- `ICreditCardForecastService`
- `IAvailableToSpendService`

### ViewModels Required

- `GoalsViewModel`
- `GoalEditorViewModel`
- `CreditCardDetailsViewModel`

### Screens Required

- Goals screen.
- Goal editor.
- Credit card details section inside account editor.

### Testing Requirements

- Goal allocations reduce available-to-spend.
- Credit card planned payments appear in forecasts.
- Minimum payments are handled when planned payments are missing.
- User-selected card payment amounts produce payoff timelines.
- Manual finance charges affect payoff timing and total paid.
- Optional credit card fields do not block basic account usage.

### Exit Criteria

- Forecasts reflect savings reservations and upcoming credit card obligations.
- A payoff calculation can tell the user when a credit-card debt is paid off for a chosen payment amount.

## Phase 7: UI Data Wiring

### Goals

- Connect the visual shell and feature screens to the real local SQLite repositories.
- Make the app usable for actual data entry and saved workflows.
- Persist user preferences, including currency, navigation style, power-user visibility, and theme settings.

### Components To Build

- Accounts CRUD UI wired to `IAccountRepository`.
- Categories picker and creation flow wired to `ICategoryRepository`.
- Transactions UI wired to `ITransactionRepository`.
- Scheduled transactions UI wired to recurrence editor and `IScheduledTransactionRepository`.
- Dashboard and forecast screens reading real stored data.
- Dashboard totals respect account-level inclusion settings for emergency, hidden, or excluded accounts.
- Dashboard and Analytics show an interactive projected-balance line graph for included accounts.
- Preferences screen wired to `ISettingsRepository` and appearance preference storage.
- Import/export screen wired to backup services with path-based JSON files first; native file pickers can be polished later.
- Brand asset folder for Banccoon logos and rotating mascot art.
- Reconciliation screen wired to check-in, grouped spending, and balance-adjustment services.
- Category pickers filtered by income/expense type, while older untyped categories remain usable.
- Delete-all-data workflow with backup acknowledgement and explicit typed confirmation.

### Brand Assets

Place app-ready logo assets in:

`src/Banccoon.App/Resources/Images/banccoon/`

Recommended logo variants:

- `banccoon_logo_full_light.svg` or `.png`: full wordmark plus mascot for light UI.
- `banccoon_logo_full_dark.svg` or `.png`: full wordmark plus mascot for dark UI.
- `banccoon_mark.svg` or `.png`: compact square/circle mascot or B mark for the rail.
- `banccoon_mascot_idle_01.png`: friendly default banking raccoon.
- `banccoon_mascot_idle_02.png`: alternate expression/pose for occasional rotation.
- `banccoon_mascot_focus.png`: calmer version for dashboard/check-in moments.
- Optional fun extras: seasonal or tiny mood variants named `banccoon_mascot_alt_01.png`, `banccoon_mascot_alt_02.png`, etc.

Use lowercase filenames with underscores so .NET MAUI can turn them into image resource names cleanly.

### ViewModels Required

- `AccountsViewModel`
- `AccountEditorViewModel`
- `TransactionsViewModel`
- `TransactionEditorViewModel`
- `ScheduledTransactionsViewModel`
- `ScheduledTransactionEditorViewModel`
- `ForecastViewModel`
- `PreferencesViewModel`
- `ImportExportViewModel`

### Screens Required

- Real Accounts screen.
- Real Transactions screen.
- Real Scheduled screen.
- Real Forecast screen.
- Real Reconciliation screen.
- Real Preferences screen.
- Real Data/import/export screen.

### Testing Requirements

- ViewModel tests where behavior is nontrivial.
- Repository integration tests remain the persistence safety net.
- Manual app run checks for create/edit/delete and restart persistence.

### Exit Criteria

- A user can enter real accounts and scheduled transactions in the UI.
- Data persists between app restarts and builds.
- Dashboard and forecast use saved local data.
- Preferences persist through restart.
- Users can reconcile expected scheduled items against real balances.
- Users can export, validate, merge/replace import, and intentionally reset local data from the app.

## Phase 8: Desktop Product Hardening

### Goals

- Turn the UI-backed MVP into a dependable Windows desktop product.
- Improve reliability, error handling, and everyday usability after the main workflows exist.

### Components To Build

- Reminder configuration.
- Tray/app lifecycle support for reminders.
- Desktop notification scheduling once the app can keep a background/tray presence.
- Category management moved into Preferences with type-aware defaults, editing, deletion safeguards, and optional archive behavior.
- User-selectable sorting and filtering for transactions, scheduled items, goals, and analytics views.
- Analytics graph expansion with per-account lines, toggled series, richer tooltips, and category overlays.
- Error and validation presentation.
- Empty states.
- Import/export safety confirmations.
- Database backup before risky operations.
- Polish delete-all-data workflow with a focused Banccoon warning mascot and richer confirmations.
- Settings screen hardening.
- Logging that does not collect private analytics.

### Data Models Required

- `ReminderSettings`
- `ForecastSettings`
- `PrivacySettings`

### Services Required

- `IReminderService`
- `ISettingsService`
- `IAppNotificationService`
- `ITrayLifecycleService`
- `ICategoryManagementService`
- `IListViewPreferencesService`
- `ILocalDiagnosticsService`
- `ILocalDataResetService`

### ViewModels Required

- `SettingsViewModel`
- `ReminderSettingsViewModel`

### Screens Required

- Reminder configuration.
- Tray/background behavior for Windows reminders.
- Category management preferences.
- Sorting and filtering preferences.
- Diagnostics/export log option if needed.
- Hardened import/export confirmations.
- Delete-all-data confirmation flow with multiple checks and a focused Banccoon warning mascot.

### Testing Requirements

- Settings persistence.
- Reminder scheduling logic.
- Backup-before-restore behavior.
- Delete-all-data requires multiple explicit confirmations and offers backup/export first.
- Error handling paths for failed import and failed database access.

### Exit Criteria

- Banccoon feels like a complete local Windows desktop application rather than a prototype.

## Phase 9: Future Optional Features

### Goals

- Add optional advanced features without compromising the offline-first architecture.

### Future Components

- Local OCR transaction detection.
- Optional bank synchronization.
- Optional encrypted cloud synchronization.
- Mobile-specific UI shells.
- Rich analytics based on logged or imported transactions.
- Advanced appearance customization with accent colors, saved themes, custom dashboard layouts, and user-selected navigation style.

### Architectural Constraints

- No feature should require an application server for core use.
- Bank sync must be disabled by default.
- OCR should prefer on-device processing.
- Imports from OCR or bank sync should enter through the same review/reconciliation workflow as manual data.
- Users must approve detected transactions before they affect financial history.

### Exit Criteria

- Optional integrations enhance the product without weakening privacy, portability, or offline usability.

## Suggested Milestones

1. Architecture foundation and solution skeleton.
2. Forecasting core with recurrence tests.
3. Local persistence and basic CRUD screens.
4. Dashboard and forecast timeline from real local data.
5. Versioned import/export and backup/restore.
6. Guided weekly check-in and reconciliation.
7. Savings goals and credit card obligations.
8. UI data wiring for real saved workflows.
9. Windows desktop hardening for V1.
10. Optional OCR and bank sync research spikes.

## Technical Priorities

1. Correct forecasting logic.
2. Reliable recurrence behavior.
3. Data portability.
4. Local persistence reliability.
5. Reconciliation workflow usability.
6. UI polish.

## Early Decisions To Keep Stable

- Store money as `decimal`.
- Use `Guid` IDs for portable data.
- Keep categories optional.
- Keep all financial data local by default.
- Store recurrence as structured data, not only text.
- Keep forecast and recurrence logic out of the MAUI UI project.
- Treat import/export compatibility as a permanent product requirement.
