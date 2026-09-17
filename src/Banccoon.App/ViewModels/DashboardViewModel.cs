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
    private const int HistoricalDays = 7;

    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IForecastService forecastService;
    private readonly IAvailableToSpendService availableToSpendService;
    private readonly IFreeToSpendWindowService freeToSpendWindowService;
    private readonly IHistoricalBalanceService historicalBalanceService;

    private bool isLoading;
    private bool isCalcOpen;
    private string freeToSpendText = string.Empty;
    private string lowestForecastedBalanceText = string.Empty;
    private string reservedForGoalsText = string.Empty;
    private string safetyBufferText = string.Empty;
    private string freeToSpendWindowText = string.Empty;
    private string graphWindowSummaryText = string.Empty;

    public DashboardViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        ISettingsRepository settingsRepository,
        IForecastService forecastService,
        IAvailableToSpendService availableToSpendService,
        IFreeToSpendWindowService freeToSpendWindowService,
        IHistoricalBalanceService historicalBalanceService)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.settingsRepository = settingsRepository;
        this.forecastService = forecastService;
        this.availableToSpendService = availableToSpendService;
        this.freeToSpendWindowService = freeToSpendWindowService;
        this.historicalBalanceService = historicalBalanceService;

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

    public string SafetyBufferText
    {
        get => safetyBufferText;
        private set => SetProperty(ref safetyBufferText, value);
    }

    public string FreeToSpendWindowText
    {
        get => freeToSpendWindowText;
        private set => SetProperty(ref freeToSpendWindowText, value);
    }

    public string GraphWindowSummaryText
    {
        get => graphWindowSummaryText;
        private set => SetProperty(ref graphWindowSummaryText, value);
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
            var dashboardAccountIds = dashboardAccounts.Select(account => account.Id).ToHashSet();
            var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
            var savingsGoals = await savingsGoalRepository.GetAllAsync(cancellationToken);
            var today = dateProvider.Today;

            await LoadFreeToSpendAsync(today, settings, dashboardAccounts, scheduledTransactions, savingsGoals);
            await LoadChartAsync(today, settings, dashboardAccounts, dashboardAccountIds, scheduledTransactions, cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task LoadFreeToSpendAsync(
        DateOnly today,
        AppSettings settings,
        IReadOnlyList<Account> dashboardAccounts,
        IReadOnlyList<ScheduledTransaction> scheduledTransactions,
        IReadOnlyList<SavingsGoal> savingsGoals)
    {
        var window = freeToSpendWindowService.GetWindow(today, settings, scheduledTransactions);
        var request = new ForecastRequest(window.StartDate, window.EndDate, dashboardAccounts, scheduledTransactions);
        var forecast = forecastService.CreateForecast(request);
        var breakdown = availableToSpendService.Calculate(forecast, savingsGoals, settings.SafetyBuffer);

        FreeToSpendText = MoneyFormat.Format(breakdown.AvailableToSpend, settings.DefaultCurrency);
        LowestForecastedBalanceText = MoneyFormat.Format(breakdown.LowestForecastedBalance, settings.DefaultCurrency);
        ReservedForGoalsText = MoneyFormat.Format(-breakdown.ReservedForSavingsGoals, settings.DefaultCurrency);
        SafetyBufferText = MoneyFormat.Format(-breakdown.SafetyBuffer, settings.DefaultCurrency);
        FreeToSpendWindowText = $"{DateDisplay.Format(window.StartDate, settings.DateDisplayFormat)} – {DateDisplay.Format(window.EndDate, settings.DateDisplayFormat)}";

        return Task.CompletedTask;
    }

    private async Task LoadChartAsync(
        DateOnly today,
        AppSettings settings,
        IReadOnlyList<Account> dashboardAccounts,
        HashSet<Guid> dashboardAccountIds,
        IReadOnlyList<ScheduledTransaction> scheduledTransactions,
        CancellationToken cancellationToken)
    {
        var graphRequest = ForecastRequest.ForPeriod(today, settings.DefaultForecastPeriod, dashboardAccounts, scheduledTransactions);
        var graphForecast = forecastService.CreateForecast(graphRequest);
        GraphWindowSummaryText = $"{DateDisplay.Format(today.AddDays(-HistoricalDays), settings.DateDisplayFormat)} – {DateDisplay.Format(graphForecast.EndDate, settings.DateDisplayFormat)}";

        var allTransactions = await transactionRepository.GetAllAsync(cancellationToken);
        var currentTotalBalance = dashboardAccounts.Sum(account => account.CurrentBalance);
        var historicalPoints = historicalBalanceService.GetHistoricalBalances(
            today.AddDays(-HistoricalDays),
            today,
            currentTotalBalance,
            dashboardAccountIds,
            allTransactions);

        var eventsByDate = graphForecast.Events.ToLookup(forecastEvent => forecastEvent.Date);

        // Mutates a collection bound to live UI (the balance chart) - must run on the UI thread,
        // which the awaits above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            ChartPoints.Clear();
            foreach (var point in historicalPoints.Where(point => point.Date < today))
            {
                ChartPoints.Add(new ForecastChartPointViewModel(
                    point.Date,
                    point.Balance,
                    Array.Empty<string>(),
                    settings.DefaultCurrency,
                    settings.DateDisplayFormat,
                    isHistorical: true));
            }

            foreach (var point in graphForecast.ProjectedBalances)
            {
                var eventSummaries = eventsByDate[point.Date]
                    .Select(forecastEvent => $"{forecastEvent.Name}: {MoneyFormat.Format(forecastEvent.SignedAmount, settings.DefaultCurrency)}")
                    .ToArray();

                ChartPoints.Add(new ForecastChartPointViewModel(
                    point.Date,
                    point.Balance,
                    eventSummaries,
                    settings.DefaultCurrency,
                    settings.DateDisplayFormat,
                    isCurrentDate: point.Date == today));
            }
        });
    }
}
