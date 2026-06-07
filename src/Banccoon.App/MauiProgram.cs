using Banccoon.App.ViewModels;
using Banccoon.App.Views;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;
using Banccoon.Core.Recurrence;
using Banccoon.Infrastructure.Database;
using Banccoon.Infrastructure.ImportExport;
using Banccoon.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Banccoon.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
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
        builder.Services.AddSingleton<IForecastService, ForecastService>();
        builder.Services.AddSingleton<IDatabasePathProvider, LocalAppDataDatabasePathProvider>();
        builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        builder.Services.AddSingleton<IBanccoonDatabaseInitializer, BanccoonDatabaseInitializer>();
        builder.Services.AddSingleton<IAccountRepository, SqliteAccountRepository>();
        builder.Services.AddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        builder.Services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
        builder.Services.AddSingleton<IScheduledTransactionRepository, SqliteScheduledTransactionRepository>();
        builder.Services.AddSingleton<ISavingsGoalRepository, SqliteSavingsGoalRepository>();
        builder.Services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        builder.Services.AddSingleton<IExportValidator, ExportValidator>();
        builder.Services.AddSingleton<IExportService, RepositoryExportService>();
        builder.Services.AddSingleton<IImportService, RepositoryImportService>();
        builder.Services.AddSingleton<IBackupService, JsonBackupService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<RecurrenceEditorViewModel>();
        builder.Services.AddTransient<ShellViewModel>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
