using Banccoon.App.ViewModels;
using Banccoon.App.Views;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Recurrence;
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
        builder.Services.AddSingleton<IRecurrenceService, RecurrenceService>();
        builder.Services.AddSingleton<IScheduledTransactionProjectionService, ScheduledTransactionProjectionService>();
        builder.Services.AddSingleton<IAccountBalanceService, AccountBalanceService>();
        builder.Services.AddSingleton<IForecastService, ForecastService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
