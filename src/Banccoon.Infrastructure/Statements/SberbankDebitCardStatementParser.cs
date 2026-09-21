using System.Globalization;
using System.Text.RegularExpressions;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Banccoon.Infrastructure.Statements;

public sealed class SberbankDebitCardStatementParser : IStatementParser
{
    private static readonly Regex PeriodPattern = new(
        @"За период\s+(?<start>\d{2}\.\d{2}\.\d{4})\s+[—-]\s+(?<end>\d{2}\.\d{2}\.\d{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BalancePattern = new(
        @"Остаток на\s+\d{2}\.\d{2}\.\d{4}\s+(?<amount>[+-]?\d[\d\s]*,\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OperationLinePattern = new(
        @"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2})\s+(?<category>.+?)\s+(?<amount>[+]?-?\d[\d\s]*,\d{2})\s+(?<balance>[+-]?\d[\d\s]*,\d{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DescriptionStartPattern = new(
        @"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<reference>\d{4,})\s+(?<description>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CardMaskPattern = new(
        @"(\*{2,}|\u2022{2,})\s*\d{2,4}|\*{4}\d{4}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CardLastFourPattern = new(
        @"(?:\*{2,}|\u2022{2,})\s*(?<last>\d{4})|\*{4}(?<last>\d{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AccountNumberPattern = new(
        @"\u041d\u043e\u043c\u0435\u0440 \u0441\u0447\u0451\u0442\u0430\s+(?<number>[\d\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public StatementParserDescriptor Descriptor { get; } = new(
        "sberbank-debit-card-pdf",
        "Sberbank debit card PDF",
        [".pdf"]);

