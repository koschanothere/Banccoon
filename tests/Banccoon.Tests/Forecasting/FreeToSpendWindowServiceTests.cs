using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Forecasting;

public sealed class FreeToSpendWindowServiceTests
{
    private readonly FreeToSpendWindowService service = new(
        new ScheduledTransactionProjectionService(new RecurrenceService()));

    [Fact]
    public void GetWindow_RollingDays_ReturnsTodayPlusConfiguredDays()
    {
        var settings = new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)
        {
            FreeToSpendWindowMode = FreeToSpendWindowMode.RollingDays,
            FreeToSpendWindowDays = 7
        };

        var window = service.GetWindow(new DateOnly(2026, 6, 10), settings, Array.Empty<ScheduledTransaction>());

        Assert.Equal(new DateOnly(2026, 6, 10), window.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 16), window.EndDate);
    }

    [Fact]
    public void GetWindow_CalendarWeek_EndsOnUpcomingSunday()
    {
        var settings = new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)
        {
            FreeToSpendWindowMode = FreeToSpendWindowMode.CalendarWeek
        };

        // 2026-06-10 is a Wednesday.
        var window = service.GetWindow(new DateOnly(2026, 6, 10), settings, Array.Empty<ScheduledTransaction>());

        Assert.Equal(new DateOnly(2026, 6, 14), window.EndDate);
        Assert.Equal(DayOfWeek.Sunday, window.EndDate.DayOfWeek);
    }

    [Fact]
    public void GetWindow_CalendarWeek_WhenTodayIsSunday_EndsToday()
    {
        var settings = new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)
        {
            FreeToSpendWindowMode = FreeToSpendWindowMode.CalendarWeek
        };

        // 2026-06-14 is a Sunday.
        var window = service.GetWindow(new DateOnly(2026, 6, 14), settings, Array.Empty<ScheduledTransaction>());

        Assert.Equal(new DateOnly(2026, 6, 14), window.EndDate);
    }

    [Fact]
    public void GetWindow_CalendarMonth_EndsOnLastDayOfMonth()
    {
        var settings = new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)
        {
            FreeToSpendWindowMode = FreeToSpendWindowMode.CalendarMonth
        };

        var window = service.GetWindow(new DateOnly(2026, 2, 5), settings, Array.Empty<ScheduledTransaction>());

        Assert.Equal(new DateOnly(2026, 2, 28), window.EndDate);
    }

    [Fact]
    public void GetWindow_UntilNextMajorPayment_EndsOnFirstOccurrenceMeetingThreshold()
    {
        var settings = new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)
        {
            FreeToSpendWindowMode = FreeToSpendWindowMode.UntilNextMajorPayment,
            MajorPaymentThreshold = 1000m
        };
        var smallBill = CreateOneTimeScheduledTransaction(
            "Subscription", 20m, TransactionType.Expense, new DateOnly(2026, 6, 12));
        var rent = CreateOneTimeScheduledTransaction(
            "Rent", 1200m, TransactionType.Expense, new DateOnly(2026, 6, 20));

        var window = service.GetWindow(
            new DateOnly(2026, 6, 10),
            settings,
            new[] { smallBill, rent });

        Assert.Equal(new DateOnly(2026, 6, 20), window.EndDate);
    }

    [Fact]
    public void GetWindow_UntilNextMajorPayment_WithNoQualifyingPayment_FallsBackToRollingDays()
    {
        var settings = new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)
        {
            FreeToSpendWindowMode = FreeToSpendWindowMode.UntilNextMajorPayment,
            MajorPaymentThreshold = 1000m,
            FreeToSpendWindowDays = 7
        };
        var smallBill = CreateOneTimeScheduledTransaction(
            "Subscription", 20m, TransactionType.Expense, new DateOnly(2026, 6, 12));

        var window = service.GetWindow(
            new DateOnly(2026, 6, 10),
            settings,
            new[] { smallBill });

        Assert.Equal(new DateOnly(2026, 6, 16), window.EndDate);
    }

    private static ScheduledTransaction CreateOneTimeScheduledTransaction(
        string name,
        decimal amount,
        TransactionType type,
        DateOnly date)
    {
        return new ScheduledTransaction(
            Guid.NewGuid(),
            name,
            amount,
            Guid.NewGuid(),
            null,
            type,
            new RecurrenceRule(
                RecurrenceFrequency.Yearly,
                1,
                date,
                EndDate: date),
            date,
            Active: true);
    }
}
