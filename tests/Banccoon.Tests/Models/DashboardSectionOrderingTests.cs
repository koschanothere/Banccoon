using Banccoon.Core.Models;
using Xunit;

namespace Banccoon.Tests.Models;

public sealed class DashboardSectionOrderingTests
{
    [Fact]
    public void Parse_RoundTripsAFormattedOrder()
    {
        var order = new[] { DashboardSection.Goals, DashboardSection.Upcoming, DashboardSection.Analytics };

        var parsed = DashboardSectionOrdering.Parse(DashboardSectionOrdering.Format(order));

        Assert.Equal(order, parsed);
    }

    [Fact]
    public void Parse_WhenValueIsEmpty_ReturnsDefault()
    {
        var parsed = DashboardSectionOrdering.Parse(string.Empty);

        Assert.Equal(DashboardSectionOrdering.Default, parsed);
    }

    [Fact]
    public void Parse_SkipsUnrecognizedTokensAndAppendsMissingSections()
    {
        var parsed = DashboardSectionOrdering.Parse("Goals,NotARealSection,Upcoming");

        Assert.Equal(new[] { DashboardSection.Goals, DashboardSection.Upcoming, DashboardSection.Analytics }, parsed);
    }

    [Fact]
    public void Parse_AlwaysReturnsEveryKnownSectionExactlyOnce()
    {
        var parsed = DashboardSectionOrdering.Parse("Analytics,Analytics,Analytics");

        Assert.Equal(DashboardSectionOrdering.Default.Count, parsed.Count);
        Assert.Equal(parsed.Count, parsed.Distinct().Count());
    }
}
