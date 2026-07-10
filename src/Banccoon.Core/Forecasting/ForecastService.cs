using Banccoon.Core.CreditCards;
using Banccoon.Core.Models;
using Banccoon.Core.Savings;

namespace Banccoon.Core.Forecasting;

public sealed class ForecastService : IForecastService
{
    private readonly IAccountBalanceService accountBalanceService;
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;
    private readonly ICreditCardForecastService? creditCardForecastService;
    private readonly ISavingsGoalAllocationService? savingsGoalAllocationService;

    public ForecastService(
        IAccountBalanceService accountBalanceService,
        IScheduledTransactionProjectionService scheduledTransactionProjectionService,
        ICreditCardForecastService? creditCardForecastService = null,
        ISavingsGoalAllocationService? savingsGoalAllocationService = null)
    {
        this.accountBalanceService = accountBalanceService;
        this.scheduledTransactionProjectionService = scheduledTransactionProjectionService;
        this.creditCardForecastService = creditCardForecastService;
        this.savingsGoalAllocationService = savingsGoalAllocationService;
    }

    public ForecastResult CreateForecast(ForecastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EndDate < request.StartDate)
        {
            throw new ArgumentException("Forecast end date must be on or after start date.", nameof(request));
        }

        var currentBalance = accountBalanceService.GetCurrentBalance(request.Accounts);
        var paidScheduledOccurrences = request.Transactions?
            .Where(transaction =>
                transaction.PaidScheduledTransactionId.HasValue
                && transaction.PaidScheduledOccurrenceDate.HasValue)
            .Select(transaction => new PaidScheduledOccurrence(
                transaction.PaidScheduledTransactionId!.Value,
                transaction.PaidScheduledOccurrenceDate!.Value))
            .ToHashSet() ?? new HashSet<PaidScheduledOccurrence>();
        var scheduledEvents = scheduledTransactionProjectionService
            .Project(request.ScheduledTransactions, request.StartDate, request.EndDate)
            .Where(forecastEvent => !paidScheduledOccurrences.Contains(new PaidScheduledOccurrence(
                forecastEvent.SourceId,
                forecastEvent.Date)))
            .ToArray();
        var creditCardEvents = ProjectCreditCardEvents(request);
        var forecastEvents = scheduledEvents
            .Concat(creditCardEvents)
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

        var reservedForSavingsGoals = savingsGoalAllocationService?
            .GetAllocations(request.SavingsGoals ?? Array.Empty<SavingsGoal>())
            .Sum(allocation => allocation.ReservedAmount) ?? 0m;

        return new ForecastResult(
            request.StartDate,
            request.EndDate,
            currentBalance,
            runningBalance,
            lowestBalance,
            Math.Max(0m, lowestBalance - reservedForSavingsGoals),
            upcomingObligations,
            projectedBalances,
            forecastEvents);
    }

    private IReadOnlyList<ForecastEvent> ProjectCreditCardEvents(ForecastRequest request)
    {
        if (creditCardForecastService is null)
        {
            return Array.Empty<ForecastEvent>();
        }

        return creditCardForecastService
            .ProjectPayments(request.Accounts, request.StartDate, request.EndDate)
            .Select(payment => new ForecastEvent(
                payment.AccountId,
                payment.PaymentDate,
                $"{payment.AccountName} payment",
                payment.Amount,
                TransactionType.Expense,
                payment.AccountId,
                CategoryId: null,
                ForecastEventKind.CreditCardPayment))
            .ToArray();
    }

    private readonly record struct PaidScheduledOccurrence(Guid ScheduledTransactionId, DateOnly OccurrenceDate);
}