    public bool CanParse(StatementParseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(Path.GetExtension(request.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(request.FilePath))
        {
            return false;
        }

        try
        {
            using var document = PdfDocument.Open(request.FilePath);
            var firstPageLines = ExtractLines(document.GetPage(1));
            return firstPageLines.Any(line => line.Contains("Сбер", StringComparison.OrdinalIgnoreCase))
                && firstPageLines.Any(line => line.Contains("Выписка по счёту дебетовой карты", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Выписка по счету дебетовой карты", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public Task<ParsedStatement> ParseAsync(
        StatementParseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = PdfDocument.Open(request.FilePath);
            var lines = document
                .GetPages()
                .SelectMany(ExtractLines)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            return ParseExtractedLines(
                lines,
                Path.GetFileName(request.FilePath));
        }, cancellationToken);
    }

    internal ParsedStatement ParseExtractedLines(
        IReadOnlyList<string> lines,
        string sourceName)
    {
        if (!lines.Any(line => line.Contains("Сбер", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The PDF does not look like a Sberbank statement.");
        }

        var (periodStart, periodEnd) = ParsePeriod(lines);
        var balances = lines
            .Select(line => BalancePattern.Match(line))
            .Where(match => match.Success)
            .Select(match => ParseMoney(match.Groups["amount"].Value))
            .ToArray();

        var rows = ParseRows(lines);
        if (rows.Count == 0)
        {
            throw new InvalidDataException("No Sberbank statement operations were found.");
        }

        // Rows are in the statement's own printed order (newest first for this bank), so rows[0]
        // is the most recent operation. Its running balance is what the account actually holds -
        // the "Остаток на <date>" summary line lower in the header has been observed to disagree
        // with it, so it's only used as a fallback for parsers/paths that never captured a
        // per-row balance at all.
        var closingBalance = rows[0].BalanceAfter ?? (balances.Length == 0 ? null : balances.Last());

        return new ParsedStatement(
            Descriptor.Id,
            Descriptor.Name,
            sourceName,
            rows,
            periodStart,
            periodEnd,
            balances.FirstOrDefault(),
            closingBalance,
            ParseAccountNumber(lines),
            ParseCardLastFourDigits(lines));
    }

    private static string? ParseAccountNumber(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = AccountNumberPattern.Match(NormalizeWhitespace(line));
            if (!match.Success)
            {
                continue;
            }

            var digits = DigitsOnly(match.Groups["number"].Value);
            if (digits.Length > 0)
            {
                return digits;
            }
        }

        return null;
    }

    private static string? ParseCardLastFourDigits(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = CardLastFourPattern.Match(NormalizeWhitespace(line));
            if (match.Success)
            {
                return match.Groups["last"].Value;
            }
        }

        return null;
    }

    private static IReadOnlyList<ParsedStatementRow> ParseRows(IReadOnlyList<string> lines)
    {
        var rows = new List<ParsedStatementRow>();
        PendingOperation? pending = null;

        foreach (var rawLine in lines)
        {
            var line = NormalizeWhitespace(rawLine);
            if (line.Length == 0)
            {
                continue;
            }

            // The operations table's header repeats at the top of every page, and the statement
            // ends with certificate/legal boilerplate. Both are unambiguous "the current table
            // just ended" signals. Flushing here - rather than relying on an ever-growing list of
            // ignored line prefixes - is what stops that boilerplate from silently gluing onto
            // whichever transaction happened to still be pending when the page broke: previously
            // any line that wasn't explicitly ignored got appended to the pending row's
            // description, which on a real multi-page statement corrupted the last transaction
            // before every page break plus the very last transaction in the document.
            if (IsTableBoundary(line))
            {
                FlushPending(rows, ref pending);
                continue;
            }

            var operationMatch = OperationLinePattern.Match(line);
            if (operationMatch.Success)
            {
                FlushPending(rows, ref pending);
                pending = new PendingOperation(
                    ParseDate(operationMatch.Groups["date"].Value),
                    ParseTime(operationMatch.Groups["time"].Value),
                    operationMatch.Groups["category"].Value.Trim(),
                    operationMatch.Groups["amount"].Value,
                    ParseMoney(operationMatch.Groups["balance"].Value),
                    line);
                continue;
            }

            if (pending is null)
            {
                continue;
            }

            var descriptionMatch = DescriptionStartPattern.Match(line);
            if (descriptionMatch.Success)
            {
                pending.ExternalReference ??= descriptionMatch.Groups["reference"].Value;
                AddDescriptionLine(pending, descriptionMatch.Groups["description"].Value);
                continue;
            }

            AddDescriptionLine(pending, line);
        }

        FlushPending(rows, ref pending);
        return rows;
    }

    private static bool IsTableBoundary(string line)
    {
        return line.StartsWith("ДАТА ОПЕРАЦИИ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Продолжение", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Дата формирования", StringComparison.OrdinalIgnoreCase);
    }

    private static void FlushPending(List<ParsedStatementRow> rows, ref PendingOperation? pending)
    {
        if (pending is null)
        {
            return;
        }

        var amount = ParseMoney(pending.AmountText);
        var type = pending.AmountText.TrimStart().StartsWith('+')
            ? TransactionType.Income
            : TransactionType.Expense;
        var description = CleanDescription(
            pending.DescriptionLines.Count == 0
                ? pending.Category
                : string.Join(' ', pending.DescriptionLines));
        var rawText = string.Join(' ', new[] { pending.RawLine }.Concat(pending.DescriptionLines));

        rows.Add(new ParsedStatementRow(
            pending.Date,
            Math.Abs(amount),
            type,
            string.IsNullOrWhiteSpace(description) ? pending.Category : description,
            Counterparty: string.IsNullOrWhiteSpace(description) ? pending.Category : description,
            ExternalReference: pending.ExternalReference,
            RawText: rawText,
            BalanceAfter: pending.BalanceAfter,
            Time: pending.Time));

        pending = null;
    }

    private static void AddDescriptionLine(PendingOperation pending, string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            pending.DescriptionLines.Add(line);
        }
    }

    private static IReadOnlyList<string> ExtractLines(Page page)
    {
        var words = page.GetWords()
            .OrderByDescending(word => GetVerticalCenter(word))
            .ThenBy(word => word.BoundingBox.Left)
            .ToArray();
        var lineGroups = new List<List<Word>>();

        foreach (var word in words)
        {
            var center = GetVerticalCenter(word);
            var line = lineGroups.FirstOrDefault(group =>
                Math.Abs(GetVerticalCenter(group[0]) - center) <= 2.4);
            if (line is null)
            {
                line = new List<Word>();
                lineGroups.Add(line);
            }

            line.Add(word);
        }

        return lineGroups
            .OrderByDescending(group => GetVerticalCenter(group[0]))
            .Select(group => NormalizeWhitespace(string.Join(' ', group
                .OrderBy(word => word.BoundingBox.Left)
                .Select(word => word.Text))))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static double GetVerticalCenter(Word word)
    {
        return (word.BoundingBox.Top + word.BoundingBox.Bottom) / 2d;
    }

    private static (DateOnly? Start, DateOnly? End) ParsePeriod(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = PeriodPattern.Match(line);
            if (match.Success)
            {
                return (
                    ParseDate(match.Groups["start"].Value),
                    ParseDate(match.Groups["end"].Value));
            }
        }

        return (null, null);
    }

    private static DateOnly ParseDate(string text)
    {
        return DateOnly.ParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static TimeOnly ParseTime(string text)
    {
        return TimeOnly.ParseExact(text, "HH:mm", CultureInfo.InvariantCulture);
    }

    private static decimal ParseMoney(string text)
    {
        var normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("+", string.Empty, StringComparison.Ordinal);
        return decimal.Parse(normalized, NumberStyles.Number, new CultureInfo("ru-RU"));
    }

    private static string CleanDescription(string text)
    {
        var cleaned = CardMaskPattern.Replace(text, string.Empty);
        cleaned = Regex.Replace(cleaned, @"\.?\s*Операция по карте\.?", string.Empty, RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\s+", " ", RegexOptions.CultureInvariant);
        return cleaned.Trim(' ', '.', '-', '*');
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
    }

    private static string DigitsOnly(string value)
    {
        return string.Concat(value.Where(char.IsDigit));
    }

    private sealed class PendingOperation
    {
        public PendingOperation(
            DateOnly date,
            TimeOnly time,
            string category,
            string amountText,
            decimal balanceAfter,
            string rawLine)
        {
            Date = date;
            Time = time;
            Category = category;
            AmountText = amountText;
            BalanceAfter = balanceAfter;
            RawLine = rawLine;
        }

        public DateOnly Date { get; }

        public TimeOnly Time { get; }

        public string Category { get; }

        public string AmountText { get; }

        public decimal BalanceAfter { get; }

        public string RawLine { get; }

        public string? ExternalReference { get; set; }

        public List<string> DescriptionLines { get; } = new();
    }
}
