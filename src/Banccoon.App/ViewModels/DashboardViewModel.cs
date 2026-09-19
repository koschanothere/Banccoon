using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private const int HistoricalDays = 7;
    private const int UpcomingObligationCount = 5;

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
    private ForecastPeriod selectedForecastPeriod = ForecastPeriod.ThirtyDays;
    private string forecastEndBalanceText = string.Empty;
    private string forecastStatusText = string.Empty;

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
        IHistoricalBalanceService historicalBalanceService,
        ICategoryRepository categoryRepository,
        IAnalyticsService analyticsService)
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
        UpcomingObligations = [];
        Goals = [];
        Analytics = new AnalyticsViewModel(
            dateProvider,
            transactionRepository,
            categoryRepository,
            analyticsService,
            categoryId => RaiseCategoryDrillDownRequested(categoryId));
        ToggleCalcCommand = new RelayCommand(() => IsCalcOpen = !IsCalcOpen);
        SaveForecastPeriodCommand = new RelayCommand(() => _ = SaveForecastPeriodAsync());
    }

    // The Analytics section's "drill down into this category" action needs Shell navigation,
    // which ViewModels in this app don't perform directly (see StatementImportPage.xaml.cs's
    // OnCloseClicked for the established convention) - so it's surfaced as an event for
    // DashboardPage's code-behind to act on instead.
    public event Func<Guid?, Task>? CategoryDrillDownRequested;

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

    public IReadOnlyList<ForecastPeriod> ForecastPeriods { get; } = Enum.GetValues<ForecastPeriod>();

    public ForecastPeriod SelectedForecastPeriod
    {
        get => selectedForecastPeriod;
        set => SetProperty(ref selectedForecastPeriod, value);
    }

    public string ForecastEndBalanceText
    {
        get => forecastEndBalanceText;
        private set => SetProperty(ref forecastEndBalanceText, value);
    }

    public string ForecastStatusText
    {
        get => forecastStatusText;
        private set => SetProperty(ref forecastStatusText, value);
    }

    public ObservableCollection<UpcomingObligationRowViewModel> UpcomingObligations { get; }

    public ObservableCollection<SavingsGoalRowViewModel> Goals { get; }

    public AnalyticsViewModel Analytics { get; }

    public ICommand ToggleCalcCommand { get; }

    public ICommand SaveForecastPeriodCommand { get; }

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

            await RunOnMainThreadAsync(() =>
            {
                SelectedForecastPeriod = settings.DefaultForecastPeriod;
                ForecastStatusText = string.Empty;
            });

            await LoadFreeToSpendAsync(today, settings, dashboardAccounts, scheduledTransactions, savingsGoals);
            await LoadChartAsync(today, settings, dashboardAccounts, dashboardAccountIds, scheduledTransactions, cancellationToken);
            await Analytics.InitializeAsync(settings.DefaultCurrency, cancellationToken);
        }
        finally
        {
            // Touches UI-bound state after an await that may have resumed off the UI thread (see
            // ViewModelBase.RunOnMainThreadAsync).
            await RunOnMainThreadAsync(() => IsLoading = false);
        }
    }

    private Task RaiseCategoryDrillDownRequested(Guid? categoryId)
    {
        return CategoryDrillDownRequested?.Invoke(categoryId) ?? Task.CompletedTask;
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

        // Called from InitializeAsync after several awaits that may have resumed off the UI
        // thread (see ViewModelBase.RunOnMainThreadAsync) - this is the dashboard's hero card, the
        // first thing shown on every launch, so it's a high-traffic instance of that same bug.
        return RunOnMainThreadAsync(() =>
        {
            FreeToSpendText = MoneyFormat.Format(breakdown.AvailableToSpend, settings.DefaultCurrency);
            LowestForecastedBalanceText = MoneyFormat.Format(breakdown.LowestForecastedBalance, settings.DefaultCurrency);
            ReservedForGoalsText = MoneyFormat.Format(-breakdown.ReservedForSavingsGoals, settings.DefaultCurrency);
            SafetyBufferText = MoneyFormat.Format(-breakdown.SafetyBuffer, settings.DefaultCurrency);
            FreeToSpendWindowText = $"{DateDisplay.Format(window.StartDate, settings.DateDisplayFormat)} – {DateDisplay.Format(window.EndDate, settings.DateDisplayFormat)}";

            Goals.Clear();
            foreach (var goal in savingsGoals)
            {
                Goals.Add(new SavingsGoalRowViewModel(goal, settings.DefaultCurrency, settings.DateDisplayFormat));
            }
        });
    }

    private async Task SaveForecastPeriodAsync()
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { DefaultForecastPeriod = SelectedForecastPeriod });

        await RunOnMainThreadAsync(() => ForecastStatusText = "Saved.");
        await InitializeAsync();
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

        // This method is itself called after InitializeAsync's own awaits, which may have already
        // resumed off the UI thread (see ViewModelBase.RunOnMainThreadAsync) - so even this
        // "before my own first await" mutation isn't safe by default.
        await RunOnMainThreadAsync(() =>
        {
            GraphWindowSummaryText = $"{DateDisplay.Format(today.AddDays(-HistoricalDays), settings.DateDisplayFormat)} – {DateDisplay.Format(graphForecast.EndDate, settings.DateDisplayFormat)}";
            ForecastEndBalanceText = MoneyFormat.Format(graphForecast.ForecastedBalance, settings.DefaultCurrency);
        });

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

            UpcomingObligations.Clear();
            foreach (var obligation in graphForecast.UpcomingObligations.OrderBy(obligation => obligation.Date).Take(UpcomingObligationCount))
            {
                UpcomingObligations.Add(new UpcomingObligationRowViewModel(obligation, today, settings.DefaultCurrency));
            }
        });
    }
}
