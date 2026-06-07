namespace Banccoon.Core.Forecasting;

public sealed class ForecastService : IForecastService
{
    private readonly IAccountBalanceService accountBalanceService;
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;

    public ForecastService(
        IAccountBalanceService accountBalanceService,
        IScheduledTransactionProjectionService scheduledTransactionProjectionService)
    {
        this.accountBalanceService = accountBalanceService;
        this.scheduledTransactionProjectionService = scheduledTransactionProjectionService;
    }

    public ForecastResult CreateForecast(ForecastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EndDate < request.StartDate)
        {
            throw new ArgumentException("Forecast end date must be on or after start date.", nameof(request));
        }

        var currentBalance = accountBalanceService.GetCurrentBalance(request.Accounts);
        var forecastEvents = scheduledTransactionProjectionService
            .Project(request.ScheduledTransactions, request.StartDate, request.EndDate)
            .OrderBy(forecastEvent => forecastEvent.Date)
            .ThenBy(forecastEvent => forecastEvent.SignedAmount)
            .ThenBy(forecastEvent => forecastEvent.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectedBalances = new List<ProjectedBalancePoint>
        {
            new(request.StartDate, currentBalance)
        };

        var runningBalance = currentBalance;
        var lowestBalance = currentBalance;

        foreach (var forecastEvent in forecastEvents)
        {
            runningBalance += forecastEvent.SignedAmount;
            lowestBalance = Math.Min(lowestBalance, runningBalance);
            projectedBalances.Add(new ProjectedBalancePoint(forecastEvent.Date, runningBalance));
        }

        var upcomingObligations = forecastEvents
            .Where(forecastEvent => forecastEvent.SignedAmount < 0)
            .Select(forecastEvent => new UpcomingObligation(
                forecastEvent.Date,
                forecastEvent.Name,
                Math.Abs(forecastEvent.SignedAmount),
                forecastEvent.AccountId,
                forecastEvent.CategoryId,
                forecastEvent.Kind))
            .ToArray();

        return new ForecastResult(
            request.StartDate,
            request.EndDate,
            currentBalance,
            runningBalance,
            lowestBalance,
            Math.Max(0m, lowestBalance),
            upcomingObligations,
            projectedBalances,
            forecastEvents);
    }
}
