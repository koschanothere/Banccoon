using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

// Pulled out of StatementImportService since it's a self-contained, pure-function concern: given
// one incoming row and the account's known transactions, decide whether it's already recorded.
internal static class StatementDuplicateDetector
{
    public static Transaction? FindDuplicate(
        Guid accountId,
        ParsedStatementRow parsedRow,
        string normalizedDescription,
        IReadOnlyList<Transaction> existingTransactions)
    {
        return existingTransactions.FirstOrDefault(transaction =>
            transaction.Date == parsedRow.Date
            && IsTimeCloseEnough(transaction.Time, parsedRow.Time)
            && decimal.Round(Math.Abs(transaction.Amount), 2) == decimal.Round(Math.Abs(parsedRow.Amount), 2)
            && IsSameOperationOrTransferCounterpart(transaction, accountId, parsedRow, normalizedDescription));
    }

    private static bool IsSameOperationOrTransferCounterpart(
        Transaction transaction,
        Guid accountId,
        ParsedStatementRow parsedRow,
        string normalizedDescription)
    {
        if (transaction.AccountId == accountId && transaction.Type == parsedRow.Type)
        {
            return IsDescriptionMatch(transaction.Notes, normalizedDescription, parsedRow.ExternalReference);
        }

        // A Transfer already recorded - from the OTHER account's own import/approval - with this
        // account as its destination is the same real-world event, even though this statement
        // describes it completely differently (different bank, different wording, maybe not even
        // calling it a transfer at all). Date, close-enough time, and amount are the only signal
        // that can be trusted across two independently-worded statements.
        return transaction.Type == TransactionType.Transfer && transaction.DestinationAccountId == accountId;
    }

    private static bool IsTimeCloseEnough(TimeOnly? first, TimeOnly? second)
    {
        if (first is null || second is null)
        {
            return true;
        }

        // TimeOnly's own "-" operator wraps to stay non-negative (10:00 - 10:02 comes back as
        // ~23h58m, not -2m), so the plain difference is computed from each side's minute-of-day
        // instead. The 1440-minus-diff branch also handles the midnight case (23:59 vs 00:01
        // should read as 2 minutes apart, not 23h58m).
        var diff = Math.Abs(first.Value.ToTimeSpan().TotalMinutes - second.Value.ToTimeSpan().TotalMinutes);
        return Math.Min(diff, 1440 - diff) <= 5;
    }

    private static bool IsDescriptionMatch(
        string? transactionNotes,
        string normalizedDescription,
        string? externalReference)
    {
        if (!string.IsNullOrWhiteSpace(externalReference)
            && transactionNotes?.Contains(externalReference, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(transactionNotes) || string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return true;
        }

        var normalizedNotes = new CategorySuggestionService().Normalize(transactionNotes);
        return normalizedNotes.Contains(normalizedDescription, StringComparison.OrdinalIgnoreCase)
            || normalizedDescription.Contains(normalizedNotes, StringComparison.OrdinalIgnoreCase);
    }
}
