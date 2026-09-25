using Banccoon.App.Diagnostics;
using Banccoon.App.Services;
using Banccoon.App.ViewModels;
using Banccoon.App.Views;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.Categories;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Forecasting;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Reconciliation;
using Banccoon.Core.Savings;
using Banccoon.Core.Statements;
using Banccoon.Core.Transactions;
using Banccoon.Infrastructure.Database;
using Banccoon.Infrastructure.ImportExport;
using Banccoon.Infrastructure.Repositories;
using Banccoon.Infrastructure.Statements;
using Microsoft.Extensions.DependencyInjection;

namespace Banccoon.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Diagnostics for the intermittent "sidebar goes blank, tabs stop navigating" report -
        // it isn't a crash (the process keeps running), so it needs whatever exception caused it
        // caught and logged rather than silently swallowed, to have something to look at next time.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            DiagnosticLog.Write($"AppDomain unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            DiagnosticLog.Write($"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<IDateProvider, SystemDateProvider>();
        builder.Services.AddSingleton<IRecurrenceValidationService, RecurrenceValidationService>();
        builder.Services.AddSingleton<IRecurrenceDescriptionService, RecurrenceDescriptionService>();
        builder.Services.AddSingleton<IRecurrenceSyntaxService, RecurrenceSyntaxService>();
        builder.Services.AddSingleton<IRecurrenceService, RecurrenceService>();
        builder.Services.AddSingleton<IScheduledTransactionProjectionService, ScheduledTransactionProjectionService>();
        builder.Services.AddSingleton<IAccountBalanceService, AccountBalanceService>();
        builder.Services.AddSingleton<ISavingsGoalAllocationService, SavingsGoalAllocationService>();
        builder.Services.AddSingleton<ILegacySavingsGoalConversionService, LegacySavingsGoalConversionService>();
        builder.Services.AddSingleton<IAvailableToSpendService, AvailableToSpendService>();
        builder.Services.AddSingleton<IFreeToSpendWindowService, FreeToSpendWindowService>();
        builder.Services.AddSingleton<IHistoricalBalanceService, HistoricalBalanceService>();
        builder.Services.AddSingleton<ICreditCardForecastService, CreditCardForecastService>();
        builder.Services.AddSingleton<ITransactionBalanceService, TransactionBalanceService>();
        builder.Services.AddSingleton<ITransactionApplicationService, TransactionApplicationService>();
        builder.Services.AddSingleton<ITransactionBalanceHistoryService, TransactionBalanceHistoryService>();
        builder.Services.AddSingleton<IScheduledOccurrenceResolutionService, ScheduledOccurrenceResolutionService>();
        builder.Services.AddSingleton<ICategoryManagementService, CategoryManagementService>();
        builder.Services.AddSingleton<ICategorySuggestionService, CategorySuggestionService>();
        builder.Services.AddSingleton<IStatementParser, SberbankDebitCardStatementParser>();
        builder.Services.AddSingleton<IStatementParserRegistry, StatementParserRegistry>();
        builder.Services.AddSingleton<IStatementImportService, StatementImportService>();
        builder.Services.AddSingleton<IBankCategoryService, BankCategoryService>();
        builder.Services.AddSingleton<IForecastService, ForecastService>();
        builder.Services.AddSingleton<ICheckInService, CheckInService>();
        builder.Services.AddSingleton<IReconciliationService, ReconciliationService>();
        builder.Services.AddSingleton<IGroupedSpendingService, GroupedSpendingService>();
        builder.Services.AddSingleton<IBalanceAdjustmentService, BalanceAdjustmentService>();
        builder.Services.AddSingleton<IExpectedTransactionMatcher, ExpectedTransactionMatcher>();
        builder.Services.AddSingleton<ITransactionDeletionService, TransactionDeletionService>();
        builder.Services.AddSingleton<IAnalyticsService, AnalyticsService>();
        builder.Services.AddSingleton<IAutoBackupRunner, AutoBackupRunner>();
        builder.Services.AddSingleton<IDatabasePathProvider, LocalAppDataDatabasePathProvider>();
        builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        builder.Services.AddSingleton<IBanccoonDatabaseInitializer, BanccoonDatabaseInitializer>();
        // Every page's own repository reads run through a Cached* decorator wrapping the real
        // SQLite repository (see EntityCache) - the whole local database is a few hundred KB, so
        // keeping it in memory after the first load and patching it on every write is what makes
        // switching tabs fast, instead of every OnAppearing re-querying SQLite from scratch.
        // Statement import batches/rows are deliberately left uncached - that's an occasional
        // guided workflow, not a page revisited on every navigation.
        builder.Services.AddSingleton<SqliteAccountRepository>();
        builder.Services.AddSingleton<IAccountRepository>(sp => new CachedAccountRepository(sp.GetRequiredService<SqliteAccountRepository>()));
        builder.Services.AddSingleton<SqliteCategoryRepository>();
        builder.Services.AddSingleton<ICategoryRepository>(sp => new CachedCategoryRepository(sp.GetRequiredService<SqliteCategoryRepository>()));
        builder.Services.AddSingleton<SqliteTransactionRepository>();
        builder.Services.AddSingleton<ITransactionRepository>(sp => new CachedTransactionRepository(sp.GetRequiredService<SqliteTransactionRepository>(), sp.GetRequiredService<IDateProvider>()));
        builder.Services.AddSingleton<SqliteScheduledTransactionRepository>();
        builder.Services.AddSingleton<IScheduledTransactionRepository>(sp => new CachedScheduledTransactionRepository(sp.GetRequiredService<SqliteScheduledTransactionRepository>()));
        builder.Services.AddSingleton<SqliteSavingsGoalRepository>();
        builder.Services.AddSingleton<ISavingsGoalRepository>(sp => new CachedSavingsGoalRepository(sp.GetRequiredService<SqliteSavingsGoalRepository>()));
        builder.Services.AddSingleton<SqliteSettingsRepository>();
        builder.Services.AddSingleton<ISettingsRepository>(sp => new CachedSettingsRepository(sp.GetRequiredService<SqliteSettingsRepository>()));
        builder.Services.AddSingleton<IStatementImportRepository, SqliteStatementImportRepository>();
        builder.Services.AddSingleton<IBankCategoryLinkRepository, SqliteBankCategoryLinkRepository>();
        builder.Services.AddSingleton<SqliteCategoryLearningRuleRepository>();
        builder.Services.AddSingleton<ICategoryLearningRuleRepository>(sp => new CachedCategoryLearningRuleRepository(sp.GetRequiredService<SqliteCategoryLearningRuleRepository>()));
        builder.Services.AddSingleton<SqliteScheduledOccurrenceOverrideRepository>();
        builder.Services.AddSingleton<IScheduledOccurrenceOverrideRepository>(sp => new CachedScheduledOccurrenceOverrideRepository(sp.GetRequiredService<SqliteScheduledOccurrenceOverrideRepository>()));
        builder.Services.AddSingleton<IExportValidator, ExportValidator>();
        builder.Services.AddSingleton<IExportService, RepositoryExportService>();
        builder.Services.AddSingleton<ILocalDataResetService, LocalDataResetService>();
        builder.Services.AddSingleton<IImportService, RepositoryImportService>();
        builder.Services.AddSingleton<IBackupService, JsonBackupService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<AppShellViewModel>();
        builder.Services.AddTransient<RecurrenceEditorViewModel>();
        builder.Services.AddTransient<CreditCardDetailsViewModel>();
        builder.Services.AddTransient<StatementImportViewModel>();
        builder.Services.AddTransient<ReconciliationViewModel>();
        builder.Services.AddTransient<AppLockViewModel>();

        // Singleton, not transient: each of these is one FlyoutItem's ShellContent
        // (ContentTemplate="{DataTemplate views:XPage}"), and Shell re-invokes that DataTemplate -
        // i.e. resolves a fresh instance from this container - on every single tab switch, not
        // just the first visit. Confirmed via diagnostics.log timing: InitializeComponent() alone
        // (building the whole native XAML visual tree from scratch) cost 50-150ms on every switch,
        // which was the entire "delay before content appears" complaint - the ViewModel/data work
        // was already sub-millisecond after the first load. Singleton means the container hands
        // back the same already-built page forever, so that cost is paid once per app run instead
        // of once per navigation. The ViewModels stay Transient (no change needed) - since the page
        // is now only ever constructed once, its ViewModel is also only ever resolved once, simply
        // by virtue of being a constructor parameter of a page that's no longer rebuilt.
        //
        // Side effect worth knowing: transient UI state on these ViewModels (SelectMode/FilterOpen
        // on Transactions, IsCalcOpen on Dashboard, SelectedCategory on Settings, etc.) used to
        // silently reset every time you left and returned to a tab, because the whole
        // page+ViewModel pair was being thrown away and recreated. Now it persists across tab
        // switches instead, since the same instance sticks around - e.g. leaving Transactions with
        // the filter panel open and coming back will show it still open. InitializeAsync's own data
        // refresh on every OnAppearing is unaffected either way.
        //
        // StatementImportPage/ReconciliationPage/AppLockPage stay Transient deliberately - they're one-off
        // modal-style flows (GoToAsync/PushModalAsync, not a FlyoutItem tab), where fresh state
        // per visit is correct, not an accident to fix.
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<TransactionsPage>();
        builder.Services.AddSingleton<AccountsPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<StatementImportPage>();
        builder.Services.AddTransient<ReconciliationPage>();
        builder.Services.AddTransient<AppLockPage>();

#if WINDOWS
        ConfigureWindowsInputBorders();
#endif

        return builder.Build();
    }

#if WINDOWS
    // Entry/Picker/DatePicker have no cross-platform border property in MAUI, so the subtle
    // outline that makes them read as distinct fields (rather than blending into the card
    // background) is applied directly to the native WinUI control here. One neutral gray works
    // for both themes, avoiding the extra plumbing a theme-reactive native color would need.
    private static void ConfigureWindowsInputBorders()
    {
        var borderColor = Windows.UI.Color.FromArgb(255, 148, 156, 166);
        var borderThickness = new Microsoft.UI.Xaml.Thickness(1);

        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BanccoonBorder", (handler, _) =>
        {
            handler.PlatformView.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
            handler.PlatformView.BorderThickness = borderThickness;
        });

        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("BanccoonBorder", (handler, _) =>
        {
            handler.PlatformView.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
            handler.PlatformView.BorderThickness = borderThickness;
        });

        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("BanccoonBorder", (handler, _) =>
        {
            handler.PlatformView.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
            handler.PlatformView.BorderThickness = borderThickness;
        });
    }
#endif
}
