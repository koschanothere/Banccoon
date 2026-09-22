using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Abstractions;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AccountsViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private readonly ISettingsRepository settingsRepository;

    private string currency = "EUR";
    private Guid? primaryAccountId;
    private AccountType? pendingAddAccountType;
    private bool isLoading;
    private bool showArchived;
    private AccountRowViewModel? detailAccount;
    private bool isDetailOpen;

    public AccountsViewModel(
        IAccountRepository accountRepository,
        ISettingsRepository settingsRepository,
        ICreditCardForecastService creditCardForecastService,
        IDateProvider dateProvider)
    {
        this.accountRepository = accountRepository;
        this.settingsRepository = settingsRepository;

        Form = new AccountFormViewModel(accountRepository, () => InitializeAsync());
        CardDetails = new CreditCardDetailsViewModel(creditCardForecastService, dateProvider);

        Accounts = [];
        ToggleFavoriteCommand = new RelayCommand<Guid>(id => _ = ToggleFavoriteAsync(id));
        ToggleShowArchivedCommand = new RelayCommand(() => ShowArchived = !ShowArchived);
        OpenAddAccountCommand = new RelayCommand(() => Form.OpenForCreate(currency));

        CloseDetailCommand = new RelayCommand(CloseDetail);
        DetailEditCommand = new RelayCommand(() =>
        {
            var row = DetailAccount;
            CloseDetail();
            row?.EditCommand.Execute(null);
        });
        DetailArchiveCommand = new RelayCommand(() =>
        {
            var row = DetailAccount;
            CloseDetail();
            row?.ArchiveCommand.Execute(null);
        });
        DetailUnarchiveCommand = new RelayCommand(() =>
        {
            var row = DetailAccount;
            CloseDetail();
            row?.UnarchiveCommand.Execute(null);
        });
        DetailSetPrimaryCommand = new RelayCommand(() =>
        {
            var row = DetailAccount;
            CloseDetail();
            row?.SetPrimaryCommand.Execute(null);
        });
        DetailOpenCardDetailsCommand = new RelayCommand(() =>
        {
            var row = DetailAccount;
            CloseDetail();
            row?.OpenCardDetailsCommand.Execute(null);
        });
        DetailViewTransactionsCommand = new RelayCommand(() =>
        {
            var accountId = DetailAccount?.Id;
            CloseDetail();
            if (accountId is { } id)
            {
                _ = RaiseViewTransactionsRequestedAsync(id);
            }
        });
    }

    public AccountFormViewModel Form { get; }

    public CreditCardDetailsViewModel CardDetails { get; }

    // The Accounts row's own detail-card action buttons need Shell navigation for "view
    // transactions," which ViewModels in this app don't perform directly (see
    // StatementImportPage.xaml.cs's OnCloseClicked for the established convention) - surfaced as
    // an event for AccountsPage's code-behind to act on instead.
    public event Func<Guid, Task>? ViewTransactionsRequested;

    public AccountRowViewModel? DetailAccount
    {
        get => detailAccount;
        private set => SetProperty(ref detailAccount, value);
    }

    public bool IsDetailOpen
    {
        get => isDetailOpen;
        private set => SetProperty(ref isDetailOpen, value);
    }

    public ICommand CloseDetailCommand { get; }

    public ICommand DetailEditCommand { get; }

    public ICommand DetailArchiveCommand { get; }

    public ICommand DetailUnarchiveCommand { get; }

    public ICommand DetailSetPrimaryCommand { get; }

    public ICommand DetailOpenCardDetailsCommand { get; }

    public ICommand DetailViewTransactionsCommand { get; }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool ShowArchived
    {
        get => showArchived;
        private set
        {
            if (SetProperty(ref showArchived, value))
            {
                OnPropertyChanged(nameof(ShowArchivedToggleText));
                _ = InitializeAsync();
            }
        }
    }

    public string ShowArchivedToggleText => ShowArchived
        ? Translator.Get("Accounts_ShowActiveToggle")
        : Translator.Get("Accounts_ShowArchivedToggle");

    public ObservableCollection<AccountRowViewModel> Accounts { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ToggleShowArchivedCommand { get; }

    public ICommand OpenAddAccountCommand { get; }

    // Set when navigated here from the Dashboard's "+ Add goal" (see AccountsPage.ApplyQueryAttributes,
    // which Shell calls before OnAppearing -> InitializeAsync). Consumed once, at the end of the
    // next InitializeAsync, so it opens the add form over a freshly loaded list.
    public void SetPendingAddAccountType(AccountType type)
    {
        pendingAddAccountType = type;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var settings = await settingsRepository.GetAsync(cancellationToken);
            currency = settings.DefaultCurrency;
            primaryAccountId = settings.PrimaryAccountId;
            PrivacyMode.IsEnabled = settings.PrivacyModeEnabled;

            // Already ordered by SortOrder then Name (see SqliteAccountRepository) - re-sorting
            // here by name would silently undo manual reordering.
            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            var visible = accounts
                .Where(account => account.IsArchived == ShowArchived)
                .ToList();

            // Mutates a collection bound to live UI - must run on the UI thread, which the awaits
            // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
            await RunOnMainThreadAsync(() =>
            {
                Accounts.Clear();
                for (var index = 0; index < visible.Count; index++)
                {
                    var account = visible[index];
                    Accounts.Add(new AccountRowViewModel(
                        account,
                        account.Id == primaryAccountId,
                        canMoveUp: index > 0,
                        canMoveDown: index < visible.Count - 1,
                        ToggleFavoriteCommand,
                        onEdit: Form.OpenForEdit,
                        onArchive: ArchiveAsync,
                        onUnarchive: UnarchiveAsync,
                        onSetPrimary: SetPrimaryAsync,
                        onOpenCardDetails: CardDetails.Open,
                        onMoveUp: id => MoveAsync(id, -1),
                        onMoveDown: id => MoveAsync(id, 1),
                        onOpenDetail: OpenDetail));
                }
            });
        }
        finally
        {
            // Touches UI-bound state after an await that may have resumed off the UI thread (see
            // ViewModelBase.RunOnMainThreadAsync).
            await RunOnMainThreadAsync(() => IsLoading = false);
        }

        if (pendingAddAccountType is { } addType)
        {
            pendingAddAccountType = null;
            await RunOnMainThreadAsync(() => Form.OpenForCreate(currency, addType));
        }
    }

    private async Task ToggleFavoriteAsync(Guid accountId)
    {
        var row = Accounts.FirstOrDefault(account => account.Id == accountId);
        if (row is null)
        {
            return;
        }

        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            return;
        }

        var updated = account with { IsFavorite = !account.IsFavorite };
        await accountRepository.SaveAsync(updated);

        // A scalar property setter, but still touches UI-bound state after an await that may have
        // resumed off the UI thread - same rule as collection mutations (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() => row.IsFavorite = updated.IsFavorite);
    }

    private async Task ArchiveAsync(Guid accountId)
    {
        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            return;
        }

        await accountRepository.SaveAsync(account with { IsArchived = true });
        await InitializeAsync();
    }

    private async Task UnarchiveAsync(Guid accountId)
    {
        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            return;
        }

        await accountRepository.SaveAsync(account with { IsArchived = false });
        await InitializeAsync();
    }

    private async Task SetPrimaryAsync(Guid accountId)
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { PrimaryAccountId = accountId });
        await InitializeAsync();
    }

    private async Task MoveAsync(Guid accountId, int direction)
    {
        var order = Accounts.Select(row => row.Id).ToList();
        var index = order.IndexOf(accountId);
        var targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= order.Count)
        {
            return;
        }

        (order[index], order[targetIndex]) = (order[targetIndex], order[index]);

        // Renumbers every currently-visible account to its new position rather than swapping two
        // raw SortOrder values, since every account shares the same default (0) until the first
        // reorder - a plain swap would leave the rest of the list in an undefined relative order.
        for (var position = 0; position < order.Count; position++)
        {
            var account = await accountRepository.GetByIdAsync(order[position]);
            if (account is null || account.SortOrder == position)
            {
                continue;
            }

            await accountRepository.SaveAsync(account with { SortOrder = position });
        }

        await InitializeAsync();
    }

    private void OpenDetail(AccountRowViewModel row)
    {
        DetailAccount = row;
        IsDetailOpen = true;
    }

    private void CloseDetail()
    {
        IsDetailOpen = false;
    }

    private Task RaiseViewTransactionsRequestedAsync(Guid accountId)
    {
        return ViewTransactionsRequested?.Invoke(accountId) ?? Task.CompletedTask;
    }
}
