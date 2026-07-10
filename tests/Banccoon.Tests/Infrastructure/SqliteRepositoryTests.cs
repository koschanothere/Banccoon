using Banccoon.Core.Forecasting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Statements;
using Xunit;

namespace Banccoon.Tests.Infrastructure;

public sealed class SqliteRepositoryTests
{
    [Fact]
    public async Task AccountRepository_SaveAndGetById_RoundTripsCreditCardDetails()
    {
        await using var store = new SqliteTestStore();
        var account = new Account(
            Guid.NewGuid(),
            "Main credit card",
            AccountType.CreditCard,
            -250.75m,
            "EUR",
            new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero),
            false,
            new CreditCardDetails(
                CurrentDebt: 250.75m,
                StatementDayOfMonth: 10,
                PaymentDueDayOfMonth: 25,
                MinimumPayment: 25m,
                PlannedPaymentAmount: 100m),
            IncludeInDashboardTotals: false,
            AccountNumber: "ACC-001",
            CardLastFourDigits: "1234");

        await store.Accounts.SaveAsync(account);

        var loaded = await store.Accounts.GetByIdAsync(account.Id);

        Assert.Equal(account, loaded);
    }

    [Fact]
    public async Task CategoryRepository_SaveUpdateAndDelete_PersistsChanges()
    {
        await using var store = new SqliteTestStore();
        var category = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);

        await store.Categories.SaveAsync(category);
        var updated = category with { Name = "Groceries" };
        await store.Categories.SaveAsync(updated);
        var saved = await store.Categories.GetByIdAsync(category.Id);
        await store.Categories.DeleteAsync(category.Id);

        var loaded = await store.Categories.GetByIdAsync(category.Id);

        Assert.Equal(updated, saved);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TransactionRepository_SaveAndGetByAccountId_RoundTripsOptionalCategory()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount("Checking");
        var category = new Category(Guid.NewGuid(), "Utilities");
        var scheduledTransaction = new ScheduledTransaction(
            Guid.NewGuid(),
            "Power bill",
            45.50m,
            account.Id,
            category.Id,
            TransactionType.Expense,
            new RecurrenceRule(
                RecurrenceFrequency.Monthly,
                1,
                new DateOnly(2026, 6, 9),
                DayOfMonth: 9),
            new DateOnly(2026, 6, 9),
            Active: true);
        var transaction = new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            45.50m,
            account.Id,
            category.Id,
            "Electricity",
            TransactionType.Expense,
            PaidScheduledTransactionId: scheduledTransaction.Id,
            PaidScheduledOccurrenceDate: new DateOnly(2026, 6, 9),
            Name: "Power bill");

        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        await store.ScheduledTransactions.SaveAsync(scheduledTransaction);
        await store.Transactions.SaveAsync(transaction);

        var transactions = await store.Transactions.GetByAccountIdAsync(account.Id);

        Assert.Equal(transaction, Assert.Single(transactions));
    }

    [Fact]
    public async Task TransactionRepository_SaveAndGetByAccountId_RoundTripsTransferDestination()
    {
        await using var store = new SqliteTestStore();
        var source = CreateAccount("Checking");
        var destination = CreateAccount("Savings");
        var transaction = new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 11),
            100m,
            source.Id,
            null,
            "Transfer to savings",
            TransactionType.Transfer,
            destination.Id);

        await store.Accounts.SaveAsync(source);
        await store.Accounts.SaveAsync(destination);
        await store.Transactions.SaveAsync(transaction);

        var transactions = await store.Transactions.GetByAccountIdAsync(source.Id);

        Assert.Equal(transaction, Assert.Single(transactions));
    }

    [Fact]
    public async Task ScheduledTransactionRepository_SaveAndGetById_RoundTripsRecurrenceRule()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount("Checking");
        var scheduledTransaction = new ScheduledTransaction(
            Guid.NewGuid(),
            "Rent",
            900m,
            account.Id,
            null,
            TransactionType.Expense,
            new RecurrenceRule(
                RecurrenceFrequency.Monthly,
                1,
                new DateOnly(2026, 6, 1),
                DayOfMonth: 25),
            new DateOnly(2026, 6, 25),
            Active: true,
            "Landlord transfer");

        await store.Accounts.SaveAsync(account);
        await store.ScheduledTransactions.SaveAsync(scheduledTransaction);

        var loaded = await store.ScheduledTransactions.GetByIdAsync(scheduledTransaction.Id);

        Assert.Equal(scheduledTransaction, loaded);
    }

    [Fact]
    public async Task SavingsGoalRepository_SaveAndGetAll_RoundTripsGoal()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount("Savings");
        var goal = new SavingsGoal(
            Guid.NewGuid(),
            "Emergency fund",
            5000m,
            1200m,
            new DateOnly(2027, 1, 1),
            account.Id);

        await store.Accounts.SaveAsync(account);
        await store.SavingsGoals.SaveAsync(goal);

        var goals = await store.SavingsGoals.GetAllAsync();

        Assert.Equal(goal, Assert.Single(goals));
    }

    [Fact]
    public async Task SettingsRepository_WhenNoSettingsSaved_ReturnsDefaults()
    {
        await using var store = new SqliteTestStore();

        var settings = await store.Settings.GetAsync();

        Assert.Equal("EUR", settings.DefaultCurrency);
        Assert.Equal(ForecastPeriod.ThirtyDays, settings.DefaultForecastPeriod);
        Assert.Equal(ReminderFrequency.Weekly, settings.ReminderFrequency);
        Assert.Equal(DateDisplayFormat.DayMonthYear, settings.DateDisplayFormat);
        Assert.Equal(AppThemeMode.Light, settings.ThemeMode);
        Assert.Equal(AccentColor.Emerald, settings.AccentColor);
        Assert.Equal(NavigationStyle.Rail, settings.NavigationStyle);
        Assert.False(settings.ShowPowerUserFeatures);
    }

    [Fact]
    public async Task SettingsRepository_SaveAndGet_RoundTripsSettings()
    {
        await using var store = new SqliteTestStore();
        var settings = new AppSettings(
            "USD",
            ForecastPeriod.NinetyDays,
            ReminderFrequency.Biweekly,
            DateDisplayFormat.MonthDayYear,
            AppThemeMode.Dark,
            AccentColor.Blue,
            NavigationStyle.TopTabs,
            ShowPowerUserFeatures: true);

        await store.Settings.SaveAsync(settings);

        var loaded = await store.Settings.GetAsync();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task StatementImportRepository_SaveAndGet_RoundTripsBatchAndRows()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount("Checking");
        var category = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
        var batch = new StatementImportBatch(
            Guid.NewGuid(),
            account.Id,
            "fake",
            "Fake parser",
            "statement.fake",
            "C:\\statement.fake",
            new DateTimeOffset(2026, 6, 12, 9, 0, 0, TimeSpan.Zero),
            StatementImportBatchStatus.PendingReview,
            RowCount: 1);
        var row = new StatementImportRow(
            Guid.NewGuid(),
            batch.Id,
            new DateOnly(2026, 6, 10),
            25m,
            TransactionType.Expense,
            "Lunch",
            "LUNCH",
            "Cafe",
            "ref-1",
            "raw line",
            category.Id,
            null,
            StatementImportRowStatus.Pending,
            IsDuplicate: false,
            null,
            null);

        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        await store.StatementImports.SaveBatchAsync(batch);
        await store.StatementImports.SaveRowAsync(row);

        var batches = await store.StatementImports.GetAllBatchesAsync();
        var rows = await store.StatementImports.GetRowsByBatchIdAsync(batch.Id);

        Assert.Equal(batch, Assert.Single(batches));
        Assert.Equal(row, Assert.Single(rows));
    }

    [Fact]
    public async Task StatementImportRepository_DeleteBatch_RemovesBatchAndRows()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount("Checking");
        var batch = new StatementImportBatch(
            Guid.NewGuid(),
            account.Id,
            "fake",
            "Fake parser",
            "statement.fake",
            "C:\\statement.fake",
            new DateTimeOffset(2026, 6, 12, 9, 0, 0, TimeSpan.Zero),
            StatementImportBatchStatus.PendingReview,
            RowCount: 1);
        var row = new StatementImportRow(
            Guid.NewGuid(),
            batch.Id,
            new DateOnly(2026, 6, 10),
            25m,
            TransactionType.Expense,
            "Lunch",
            "LUNCH",
            null,
            null,
            null,
            null,
            null,
            StatementImportRowStatus.Pending,
            IsDuplicate: false,
            null,
            null);

        await store.Accounts.SaveAsync(account);
        await store.StatementImports.SaveBatchAsync(batch);
        await store.StatementImports.SaveRowAsync(row);

        await store.StatementImports.DeleteBatchAsync(batch.Id);

        Assert.Empty(await store.StatementImports.GetAllBatchesAsync());
        Assert.Empty(await store.StatementImports.GetRowsByBatchIdAsync(batch.Id));
    }

    [Fact]
    public async Task CategoryLearningRuleRepository_SaveAndGet_RoundTripsRule()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount("Checking");
        var category = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
        var rule = new CategoryLearningRule(
            Guid.NewGuid(),
            "Cafe",
            "CAFE",
            TransactionType.Expense,
            category.Id,
            account.Id,
            25m,
            MatchCount: 2,
            new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 12, 9, 0, 0, TimeSpan.Zero));

        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        await store.CategoryLearningRules.SaveAsync(rule);

        var loaded = await store.CategoryLearningRules.GetByIdAsync(rule.Id);

        Assert.Equal(rule, loaded);
    }

    private static Account CreateAccount(string name)
    {
        return new Account(
            Guid.NewGuid(),
            name,
            AccountType.DebitCard,
            1000m,
            "EUR",
            new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero));
    }
}
