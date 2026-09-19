using Banccoon.Core.Models;

namespace Banccoon.Core.Analytics;

public interface IAnalyticsService
{
    // currentPeriodAnchor is any date within the "current" calendar month being viewed - the
    // trend walks backward trendPeriodCount total months (including the current one) from there.
    AnalyticsReport BuildReport(
        DateOnly currentPeriodAnchor,
        int trendPeriodCount,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Category> categories);
}
