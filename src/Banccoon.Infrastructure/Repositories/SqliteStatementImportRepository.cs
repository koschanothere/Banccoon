using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteStatementImportRepository : SqliteRepositoryBase, IStatementImportRepository
{
    public SqliteStatementImportRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<StatementImportBatch>> GetAllBatchesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, AccountId, ParserId, ParserName, SourceFileName, SourceFilePath, ImportedAt, Status, RowCount
            FROM StatementImportBatches
            ORDER BY ImportedAt DESC;
            """;

        var batches = new List<StatementImportBatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            batches.Add(ReadBatch(reader));
        }

        return batches;
    }

    public async Task<StatementImportBatch?> GetBatchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, AccountId, ParserId, ParserName, SourceFileName, SourceFilePath, ImportedAt, Status, RowCount
            FROM StatementImportBatches
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBatch(reader) : null;
    }

    public async Task<IReadOnlyList<StatementImportRow>> GetRowsByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                BatchId,
                Date,
                Amount,
                Type,
                Description,
                NormalizedDescription,
                Counterparty,
                ExternalReference,
                RawText,
                SuggestedCategoryId,
                CategoryId,
                Status,
                IsDuplicate,
                DuplicateTransactionId,
                CreatedTransactionId
            FROM StatementImportRows
            WHERE BatchId = @BatchId
            ORDER BY Date DESC, Description;
            """;
        AddParameter(command, "@BatchId", batchId.ToString());

        return await ReadRowsAsync(command, cancellationToken);
    }

    public async Task<StatementImportRow?> GetRowByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                BatchId,
                Date,
                Amount,
                Type,
                Description,
                NormalizedDescription,
                Counterparty,
                ExternalReference,
                RawText,
                SuggestedCategoryId,
                CategoryId,
                Status,
                IsDuplicate,
                DuplicateTransactionId,
                CreatedTransactionId
            FROM StatementImportRows
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
    }

    public async Task SaveBatchAsync(StatementImportBatch batch, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StatementImportBatches (
                Id,
                AccountId,
                ParserId,
                ParserName,
                SourceFileName,
                SourceFilePath,
                ImportedAt,
                Status,
                RowCount
            )
            VALUES (
                @Id,
                @AccountId,
                @ParserId,
                @ParserName,
                @SourceFileName,
                @SourceFilePath,
                @ImportedAt,
                @Status,
                @RowCount
            )
            ON CONFLICT(Id) DO UPDATE SET
                AccountId = excluded.AccountId,
                ParserId = excluded.ParserId,
                ParserName = excluded.ParserName,
                SourceFileName = excluded.SourceFileName,
                SourceFilePath = excluded.SourceFilePath,
                ImportedAt = excluded.ImportedAt,
                Status = excluded.Status,
                RowCount = excluded.RowCount;
            """;
        AddBatchParameters(command, batch);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveRowAsync(StatementImportRow row, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StatementImportRows (
                Id,
                BatchId,
                Date,
                Amount,
                Type,
                Description,
                NormalizedDescription,
                Counterparty,
                ExternalReference,
                RawText,
                SuggestedCategoryId,
                CategoryId,
                Status,
                IsDuplicate,
                DuplicateTransactionId,
                CreatedTransactionId
            )
            VALUES (
                @Id,
                @BatchId,
                @Date,
                @Amount,
                @Type,
                @Description,
                @NormalizedDescription,
                @Counterparty,
                @ExternalReference,
                @RawText,
                @SuggestedCategoryId,
                @CategoryId,
                @Status,
                @IsDuplicate,
                @DuplicateTransactionId,
                @CreatedTransactionId
            )
            ON CONFLICT(Id) DO UPDATE SET
                BatchId = excluded.BatchId,
                Date = excluded.Date,
                Amount = excluded.Amount,
                Type = excluded.Type,
                Description = excluded.Description,
                NormalizedDescription = excluded.NormalizedDescription,
                Counterparty = excluded.Counterparty,
                ExternalReference = excluded.ExternalReference,
                RawText = excluded.RawText,
                SuggestedCategoryId = excluded.SuggestedCategoryId,
                CategoryId = excluded.CategoryId,
                Status = excluded.Status,
                IsDuplicate = excluded.IsDuplicate,
                DuplicateTransactionId = excluded.DuplicateTransactionId,
                CreatedTransactionId = excluded.CreatedTransactionId;
            """;
        AddRowParameters(command, row);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM StatementImportRows;
            DELETE FROM StatementImportBatches;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<StatementImportRow>> ReadRowsAsync(
        Microsoft.Data.Sqlite.SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<StatementImportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static void AddBatchParameters(Microsoft.Data.Sqlite.SqliteCommand command, StatementImportBatch batch)
    {
        AddParameter(command, "@Id", batch.Id.ToString());
        AddParameter(command, "@AccountId", batch.AccountId.ToString());
        AddParameter(command, "@ParserId", batch.ParserId);
        AddParameter(command, "@ParserName", batch.ParserName);
        AddParameter(command, "@SourceFileName", batch.SourceFileName);
        AddParameter(command, "@SourceFilePath", SqliteData.ToDbValue(batch.SourceFilePath));
        AddParameter(command, "@ImportedAt", batch.ImportedAt.ToString("O"));
        AddParameter(command, "@Status", batch.Status.ToString());
        AddParameter(command, "@RowCount", batch.RowCount);
    }

    private static void AddRowParameters(Microsoft.Data.Sqlite.SqliteCommand command, StatementImportRow row)
    {
        AddParameter(command, "@Id", row.Id.ToString());
        AddParameter(command, "@BatchId", row.BatchId.ToString());
        AddParameter(command, "@Date", SqliteData.DateToText(row.Date));
        AddParameter(command, "@Amount", SqliteData.DecimalToText(row.Amount));
        AddParameter(command, "@Type", row.Type.ToString());
        AddParameter(command, "@Description", row.Description);
        AddParameter(command, "@NormalizedDescription", row.NormalizedDescription);
        AddParameter(command, "@Counterparty", SqliteData.ToDbValue(row.Counterparty));
        AddParameter(command, "@ExternalReference", SqliteData.ToDbValue(row.ExternalReference));
        AddParameter(command, "@RawText", SqliteData.ToDbValue(row.RawText));
        AddParameter(command, "@SuggestedCategoryId", SqliteData.ToDbValue(row.SuggestedCategoryId));
        AddParameter(command, "@CategoryId", SqliteData.ToDbValue(row.CategoryId));
        AddParameter(command, "@Status", row.Status.ToString());
        AddParameter(command, "@IsDuplicate", row.IsDuplicate ? 1 : 0);
        AddParameter(command, "@DuplicateTransactionId", SqliteData.ToDbValue(row.DuplicateTransactionId));
        AddParameter(command, "@CreatedTransactionId", SqliteData.ToDbValue(row.CreatedTransactionId));
    }

    private static StatementImportBatch ReadBatch(System.Data.Common.DbDataReader reader)
    {
        return new StatementImportBatch(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadGuid(reader, "AccountId"),
            SqliteData.ReadString(reader, "ParserId"),
            SqliteData.ReadString(reader, "ParserName"),
            SqliteData.ReadString(reader, "SourceFileName"),
            SqliteData.ReadNullableString(reader, "SourceFilePath"),
            DateTimeOffset.Parse(SqliteData.ReadString(reader, "ImportedAt"), System.Globalization.CultureInfo.InvariantCulture),
            Enum.Parse<StatementImportBatchStatus>(SqliteData.ReadString(reader, "Status")),
            reader.GetInt32(reader.GetOrdinal("RowCount")));
    }

    private static StatementImportRow ReadRow(System.Data.Common.DbDataReader reader)
    {
        return new StatementImportRow(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadGuid(reader, "BatchId"),
            SqliteData.ReadDate(reader, "Date"),
            SqliteData.ReadDecimal(reader, "Amount"),
            Enum.Parse<TransactionType>(SqliteData.ReadString(reader, "Type")),
            SqliteData.ReadString(reader, "Description"),
            SqliteData.ReadString(reader, "NormalizedDescription"),
            SqliteData.ReadNullableString(reader, "Counterparty"),
            SqliteData.ReadNullableString(reader, "ExternalReference"),
            SqliteData.ReadNullableString(reader, "RawText"),
            SqliteData.ReadNullableGuid(reader, "SuggestedCategoryId"),
            SqliteData.ReadNullableGuid(reader, "CategoryId"),
            Enum.Parse<StatementImportRowStatus>(SqliteData.ReadString(reader, "Status")),
            SqliteData.ReadBoolean(reader, "IsDuplicate"),
            SqliteData.ReadNullableGuid(reader, "DuplicateTransactionId"),
            SqliteData.ReadNullableGuid(reader, "CreatedTransactionId"));
    }
}
