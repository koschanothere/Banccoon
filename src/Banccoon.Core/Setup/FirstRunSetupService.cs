using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.Core.Setup;

public interface IFirstRunSetupService
{
    // True for a brand-new install: setup not finished yet and nothing created (no accounts,
    // categories or transactions). An existing install upgrading to the version that added setup
    // has data, so it never sees it.
    Task<bool> IsSetupNeededAsync(CancellationToken cancellationToken = default);

    Task CompleteAsync(FirstRunSetupRequest request, CancellationToken cancellationToken = default);

    // Setup left some other way (a backup was restored from it): don't show it again.
    Task MarkCompletedAsync(CancellationToken cancellationToken = default);
}

public sealed class FirstRunSetupService : IFirstRunSetupService
{
    private readonly ISettingsRepository settingsRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;

    public FirstRunSetupService(
        ISettingsRepository settingsRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository)
    {
        this.settingsRepository = settingsRepository;
        this.categoryRepository = categoryRepository;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
    }

    public async Task<bool> IsSetupNeededAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        if (settings.FirstRunCompleted)
        {
            return false;
        }

        return (await accountRepository.GetAllAsync(cancellationToken)).Count == 0
            && (await categoryRepository.GetAllAsync(cancellationToken)).Count == 0
            && await transactionRepository.GetEarliestDateAsync(cancellationToken) is null;
    }

    // Categories whose name already exists (ignoring case) are reused rather than duplicated, so
    // running setup again (the dev button in Settings) only adds what's missing and never moves
    // or recolors an existing category. Settings not on the setup screen keep their values; the
    // date format is always day/month/year (decided 2026-09-28).
    public async Task CompleteAsync(FirstRunSetupRequest request, CancellationToken cancellationToken = default)
    {
        var existing = (await categoryRepository.GetAllAsync(cancellationToken)).ToList();
        Guid? fallbackCategoryId = null;
        foreach (var parent in request.Categories)
        {
            var parentId = await EnsureCategoryAsync(parent, parentId: null, existing, cancellationToken);
            if (parent.IsFallback)
            {
                fallbackCategoryId = parentId;
            }

            // A child can only go under a parent that is top-level (an existing same-named child
            // elsewhere would be reused as the "parent" - then its children stay top-level).
            var parentIsTopLevel = existing.Single(category => category.Id == parentId).ParentCategoryId is null;
            foreach (var child in parent.Children)
            {
                await EnsureCategoryAsync(child, parentIsTopLevel ? parentId : null, existing, cancellationToken);
            }
        }

        var settings = await settingsRepository.GetAsync(cancellationToken);
        await settingsRepository.SaveAsync(settings with
        {
            DisplayLanguage = request.Language,
            DefaultCurrency = request.Currency.Trim().ToUpperInvariant(),
            ThemeMode = request.ThemeMode,
            DateDisplayFormat = DateDisplayFormat.DayMonthYear,
            AutoBackupEnabled = request.AutoBackupEnabled,
            AutoBackupFrequencyDays = Math.Max(1, request.AutoBackupFrequencyDays),
            FreeToSpendWindowMode = request.FreeToSpendWindowMode,
            PreferredParserIds = string.Join(",", request.PreferredParserIds),
            FallbackCategoryId = fallbackCategoryId ?? settings.FallbackCategoryId,
            FirstRunCompleted = true
        }, cancellationToken);
    }

    public async Task MarkCompletedAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        if (!settings.FirstRunCompleted)
        {
            await settingsRepository.SaveAsync(settings with { FirstRunCompleted = true }, cancellationToken);
        }
    }

    private async Task<Guid> EnsureCategoryAsync(SetupCategory wanted, Guid? parentId, List<Category> existing, CancellationToken cancellationToken)
    {
        var name = wanted.Name.Trim();
        var match = existing.FirstOrDefault(category => string.Equals(category.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match.Id;
        }

        // The repository gives a child its parent's color (HierarchicalCategoryRepository);
        // this sets it too, for callers holding the plain repository.
        var color = parentId is { } id ? existing.Single(category => category.Id == id).Color : wanted.Color;
        var category = new Category(Guid.NewGuid(), name, wanted.Type, color, parentId);
        await categoryRepository.SaveAsync(category, cancellationToken);
        existing.Add(category);
        return category.Id;
    }
}
