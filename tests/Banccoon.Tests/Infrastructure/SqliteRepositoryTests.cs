using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
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
                PlannedPaymentAmount: 100m));

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
        var transaction = new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            45.50m,
            account.Id,
            category.Id,
            "Electricity",
            TransactionType.Expense);

        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        await store.Transactions.SaveAsync(transaction);

        var transactions = await store.Transactions.GetByAccountIdAsync(account.Id);

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
            Active: true);

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
    }

    [Fact]
    public async Task SettingsRepository_SaveAndGet_RoundTripsSettings()
    {
        await using var store = new SqliteTestStore();
        var settings = new AppSettings(
            "USD",
            ForecastPeriod.NinetyDays,
            ReminderFrequency.Biweekly,
            DateDisplayFormat.MonthDayYear);

        await store.Settings.SaveAsync(settings);

        var loaded = await store.Settings.GetAsync();

        Assert.Equal(settings, loaded);
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
