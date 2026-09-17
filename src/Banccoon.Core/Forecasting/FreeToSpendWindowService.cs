using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed class FreeToSpendWindowService : IFreeToSpendWindowService
{
    private const int MajorPaymentLookaheadDays = 180;

    private readonly IScheduledTransactionProjectionService projectionService;

    public FreeToSpendWindowService(IScheduledTransactionProjectionService projectionService)
    {
        this.projectionService = projectionService;
    }

    public FreeToSpendWindow GetWindow(
        DateOnly today,
        AppSettings settings,
        IReadOnlyCollection<ScheduledTransaction> scheduledTransactions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scheduledTransactions);

        return settings.FreeToSpendWindowMode switch
        {
            FreeToSpendWindowMode.CalendarWeek => new FreeToSpendWindow(today, GetEndOfWeek(today)),
            FreeToSpendWindowMode.CalendarMonth => new FreeToSpendWindow(today, GetEndOfMonth(today)),
            FreeToSpendWindowMode.UntilNextMajorPayment => new FreeToSpendWindow(
                today,
                GetNextMajorPaymentDate(today, settings, scheduledTransactions)),
            _ => new FreeToSpendWindow(today, GetRollingEnd(today, settings.FreeToSpendWindowDays))
        };
    }

    private static DateOnly GetRollingEnd(DateOnly today, int windowDays)
    {
        return today.AddDays(Math.Max(1, windowDays) - 1);
    }

    private static DateOnly GetEndOfWeek(DateOnly today)
    {
        var daysUntilSunday = (7 - (int)today.DayOfWeek) % 7;
        return today.AddDays(daysUntilSunday);
    }

    private static DateOnly GetEndOfMonth(DateOnly today)
    {
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        return new DateOnly(today.Year, today.Month, daysInMonth);
    }

    private DateOnly GetNextMajorPaymentDate(
        DateOnly today,
        AppSettings settings,
        IReadOnlyCollection<ScheduledTransaction> scheduledTransactions)
    {
        var lookaheadEnd = today.AddDays(MajorPaymentLookaheadDays);
        var nextMajorEvent = projectionService
            .Project(scheduledTransactions, today.AddDays(1), lookaheadEnd)
            .Where(forecastEvent => Math.Abs(forecastEvent.Amount) >= settings.MajorPaymentThreshold)
            .OrderBy(forecastEvent => forecastEvent.Date)
            .FirstOrDefault();

        return nextMajorEvent?.Date ?? GetRollingEnd(today, settings.FreeToSpendWindowDays);
    }
}
