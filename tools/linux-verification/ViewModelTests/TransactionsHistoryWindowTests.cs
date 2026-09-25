using Banccoon.App.Localization;
using Banccoon.Core.Models;
using Banccoon.Tests.Infrastructure;
using Xunit;

// 2026-09-25: the Transactions page loads the last 12 months; "Load earlier month" fetches older
// ones, and reopening the page drops them again.
public sealed class TransactionsHistoryWindowTests
{
    [Fact]
    public async Task LoadsTwelveMonths_ThenEarlierMonthsOnRequest_AndForgetsThemOnReopen()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var card = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(card);
        // Today 15 Jun 2026: the recent window starts 1 Jul 2025. June 2025 has nothing.
        foreach (var (date, name) in new[]
        {
            (new DateOnly(2026, 6, 10), "Recent"),
            (new DateOnly(2025, 7, 2), "Start of window"),
            (new DateOnly(2025, 5, 20), "May 2025"),
            (new DateOnly(2024, 1, 3), "Jan 2024")
        })
        {
            await TransactionsDeleteTests.RecordAsync(store, new Transaction(Guid.NewGuid(), date, 10m, card.Id, null, null, TransactionType.Expense, Name: name));
        }

        var vm = TransactionsDeleteTests.CreateViewModel(store, new DateOnly(2026, 6, 15));
        await vm.InitializeAsync();

        Assert.Equal(["Recent", "Start of window"], vm.Rows.Select(r => r.Name));
        Assert.True(vm.CanLoadEarlierMonth);
        Assert.Equal(string.Format(Translator.Get("Transactions_LoadedSinceFormat"), "01/07/2025"), vm.LoadedSinceText);

        // Skips the empty June and stops at the first month with something in it.
        vm.LoadEarlierMonthCommand.Execute(null);
        await WaitForAsync(() => vm.Rows.Count == 3);
        Assert.Equal("May 2025", vm.Rows[2].Name);
        Assert.True(vm.CanLoadEarlierMonth);

        vm.LoadEarlierMonthCommand.Execute(null);
        await WaitForAsync(() => vm.Rows.Count == 4);
        Assert.Equal("Jan 2024", vm.Rows[3].Name);
        Assert.False(vm.CanLoadEarlierMonth);

        await vm.InitializeAsync();
        Assert.Equal(["Recent", "Start of window"], vm.Rows.Select(r => r.Name));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
