using Banccoon.App.Formatting;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class TransactionRowViewModel : ViewModelBase
{
    private bool isSelected;
    private bool isSelectModeActive;

    public TransactionRowViewModel(
        Transaction transaction,
        string categoryOrDestinationText,
        decimal balanceAfter,
        string currency)
    {
        Id = transaction.Id;
        AccountId = transaction.AccountId;
        CategoryId = transaction.CategoryId;
        Date = transaction.Date;
        Type = transaction.Type;
        Name = string.IsNullOrWhiteSpace(transaction.Name) ? categoryOrDestinationText : transaction.Name;
        CategoryOrDestinationText = categoryOrDestinationText;
        IsScheduled = transaction.PaidScheduledTransactionId.HasValue;

        var signedAmount = transaction.Type == TransactionType.Expense
            ? -Math.Abs(transaction.Amount)
            : transaction.Type == TransactionType.Income
                ? Math.Abs(transaction.Amount)
                : -Math.Abs(transaction.Amount);
        AmountText = MoneyFormat.Format(signedAmount, currency);
        IsPositive = signedAmount > 0;
        BalanceAfterText = MoneyFormat.Format(balanceAfter, currency);

        BadgeColor = CategoryId is { } categoryId
            ? CategoryColorPalette.GetColorForCategory(categoryId)
            : CategoryColorPalette.GetTransferColor();
        BadgeLetter = categoryOrDestinationText.Length > 0
            ? categoryOrDestinationText[..1].ToUpperInvariant()
            : "?";
    }

    public Guid Id { get; }

    public Guid AccountId { get; }

    public Guid? CategoryId { get; }

    public DateOnly Date { get; }

    public TransactionType Type { get; }

    public string Name { get; }

    public string CategoryOrDestinationText { get; }

    public bool IsScheduled { get; }

    public string AmountText { get; }

    public bool IsPositive { get; }

    public string BalanceAfterText { get; }

    public Color BadgeColor { get; }

    public string BadgeLetter { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsSelectModeActive
    {
        get => isSelectModeActive;
        set => SetProperty(ref isSelectModeActive, value);
    }
}
