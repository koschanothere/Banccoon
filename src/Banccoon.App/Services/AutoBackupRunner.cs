using Banccoon.Core.ImportExport;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.Services;

// Called once from DashboardViewModel.InitializeAsync (the app's default first tab) - there's no
// background/daemon scheduling in this desktop app, so "automatic" means "checked whenever the
// app is next opened," not a fixed wall-clock trigger.
public sealed class AutoBackupRunner : IAutoBackupRunner
{
    private readonly IBackupService backupService;
    private readonly ISettingsRepository settingsRepository;

    public AutoBackupRunner(IBackupService backupService, ISettingsRepository settingsRepository)
    {
        this.backupService = backupService;
        this.settingsRepository = settingsRepository;
    }

    public async Task RunIfDueAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.AutoBackupEnabled)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (settings.LastAutoBackupAt is { } lastRun && (now - lastRun).TotalDays < settings.AutoBackupFrequencyDays)
        {
            return;
        }

        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Banccoon",
                "AutoBackups");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, $"banccoon-autobackup-{now:yyyyMMdd-HHmmss}.json");

            await backupService.CreateBackupAsync(filePath, cancellationToken);

            var existingBackups = Directory.GetFiles(folder, "banccoon-autobackup-*.json")
                .OrderByDescending(path => path)
                .ToList();
            foreach (var staleBackup in existingBackups.Skip(settings.AutoBackupRetentionCount))
            {
                File.Delete(staleBackup);
            }

            await settingsRepository.SaveAsync(settings with { LastAutoBackupAt = now }, cancellationToken);
        }
        catch
        {
            // A missed automatic backup isn't worth surfacing as an error interrupting startup -
            // manual export is always available, and this will simply retry next launch.
        }
    }
}
