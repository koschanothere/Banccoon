using Banccoon.Core.Forecasting;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.ImportExport;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.ImportExport;

public sealed class ImportExportServiceTests
{
    [Fact]
    public async Task CreateExportAsync_IncludesAllPortableData()
    {
        await using var store = new SqliteTestStore();
        var services = CreateServices(store);
        var sample = await SeedSampleDataAsync(store);

        var export = await services.ExportService.CreateExportAsync();

        Assert.Equal(ExportFormat.CurrentVersion, export.ExportFormatVersion);
        Assert.Equal(sample.Account, Assert.Single(export.Data.Accounts));
        Assert.Equal(sample.Category, Assert.Single(export.Data.Categories));
        Assert.Equal(sample.Transaction, Assert.Single(export.Data.Transactions));
        Assert.Equal(sample.ScheduledTransaction, Assert.Single(export.Data.ScheduledTransactions));
        Assert.Equal(sample.SavingsGoal, Assert.Single(export.Data.SavingsGoals));
        Assert.Equal(sample.Settings, export.Data.Settings);
        Assert.Equal(sample.StatementImportBatch, Assert.Single(export.Data.StatementImportBatches));
        Assert.Equal(sample.StatementImportRow, Assert.Single(export.Data.StatementImportRows));
        Assert.Equal(sample.CategoryLearningRule, Assert.Single(export.Data.CategoryLearningRules));
    }

