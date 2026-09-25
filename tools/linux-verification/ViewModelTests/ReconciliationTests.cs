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

public sealed class ReconciliationTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly RentDue = new(2026, 6, 10);

    [Fact]
    public async Task Attach_LinksTheImportedPaymentWithoutCreatingAnotherTransaction()
    {
        await using var f = await Fixture.CreateAsync(balance: 300m, withRent: true, recordedRentPayment: true);
        var vm = f.Vm;
        await vm.InitializeAsync(fromStatementAccountId: f.AccountId);

        Assert.True(vm.IsPrefilledFromStatement);
        Assert.True(vm.IsMatched);
        await f.RunAsync(vm.ContinueCommand);
        Assert.True(vm.IsExpectedStep);

        var rent = Assert.Single(vm.Expected.Rows);
        // Nothing is suggested up front (2026-09-25); "Attach…" opens the search.
        Assert.False(rent.IsAttachOpen);
        Assert.Empty(rent.AttachChoices);
        rent.ToggleAttachCommand.Execute(null);
        rent.AttachSearchText = "no such thing";
        Assert.True(rent.HasNoAttachChoices);
        rent.AttachSearchText = string.Empty;
        await f.RunAsync(Assert.Single(rent.AttachChoices).AttachCommand);

        Assert.Empty(vm.Expected.Rows);
        var transactions = await f.Store.Transactions.GetAllAsync();
        var payment = Assert.Single(transactions);
        Assert.Equal(f.RentId, payment.PaidScheduledTransactionId);
        Assert.Equal(RentDue, payment.PaidScheduledOccurrenceDate);
        Assert.Equal(300m, (await f.Store.Accounts.GetByIdAsync(f.AccountId))!.CurrentBalance);
        Assert.True(vm.IsMatched);
    }

    // The old suggestion only offered same-account, same-type payments within a week and 25% of the
    // amount, and nothing else could be attached. Now any unlinked recorded transaction can be.
    [Fact]
    public async Task Attach_CanPickATransactionTheOldSuggestionNeverOffered()
    {
        await using var f = await Fixture.CreateAsync(balance: 300m, withRent: true);
        var odd = new Transaction(Guid.NewGuid(), RentDue.AddDays(-20), 650m, f.AccountId, null, null, TransactionType.Expense, Name: "Landlord, cash");
        var coffee = new Transaction(Guid.NewGuid(), RentDue, 4m, f.AccountId, null, null, TransactionType.Expense, Name: "Coffee");
        await f.Store.Transactions.SaveAsync(odd);
        await f.Store.Transactions.SaveAsync(coffee);
        var vm = f.Vm;
        await vm.InitializeAsync(f.AccountId);
        await f.RunAsync(vm.ContinueCommand);
        var rent = Assert.Single(vm.Expected.Rows);

        rent.ToggleAttachCommand.Execute(null);
        // Nearest the due date first, whatever the amount.
        Assert.Equal(["Coffee", "Landlord, cash"], rent.AttachChoices.Select(c => c.Name));
        rent.AttachSearchText = "650";
        await f.RunAsync(Assert.Single(rent.AttachChoices).AttachCommand);

        Assert.Empty(vm.Expected.Rows);
        var attached = await f.Store.Transactions.GetByIdAsync(odd.Id);
        Assert.Equal(f.RentId, attached!.PaidScheduledTransactionId);
        Assert.Null((await f.Store.Transactions.GetByIdAsync(coffee.Id))!.PaidScheduledTransactionId);
    }

    [Fact]
    public async Task MarkPaid_ClosesAShortageAndTheListShrinks()
    {
        await using var f = await Fixture.CreateAsync(balance: 1000m, withRent: true);
        var vm = f.Vm;
        await vm.InitializeAsync(null);
        vm.ActualBalanceText = "300";
        Assert.Equal(Translator.Get("Reconciliation_ShortageFormat").Replace("{0}", "RUB 700.00"), vm.ComparisonText);

        await f.RunAsync(vm.ContinueCommand);
        var rent = Assert.Single(vm.Expected.Rows);
        Assert.Empty(rent.AttachChoices);
        await f.RunAsync(rent.MarkPaidCommand);

        Assert.Empty(vm.Expected.Rows);
        Assert.True(vm.IsMatched);
        Assert.Equal("RUB 300.00", vm.AppBalanceText);
    }

    [Fact]
    public async Task GroupedSpendingThenAdjustment_EndsMatchedWithAuditableTransactions()
    {
        await using var f = await Fixture.CreateAsync(balance: 1000m);
        var vm = f.Vm;
        await vm.InitializeAsync(null);
        vm.ActualBalanceText = "900";
        await f.RunAsync(vm.ContinueCommand); // -> expected (nothing scheduled)
        Assert.Empty(vm.Expected.Rows);
        await f.RunAsync(vm.ContinueCommand); // -> explain
        Assert.True(vm.IsExplainStep);

        vm.GroupedSpending.AmountText = "60";
        vm.GroupedSpending.Category = vm.GroupedSpending.CategoryOptions.First(o => o.Name == "Groceries");
        vm.GroupedSpending.NoteText = "Market, cash";
        await f.RunAsync(vm.GroupedSpending.AddCommand);

        Assert.Equal("RUB 940.00", vm.AppBalanceText);
        Assert.Equal("Market, cash: RUB -60.00", Assert.Single(vm.GroupedSpending.AddedEntries).Name);
        Assert.True(vm.HasDifference);

        await f.RunAsync(vm.ContinueCommand); // -> adjust
        Assert.True(vm.IsAdjustStep);
        Assert.Equal(string.Format(Translator.Get("Reconciliation_AdjustmentPreviewFormat"), "RUB -40.00"), vm.AdjustmentText);
        await f.RunAsync(vm.RecordAdjustmentCommand);

        Assert.True(vm.IsDoneStep);
        Assert.True(vm.IsMatched);
        var transactions = await f.Store.Transactions.GetAllAsync();
        Assert.Equal(2, transactions.Count);
        var grouped = Assert.Single(transactions, t => t.Name == "Market, cash");
        Assert.Equal(TransactionType.Expense, grouped.Type);
        Assert.NotNull(grouped.CategoryId);
        var adjustment = Assert.Single(transactions, t => t.Name == Translator.Get("Reconciliation_AdjustmentName"));
        Assert.Equal(TransactionType.Expense, adjustment.Type);
        Assert.Equal(40m, adjustment.Amount);
        Assert.Equal(Today, adjustment.Date);
        Assert.Equal(900m, (await f.Store.Accounts.GetByIdAsync(f.AccountId))!.CurrentBalance);
    }

    [Fact]
    public async Task Surplus_IsAdjustedWithAnIncomeTransaction()
    {
        await using var f = await Fixture.CreateAsync(balance: 1000m);
        var vm = f.Vm;
        await vm.InitializeAsync(null);
        vm.ActualBalanceText = "1025.50";
        await f.RunAsync(vm.ContinueCommand);
        await f.RunAsync(vm.ContinueCommand);
        await f.RunAsync(vm.ContinueCommand);

        await f.RunAsync(vm.RecordAdjustmentCommand);

        var adjustment = Assert.Single(await f.Store.Transactions.GetAllAsync());
        Assert.Equal(TransactionType.Income, adjustment.Type);
        Assert.Equal(25.50m, adjustment.Amount);
        Assert.Equal(1025.50m, (await f.Store.Accounts.GetByIdAsync(f.AccountId))!.CurrentBalance);
    }

    [Fact]
    public async Task BalanceStep_WillNotContinueWithoutANumber()
    {
        await using var f = await Fixture.CreateAsync(balance: 1000m);
        var vm = f.Vm;
        await vm.InitializeAsync(null);
        vm.ActualBalanceText = "lots";

        await f.RunAsync(vm.ContinueCommand);

        Assert.True(vm.IsBalanceStep);
        Assert.Equal(Translator.Get("Reconciliation_ActualBalanceMustBeNumber"), vm.StatusText);
    }

    [Fact]
    public async Task FinishingWithoutAdjusting_ReportsTheUnexplainedDifferenceAndWritesNothing()
    {
        await using var f = await Fixture.CreateAsync(balance: 1000m);
        var vm = f.Vm;
        await vm.InitializeAsync(null);
        vm.ActualBalanceText = "990";
        await f.RunAsync(vm.ContinueCommand);
        await f.RunAsync(vm.ContinueCommand);
        await f.RunAsync(vm.ContinueCommand);

        vm.FinishWithoutAdjustingCommand.Execute(null);

        Assert.True(vm.IsDoneStep);
        Assert.Equal(string.Format(Translator.Get("Reconciliation_DoneUnexplainedFormat"), "RUB -10.00"), vm.DoneSummaryText);
        Assert.Empty(await f.Store.Transactions.GetAllAsync());
    }

    [Fact]
    public async Task ExpectedItems_OnlyShowTheChosenAccountsSchedules()
    {
        await using var f = await Fixture.CreateAsync(balance: 1000m, withRent: true, rentOnOtherAccount: true);
        var vm = f.Vm;
        await vm.InitializeAsync(null);
        vm.SelectedAccount = vm.AccountOptions.Single(o => o.Id == f.AccountId);
        vm.ActualBalanceText = "1000";

        await f.RunAsync(vm.ContinueCommand);

        Assert.Empty(vm.Expected.Rows);
    }

    private sealed class FixedDate(DateOnly today) : IDateProvider
    {
        public DateOnly Today { get; } = today;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteTestStore Store { get; } = new();
        public ReconciliationViewModel Vm { get; private set; } = null!;
        public Guid AccountId { get; } = Guid.NewGuid();
        public Guid RentId { get; } = Guid.NewGuid();

        public static async Task<Fixture> CreateAsync(decimal balance, bool withRent = false, bool recordedRentPayment = false, bool rentOnOtherAccount = false)
        {
            Translator.SetLanguage("en");
            var f = new Fixture();
            var otherAccountId = Guid.NewGuid();
            await f.Store.Accounts.SaveAsync(new Account(f.AccountId, "Card", AccountType.DebitCard, balance, "RUB", DateTimeOffset.UtcNow));
            await f.Store.Accounts.SaveAsync(new Account(otherAccountId, "Other", AccountType.Savings, 0m, "RUB", DateTimeOffset.UtcNow));
            await f.Store.Settings.SaveAsync((await f.Store.Settings.GetAsync()) with { DefaultCurrency = "RUB", PrimaryAccountId = f.AccountId });
            await f.Store.Categories.SaveAsync(new Category(Guid.NewGuid(), "Groceries", TransactionType.Expense));
            if (withRent)
            {
                await f.Store.ScheduledTransactions.SaveAsync(new ScheduledTransaction(
                    f.RentId, "Rent", 700m, rentOnOtherAccount ? otherAccountId : f.AccountId, null, TransactionType.Expense,
                    new RecurrenceRule(RecurrenceFrequency.Monthly, 1, RentDue, DayOfMonth: 10), RentDue, Active: true));
            }

            if (recordedRentPayment)
            {
                await f.Store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), RentDue, 700m, f.AccountId, null, null, TransactionType.Expense, Name: "LANDLORD LLC"));
            }

            var recurrence = new RecurrenceService();
            f.Vm = new ReconciliationViewModel(
                new FixedDate(Today),
                f.Store.Accounts,
                f.Store.Categories,
                f.Store.Transactions,
                f.Store.ScheduledTransactions,
                f.Store.ScheduledOccurrenceOverrides,
                f.Store.Settings,
                new ScheduledTransactionProjectionService(recurrence),
                new ScheduledOccurrenceResolutionService(),
                new TransactionApplicationService(new TransactionBalanceService()),
                new RecurrenceDescriptionService(),
                new ExpectedTransactionMatcher(),
                new ReconciliationService(),
                new GroupedSpendingService(),
                new BalanceAdjustmentService());
            return f;
        }

        // Commands are fire-and-forget; give the async continuation time to finish its SQLite work.
        public async Task RunAsync(System.Windows.Input.ICommand command)
        {
            command.Execute(null);
            for (var i = 0; i < 50; i++)
            {
                await Task.Delay(20);
                if (!Vm.IsBusy) break;
            }

            await Task.Delay(150);
        }

        public ValueTask DisposeAsync() => Store.DisposeAsync();
    }
}
