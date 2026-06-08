using System.Globalization;

namespace Banccoon.App.Converters;

public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            Banccoon.Core.Models.DateDisplayFormat.DayMonthYear => "DD/MM/YYYY",
            Banccoon.Core.Models.DateDisplayFormat.MonthDayYear => "MM/DD/YYYY",
            Banccoon.Core.Models.DateDisplayFormat.YearMonthDay => "YYYY-MM-DD",
            Banccoon.Core.Recurrence.RecurrenceFrequency.Daily => "day",
            Banccoon.Core.Recurrence.RecurrenceFrequency.Weekly => "week",
            Banccoon.Core.Recurrence.RecurrenceFrequency.Monthly => "month",
            Banccoon.Core.Recurrence.RecurrenceFrequency.Yearly => "year",
            null => string.Empty,
            _ => SplitPascalCase(value.ToString() ?? string.Empty)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static string SplitPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = new List<char>(text.Length + 4) { text[0] };
        for (var index = 1; index < text.Length; index++)
        {
            var current = text[index];
            var previous = text[index - 1];
            if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
            {
                result.Add(' ');
            }

            result.Add(current);
        }

        return new string(result.ToArray());
    }
}
