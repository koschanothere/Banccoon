using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.App.Services;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Savings;

namespace Banccoon.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private const int DefaultHistoricalDays = 7;
    private const int UpcomingObligationCount = 5;

    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IForecastService forecastService;
    private readonly IAvailableToSpendService availableToSpendService;
    private readonly IFreeToSpendWindowService freeToSpendWindowService;
    private readonly IHistoricalBalanceService historicalBalanceService;
    private readonly IAutoBackupRunner autoBackupRunner;

    // Cached from the most recent InitializeAsync so the graph can be redrawn for a custom range
    // without re-fetching everything from the repositories again.
    private DateOnly today;
    private AppSettings settings = null!;
    private IReadOnlyList<Account> dashboardAccounts = [];
    private HashSet<Guid> dashboardAccountIds = [];
    private IReadOnlyList<ScheduledTransaction> scheduledTransactions = [];
    private IReadOnlyList<Transaction> allTransactions = [];
    private DateOnly defaultRangeStart;
    private DateOnly defaultRangeEnd;

    private bool isLoading;
    private bool isCalcOpen;
    private string freeToSpendText = string.Empty;
    private string currentBalanceText = string.Empty;
    private DashboardPrimaryMetric primaryMetric = DashboardPrimaryMetric.FreeToSpend;
    private string lowestForecastedBalanceText = string.Empty;
    private string reservedForGoalsText = string.Empty;
    private string safetyBufferText = string.Empty;
    private string freeToSpendWindowText = string.Empty;
    private string graphWindowSummaryText = string.Empty;
    private DateTime rangeStartDate;
    private DateTime rangeEndDate;
    private string graphStatusText = string.Empty;
    private int upcomingSectionRow;
    private int analyticsSectionRow;
    private int goalsSectionRow;

    public DashboardViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISettingsRepository settingsRepository,
        IForecastService forecastService,
        IAvailableToSpendService availableToSpendService,
        IFreeToSpendWindowService freeToSpendWindowService,
        IHistoricalBalanceService historicalBalanceService,
        ICategoryRepository categoryRepository,
        IAnalyticsService analyticsService,
        IAutoBackupRunner autoBackupRunner)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.settingsRepository = settingsRepository;
        this.forecastService = forecastService;
        this.availableToSpendService = availableToSpendService;
        this.freeToSpendWindowService = freeToSpendWindowService;
        this.historicalBalanceService = historicalBalanceService;
        this.autoBackupRunner = autoBackupRunner;

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
        ApplyRangeCommand = new RelayCommand(() => RedrawChart());
        ResetRangeCommand = new RelayCommand(() => ResetRange());
        AddGoalCommand = new RelayCommand(() => _ = AddGoalRequested?.Invoke());
    }

    // The Analytics section's "drill down into this category" action needs Shell navigation,
    // which ViewModels in this app don't perform directly (see StatementImportPage.xaml.cs's
    // OnCloseClicked for the established convention) - so it's surfaced as an event for
    // DashboardPage's code-behind to act on instead.
    public event Func<Guid?, Task>? CategoryDrillDownRequested;

    // Goals are Goal-type accounts, created through Accounts' own add form - the page navigates
    // there (preset to AccountType.Goal) rather than this widget growing a second goal editor.
    public event Func<Task>? AddGoalRequested;

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

    public string CurrentBalanceText
    {
        get => currentBalanceText;
        private set => SetProperty(ref currentBalanceText, value);
    }

    public bool IsFreeToSpendPrimary => primaryMetric == DashboardPrimaryMetric.FreeToSpend;

    public bool IsCurrentBalancePrimary => primaryMetric == DashboardPrimaryMetric.CurrentBalance;

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

    // Zero-persistence: a custom range is view-only state, reset every time the dashboard loads
    // rather than saved anywhere (see docs/development-phases.md's Phase 7 note on this).
    public DateTime RangeStartDate
    {
        get => rangeStartDate;
        set => SetProperty(ref rangeStartDate, value);
    }

    public DateTime RangeEndDate
    {
        get => rangeEndDate;
        set => SetProperty(ref rangeEndDate, value);
    }

    public string GraphStatusText
    {
        get => graphStatusText;
        private set => SetProperty(ref graphStatusText, value);
    }

    public ObservableCollection<ForecastChartPointViewModel> ChartPoints { get; }

    public ObservableCollection<UpcomingObligationRowViewModel> UpcomingObligations { get; }

    public ObservableCollection<GoalAccountRowViewModel> Goals { get; }

    public AnalyticsViewModel Analytics { get; }

    public int UpcomingSectionRow
    {
        get => upcomingSectionRow;
        private set => SetProperty(ref upcomingSectionRow, value);
    }

    public int AnalyticsSectionRow
    {
        get => analyticsSectionRow;
        private set => SetProperty(ref analyticsSectionRow, value);
    }

    public int GoalsSectionRow
    {
        get => goalsSectionRow;
        private set => SetProperty(ref goalsSectionRow, value);
    }

    public ICommand ToggleCalcCommand { get; }

    public ICommand ApplyRangeCommand { get; }

    public ICommand ResetRangeCommand { get; }

    public ICommand AddGoalCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            settings = await settingsRepository.GetAsync(cancellationToken);
            PrivacyMode.IsEnabled = settings.PrivacyModeEnabled;
            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            // Archived accounts are history, not money you have: they stay out of the totals, the
            // forecast and free-to-spend even if their "include in totals" flag is still set.
            dashboardAccounts = accounts.Where(account => account.IncludeInDashboardTotals && !account.IsArchived).ToList();
            dashboardAccountIds = dashboardAccounts.Select(account => account.Id).ToHashSet();
            scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
            allTransactions = await transactionRepository.GetAllAsync(cancellationToken);
            // Every non-archived goal, including ones excluded from dashboard totals - excluding
            // one from "free to spend" doesn't stop it being a goal worth tracking here.
            var goalAccounts = accounts
                .Where(account => account.Type == AccountType.Goal && !account.IsArchived)
                .ToList();
            today = dateProvider.Today;
            defaultRangeStart = today.AddDays(-DefaultHistoricalDays);
            defaultRangeEnd = today.AddDays((int)settings.DefaultForecastPeriod - 1);

            var order = DashboardSectionOrdering.Parse(settings.DashboardSectionOrder).ToList();

            await RunOnMainThreadAsync(() =>
            {
                RangeStartDate = defaultRangeStart.ToDateTime(TimeOnly.MinValue);
                RangeEndDate = defaultRangeEnd.ToDateTime(TimeOnly.MinValue);
                GraphStatusText = string.Empty;

                UpcomingSectionRow = order.IndexOf(DashboardSection.Upcoming);
                AnalyticsSectionRow = order.IndexOf(DashboardSection.Analytics);
                GoalsSectionRow = order.IndexOf(DashboardSection.Goals);
            });

            await LoadFreeToSpendAsync(today, settings, dashboardAccounts, scheduledTransactions, goalAccounts);
            await LoadUpcomingObligationsAsync(today, settings, dashboardAccounts, scheduledTransactions);
            await RunOnMainThreadAsync(() => RedrawChart());
            await Analytics.InitializeAsync(settings.DefaultCurrency, cancellationToken);
            await autoBackupRunner.RunIfDueAsync(settings, cancellationToken);
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
        DateOnly asOfToday,
        AppSettings appSettings,
        IReadOnlyList<Account> accountsForTotals,
        IReadOnlyList<ScheduledTransaction> scheduled,
        IReadOnlyList<Account> goalAccounts)
    {
        var window = freeToSpendWindowService.GetWindow(asOfToday, appSettings, scheduled);
        var request = new ForecastRequest(window.StartDate, window.EndDate, accountsForTotals, scheduled);
        var forecast = forecastService.CreateForecast(request);
        // Reserve the money held in goal accounts that the forecast itself counted (see GoalAccounts).
        var breakdown = availableToSpendService.Calculate(
            forecast,
            GoalAccounts.AsSavingsGoals(accountsForTotals),
            appSettings.SafetyBuffer);

        // Called from InitializeAsync after several awaits that may have resumed off the UI
        // thread (see ViewModelBase.RunOnMainThreadAsync) - this is the dashboard's hero card, the
        // first thing shown on every launch, so it's a high-traffic instance of that same bug.
        var currentBalance = accountsForTotals.Sum(account => account.CurrentBalance);

        return RunOnMainThreadAsync(() =>
        {
            FreeToSpendText = MoneyFormat.Format(breakdown.AvailableToSpend, appSettings.DefaultCurrency);
            CurrentBalanceText = MoneyFormat.Format(currentBalance, appSettings.DefaultCurrency);
            primaryMetric = appSettings.DashboardPrimaryMetric;
            OnPropertyChanged(nameof(IsFreeToSpendPrimary));
            OnPropertyChanged(nameof(IsCurrentBalancePrimary));
            LowestForecastedBalanceText = MoneyFormat.Format(breakdown.LowestForecastedBalance, appSettings.DefaultCurrency);
            ReservedForGoalsText = MoneyFormat.Format(-breakdown.ReservedForSavingsGoals, appSettings.DefaultCurrency);
            SafetyBufferText = MoneyFormat.Format(-breakdown.SafetyBuffer, appSettings.DefaultCurrency);
            var windowDayCount = window.EndDate.DayNumber - window.StartDate.DayNumber + 1;
            FreeToSpendWindowText = Translator.GetPlural("Dashboard_FreeToSpendWindowDuration", windowDayCount);

            Goals.Clear();
            foreach (var goalAccount in goalAccounts)
            {
                Goals.Add(new GoalAccountRowViewModel(goalAccount));
            }
        });
    }

    // Deliberately independent of the graph's own (possibly custom, exploratory) date range - this
    // widget exists to surface real upcoming obligations for taking action on, so it always uses
    // the saved forecast period regardless of whatever window the user is currently looking at on
    // the graph.
    private Task LoadUpcomingObligationsAsync(
        DateOnly asOfToday,
        AppSettings appSettings,
        IReadOnlyList<Account> accountsForForecast,
        IReadOnlyList<ScheduledTransaction> scheduled)
    {
        var request = ForecastRequest.ForPeriod(asOfToday, appSettings.DefaultForecastPeriod, accountsForForecast, scheduled);
        var forecast = forecastService.CreateForecast(request);

        return RunOnMainThreadAsync(() =>
        {
            UpcomingObligations.Clear();
            foreach (var obligation in forecast.UpcomingObligations.OrderBy(obligation => obligation.Date).Take(UpcomingObligationCount))
            {
                UpcomingObligations.Add(new UpcomingObligationRowViewModel(obligation, asOfToday, appSettings.DefaultCurrency));
            }
        });
    }

    private void ResetRange()
    {
        RangeStartDate = defaultRangeStart.ToDateTime(TimeOnly.MinValue);
        RangeEndDate = defaultRangeEnd.ToDateTime(TimeOnly.MinValue);
        RedrawChart();
    }

    // Synchronous and UI-thread-only (called from a command or from within an already-marshaled
    // block) - everything it reads was fetched once by InitializeAsync, so redrawing for a new
    // custom range never needs to hit the repositories again.
    private void RedrawChart()
    {
        var rangeStart = DateOnly.FromDateTime(RangeStartDate);
        var rangeEnd = DateOnly.FromDateTime(RangeEndDate);
        if (rangeEnd < rangeStart)
        {
            GraphStatusText = Translator.Get("Dashboard_EndDateMustBeAfterStart");
            return;
        }

        GraphStatusText = string.Empty;
        GraphWindowSummaryText = $"{DateDisplay.Format(rangeStart, settings.DateDisplayFormat)} – {DateDisplay.Format(rangeEnd, settings.DateDisplayFormat)}";

        ChartPoints.Clear();

        // The historical/forecast split always happens at "today", regardless of how far the
        // chosen range reaches in either direction - a forecast can only ever project forward
        // from the account's actual current balance, never from some other day.
        var historicalEnd = rangeStart > today ? rangeStart : (rangeEnd < today ? rangeEnd : today.AddDays(-1));

        // Today's own recorded transactions are what move the line from yesterday's point to
        // today's, so they're listed on today's (first forecast) point below.
        IReadOnlyList<string> todaysRecordedSummaries = [];
        if (rangeStart <= today)
        {
            // Always walk back from today, then drop the days past historicalEnd: the service's
            // currentTotalBalance is the total at the END of its endDate, and the accounts' live
            // balances are the total at the end of today. Walking back from historicalEnd instead
            // would show today's balance as yesterday's (or, for a range entirely in the past,
            // ignore every transaction between the range's end and today).
            var currentTotalBalance = dashboardAccounts.Sum(account => account.CurrentBalance);
            var historicalPoints = historicalBalanceService.GetHistoricalBalances(
                rangeStart,
                today,
                currentTotalBalance,
                dashboardAccountIds,
                allTransactions);

            foreach (var point in historicalPoints.Where(point => point.Date <= historicalEnd))
            {
                ChartPoints.Add(new ForecastChartPointViewModel(
                    point.Date,
                    point.Balance,
                    FormatHistoricalEvents(point.Events),
                    settings.DefaultCurrency,
                    settings.DateDisplayFormat,
                    isHistorical: true));
            }

            todaysRecordedSummaries = FormatHistoricalEvents(
                historicalPoints.FirstOrDefault(point => point.Date == today)?.Events ?? []);
        }

        if (rangeEnd >= today)
        {
            var forecastStart = rangeStart > today ? rangeStart : today;
            var graphRequest = new ForecastRequest(forecastStart, rangeEnd, dashboardAccounts, scheduledTransactions);
            var graphForecast = forecastService.CreateForecast(graphRequest);
            var eventsByDate = graphForecast.Events.ToLookup(forecastEvent => forecastEvent.Date);
            var todaysRecordedSummariesShown = false;

            foreach (var point in graphForecast.ProjectedBalances)
            {
                var eventSummaries = eventsByDate[point.Date]
                    .Select(forecastEvent => ChartEventSummaryFormat.Format(forecastEvent, settings.DefaultCurrency))
                    .ToArray();

                // Only on the first of today's points - a day with several scheduled events has
                // several points dated today, and repeating the list on each would be noise.
                if (point.Date == today && !todaysRecordedSummariesShown)
                {
                    eventSummaries = [.. todaysRecordedSummaries, .. eventSummaries];
                    todaysRecordedSummariesShown = true;
                }

                ChartPoints.Add(new ForecastChartPointViewModel(
                    point.Date,
                    point.Balance,
                    eventSummaries,
                    settings.DefaultCurrency,
                    settings.DateDisplayFormat,
                    isCurrentDate: point.Date == today));
            }
        }
    }

    private string[] FormatHistoricalEvents(IReadOnlyList<HistoricalBalanceEvent> events)
    {
        return events
            .Select(historicalEvent => ChartEventSummaryFormat.Format(historicalEvent, settings.DefaultCurrency))
            .ToArray();
    }
}
