using Banccoon.Core.Models;
using Banccoon.Core.Savings;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Savings;

public sealed class LegacySavingsGoalConversionServiceTests
{
    [Fact]
    public async Task ConvertOnceAsync_CreatesAGoalAccountPerLegacyGoal()
    {
        await using var store = new SqliteTestStore();
        await store.Settings.SaveAsync((await store.Settings.GetAsync()) with { DefaultCurrency = "RUB" });
        var savings = new Account(Guid.NewGuid(), "Savings", AccountType.Savings, 5000m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(savings);
        var vacation = new SavingsGoal(Guid.NewGuid(), "Vacation", 1000m, 300m, new DateOnly(2027, 6, 1), AccountId: savings.Id);
        var noTarget = new SavingsGoal(Guid.NewGuid(), "Rainy day", 0m, 50m, null);
        await store.SavingsGoals.SaveAsync(vacation);
        await store.SavingsGoals.SaveAsync(noTarget);

        var converted = await CreateService(store).ConvertOnceAsync();

        Assert.Equal(2, converted);
        var vacationAccount = await store.Accounts.GetByIdAsync(vacation.Id);
        Assert.NotNull(vacationAccount);
        Assert.Equal("Vacation", vacationAccount.Name);
        Assert.Equal(AccountType.Goal, vacationAccount.Type);
        Assert.Equal(300m, vacationAccount.CurrentBalance);
        Assert.Equal(1000m, vacationAccount.PlanningValue);
        Assert.Equal("RUB", vacationAccount.Currency);
        Assert.False(vacationAccount.IncludeInDashboardTotals);
        Assert.False(vacationAccount.IsArchived);
        Assert.Null((await store.Accounts.GetByIdAsync(noTarget.Id))!.PlanningValue);
        // The linked account the goal's money actually sits in is left exactly as it was.
        Assert.Equal(savings, await store.Accounts.GetByIdAsync(savings.Id));
    }

    [Fact]
    public async Task ConvertOnceAsync_LeavesTheLegacyRowsUntouched()
    {
        await using var store = new SqliteTestStore();
        var goal = new SavingsGoal(Guid.NewGuid(), "Vacation", 1000m, 300m, new DateOnly(2027, 6, 1));
        await store.SavingsGoals.SaveAsync(goal);

        await CreateService(store).ConvertOnceAsync();

        Assert.Equal(goal, Assert.Single(await store.SavingsGoals.GetAllAsync()));
    }

    [Fact]
    public async Task ConvertOnceAsync_RunsOnlyOnce_EvenIfTheAccountIsLaterDeletedOrMoreLegacyGoalsAppear()
    {
        await using var store = new SqliteTestStore();
        var goal = new SavingsGoal(Guid.NewGuid(), "Vacation", 1000m, 300m, null);
        await store.SavingsGoals.SaveAsync(goal);
        var service = CreateService(store);
        await service.ConvertOnceAsync();

        await store.Accounts.DeleteAsync(goal.Id);
        await store.SavingsGoals.SaveAsync(new SavingsGoal(Guid.NewGuid(), "Later", 10m, 1m, null));
        var secondRun = await service.ConvertOnceAsync();

        Assert.Equal(0, secondRun);
        Assert.Empty(await store.Accounts.GetAllAsync());
        Assert.True((await store.Settings.GetAsync()).LegacySavingsGoalsConverted);
    }

    [Fact]
    public async Task ConvertOnceAsync_WithNoLegacyGoals_StillMarksItselfDone()
    {
        await using var store = new SqliteTestStore();

        Assert.Equal(0, await CreateService(store).ConvertOnceAsync());
        Assert.True((await store.Settings.GetAsync()).LegacySavingsGoalsConverted);
    }

    [Fact]
    public async Task ConvertOnceAsync_NeverOverwritesAnExistingAccountWithTheSameId()
    {
        await using var store = new SqliteTestStore();
        var goal = new SavingsGoal(Guid.NewGuid(), "Vacation", 1000m, 300m, null);
        await store.SavingsGoals.SaveAsync(goal);
        var existing = new Account(goal.Id, "Already here", AccountType.Goal, 999m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(existing);

        Assert.Equal(0, await CreateService(store).ConvertOnceAsync());
        Assert.Equal("Already here", (await store.Accounts.GetByIdAsync(goal.Id))!.Name);
    }

    [Fact]
    public async Task ConvertOnceAsync_KeepsOtherSettingsIntact()
    {
        await using var store = new SqliteTestStore();
        var before = (await store.Settings.GetAsync()) with { DefaultCurrency = "RUB", SafetyBuffer = 123m, DisplayLanguage = "ru" };
        await store.Settings.SaveAsync(before);

        await CreateService(store).ConvertOnceAsync();

        Assert.Equal(before with { LegacySavingsGoalsConverted = true }, await store.Settings.GetAsync());
    }

    private static LegacySavingsGoalConversionService CreateService(SqliteTestStore store)
    {
        return new LegacySavingsGoalConversionService(store.SavingsGoals, store.Accounts, store.Settings);
    }
}
