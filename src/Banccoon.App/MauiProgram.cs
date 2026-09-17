using Banccoon.App.Diagnostics;
using Banccoon.App.ViewModels;
using Banccoon.App.Views;
using Banccoon.Core.Abstractions;
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
        builder.Services.AddSingleton<IForecastService, ForecastService>();
        builder.Services.AddSingleton<ICheckInService, CheckInService>();
        builder.Services.AddSingleton<IReconciliationService, ReconciliationService>();
        builder.Services.AddSingleton<IGroupedSpendingService, GroupedSpendingService>();
        builder.Services.AddSingleton<IBalanceAdjustmentService, BalanceAdjustmentService>();
        builder.Services.AddSingleton<IDatabasePathProvider, LocalAppDataDatabasePathProvider>();
        builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        builder.Services.AddSingleton<IBanccoonDatabaseInitializer, BanccoonDatabaseInitializer>();
        builder.Services.AddSingleton<IAccountRepository, SqliteAccountRepository>();
        builder.Services.AddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        builder.Services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
        builder.Services.AddSingleton<IScheduledTransactionRepository, SqliteScheduledTransactionRepository>();
        builder.Services.AddSingleton<ISavingsGoalRepository, SqliteSavingsGoalRepository>();
        builder.Services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        builder.Services.AddSingleton<IStatementImportRepository, SqliteStatementImportRepository>();
        builder.Services.AddSingleton<ICategoryLearningRuleRepository, SqliteCategoryLearningRuleRepository>();
        builder.Services.AddSingleton<IScheduledOccurrenceOverrideRepository, SqliteScheduledOccurrenceOverrideRepository>();
        builder.Services.AddSingleton<IExportValidator, ExportValidator>();
        builder.Services.AddSingleton<IExportService, RepositoryExportService>();
        builder.Services.AddSingleton<IImportService, RepositoryImportService>();
        builder.Services.AddSingleton<IBackupService, JsonBackupService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<AppShellViewModel>();
        builder.Services.AddTransient<RecurrenceEditorViewModel>();
        builder.Services.AddTransient<CreditCardDetailsViewModel>();

        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<SettingsPage>();

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
