namespace Banccoon.Core.Transactions;

public interface ITransactionDeletionService
{
    // Deletes each transaction and reverses its effect on the account balance(s) it moved - both
    // accounts for a transfer - so deleting an expense puts the money back. Unknown ids are
    // ignored. Returns how many transactions were deleted.
    Task<int> DeleteAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default);
}
