using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

public sealed class StatementAccountViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;

    private NamedOptionViewModel? selectedAccount;
    private bool isCreatingNewAccount;
    private string newAccountName = string.Empty;
    private AccountType newAccountType = AccountType.DebitCard;
    private string newAccountStartingBalanceText = "0";
    private string statusText = string.Empty;

    public StatementAccountViewModel(IAccountRepository accountRepository)
    {
        this.accountRepository = accountRepository;
        AccountOptions = [];
        AccountTypes = Enum.GetValues<AccountType>();

        UseExistingAccountCommand = new RelayCommand(() => SetCreatingNewAccount(false));
        UseNewAccountCommand = new RelayCommand(() => SetCreatingNewAccount(true));
    }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public NamedOptionViewModel? SelectedAccount
    {
        get => selectedAccount;
        set
        {
            if (SetProperty(ref selectedAccount, value))
            {
                OnPropertyChanged(nameof(CanContinue));
            }
        }
    }

    public bool IsCreatingNewAccount
    {
        get => isCreatingNewAccount;
        private set => SetProperty(ref isCreatingNewAccount, value);
    }

    public string NewAccountName
    {
        get => newAccountName;
        set
        {
            if (SetProperty(ref newAccountName, value))
            {
                OnPropertyChanged(nameof(CanContinue));
            }
        }
    }

    public IReadOnlyList<AccountType> AccountTypes { get; }

    public AccountType NewAccountType
    {
        get => newAccountType;
        set => SetProperty(ref newAccountType, value);
    }

    public string NewAccountStartingBalanceText
    {
        get => newAccountStartingBalanceText;
        set => SetProperty(ref newAccountStartingBalanceText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public void SetStatus(string message) => StatusText = message;

    public bool CanContinue => IsCreatingNewAccount
        ? !string.IsNullOrWhiteSpace(NewAccountName)
        : SelectedAccount is not null;

    public ICommand UseExistingAccountCommand { get; }

    public ICommand UseNewAccountCommand { get; }

    public async Task LoadAsync(ParsedStatement statement, string suggestedName, CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var nonArchived = accounts.Where(account => !account.IsArchived).OrderBy(account => account.Name).ToList();

        // Mutates a collection bound to a live Picker - must run on the UI thread, which the await
        // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            AccountOptions.Clear();
            foreach (var account in nonArchived)
            {
                AccountOptions.Add(new NamedOptionViewModel(account.Id, account.Name));
            }

            var matchedAccount = nonArchived.FirstOrDefault(account =>
                (!string.IsNullOrWhiteSpace(statement.CardLastFourDigits) && account.CardLastFourDigits == statement.CardLastFourDigits)
                || (!string.IsNullOrWhiteSpace(statement.AccountNumber) && account.AccountNumber == statement.AccountNumber));

            IsCreatingNewAccount = matchedAccount is null && AccountOptions.Count == 0;
            SelectedAccount = matchedAccount is null
                ? AccountOptions.FirstOrDefault()
                : AccountOptions.FirstOrDefault(option => option.Id == matchedAccount.Id);

            NewAccountName = suggestedName;
            NewAccountStartingBalanceText = (statement.ClosingBalance ?? statement.OpeningBalance ?? 0m)
                .ToString(CultureInfo.InvariantCulture);
            StatusText = string.Empty;
            OnPropertyChanged(nameof(CanContinue));
        });
    }

    public async Task<Guid?> ResolveAccountIdAsync(ParsedStatement statement, string currency, CancellationToken cancellationToken = default)
    {
        if (IsCreatingNewAccount)
        {
            if (string.IsNullOrWhiteSpace(NewAccountName))
            {
                StatusText = "Name is required.";
                return null;
            }

            if (!decimal.TryParse(NewAccountStartingBalanceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var startingBalance))
            {
                StatusText = "Starting balance must be a number.";
                return null;
            }

            var account = new Account(
                Guid.NewGuid(),
                NewAccountName.Trim(),
                NewAccountType,
                startingBalance,
                currency,
                DateTimeOffset.UtcNow,
                AccountNumber: statement.AccountNumber,
                CardLastFourDigits: statement.CardLastFourDigits);

            await accountRepository.SaveAsync(account, cancellationToken);
            return account.Id;
        }

        if (SelectedAccount is null)
        {
            StatusText = "Choose an account.";
            return null;
        }

        return SelectedAccount.Id;
    }

    private void SetCreatingNewAccount(bool creatingNew)
    {
        IsCreatingNewAccount = creatingNew;
        OnPropertyChanged(nameof(CanContinue));
    }
}
