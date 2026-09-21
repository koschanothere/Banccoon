using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
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
    private string lastAutoBackupText = "Never";
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

            await RunOnMainThreadAsync(() => ExportStatusText = $"Saved to {filePath}");
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => ExportStatusText = $"Export failed: {ex.Message}");
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
                PickerTitle = "Choose a Banccoon backup file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } }
                })
            });
        }
        catch (Exception)
        {
            await RunOnMainThreadAsync(() => RestoreStatusText = "Could not open the file picker.");
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
                    ? "This file looks valid and is ready to restore."
                    : string.Join(" ", validation.Validation.Errors);
                HasPendingRestore = true;
            });
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => RestoreStatusText = $"Could not read that file: {ex.Message}");
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
                RestoreStatusText = $"Restored {result.AccountsImported} account(s), {result.TransactionsImported} transaction(s), "
                    + $"{result.CategoriesImported} categor{(result.CategoriesImported == 1 ? "y" : "ies")}, "
                    + $"{result.ScheduledTransactionsImported} scheduled rule(s), {result.SavingsGoalsImported} goal(s).";
                pendingRestoreFilePath = null;
                HasPendingRestore = false;
                PendingRestoreFileName = string.Empty;
                PendingRestoreIsValid = false;
                PendingRestoreSummaryText = string.Empty;
            });
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => RestoreStatusText = $"Restore failed: {ex.Message}");
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
                DeleteAllStatusText = "All local data deleted.";
                IsConfirmingDeleteAll = false;
            });
        }
        catch (Exception ex)
        {
            await RunOnMainThreadAsync(() => DeleteAllStatusText = $"Delete failed: {ex.Message}");
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
        LastAutoBackupText = settings.LastAutoBackupAt is { } lastAutoBackupAt
            ? lastAutoBackupAt.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
            : "Never";
        AutoBackupStatusText = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAutoBackupSettingsAsync()
    {
        if (!int.TryParse(AutoBackupFrequencyDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequencyDays) || frequencyDays < 1)
        {
            AutoBackupStatusText = "Frequency must be a whole number of at least 1 day.";
            return;
        }

        if (!int.TryParse(AutoBackupRetentionCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retentionCount) || retentionCount < 1)
        {
            AutoBackupStatusText = "Retention count must be a whole number of at least 1.";
            return;
        }

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with
        {
            AutoBackupEnabled = AutoBackupEnabled,
            AutoBackupFrequencyDays = frequencyDays,
            AutoBackupRetentionCount = retentionCount
        });

        await RunOnMainThreadAsync(() => AutoBackupStatusText = "Saved.");
    }

    private void OpenDiagnosticsLog()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Banccoon",
            "diagnostics.log");

        if (!File.Exists(logPath))
        {
            DiagnosticsStatusText = "No diagnostics log yet - nothing has been recorded.";
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
