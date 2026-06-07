using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteAccountRepository : SqliteRepositoryBase, IAccountRepository
{
    public SqliteAccountRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                Name,
                Type,
                CurrentBalance,
                Currency,
                CreatedDate,
                IsArchived,
                CreditCardCurrentDebt,
                StatementDayOfMonth,
                PaymentDueDayOfMonth,
                MinimumPayment,
                PlannedPaymentAmount
            FROM Accounts
            ORDER BY Name;
            """;

        var accounts = new List<Account>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                Name,
                Type,
                CurrentBalance,
                Currency,
                CreatedDate,
                IsArchived,
                CreditCardCurrentDebt,
                StatementDayOfMonth,
                PaymentDueDayOfMonth,
                MinimumPayment,
                PlannedPaymentAmount
            FROM Accounts
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Accounts (
                Id,
                Name,
                Type,
                CurrentBalance,
                Currency,
                CreatedDate,
                IsArchived,
                CreditCardCurrentDebt,
                StatementDayOfMonth,
                PaymentDueDayOfMonth,
                MinimumPayment,
                PlannedPaymentAmount
            )
            VALUES (
                @Id,
                @Name,
                @Type,
                @CurrentBalance,
                @Currency,
                @CreatedDate,
                @IsArchived,
                @CreditCardCurrentDebt,
                @StatementDayOfMonth,
                @PaymentDueDayOfMonth,
                @MinimumPayment,
                @PlannedPaymentAmount
            )
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Type = excluded.Type,
                CurrentBalance = excluded.CurrentBalance,
                Currency = excluded.Currency,
                CreatedDate = excluded.CreatedDate,
                IsArchived = excluded.IsArchived,
                CreditCardCurrentDebt = excluded.CreditCardCurrentDebt,
                StatementDayOfMonth = excluded.StatementDayOfMonth,
                PaymentDueDayOfMonth = excluded.PaymentDueDayOfMonth,
                MinimumPayment = excluded.MinimumPayment,
                PlannedPaymentAmount = excluded.PlannedPaymentAmount;
            """;

        AddParameter(command, "@Id", account.Id.ToString());
        AddParameter(command, "@Name", account.Name);
        AddParameter(command, "@Type", account.Type.ToString());
        AddParameter(command, "@CurrentBalance", SqliteData.DecimalToText(account.CurrentBalance));
        AddParameter(command, "@Currency", account.Currency);
        AddParameter(command, "@CreatedDate", account.CreatedDate.ToString("O"));
        AddParameter(command, "@IsArchived", account.IsArchived ? 1 : 0);
        AddParameter(command, "@CreditCardCurrentDebt", SqliteData.ToDbValue(account.CreditCardDetails?.CurrentDebt));
        AddParameter(command, "@StatementDayOfMonth", account.CreditCardDetails?.StatementDayOfMonth ?? (object)DBNull.Value);
        AddParameter(command, "@PaymentDueDayOfMonth", account.CreditCardDetails?.PaymentDueDayOfMonth ?? (object)DBNull.Value);
        AddParameter(command, "@MinimumPayment", SqliteData.ToDbValue(account.CreditCardDetails?.MinimumPayment));
        AddParameter(command, "@PlannedPaymentAmount", SqliteData.ToDbValue(account.CreditCardDetails?.PlannedPaymentAmount));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Accounts WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Accounts;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Account ReadAccount(System.Data.Common.DbDataReader reader)
    {
        CreditCardDetails? creditCardDetails = new CreditCardDetails(
            SqliteData.ReadNullableDecimal(reader, "CreditCardCurrentDebt"),
            SqliteData.ReadNullableInt32(reader, "StatementDayOfMonth"),
            SqliteData.ReadNullableInt32(reader, "PaymentDueDayOfMonth"),
            SqliteData.ReadNullableDecimal(reader, "MinimumPayment"),
            SqliteData.ReadNullableDecimal(reader, "PlannedPaymentAmount"));

        if (creditCardDetails.CurrentDebt is null
            && creditCardDetails.StatementDayOfMonth is null
            && creditCardDetails.PaymentDueDayOfMonth is null
            && creditCardDetails.MinimumPayment is null
            && creditCardDetails.PlannedPaymentAmount is null)
        {
            creditCardDetails = null;
        }

        return new Account(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadString(reader, "Name"),
            Enum.Parse<AccountType>(SqliteData.ReadString(reader, "Type")),
            SqliteData.ReadDecimal(reader, "CurrentBalance"),
            SqliteData.ReadString(reader, "Currency"),
            DateTimeOffset.Parse(SqliteData.ReadString(reader, "CreatedDate"), System.Globalization.CultureInfo.InvariantCulture),
            SqliteData.ReadBoolean(reader, "IsArchived"),
            creditCardDetails);
    }
}
