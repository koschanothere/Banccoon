using Banccoon.Core.Forecasting;
using Banccoon.Core.Reconciliation;
using Xunit;

namespace Banccoon.Tests.Reconciliation;

public sealed class ReconciliationServiceTests
{
    private readonly ReconciliationService service = new();

    [Fact]
    public void Compare_WhenActualMatchesExpected_ReturnsMatched()
    {
        var result = service.Compare(
            1000m,
            1000.005m,
            new DateOnly(2026, 6, 7),
            tolerance: 0.01m);

        Assert.Equal(ReconciliationStatus.Matched, result.Status);
        Assert.Equal(0.005m, result.Difference);
    }

    [Fact]
    public void Compare_WhenActualIsHigher_ReturnsSurplus()
    {
        var result = service.Compare(
            1000m,
            1075m,
            new DateOnly(2026, 6, 7));

        Assert.Equal(ReconciliationStatus.Surplus, result.Status);
        Assert.Equal(75m, result.Difference);
    }

    [Fact]
    public void Compare_WhenActualIsLower_ReturnsShortage()
    {
        var result = service.Compare(
            1000m,
            925m,
            new DateOnly(2026, 6, 7));

        Assert.Equal(ReconciliationStatus.Shortage, result.Status);
        Assert.Equal(-75m, result.Difference);
    }

    [Fact]
    public void Compare_WhenForecastResultProvided_UsesProjectedBalanceAtActualDate()
    {
        var forecast = new ForecastResult(
            new DateOnly(2026, 6, 7),
            new DateOnly(2026, 6, 30),
            1000m,
            1300m,
            700m,
            700m,
            Array.Empty<UpcomingObligation>(),
            [
                new ProjectedBalancePoint(new DateOnly(2026, 6, 7), 1000m),
                new ProjectedBalancePoint(new DateOnly(2026, 6, 10), 700m),
                new ProjectedBalancePoint(new DateOnly(2026, 6, 20), 1300m)
            ],
            Array.Empty<ForecastEvent>());

        var result = service.Compare(forecast, 650m, new DateOnly(2026, 6, 12));

        Assert.Equal(700m, result.ExpectedBalance);
        Assert.Equal(-50m, result.Difference);
        Assert.Equal(ReconciliationStatus.Shortage, result.Status);
    }

    [Fact]
    public void Compare_WhenToleranceIsNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Compare(
            1000m,
            1000m,
            new DateOnly(2026, 6, 7),
            tolerance: -1m));
    }
}
