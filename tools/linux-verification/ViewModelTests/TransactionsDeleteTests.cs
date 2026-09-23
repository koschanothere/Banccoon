using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Reconciliation;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Transactions;
using Banccoon.Tests.Infrastructure;
using Xunit;

public sealed class TransactionsDeleteTests
{
    [Fact]
    public async Task DeleteSelected_RemovesTheTransactionsAndPutsTheirMoneyBack()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var card = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        var savings = new Account(Guid.NewGuid(), "Savings", AccountType.Savings, 0m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(card);
        await store.Accounts.SaveAsync(savings);
        await RecordAsync(store, new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 10), 40m, card.Id, null, null, TransactionType.Expense, Name: "Lunch"));
        await RecordAsync(store, new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 11), 100m, card.Id, null, null, TransactionType.Transfer, savings.Id, Name: "To savings"));
        await RecordAsync(store, new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 12), 7m, card.Id, null, null, TransactionType.Expense, Name: "Coffee"));
        var vm = CreateViewModel(store);
        await vm.InitializeAsync();
        Assert.Equal(3, vm.Rows.Count);

        vm.ToggleSelectModeCommand.Execute(null);
        vm.Rows.Single(r => r.Name == "Lunch").IsSelected = true;
        vm.Rows.Single(r => r.Name == "To savings").IsSelected = true;
        vm.DeleteSelectedCommand.Execute(null);
        await Task.Delay(300);

        Assert.Equal(["Coffee"], vm.Rows.Select(r => r.Name));
        Assert.Equal(993m, (await store.Accounts.GetByIdAsync(card.Id))!.CurrentBalance);
        Assert.Equal(0m, (await store.Accounts.GetByIdAsync(savings.Id))!.CurrentBalance);
    }

    private static async Task RecordAsync(SqliteTestStore store, Transaction transaction)
    {
        var accounts = (await store.Accounts.GetAllAsync()).ToDictionary(a => a.Id);
        foreach (var updated in new TransactionApplicationService(new TransactionBalanceService()).ApplyNewTransaction(transaction, accounts))
        {
            await store.Accounts.SaveAsync(updated);
        }

        await store.Transactions.SaveAsync(transaction);
    }

    private static TransactionsViewModel CreateViewModel(SqliteTestStore store)
    {
        var applicationService = new TransactionApplicationService(new TransactionBalanceService());
        return new TransactionsViewModel(
            new FixedDate(new DateOnly(2026, 6, 15)),
            store.Accounts,
            store.Categories,
            store.Transactions,
            store.ScheduledTransactions,
            store.ScheduledOccurrenceOverrides,
            store.Settings,
            new ScheduledTransactionProjectionService(new RecurrenceService()),
            new ScheduledOccurrenceResolutionService(),
            applicationService,
            new TransactionBalanceHistoryService(new TransactionBalanceService()),
            new RecurrenceDescriptionService(),
            new RecurrenceSyntaxService(),
            new RecurrenceValidationService(),
            new ExpectedTransactionMatcher(),
            new TransactionDeletionService(store.Transactions, store.Accounts, applicationService));
    }

    private sealed class FixedDate(DateOnly today) : IDateProvider
    {
        public DateOnly Today { get; } = today;
    }
}
