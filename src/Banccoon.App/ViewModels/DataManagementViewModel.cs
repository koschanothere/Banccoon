using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace Banccoon.App.ViewModels;

public sealed class DataManagementViewModel : ViewModelBase
{
    private readonly IBackupService backupService;
    private readonly ILocalDataResetService localDataResetService;
    private readonly ISettingsRepository settingsRepository;
    private readonly IDatabasePathProvider databasePathProvider;

    private bool isBusy;
    private string exportStatusText = string.Empty;
    private string restoreStatusText = string.Empty;
    private string? pendingRestoreFilePath;
    private bool hasPendingRestore;
    private string pendingRestoreFileName = string.Empty;
    private bool pendingRestoreIsValid;
    private string pendingRestoreSummaryText = string.Empty;
    private bool isConfirmingDeleteAll;
    private string deleteAllStatusText = string.Empty;
    private bool autoBackupEnabled;
    private string autoBackupFrequencyDaysText = "30";
    private string autoBackupRetentionCountText = "5";
    private string autoBackupStatusText = string.Empty;
    private string lastAutoBackupText = string.Format(Translator.Get("Settings_LastAutomaticBackupFormat"), Translator.Get("Settings_Never"));
    private string diagnosticsStatusText = string.Empty;

    public DataManagementViewModel(
        IBackupService backupService,
        ILocalDataResetService localDataResetService,
        ISettingsRepository settingsRepository,
        IDatabasePathProvider databasePathProvider)
    {
        this.backupService = backupService;
        this.localDataResetService = localDataResetService;
        this.settingsRepository = settingsRepository;
        this.databasePathProvider = databasePathProvider;

        ExportCommand = new RelayCommand(() => _ = ExportAsync());
        PickRestoreFileCommand = new RelayCommand(() => _ = PickRestoreFileAsync());
        CancelRestoreCommand = new RelayCommand(() => _ = CancelRestoreAsync());
        ConfirmMergeCommand = new RelayCommand(() => _ = RestoreAsync(ImportMode.Merge));
        ConfirmReplaceCommand = new RelayCommand(() => _ = RestoreAsync(ImportMode.Replace));
        RequestDeleteAllCommand = new RelayCommand(() => IsConfirmingDeleteAll = true);
        CancelDeleteAllCommand = new RelayCommand(() => IsConfirmingDeleteAll = false);
        ConfirmDeleteAllCommand = new RelayCommand(() => _ = DeleteAllAsync());
        SaveAutoBackupSettingsCommand = new RelayCommand(() => _ = SaveAutoBackupSettingsAsync());
        OpenDiagnosticsLogCommand = new RelayCommand(OpenDiagnosticsLog);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string ExportStatusText
    {
        get => exportStatusText;
        private set => SetProperty(ref exportStatusText, value);
    }

    public string RestoreStatusText
    {
        get => restoreStatusText;
        private set => SetProperty(ref restoreStatusText, value);
    }

    public bool HasPendingRestore
    {
        get => hasPendingRestore;
        private set => SetProperty(ref hasPendingRestore, value);
    }

    public string PendingRestoreFileName
    {
        get => pendingRestoreFileName;
        private set => SetProperty(ref pendingRestoreFileName, value);
    }

    public bool PendingRestoreIsValid
    {
        get => pendingRestoreIsValid;
        private set => SetProperty(ref pendingRestoreIsValid, value);
    }

    public string PendingRestoreSummaryText
    {
        get => pendingRestoreSummaryText;
        private set => SetProperty(ref pendingRestoreSummaryText, value);
    }

    public bool IsConfirmingDeleteAll
    {
        get => isConfirmingDeleteAll;
        private set => SetProperty(ref isConfirmingDeleteAll, value);
    }

    public string DeleteAllStatusText
    {
        get => deleteAllStatusText;
        private set => SetProperty(ref deleteAllStatusText, value);
    }

    public bool AutoBackupEnabled
    {
        get => autoBackupEnabled;
        set => SetProperty(ref autoBackupEnabled, value);
    }

    public string AutoBackupFrequencyDaysText
    {
        get => autoBackupFrequencyDaysText;
        set => SetProperty(ref autoBackupFrequencyDaysText, value);
    }

    public string AutoBackupRetentionCountText
    {
        get => autoBackupRetentionCountText;
        set => SetProperty(ref autoBackupRetentionCountText, value);
    }

    public string AutoBackupStatusText
    {
        get => autoBackupStatusText;
        private set => SetProperty(ref autoBackupStatusText, value);
    }

    public string LastAutoBackupText
    {
        get => lastAutoBackupText;
        private set => SetProperty(ref lastAutoBackupText, value);
    }

    public string DiagnosticsStatusText
    {
        get => diagnosticsStatusText;
        private set => SetProperty(ref diagnosticsStatusText, value);
    }

    public string AppVersionText { get; } = $"Banccoon {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    public ICommand SaveAutoBackupSettingsCommand { get; }

    public ICommand OpenDiagnosticsLogCommand { get; }

    public ICommand OpenDataFolderCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand PickRestoreFileCommand { get; }

    public ICommand CancelRestoreCommand { get; }

    public ICommand ConfirmMergeCommand { get; }

    public ICommand ConfirmReplaceCommand { get; }

    public ICommand RequestDeleteAllCommand { get; }

    public ICommand CancelDeleteAllCommand { get; }

    public ICommand ConfirmDeleteAllCommand { get; }

    private async Task ExportAsync()
    {
        await RunOnMainThreadAsync(() => IsBusy = true);
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Banccoon",
                "Backups");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, $"banccoon-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            await backupService.CreateBackupAsync(filePath);

            await RunOnMainThreadAsync(() => ExportStatusText = string.Format(Translator.Get("Settings_SavedToFormat"), filePath));
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => ExportStatusText = string.Format(Translator.Get("Settings_ExportFailedFormat"), ex.Message));
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private async Task PickRestoreFileAsync()
    {
        FileResult? result;
        try
        {
            result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Translator.Get("Settings_ChooseBackupFilePickerTitle"),
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } }
                })
            });
        }
        catch (Exception)
        {
            await RunOnMainThreadAsync(() => RestoreStatusText = Translator.Get("StatementImport_CouldNotOpenFilePicker"));
            return;
        }

        if (result is null)
        {
            return;
        }

        var pickedFilePath = result.FullPath;
        var pickedFileName = result.FileName;

        await RunOnMainThreadAsync(() =>
        {
            IsBusy = true;
            RestoreStatusText = string.Empty;
        });

        try
        {
            var validation = await backupService.RestoreBackupAsync(pickedFilePath, ImportMode.ValidateOnly);

            await RunOnMainThreadAsync(() =>
            {
                pendingRestoreFilePath = pickedFilePath;
                PendingRestoreFileName = pickedFileName;
                PendingRestoreIsValid = validation.Validation.IsValid;
                PendingRestoreSummaryText = validation.Validation.IsValid
                    ? Translator.Get("Settings_BackupFileValid")
                    : string.Join(" ", validation.Validation.Errors);
                HasPendingRestore = true;
            });
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => RestoreStatusText = string.Format(Translator.Get("Settings_CouldNotReadFileFormat"), ex.Message));
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private Task CancelRestoreAsync()
    {
        return RunOnMainThreadAsync(() =>
        {
            pendingRestoreFilePath = null;
            HasPendingRestore = false;
            PendingRestoreFileName = string.Empty;
            PendingRestoreIsValid = false;
            PendingRestoreSummaryText = string.Empty;
        });
    }

    private async Task RestoreAsync(ImportMode mode)
    {
        if (pendingRestoreFilePath is not { } filePath || !PendingRestoreIsValid)
        {
            return;
        }

        await RunOnMainThreadAsync(() => IsBusy = true);
        try
        {
            var result = await backupService.RestoreBackupAsync(filePath, mode);

            await RunOnMainThreadAsync(() =>
            {
                RestoreStatusText = string.Format(
                    Translator.Get("Settings_RestoredSummaryFormat"),
                    Translator.GetPlural("Settings_RestoredAccountsCount", result.AccountsImported),
                    Translator.GetPlural("Settings_RestoredTransactionsCount", result.TransactionsImported),
                    Translator.GetPlural("DataManagement_CategoryCount", result.CategoriesImported),
                    Translator.GetPlural("Settings_RestoredScheduledRulesCount", result.ScheduledTransactionsImported),
                    Translator.GetPlural("Settings_RestoredGoalsCount", result.SavingsGoalsImported));
                pendingRestoreFilePath = null;
                HasPendingRestore = false;
                PendingRestoreFileName = string.Empty;
                PendingRestoreIsValid = false;
                PendingRestoreSummaryText = string.Empty;
            });
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => RestoreStatusText = string.Format(Translator.Get("Settings_RestoreFailedFormat"), ex.Message));
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private async Task DeleteAllAsync()
    {
        await RunOnMainThreadAsync(() => IsBusy = true);
        try
        {
            await localDataResetService.ResetAllAsync();

            await RunOnMainThreadAsync(() =>
            {
                DeleteAllStatusText = Translator.Get("Settings_AllDataDeleted");
                IsConfirmingDeleteAll = false;
            });
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => DeleteAllStatusText = string.Format(Translator.Get("Settings_DeleteFailedFormat"), ex.Message));
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }

    public Task InitializeAsync(AppSettings settings)
    {
        AutoBackupEnabled = settings.AutoBackupEnabled;
        AutoBackupFrequencyDaysText = settings.AutoBackupFrequencyDays.ToString(CultureInfo.InvariantCulture);
        AutoBackupRetentionCountText = settings.AutoBackupRetentionCount.ToString(CultureInfo.InvariantCulture);
        LastAutoBackupText = string.Format(
            Translator.Get("Settings_LastAutomaticBackupFormat"),
            settings.LastAutoBackupAt is { } lastAutoBackupAt
                ? lastAutoBackupAt.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
                : Translator.Get("Settings_Never"));
        AutoBackupStatusText = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAutoBackupSettingsAsync()
    {
        if (!int.TryParse(AutoBackupFrequencyDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequencyDays) || frequencyDays < 1)
        {
            AutoBackupStatusText = Translator.Get("Settings_FrequencyMustBeAtLeast1Day");
            return;
        }

        if (!int.TryParse(AutoBackupRetentionCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retentionCount) || retentionCount < 1)
        {
            AutoBackupStatusText = Translator.Get("Settings_RetentionMustBeAtLeast1");
            return;
        }

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with
        {
            AutoBackupEnabled = AutoBackupEnabled,
            AutoBackupFrequencyDays = frequencyDays,
            AutoBackupRetentionCount = retentionCount
        });

        await RunOnMainThreadAsync(() => AutoBackupStatusText = Translator.Get("Common_Saved"));
    }

    private void OpenDiagnosticsLog()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Banccoon",
            "diagnostics.log");

        if (!File.Exists(logPath))
        {
            DiagnosticsStatusText = Translator.Get("Settings_NoDiagnosticsLogYet");
            return;
        }

        DiagnosticsStatusText = string.Empty;
        Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
    }

    private void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(databasePathProvider.DatabasePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
}
