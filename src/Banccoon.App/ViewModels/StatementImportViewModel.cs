using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace Banccoon.App.ViewModels;

public enum StatementImportStep
{
    PickFile,
    ConfirmAccount,
    Review
}

public sealed class StatementImportViewModel : ViewModelBase
{
    private readonly IStatementImportService statementImportService;
    private readonly IStatementParserRegistry statementParserRegistry;
    private readonly ISettingsRepository settingsRepository;

    private string currency = "EUR";
    private StatementImportStep currentStep = StatementImportStep.PickFile;
    private bool isBusy;
    private string? filePath;
    private string fileName = string.Empty;
    private ParsedStatement? statement;
    private string previewStatusText = "Choose a bank statement file to begin.";

    public StatementImportViewModel(
        IStatementImportService statementImportService,
        IStatementParserRegistry statementParserRegistry,
        IStatementImportRepository statementImportRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository,
        ISettingsRepository settingsRepository)
    {
        this.statementImportService = statementImportService;
        this.statementParserRegistry = statementParserRegistry;
        this.settingsRepository = settingsRepository;

        Account = new StatementAccountViewModel(accountRepository);
        Review = new StatementImportReviewViewModel(statementImportService, statementImportRepository, categoryRepository);

        PickFileCommand = new RelayCommand(() => _ = PickFileAsync());
        ContinueFromPickCommand = new RelayCommand(() => _ = ContinueFromPickAsync());
        ContinueFromAccountCommand = new RelayCommand(() => _ = ContinueFromAccountAsync());
        BackToPickCommand = new RelayCommand(() => CurrentStep = StatementImportStep.PickFile);
    }

    public StatementAccountViewModel Account { get; }

    public StatementImportReviewViewModel Review { get; }

    public StatementImportStep CurrentStep
    {
        get => currentStep;
        private set
        {
            if (SetProperty(ref currentStep, value))
            {
                OnPropertyChanged(nameof(IsPickFileStep));
                OnPropertyChanged(nameof(IsConfirmAccountStep));
                OnPropertyChanged(nameof(IsReviewStep));
            }
        }
    }

    public bool IsPickFileStep => CurrentStep == StatementImportStep.PickFile;

    public bool IsConfirmAccountStep => CurrentStep == StatementImportStep.ConfirmAccount;

    public bool IsReviewStep => CurrentStep == StatementImportStep.Review;

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string FileName
    {
        get => fileName;
        private set => SetProperty(ref fileName, value);
    }

    public string PreviewStatusText
    {
        get => previewStatusText;
        private set => SetProperty(ref previewStatusText, value);
    }

    public bool HasStatement => statement is not null;

    public string ParserNameText => statement?.ParserName ?? string.Empty;

    public string PeriodText => statement is { PeriodStart: { } start, PeriodEnd: { } end }
        ? $"{start:dd/MM/yyyy} - {end:dd/MM/yyyy}"
        : "Unknown";

    public string RowCountText => statement is null ? string.Empty : $"{statement.Rows.Count} row(s)";

    public string DetectedBalanceText => statement?.ClosingBalance is { } balance
        ? MoneyFormat.Format(balance, currency)
        : "Not detected";

    public string DetectedAccountText => statement is null
        ? string.Empty
        : !string.IsNullOrWhiteSpace(statement.CardLastFourDigits)
            ? $"Card ending {statement.CardLastFourDigits}"
            : !string.IsNullOrWhiteSpace(statement.AccountNumber)
                ? AccountNumberFormat.Mask(statement.AccountNumber)
                : "Not detected";

    public bool CanContinueFromPick => statement is not null;

    public ICommand PickFileCommand { get; }

    public ICommand ContinueFromPickCommand { get; }

    public ICommand ContinueFromAccountCommand { get; }

    public ICommand BackToPickCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        currency = settings.DefaultCurrency;

        CurrentStep = StatementImportStep.PickFile;
        filePath = null;
        FileName = string.Empty;
        statement = null;
        PreviewStatusText = "Choose a bank statement file to begin.";
        OnPropertyChanged(nameof(HasStatement));
        OnPropertyChanged(nameof(CanContinueFromPick));
    }

    private async Task PickFileAsync()
    {
        var extensions = statementParserRegistry.AvailableParsers
            .SelectMany(descriptor => descriptor.SupportedFileExtensions)
            .Distinct()
            .ToArray();

        // Every mutation below runs through RunOnMainThreadAsync since each await in this method
        // (the file picker, PreviewAsync) may resume off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        FileResult? result;
        try
        {
            result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a bank statement file",
                FileTypes = extensions.Length == 0
                    ? null
                    : new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, extensions }
                    })
            });
        }
        catch (Exception)
        {
            await RunOnMainThreadAsync(() => PreviewStatusText = "Could not open the file picker.");
            return;
        }

        if (result is null)
        {
            return;
        }

        var pickedFilePath = result.FullPath;
        await RunOnMainThreadAsync(() =>
        {
            filePath = pickedFilePath;
            FileName = result.FileName;
            statement = null;
            OnPropertyChanged(nameof(HasStatement));
            OnPropertyChanged(nameof(CanContinueFromPick));
            IsBusy = true;
        });

        try
        {
            var preview = await statementImportService.PreviewAsync(pickedFilePath);
            await RunOnMainThreadAsync(() =>
            {
                statement = preview.Statement;
                PreviewStatusText = preview.Message;
            });
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }

        await RunOnMainThreadAsync(() =>
        {
            OnPropertyChanged(nameof(HasStatement));
            OnPropertyChanged(nameof(ParserNameText));
            OnPropertyChanged(nameof(PeriodText));
            OnPropertyChanged(nameof(RowCountText));
            OnPropertyChanged(nameof(DetectedBalanceText));
            OnPropertyChanged(nameof(DetectedAccountText));
            OnPropertyChanged(nameof(CanContinueFromPick));
        });
    }

    private async Task ContinueFromPickAsync()
    {
        if (statement is null)
        {
            return;
        }

        await Account.LoadAsync(statement, FileName);
        await RunOnMainThreadAsync(() => CurrentStep = StatementImportStep.ConfirmAccount);
    }

    private async Task ContinueFromAccountAsync()
    {
        if (statement is null || filePath is null)
        {
            return;
        }

        var accountId = await Account.ResolveAccountIdAsync(statement, currency);
        if (accountId is null)
        {
            return;
        }

        await RunOnMainThreadAsync(() => IsBusy = true);
        try
        {
            var result = await statementImportService.CreatePendingImportAsync(accountId.Value, filePath, statement);
            if (!result.ParserAvailable || result.Batch is null)
            {
                await RunOnMainThreadAsync(() => Account.SetStatus(result.Message));
                return;
            }

            await Review.LoadAsync(result.Batch.Id, currency);
            await RunOnMainThreadAsync(() => CurrentStep = StatementImportStep.Review);
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }
}
