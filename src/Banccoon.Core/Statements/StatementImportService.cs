using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.Core.Statements;

public sealed class StatementImportService : IStatementImportService
{
    private const string OtherCategoryName = "Other";

    private readonly IStatementParserRegistry parserRegistry;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ITransactionBalanceService transactionBalanceService;
    private readonly ICategorySuggestionService categorySuggestionService;

    public StatementImportService(
        IStatementParserRegistry parserRegistry,
        IStatementImportRepository statementImportRepository,
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        ITransactionBalanceService transactionBalanceService,
        ICategorySuggestionService categorySuggestionService)
    {
        this.parserRegistry = parserRegistry;
        this.statementImportRepository = statementImportRepository;
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.transactionBalanceService = transactionBalanceService;
        this.categorySuggestionService = categorySuggestionService;
    }

    public async Task<StatementPreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new StatementPreviewResult(
                ParserAvailable: false,
                "Choose a bank statement file first.",
                null);
        }

        var request = new StatementParseRequest(filePath);
        var parser = parserRegistry.FindParser(request);
        if (parser is null)
        {
            return new StatementPreviewResult(
                ParserAvailable: false,
                "No parser is available for this statement yet.",
                null);
        }

        var parsedStatement = await parser.ParseAsync(request, cancellationToken);
        return new StatementPreviewResult(
            ParserAvailable: true,
            $"{parsedStatement.Rows.Count} statement row(s) found.",
            parsedStatement);
    }

    public async Task<StatementImportCreateResult> CreatePendingImportAsync(
        Guid accountId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new StatementImportCreateResult(
                ParserAvailable: false,
                "Choose a bank statement file first.",
                null,
                Array.Empty<StatementImportRow>());
        }

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("The selected account could not be found.");
        }

        var request = new StatementParseRequest(filePath, accountId);
        var parser = parserRegistry.FindParser(request);
        if (parser is null)
        {
            return new StatementImportCreateResult(
                ParserAvailable: false,
                "No parser is available for this statement yet. Add a bank-specific parser after a redacted sample is provided.",
                null,
                Array.Empty<StatementImportRow>());
        }

        var parsedStatement = await parser.ParseAsync(request, cancellationToken);
        return await CreatePendingImportAsync(accountId, filePath, parsedStatement, cancellationToken);
    }

    public async Task<StatementImportCreateResult> CreatePendingImportAsync(
        Guid accountId,
        string filePath,
        ParsedStatement parsedStatement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parsedStatement);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new StatementImportCreateResult(
                ParserAvailable: false,
                "Choose a bank statement file first.",
                null,
                Array.Empty<StatementImportRow>());
        }

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("The selected account could not be found.");
        }

        var batch = new StatementImportBatch(
            Guid.NewGuid(),
            accountId,
            parsedStatement.ParserId,
            parsedStatement.ParserName,
            string.IsNullOrWhiteSpace(parsedStatement.SourceName)
                ? Path.GetFileName(filePath)
                : parsedStatement.SourceName,
            filePath,
            DateTimeOffset.UtcNow,
            StatementImportBatchStatus.PendingReview,
            parsedStatement.Rows.Count);

        var rules = await categoryLearningRuleRepository.GetAllAsync(cancellationToken);
        var existingTransactions = await transactionRepository.GetByAccountIdAsync(accountId, cancellationToken);
        var rows = parsedStatement.Rows
            .Select(row => CreateImportRow(batch.Id, row, accountId, rules, existingTransactions))
            .ToArray();

        await statementImportRepository.SaveBatchAsync(batch, cancellationToken);
        foreach (var row in rows)
        {
            await statementImportRepository.SaveRowAsync(row, cancellationToken);
        }

        return new StatementImportCreateResult(
            ParserAvailable: true,
            $"{rows.Length} statement row(s) are ready for review.",
            batch,
            rows);
    }

    public async Task<StatementRowImportResult> ApproveRowAsync(
        Guid rowId,
        Guid? categoryId,
        CancellationToken cancellationToken = default)
    {
        var row = await statementImportRepository.GetRowByIdAsync(rowId, cancellationToken)
            ?? throw new InvalidOperationException("The statement row could not be found.");

        if (row.Status == StatementImportRowStatus.Approved)
        {
            var existingTransaction = row.CreatedTransactionId.HasValue
                ? await transactionRepository.GetByIdAsync(row.CreatedTransactionId.Value, cancellationToken)
                : null;
            return new StatementRowImportResult(row, existingTransaction);
        }

        if (row.Status == StatementImportRowStatus.Skipped)
        {
            throw new InvalidOperationException("Skipped statement rows cannot be approved.");
        }

        var batch = await statementImportRepository.GetBatchByIdAsync(row.BatchId, cancellationToken)
            ?? throw new InvalidOperationException("The statement import batch could not be found.");
        var account = await accountRepository.GetByIdAsync(batch.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("The selected account could not be found.");

        var finalCategoryId = categoryId ?? row.CategoryId ?? row.SuggestedCategoryId;
        if (finalCategoryId is null && row.Type != TransactionType.Transfer)
        {
            finalCategoryId = await EnsureOtherCategoryAsync(cancellationToken);
        }

        var transaction = new Transaction(
            Guid.NewGuid(),
            row.Date,
            Math.Abs(row.Amount),
            batch.AccountId,
            finalCategoryId,
            CreateTransactionNotes(batch, row),
            row.Type);

        await accountRepository.SaveAsync(transactionBalanceService.Apply(account, transaction), cancellationToken);
        await transactionRepository.SaveAsync(transaction, cancellationToken);

        var approvedRow = row with
        {
            CategoryId = finalCategoryId,
            Status = StatementImportRowStatus.Approved,
            CreatedTransactionId = transaction.Id
        };
        await statementImportRepository.SaveRowAsync(approvedRow, cancellationToken);
        await LearnCategoryAsync(approvedRow, batch.AccountId, finalCategoryId, cancellationToken);
        await CompleteBatchIfReviewedAsync(batch, cancellationToken);

        return new StatementRowImportResult(approvedRow, transaction);
    }

    public async Task<StatementImportRow> SkipRowAsync(
        Guid rowId,
        CancellationToken cancellationToken = default)
    {
        var row = await statementImportRepository.GetRowByIdAsync(rowId, cancellationToken)
            ?? throw new InvalidOperationException("The statement row could not be found.");

        if (row.Status == StatementImportRowStatus.Approved)
        {
            throw new InvalidOperationException("Approved statement rows cannot be skipped.");
        }

        var skippedRow = row with
        {
            Status = StatementImportRowStatus.Skipped
        };
        await statementImportRepository.SaveRowAsync(skippedRow, cancellationToken);

        var batch = await statementImportRepository.GetBatchByIdAsync(row.BatchId, cancellationToken);
        if (batch is not null)
        {
            await CompleteBatchIfReviewedAsync(batch, cancellationToken);
        }

        return skippedRow;
    }

    public async Task<StatementImportCancelResult> CancelImportAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await statementImportRepository.GetBatchByIdAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new StatementImportCancelResult(false, "The statement import could not be found.");
        }

        var rows = await statementImportRepository.GetRowsByBatchIdAsync(batchId, cancellationToken);
        if (rows.Any(row => row.Status == StatementImportRowStatus.Approved))
        {
            return new StatementImportCancelResult(
                false,
                "This statement already created transactions, so the import batch cannot be cancelled.");
        }

        await statementImportRepository.DeleteBatchAsync(batchId, cancellationToken);
        return new StatementImportCancelResult(true, "Statement import cancelled.");
    }

    private StatementImportRow CreateImportRow(
        Guid batchId,
        ParsedStatementRow parsedRow,
        Guid accountId,
        IReadOnlyList<CategoryLearningRule> rules,
        IReadOnlyList<Transaction> existingTransactions)
    {
        var normalizedDescription = categorySuggestionService.Normalize(
            string.IsNullOrWhiteSpace(parsedRow.Counterparty)
                ? parsedRow.Description
                : parsedRow.Counterparty);
        var suggestion = categorySuggestionService.Suggest(parsedRow, accountId, rules);
        var duplicateTransaction = FindDuplicate(parsedRow, normalizedDescription, existingTransactions);

        return new StatementImportRow(
            Guid.NewGuid(),
            batchId,
            parsedRow.Date,
            Math.Abs(parsedRow.Amount),
            parsedRow.Type,
            parsedRow.Description.Trim(),
            normalizedDescription,
            CleanOptionalText(parsedRow.Counterparty),
            CleanOptionalText(parsedRow.ExternalReference),
            CleanOptionalText(parsedRow.RawText),
            suggestion?.CategoryId,
            null,
            StatementImportRowStatus.Pending,
            duplicateTransaction is not null,
            duplicateTransaction?.Id,
            null);
    }

    private static Transaction? FindDuplicate(
        ParsedStatementRow parsedRow,
        string normalizedDescription,
        IReadOnlyList<Transaction> existingTransactions)
    {
        return existingTransactions.FirstOrDefault(transaction =>
            transaction.Date == parsedRow.Date
            && transaction.Type == parsedRow.Type
            && decimal.Round(Math.Abs(transaction.Amount), 2) == decimal.Round(Math.Abs(parsedRow.Amount), 2)
            && IsDescriptionMatch(transaction.Notes, normalizedDescription, parsedRow.ExternalReference));
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

    private async Task<Guid> EnsureOtherCategoryAsync(CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var other = categories.FirstOrDefault(category =>
            string.Equals(category.Name, OtherCategoryName, StringComparison.OrdinalIgnoreCase)
            && category.Type is null);

        if (other is not null)
        {
            return other.Id;
        }

        var category = new Category(Guid.NewGuid(), OtherCategoryName);
        await categoryRepository.SaveAsync(category, cancellationToken);
        return category.Id;
    }

    private async Task LearnCategoryAsync(
        StatementImportRow row,
        Guid accountId,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId is null || row.Type == TransactionType.Transfer)
        {
            return;
        }

        var rules = await categoryLearningRuleRepository.GetAllAsync(cancellationToken);
        var rule = categorySuggestionService.Learn(row, accountId, categoryId.Value, rules, DateTimeOffset.UtcNow);
        await categoryLearningRuleRepository.SaveAsync(rule, cancellationToken);
    }

    private async Task CompleteBatchIfReviewedAsync(
        StatementImportBatch batch,
        CancellationToken cancellationToken)
    {
        var rows = await statementImportRepository.GetRowsByBatchIdAsync(batch.Id, cancellationToken);
        if (rows.Count > 0 && rows.All(row => row.Status != StatementImportRowStatus.Pending))
        {
            await statementImportRepository.SaveBatchAsync(batch with
            {
                Status = StatementImportBatchStatus.Completed
            }, cancellationToken);
        }
    }

    private static string CreateTransactionNotes(
        StatementImportBatch batch,
        StatementImportRow row)
    {
        var parts = new List<string>
        {
            $"Statement import: {row.Description}",
            $"Source: {batch.SourceFileName}"
        };

        if (!string.IsNullOrWhiteSpace(row.Counterparty))
        {
            parts.Add($"Counterparty: {row.Counterparty}");
        }

        if (!string.IsNullOrWhiteSpace(row.ExternalReference))
        {
            parts.Add($"Reference: {row.ExternalReference}");
        }

        return string.Join(" | ", parts);
    }

    private static string? CleanOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
