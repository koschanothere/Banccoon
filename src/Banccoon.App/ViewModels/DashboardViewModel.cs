using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IForecastService forecastService;
    private readonly IAvailableToSpendService availableToSpendService;

    private bool isLoading;
    private bool isCalcOpen;
    private string freeToSpendText = string.Empty;
    private string lowestForecastedBalanceText = string.Empty;
    private string reservedForGoalsText = string.Empty;
    private string windowSummaryText = string.Empty;

    public DashboardViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        ISettingsRepository settingsRepository,
        IForecastService forecastService,
        IAvailableToSpendService availableToSpendService)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.settingsRepository = settingsRepository;
        this.forecastService = forecastService;
        this.availableToSpendService = availableToSpendService;

        ChartPoints = [];
        ToggleCalcCommand = new RelayCommand(() => IsCalcOpen = !IsCalcOpen);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool IsCalcOpen
    {
        get => isCalcOpen;
        private set => SetProperty(ref isCalcOpen, value);
    }

    public string FreeToSpendText
    {
        get => freeToSpendText;
        private set => SetProperty(ref freeToSpendText, value);
    }

    public string LowestForecastedBalanceText
    {
        get => lowestForecastedBalanceText;
        private set => SetProperty(ref lowestForecastedBalanceText, value);
    }

    public string ReservedForGoalsText
    {
        get => reservedForGoalsText;
        private set => SetProperty(ref reservedForGoalsText, value);
    }

    public string WindowSummaryText
    {
        get => windowSummaryText;
        private set => SetProperty(ref windowSummaryText, value);
    }

    public ObservableCollection<ForecastChartPointViewModel> ChartPoints { get; }

    public ICommand ToggleCalcCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var settings = await settingsRepository.GetAsync(cancellationToken);
            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            var dashboardAccounts = accounts.Where(account => account.IncludeInDashboardTotals).ToList();
            var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
            var savingsGoals = await savingsGoalRepository.GetAllAsync(cancellationToken);

            var request = ForecastRequest.ForPeriod(
                dateProvider.Today,
                settings.DefaultForecastPeriod,
                dashboardAccounts,
                scheduledTransactions);
            var forecast = forecastService.CreateForecast(request);
            var breakdown = availableToSpendService.Calculate(forecast, savingsGoals);

            FreeToSpendText = MoneyFormat.Format(breakdown.AvailableToSpend, settings.DefaultCurrency);
            LowestForecastedBalanceText = MoneyFormat.Format(breakdown.LowestForecastedBalance, settings.DefaultCurrency);
            ReservedForGoalsText = MoneyFormat.Format(-breakdown.ReservedForSavingsGoals, settings.DefaultCurrency);
            WindowSummaryText = $"{DateDisplay.Format(forecast.StartDate, settings.DateDisplayFormat)} – {DateDisplay.Format(forecast.EndDate, settings.DateDisplayFormat)}";

            ChartPoints.Clear();
            foreach (var point in forecast.ProjectedBalances)
            {
                var eventSummaries = forecast.Events
                    .Where(forecastEvent => forecastEvent.Date == point.Date)
                    .Select(forecastEvent => $"{forecastEvent.Name}: {MoneyFormat.Format(forecastEvent.SignedAmount, settings.DefaultCurrency)}")
                    .ToArray();

                ChartPoints.Add(new ForecastChartPointViewModel(
                    point.Date,
                    point.Balance,
                    eventSummaries,
                    settings.DefaultCurrency,
                    settings.DateDisplayFormat,
                    isCurrentDate: point.Date == dateProvider.Today));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
