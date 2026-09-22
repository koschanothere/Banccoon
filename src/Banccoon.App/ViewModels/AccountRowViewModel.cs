using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class AccountRowViewModel : ViewModelBase
{
    private readonly string maskedIdentifierText;
    private readonly string fullIdentifierText;

    private bool isFavorite;
    private bool isNumberRevealed;

    public AccountRowViewModel(
        Account account,
        bool isPrimary,
        bool canMoveUp,
        bool canMoveDown,
        ICommand toggleFavoriteCommand,
        Action<Account> onEdit,
        Func<Guid, Task> onArchive,
        Func<Guid, Task> onUnarchive,
        Func<Guid, Task> onSetPrimary,
        Action<Account> onOpenCardDetails,
        Func<Guid, Task> onMoveUp,
        Func<Guid, Task> onMoveDown,
        Action<AccountRowViewModel> onOpenDetail)
    {
        Id = account.Id;
        Type = account.Type;
        Name = account.Name;
        TypeText = DisplayText.Format(account.Type);
        BalanceText = MoneyFormat.Format(account.CurrentBalance, account.Currency);
        IsArchived = account.IsArchived;
        isFavorite = account.IsFavorite;
        IsPrimary = isPrimary;
        CanMoveUp = canMoveUp;
        CanMoveDown = canMoveDown;
        ToggleFavoriteCommand = toggleFavoriteCommand;

        var hasAccountNumber = !string.IsNullOrWhiteSpace(account.AccountNumber);
        var hasCardDigits = !string.IsNullOrWhiteSpace(account.CardLastFourDigits);
        HasIdentifier = hasAccountNumber || hasCardDigits;
        maskedIdentifierText = hasAccountNumber
            ? AccountNumberFormat.Mask(account.AccountNumber)
            : hasCardDigits ? $"**** {account.CardLastFourDigits}" : string.Empty;
        fullIdentifierText = hasAccountNumber
            ? AccountNumberFormat.Format(account.AccountNumber)
            : hasCardDigits ? $"**** {account.CardLastFourDigits}" : string.Empty;

        IsGoal = account.Type == AccountType.Goal;
        if (IsGoal && account.PlanningValue is { } target && target > 0m)
        {
            HasGoalTarget = true;
            GoalTargetText = MoneyFormat.Format(target, account.Currency);
            GoalProgress = (double)Math.Clamp(account.CurrentBalance / target, 0m, 1m);
        }

        IsCreditCard = account.Type == AccountType.CreditCard;

        ToggleRevealCommand = new RelayCommand(() => IsNumberRevealed = !IsNumberRevealed);
        EditCommand = new RelayCommand(() => onEdit(account));
        ArchiveCommand = new RelayCommand(() => _ = onArchive(Id));
        UnarchiveCommand = new RelayCommand(() => _ = onUnarchive(Id));
        SetPrimaryCommand = new RelayCommand(() => _ = onSetPrimary(Id));
        OpenCardDetailsCommand = new RelayCommand(() => onOpenCardDetails(account));
        MoveUpCommand = new RelayCommand(() => _ = onMoveUp(Id));
        MoveDownCommand = new RelayCommand(() => _ = onMoveDown(Id));
        OpenDetailCommand = new RelayCommand(() => onOpenDetail(this));
    }

    public Guid Id { get; }

    public AccountType Type { get; }

    public string Name { get; }

    public string TypeText { get; }

    public string BalanceText { get; }

    public bool IsArchived { get; }

    public bool IsPrimary { get; }

    public bool CanMoveUp { get; }

    public bool CanMoveDown { get; }

    public bool IsGoal { get; }

    public bool HasGoalTarget { get; }

    public string GoalTargetText { get; } = string.Empty;

    public double GoalProgress { get; }

    public bool IsCreditCard { get; }

    public bool HasIdentifier { get; }

    public string IdentifierText => IsNumberRevealed ? fullIdentifierText : maskedIdentifierText;

    public string RevealToggleText => IsNumberRevealed
        ? Translator.Get("Accounts_RevealHide")
        : Translator.Get("Accounts_RevealShow");

    public bool IsNumberRevealed
    {
        get => isNumberRevealed;
        set
        {
            if (SetProperty(ref isNumberRevealed, value))
            {
                OnPropertyChanged(nameof(IdentifierText));
                OnPropertyChanged(nameof(RevealToggleText));
            }
        }
    }

    public bool IsFavorite
    {
        get => isFavorite;
        set => SetProperty(ref isFavorite, value);
    }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ToggleRevealCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand ArchiveCommand { get; }

    public ICommand UnarchiveCommand { get; }

    public ICommand SetPrimaryCommand { get; }

    public ICommand OpenCardDetailsCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    public ICommand OpenDetailCommand { get; }
}
