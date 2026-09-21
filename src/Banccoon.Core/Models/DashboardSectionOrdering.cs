namespace Banccoon.Core.Models;

// AppSettings stores the dashboard's section order as a plain comma-separated string (matching
// how every other setting maps straight onto a single SQLite column) - this is the one place that
// knows how to turn that string into/from an ordered, complete list of DashboardSection values.
public static class DashboardSectionOrdering
{
    public static readonly IReadOnlyList<DashboardSection> Default =
    [
        DashboardSection.Upcoming,
        DashboardSection.Analytics,
        DashboardSection.Goals
    ];

    public static string Format(IReadOnlyList<DashboardSection> order)
    {
        return string.Join(",", order);
    }

    // Defensive: an unrecognized token is skipped, and any section missing from the stored value
    // (e.g. one added in a later release) is appended in its default position - the result is
    // always a complete permutation of every known section, never a partial list.
    public static IReadOnlyList<DashboardSection> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        var parsed = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => Enum.TryParse<DashboardSection>(token, out var section) ? section : (DashboardSection?)null)
            .Where(section => section is not null)
            .Select(section => section!.Value)
            .Distinct()
            .ToList();

        foreach (var section in Default)
        {
            if (!parsed.Contains(section))
            {
                parsed.Add(section);
            }
        }

        return parsed;
    }
}
