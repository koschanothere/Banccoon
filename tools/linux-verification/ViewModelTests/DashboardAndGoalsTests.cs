using Banccoon.App.Localization;
using Banccoon.App.Services;
using Banccoon.App.ViewModels;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Savings;
using Banccoon.Tests.Infrastructure;
using Xunit;

public sealed class DashboardAndGoalsTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    [Fact]
    public async Task Chart_HistoricalPointsCarryTheDaysRealTransactions_AndWalkBackFromToday()
    {
        await using var store = await CreateStoreAsync();
        var card = await AddAccountAsync(store, "Card", AccountType.DebitCard, 1000m);
        await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), Today.AddDays(-2), 200m, card.Id, null, null, TransactionType.Expense, Name: "Groceries"));
        await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), Today, 50m, card.Id, null, null, TransactionType.Expense, Name: "Coffee"));
        var vm = CreateDashboard(store);

        await vm.InitializeAsync();

        var historical = vm.ChartPoints.Where(p => p.IsHistorical).ToList();
        Assert.Equal(7, historical.Count);
        var yesterday = historical.Single(p => p.Date == Today.AddDays(-1));
        // Walks back from TODAY: today's 50 coffee is reversed, so yesterday ended at 1050 (the old
        // code walked back from yesterday using today's balance and showed 1000 here).
        Assert.Equal(1050m, yesterday.Balance);
        Assert.Equal(Translator.Get("ForecastChart_NoAccountChanges"), yesterday.EventsText);
        var groceriesDay = historical.Single(p => p.Date == Today.AddDays(-2));
        Assert.Equal("Groceries: RUB -200.00", groceriesDay.EventsText);
        Assert.Equal(1050m, groceriesDay.Balance);
        Assert.Equal(1250m, historical.Single(p => p.Date == Today.AddDays(-3)).Balance);

        var todayPoint = vm.ChartPoints.First(p => p.Date == Today);
        Assert.True(todayPoint.IsCurrentDate);
        Assert.Equal("Coffee: RUB -50.00", todayPoint.EventsText);
        Assert.Equal(1000m, todayPoint.Balance);
    }

    [Fact]
    public async Task Chart_FullyPastCustomRange_AccountsForEverythingAfterIt()
    {
        await using var store = await CreateStoreAsync();
        var card = await AddAccountAsync(store, "Card", AccountType.DebitCard, 1000m);
        await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), Today.AddDays(-5), 300m, card.Id, null, null, TransactionType.Expense, Name: "Rent part"));
        var vm = CreateDashboard(store);
        await vm.InitializeAsync();

        vm.RangeStartDate = Today.AddDays(-20).ToDateTime(TimeOnly.MinValue);
        vm.RangeEndDate = Today.AddDays(-10).ToDateTime(TimeOnly.MinValue);
        vm.ApplyRangeCommand.Execute(null);

        Assert.All(vm.ChartPoints, p => Assert.True(p.IsHistorical));
        // The 300 spent 5 days ago (after this range) must be reversed: the range's days sat at 1300.
        Assert.All(vm.ChartPoints, p => Assert.Equal(1300m, p.Balance));
    }

    [Fact]
    public async Task Goals_WidgetListsNonArchivedGoalAccounts_WithAccountsProgressPresentation()
    {
        await using var store = await CreateStoreAsync();
        await AddAccountAsync(store, "Card", AccountType.DebitCard, 1000m);
        await AddAccountAsync(store, "Vacation", AccountType.Goal, 300m, target: 1000m);
        await AddAccountAsync(store, "Rainy day", AccountType.Goal, 50m, includeInTotals: false);
        await AddAccountAsync(store, "Old goal", AccountType.Goal, 10m, archived: true);
        await store.SavingsGoals.SaveAsync(new SavingsGoal(Guid.NewGuid(), "Legacy model goal", 999m, 999m, null));
        var vm = CreateDashboard(store);

        await vm.InitializeAsync();

        Assert.Equal(["Rainy day", "Vacation"], vm.Goals.Select(g => g.Name)); // repository order: SortOrder, then name
        var vacation = vm.Goals.Single(g => g.Name == "Vacation");
        Assert.True(vacation.HasTarget);
        Assert.Equal(0.3d, vacation.Progress, 3);
        Assert.Equal("RUB 300.00", vacation.CurrentText);
        Assert.Equal("RUB 1,000.00", vacation.TargetText);
        Assert.False(vm.Goals.Single(g => g.Name == "Rainy day").HasTarget);
    }

    [Fact]
    public async Task FreeToSpend_ReservesMoneyInGoalAccountsCountedInTotals_NotTheLegacyModel()
    {
        await using var store = await CreateStoreAsync();
        await AddAccountAsync(store, "Card", AccountType.DebitCard, 1000m);
        await AddAccountAsync(store, "Vacation", AccountType.Goal, 300m, target: 1000m);
        await AddAccountAsync(store, "Excluded goal", AccountType.Goal, 500m, includeInTotals: false);
        await store.SavingsGoals.SaveAsync(new SavingsGoal(Guid.NewGuid(), "Legacy model goal", 999m, 999m, null));
        var vm = CreateDashboard(store);

        await vm.InitializeAsync();

        Assert.Equal("RUB -300.00", vm.ReservedForGoalsText);
        Assert.Equal("RUB 1,000.00", vm.FreeToSpendText);
    }

    [Fact]
    public async Task Totals_LeaveOutArchivedAccountsEvenWhenFlaggedIncluded()
    {
        await using var store = await CreateStoreAsync();
        var card = await AddAccountAsync(store, "Card", AccountType.DebitCard, 1000m);
        await AddAccountAsync(store, "Closed card", AccountType.DebitCard, 400m, archived: true);
        await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), Today.AddDays(-1), 10m, card.Id, null, null, TransactionType.Expense, Name: "Lunch"));
        var vm = CreateDashboard(store);

        await vm.InitializeAsync();

        Assert.Equal("RUB 1,000.00", vm.CurrentBalanceText);
        Assert.Equal("RUB 1,000.00", vm.FreeToSpendText);
        Assert.Equal(1000m, vm.ChartPoints.First(p => p.Date == Today).Balance);
        Assert.Equal(1010m, vm.ChartPoints.Single(p => p.Date == Today.AddDays(-2)).Balance);
    }

    [Fact]
    public async Task AddGoal_RaisesTheNavigationRequest()
    {
        await using var store = await CreateStoreAsync();
        var vm = CreateDashboard(store);
        var raised = 0;
        vm.AddGoalRequested += () => { raised++; return Task.CompletedTask; };

        vm.AddGoalCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Accounts_PendingAddGoal_OpensTheAddFormPresetToGoal()
    {
        await using var store = await CreateStoreAsync();
        var vm = new AccountsViewModel(store.Accounts, store.Settings, new CreditCardForecastService(), new FixedDate(Today));
        vm.SetPendingAddAccountType(AccountType.Goal);

        await vm.InitializeAsync();

        Assert.True(vm.Form.IsOpen);
        Assert.Equal(AccountType.Goal, vm.Form.Type);
        Assert.True(vm.Form.IsGoalType);

        vm.Form.Close();
        await vm.InitializeAsync();
        Assert.False(vm.Form.IsOpen);
    }

    private static async Task<SqliteTestStore> CreateStoreAsync()
    {
        Translator.SetLanguage("en");
        var store = new SqliteTestStore();
        await store.Settings.SaveAsync((await store.Settings.GetAsync()) with { DefaultCurrency = "RUB", SafetyBuffer = 0m });
        return store;
    }

    private static async Task<Account> AddAccountAsync(SqliteTestStore store, string name, AccountType type, decimal balance, decimal? target = null, bool includeInTotals = true, bool archived = false)
    {
        var account = new Account(Guid.NewGuid(), name, type, balance, "RUB", DateTimeOffset.UtcNow, IsArchived: archived, IncludeInDashboardTotals: includeInTotals, PlanningValue: target);
        await store.Accounts.SaveAsync(account);
        return account;
    }

    private static DashboardViewModel CreateDashboard(SqliteTestStore store)
    {
        var projection = new ScheduledTransactionProjectionService(new RecurrenceService());
        return new DashboardViewModel(
            new FixedDate(Today),
            store.Accounts,
            store.Transactions,
            store.ScheduledTransactions,
            store.Settings,
            new ForecastService(new AccountBalanceService(), projection),
            new AvailableToSpendService(new SavingsGoalAllocationService()),
            new FreeToSpendWindowService(projection),
            new HistoricalBalanceService(),
            store.Categories,
            new AnalyticsService(),
            new NoBackups());
    }

    private sealed class FixedDate(DateOnly today) : IDateProvider
    {
        public DateOnly Today { get; } = today;
    }

    private sealed class NoBackups : IAutoBackupRunner
    {
        public Task RunIfDueAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
