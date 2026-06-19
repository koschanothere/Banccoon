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

        return new ParsedStatement(
            Descriptor.Id,
            Descriptor.Name,
            sourceName,
            rows,
            periodStart,
            periodEnd,
            balances.FirstOrDefault(),
            balances.Length == 0 ? null : balances.Last());
    }

    private static IReadOnlyList<ParsedStatementRow> ParseRows(IReadOnlyList<string> lines)
    {
        var rows = new List<ParsedStatementRow>();
        PendingOperation? pending = null;

        foreach (var rawLine in lines)
        {
            var line = NormalizeWhitespace(rawLine);
            if (ShouldIgnoreLine(line))
            {
                continue;
            }

            var operationMatch = OperationLinePattern.Match(line);
            if (operationMatch.Success)
            {
                FlushPending(rows, ref pending);
                pending = new PendingOperation(
                    ParseDate(operationMatch.Groups["date"].Value),
                    operationMatch.Groups["category"].Value.Trim(),
                    operationMatch.Groups["amount"].Value,
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
            RawText: rawText));

        pending = null;
    }

    private static void AddDescriptionLine(PendingOperation pending, string line)
    {
        var cleanedLine = NormalizeWhitespace(line);
        if (!string.IsNullOrWhiteSpace(cleanedLine) && !ShouldIgnoreLine(cleanedLine))
        {
            pending.DescriptionLines.Add(cleanedLine);
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
        return cleaned.Trim(' ', '.', '-');
    }

    private static bool ShouldIgnoreLine(string line)
    {
        return line.Length == 0
            || line.StartsWith("ДАТА ОПЕРАЦИИ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Дата обработки", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("и код авторизации", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Продолжение", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Для проверки", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Дата формирования", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("ПАО Сбербанк", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Денежные средства", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("В выписке", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Срок обработки", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Согласно статье", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Скачать электронный", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Проверить подпись", StringComparison.OrdinalIgnoreCase)
            || line == "*";
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
    }

    private sealed class PendingOperation
    {
        public PendingOperation(
            DateOnly date,
            string category,
            string amountText,
            string rawLine)
        {
            Date = date;
            Category = category;
            AmountText = amountText;
            RawLine = rawLine;
        }

        public DateOnly Date { get; }

        public string Category { get; }

        public string AmountText { get; }

        public string RawLine { get; }

        public string? ExternalReference { get; set; }

        public List<string> DescriptionLines { get; } = new();
    }
}
