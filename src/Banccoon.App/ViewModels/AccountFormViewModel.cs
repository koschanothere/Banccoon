using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AccountFormViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private readonly Func<Task> onSaved;

    private bool isOpen;
    private Guid? editingAccountId;
    private string title = Translator.Get("Accounts_NewAccountTitle");
    private string name = string.Empty;
    private AccountType type = AccountType.DebitCard;
    private string currency = "EUR";
    private string balanceText = "0";
    private string accountNumberText = string.Empty;
    private string cardLastFourDigitsText = string.Empty;
    private bool includeInDashboardTotals = true;
    private string goalTargetText = string.Empty;
    private string minimumPaymentText = string.Empty;
    private string plannedPaymentText = string.Empty;
    private string statusText = string.Empty;

    public AccountFormViewModel(IAccountRepository accountRepository, Func<Task> onSaved)
    {
        this.accountRepository = accountRepository;
        this.onSaved = onSaved;
        AccountTypes = Enum.GetValues<AccountType>();

        CloseCommand = new RelayCommand(Close);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
    }

    public IReadOnlyList<AccountType> AccountTypes { get; }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public bool IsEditing => editingAccountId.HasValue;

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public AccountType Type
    {
        get => type;
        set
        {
            if (SetProperty(ref type, value))
            {
                OnPropertyChanged(nameof(IsGoalType));
                OnPropertyChanged(nameof(IsCreditCardType));
            }
        }
    }

    public bool IsGoalType => Type == AccountType.Goal;

    public bool IsCreditCardType => Type == AccountType.CreditCard;

    public string Currency
    {
        get => currency;
        set => SetProperty(ref currency, value);
    }

    public string BalanceText
    {
        get => balanceText;
        set => SetProperty(ref balanceText, value);
    }

    public string AccountNumberText
    {
        get => accountNumberText;
        set => SetProperty(ref accountNumberText, value);
    }

    public string CardLastFourDigitsText
    {
        get => cardLastFourDigitsText;
        set => SetProperty(ref cardLastFourDigitsText, value);
    }

    public bool IncludeInDashboardTotals
    {
        get => includeInDashboardTotals;
        set => SetProperty(ref includeInDashboardTotals, value);
    }

    public string GoalTargetText
    {
        get => goalTargetText;
        set => SetProperty(ref goalTargetText, value);
    }

    public string MinimumPaymentText
    {
        get => minimumPaymentText;
        set => SetProperty(ref minimumPaymentText, value);
    }

    public string PlannedPaymentText
    {
        get => plannedPaymentText;
        set => SetProperty(ref plannedPaymentText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand CloseCommand { get; }

    public ICommand SaveCommand { get; }

    public void Close() => IsOpen = false;

    public void OpenForCreate(string defaultCurrency)
    {
        editingAccountId = null;
        Title = Translator.Get("Accounts_NewAccountTitle");
        Name = string.Empty;
        Type = AccountType.DebitCard;
        Currency = defaultCurrency;
        BalanceText = "0";
        AccountNumberText = string.Empty;
        CardLastFourDigitsText = string.Empty;
        IncludeInDashboardTotals = true;
        GoalTargetText = string.Empty;
        MinimumPaymentText = string.Empty;
        PlannedPaymentText = string.Empty;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
        IsOpen = true;
    }

    public void OpenForEdit(Account account)
    {
        editingAccountId = account.Id;
        Title = Translator.Get("Accounts_EditAccountTitle");
        Name = account.Name;
        Type = account.Type;
        Currency = account.Currency;
        BalanceText = account.CurrentBalance.ToString(CultureInfo.InvariantCulture);
        AccountNumberText = account.AccountNumber ?? string.Empty;
        CardLastFourDigitsText = account.CardLastFourDigits ?? string.Empty;
        IncludeInDashboardTotals = account.IncludeInDashboardTotals;
        GoalTargetText = account.PlanningValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        MinimumPaymentText = account.CreditCardDetails?.MinimumPayment?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        PlannedPaymentText = account.CreditCardDetails?.PlannedPaymentAmount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
        IsOpen = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusText = Translator.Get("Accounts_NameRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            StatusText = Translator.Get("Accounts_CurrencyRequired");
            return;
        }

        if (!decimal.TryParse(BalanceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var balance))
        {
            StatusText = Translator.Get("Accounts_BalanceMustBeNumber");
            return;
        }

        decimal? goalTarget = null;
        if (IsGoalType && !string.IsNullOrWhiteSpace(GoalTargetText))
        {
            if (!decimal.TryParse(GoalTargetText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedTarget))
            {
                StatusText = Translator.Get("Accounts_GoalTargetMustBeNumber");
                return;
            }

            goalTarget = parsedTarget;
        }

        var existing = editingAccountId.HasValue
            ? await accountRepository.GetByIdAsync(editingAccountId.Value)
            : null;

        var creditCardDetails = IsCreditCardType
            ? (existing?.CreditCardDetails ?? new CreditCardDetails(null, null, null, null, null)) with
            {
                MinimumPayment = ParseOrNull(MinimumPaymentText),
                PlannedPaymentAmount = ParseOrNull(PlannedPaymentText)
            }
            : existing?.CreditCardDetails;

        var account = new Account(
            editingAccountId ?? Guid.NewGuid(),
            Name.Trim(),
            Type,
            balance,
            Currency.Trim().ToUpperInvariant(),
            existing?.CreatedDate ?? DateTimeOffset.UtcNow,
            IsArchived: existing?.IsArchived ?? false,
            CreditCardDetails: creditCardDetails,
            IncludeInDashboardTotals: IncludeInDashboardTotals,
            AccountNumber: string.IsNullOrWhiteSpace(AccountNumberText) ? null : AccountNumberText.Trim(),
            CardLastFourDigits: string.IsNullOrWhiteSpace(CardLastFourDigitsText) ? null : CardLastFourDigitsText.Trim(),
            PlanningValue: IsGoalType ? goalTarget : existing?.PlanningValue,
            IsFavorite: existing?.IsFavorite ?? false);

        await accountRepository.SaveAsync(account);

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() => IsOpen = false);
        await onSaved();
    }

    private static decimal? ParseOrNull(string text)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
