using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.Core.Transactions;

public sealed class TransactionDeletionService : ITransactionDeletionService
{
    private readonly ITransactionRepository transactionRepository;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionApplicationService transactionApplicationService;

    public TransactionDeletionService(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ITransactionApplicationService transactionApplicationService)
    {
        this.transactionRepository = transactionRepository;
        this.accountRepository = accountRepository;
        this.transactionApplicationService = transactionApplicationService;
    }

    public async Task<int> DeleteAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionIds);

        // Balances are tracked in memory across the whole batch, so deleting several transactions
        // on the same account reverses each one from the already-reversed balance, not the stale
        // one read at the start.
        var accountsById = (await accountRepository.GetAllAsync(cancellationToken)).ToDictionary(account => account.Id);
        var deleted = 0;

        foreach (var transactionId in transactionIds.Distinct())
        {
            var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
            if (transaction is null)
            {
                continue;
            }

            foreach (var restoredAccount in Reverse(transaction, accountsById))
            {
                accountsById[restoredAccount.Id] = restoredAccount;
                await accountRepository.SaveAsync(restoredAccount, cancellationToken);
            }

            await transactionRepository.DeleteAsync(transaction.Id, cancellationToken);
            deleted++;
        }

        return deleted;
    }

    // Accounts are only ever archived in the app, never deleted, so a missing account only happens
    // with unusual restored data - then whatever side of the transaction still exists is reversed,
    // and the transaction is deleted either way.
    private IReadOnlyList<Account> Reverse(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById)
    {
        if (!accountsById.ContainsKey(transaction.AccountId))
        {
            return [];
        }

        var reversible = transaction.DestinationAccountId is { } destinationAccountId && !accountsById.ContainsKey(destinationAccountId)
            ? transaction with { DestinationAccountId = null }
            : transaction;

        return transactionApplicationService.ReverseTransaction(reversible, accountsById);
    }
}
