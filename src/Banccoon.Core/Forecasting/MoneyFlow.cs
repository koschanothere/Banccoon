using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public static class MoneyFlow
{
    public static decimal GetSignedAmount(decimal amount, TransactionType type)
    {
        var normalizedAmount = Math.Abs(amount);

        return type switch
        {
            TransactionType.Income => normalizedAmount,
            TransactionType.Expense => -normalizedAmount,
            TransactionType.Transfer => 0m,
            _ => throw new NotSupportedException($"Unsupported transaction type: {type}")
        };
    }
}
