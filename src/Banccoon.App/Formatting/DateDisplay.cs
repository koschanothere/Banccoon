using System.Globalization;
using Banccoon.Core.Models;

namespace Banccoon.App.Formatting;

public static class DateDisplay
{
    public static string Format(DateOnly date, DateDisplayFormat format)
    {
        return date.ToString(GetPattern(format), CultureInfo.InvariantCulture);
    }

    public static string FormatShortWithoutYear(DateOnly date, DateDisplayFormat format)
    {
        return date.ToString(GetShortPatternWithoutYear(format), CultureInfo.InvariantCulture);
    }

    public static string GetPattern(DateDisplayFormat format)
    {
        return format switch
        {
            DateDisplayFormat.MonthDayYear => "MM/dd/yyyy",
            DateDisplayFormat.YearMonthDay => "yyyy-MM-dd",
            _ => "dd/MM/yyyy"
        };
    }

    private static string GetShortPatternWithoutYear(DateDisplayFormat format)
    {
        return format switch
        {
            DateDisplayFormat.MonthDayYear => "MM/dd",
            DateDisplayFormat.YearMonthDay => "MM-dd",
            _ => "dd/MM"
        };
    }
}
