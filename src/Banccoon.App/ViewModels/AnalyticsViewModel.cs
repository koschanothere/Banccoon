using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AnalyticsViewModel : ViewModelBase
{
    private const int TrendPeriodCount = 6;

    private readonly IDateProvider dateProvider;
    private readonly ITransactionRepository transactionRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAnalyticsService analyticsService;
    private readonly Func<Guid?, Task> onCategorySelected;

    private DateOnly periodAnchor;
    private DateOnly today;
    private string currency = "EUR";
    private string monthLabel = string.Empty;
    private string incomeText = string.Empty;
    private string expenseText = string.Empty;
    private string netText = string.Empty;
    private bool canGoToNextMonth;

    public AnalyticsViewModel(
        IDateProvider dateProvider,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IAnalyticsService analyticsService,
        Func<Guid?, Task> onCategorySelected)
    {
        this.dateProvider = dateProvider;
        this.transactionRepository = transactionRepository;
        this.categoryRepository = categoryRepository;
        this.analyticsService = analyticsService;
        this.onCategorySelected = onCategorySelected;

        CategoryRows = [];
        TopMovers = [];

        PreviousMonthCommand = new RelayCommand(() => _ = ChangeMonthAsync(-1));
        NextMonthCommand = new RelayCommand(() => _ = ChangeMonthAsync(1));
    }

    public string MonthLabel
    {
        get => monthLabel;
        private set => SetProperty(ref monthLabel, value);
    }

    public string IncomeText
    {
        get => incomeText;
        private set => SetProperty(ref incomeText, value);
    }

    public string ExpenseText
    {
        get => expenseText;
        private set => SetProperty(ref expenseText, value);
    }

    public string NetText
    {
        get => netText;
        private set => SetProperty(ref netText, value);
    }

    public bool CanGoToNextMonth
    {
        get => canGoToNextMonth;
        private set => SetProperty(ref canGoToNextMonth, value);
    }

    public ObservableCollection<AnalyticsCategoryRowViewModel> CategoryRows { get; }

    public ObservableCollection<AnalyticsCategoryRowViewModel> TopMovers { get; }

    public ICommand PreviousMonthCommand { get; }

    public ICommand NextMonthCommand { get; }

    public async Task InitializeAsync(string defaultCurrency, CancellationToken cancellationToken = default)
    {
        currency = defaultCurrency;
        today = dateProvider.Today;
        periodAnchor = today;
        await RefreshAsync(cancellationToken);
    }

    private async Task ChangeMonthAsync(int monthDelta)
    {
        periodAnchor = periodAnchor.AddMonths(monthDelta);
        await RefreshAsync();
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var report = analyticsService.BuildReport(periodAnchor, TrendPeriodCount, transactions, categories);

        // Mutates collections bound to live UI after awaits that may have resumed off the UI
        // thread (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            MonthLabel = report.CurrentPeriodStart.ToString("MMMM yyyy");
            IncomeText = MoneyFormat.Format(report.CurrentPeriodIncome, currency);
            ExpenseText = MoneyFormat.Format(-report.CurrentPeriodExpense, currency);
            NetText = MoneyFormat.Format(report.CurrentPeriodIncome - report.CurrentPeriodExpense, currency);

            CategoryRows.Clear();
            foreach (var trend in report.CategoryTrends)
            {
                CategoryRows.Add(new AnalyticsCategoryRowViewModel(trend, currency, onCategorySelected));
            }

            TopMovers.Clear();
            foreach (var trend in report.TopMovers)
            {
                TopMovers.Add(new AnalyticsCategoryRowViewModel(trend, currency, onCategorySelected));
            }

            var anchorMonth = new DateOnly(periodAnchor.Year, periodAnchor.Month, 1);
            var todayMonth = new DateOnly(today.Year, today.Month, 1);
            CanGoToNextMonth = anchorMonth < todayMonth;
        });
    }
}