    [Fact]
    public async Task ValidateAsync_WhenTransactionReferencesMissingAccount_ReturnsError()
    {
        await using var store = new SqliteTestStore();
        var services = CreateServices(store);
        var transaction = new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 8),
            10m,
            Guid.NewGuid(),
            null,
            null,
            TransactionType.Expense);
        var export = new ExportEnvelope(
            ExportFormat.CurrentVersion,
            "1.0.0",
            DateTimeOffset.UtcNow,
            new ExportData(
                Array.Empty<Account>(),
                [transaction],
                Array.Empty<ScheduledTransaction>(),
                Array.Empty<Category>(),
                Array.Empty<SavingsGoal>(),
                new AppSettings("EUR", ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)));

        var validation = await services.ImportService.ValidateAsync(export);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("references missing account", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_WhenReplaceMode_RemovesExistingDataBeforeImport()
    {
        await using var store = new SqliteTestStore();
        var services = CreateServices(store);
        var existing = CreateAccount("Old account");
        await store.Accounts.SaveAsync(existing);
        var incoming = CreateExportEnvelope(CreateAccount("Imported account"));

        var result = await services.ImportService.ImportAsync(incoming, ImportMode.Replace);

        var accounts = await store.Accounts.GetAllAsync();
        Assert.True(result.Validation.IsValid);
        Assert.Equal(1, result.AccountsImported);
        Assert.Equal("Imported account", Assert.Single(accounts).Name);
    }

    [Fact]
    public async Task ImportAsync_WhenMergeMode_PreservesExistingData()
    {
        await using var store = new SqliteTestStore();
        var services = CreateServices(store);
        var existing = CreateAccount("Existing account");
        await store.Accounts.SaveAsync(existing);
        var incoming = CreateExportEnvelope(CreateAccount("Imported account"));

        await services.ImportService.ImportAsync(incoming, ImportMode.Merge);

        var accounts = await store.Accounts.GetAllAsync();
        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, account => account.Name == "Existing account");
        Assert.Contains(accounts, account => account.Name == "Imported account");
    }

    [Fact]
    public async Task BackupService_CreatesReadableJsonBackupAndRestoresIt()
    {
        await using var sourceStore = new SqliteTestStore();
        await using var targetStore = new SqliteTestStore();
        var sourceServices = CreateServices(sourceStore);
        var targetServices = CreateServices(targetStore);
        var sample = await SeedSampleDataAsync(sourceStore);
        var backupPath = Path.Combine(Path.GetTempPath(), "Banccoon.Tests", $"{Guid.NewGuid():N}.json");

        try
        {
            await sourceServices.BackupService.CreateBackupAsync(backupPath);
            var export = await sourceServices.BackupService.ReadBackupAsync(backupPath);
            var result = await targetServices.BackupService.RestoreBackupAsync(backupPath, ImportMode.Replace);

            Assert.Equal(ExportFormat.CurrentVersion, export.ExportFormatVersion);
            Assert.True(result.Validation.IsValid);
            Assert.Equal(sample.Account, await targetStore.Accounts.GetByIdAsync(sample.Account.Id));
            Assert.Equal(sample.Settings, await targetStore.Settings.GetAsync());
            Assert.Equal(sample.StatementImportBatch, await targetStore.StatementImports.GetBatchByIdAsync(sample.StatementImportBatch.Id));
            Assert.Equal(sample.StatementImportRow, await targetStore.StatementImports.GetRowByIdAsync(sample.StatementImportRow.Id));
            Assert.Equal(sample.CategoryLearningRule, await targetStore.CategoryLearningRules.GetByIdAsync(sample.CategoryLearningRule.Id));
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    private static Services CreateServices(SqliteTestStore store)
    {
        var validator = new ExportValidator();
        var exportService = new RepositoryExportService(
            store.Accounts,
            store.Categories,
            store.Transactions,
            store.ScheduledTransactions,
            store.SavingsGoals,
            store.Settings,
            store.StatementImports,
            store.CategoryLearningRules);
        var importService = new RepositoryImportService(
            store.Accounts,
            store.Categories,
            store.Transactions,
            store.ScheduledTransactions,
            store.SavingsGoals,
            store.Settings,
            validator,
            store.StatementImports,
            store.CategoryLearningRules);
        var backupService = new JsonBackupService(exportService, importService);

        return new Services(exportService, importService, backupService);
    }

    private static ExportEnvelope CreateExportEnvelope(Account account)
    {
        return new ExportEnvelope(
            ExportFormat.CurrentVersion,
            "1.0.0",
            DateTimeOffset.UtcNow,
            new ExportData(
                [account],
                Array.Empty<Transaction>(),
                Array.Empty<ScheduledTransaction>(),
                Array.Empty<Category>(),
                Array.Empty<SavingsGoal>(),
                new AppSettings(account.Currency, ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly)));
    }

    private static async Task<SampleData> SeedSampleDataAsync(SqliteTestStore store)
    {
        var account = CreateAccount("Checking");
        var category = new Category(Guid.NewGuid(), "Rent");
        var scheduledTransaction = new ScheduledTransaction(
            Guid.NewGuid(),
            "Salary",
            1500m,
            account.Id,
            null,
            TransactionType.Income,
            new RecurrenceRule(
                RecurrenceFrequency.Monthly,
                1,
                new DateOnly(2026, 6, 1),
                DayOfMonth: 10),
            new DateOnly(2026, 6, 10),
            Active: true);
        var transaction = new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 8),
            25m,
            account.Id,
            category.Id,
            "Lunch",
            TransactionType.Expense,
            PaidScheduledTransactionId: scheduledTransaction.Id,
            PaidScheduledOccurrenceDate: new DateOnly(2026, 6, 8),
            Name: "Cafe lunch");
        var savingsGoal = new SavingsGoal(
            Guid.NewGuid(),
            "Trip",
            1000m,
            250m,
            new DateOnly(2026, 12, 1),
            account.Id);
        var settings = new AppSettings("USD", ForecastPeriod.NinetyDays, ReminderFrequency.Biweekly);
        var statementImportBatch = new StatementImportBatch(
            Guid.NewGuid(),
            account.Id,
            "fake",
            "Fake statement parser",
            "statement.fake",
            null,
            new DateTimeOffset(2026, 6, 12, 9, 0, 0, TimeSpan.Zero),
            StatementImportBatchStatus.PendingReview,
            RowCount: 1);
        var statementImportRow = new StatementImportRow(
            Guid.NewGuid(),
            statementImportBatch.Id,
            new DateOnly(2026, 6, 8),
            25m,
            TransactionType.Expense,
            "Lunch",
            "LUNCH",
            "Cafe",
            "ref-1",
            null,
            category.Id,
            category.Id,
            StatementImportRowStatus.Approved,
            IsDuplicate: false,
            null,
            transaction.Id);
        var categoryLearningRule = new CategoryLearningRule(
            Guid.NewGuid(),
            "Cafe",
            "CAFE",
            TransactionType.Expense,
            category.Id,
            account.Id,
            25m,
            MatchCount: 1,
            new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero));

        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        await store.ScheduledTransactions.SaveAsync(scheduledTransaction);
        await store.Transactions.SaveAsync(transaction);
        await store.SavingsGoals.SaveAsync(savingsGoal);
        await store.Settings.SaveAsync(settings);
        await store.StatementImports.SaveBatchAsync(statementImportBatch);
        await store.StatementImports.SaveRowAsync(statementImportRow);
        await store.CategoryLearningRules.SaveAsync(categoryLearningRule);

        return new SampleData(
            account,
            category,
            transaction,
            scheduledTransaction,
            savingsGoal,
            settings,
            statementImportBatch,
            statementImportRow,
            categoryLearningRule);
    }

    private static Account CreateAccount(string name)
    {
        return new Account(
            Guid.NewGuid(),
            name,
            AccountType.DebitCard,
            1000m,
            "EUR",
            new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero),
            IncludeInDashboardTotals: true,
            AccountNumber: "ACC-EXPORT",
            CardLastFourDigits: "9876");
    }

    private sealed record Services(
        RepositoryExportService ExportService,
        RepositoryImportService ImportService,
        JsonBackupService BackupService);

    private sealed record SampleData(
        Account Account,
        Category Category,
        Transaction Transaction,
        ScheduledTransaction ScheduledTransaction,
        SavingsGoal SavingsGoal,
        AppSettings Settings,
        StatementImportBatch StatementImportBatch,
        StatementImportRow StatementImportRow,
        CategoryLearningRule CategoryLearningRule);
}
