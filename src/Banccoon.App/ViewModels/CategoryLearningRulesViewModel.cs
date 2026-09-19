using System.Collections.ObjectModel;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// Surfaces what the app has auto-learned from statement-import corrections (recipient -> category/
// type/destination-account), with search and the ability to fix a rule that was learned wrong -
// previously the only option was Forget (delete) and let it re-learn from scratch on a future
// correction; now a rule can be corrected directly.
public sealed class CategoryLearningRulesViewModel : ViewModelBase
{
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAccountRepository accountRepository;

    private IReadOnlyList<CategoryLearningRule> allRules = [];
    private IReadOnlyDictionary<Guid, Category> categoriesById = new Dictionary<Guid, Category>();
    private IReadOnlyDictionary<Guid, Account> accountsById = new Dictionary<Guid, Account>();
    private string searchText = string.Empty;

    public CategoryLearningRulesViewModel(
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository)
    {
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
        this.categoryRepository = categoryRepository;
        this.accountRepository = accountRepository;

        Rows = [];
        CategoryOptions = [];
        AccountOptions = [];
    }

    public ObservableCollection<CategoryLearningRuleRowViewModel> Rows { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                RebuildRows();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        allRules = await categoryLearningRuleRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        categoriesById = categories.ToDictionary(category => category.Id);
        accountsById = accounts.ToDictionary(account => account.Id);

        // Mutates collections bound to live UI - must run on the UI thread, which the awaits
        // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            CategoryOptions.Clear();
            foreach (var category in categories.OrderBy(category => category.Name))
            {
                CategoryOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
            }

            AccountOptions.Clear();
            foreach (var account in accounts.Where(account => !account.IsArchived).OrderBy(account => account.Name))
            {
                AccountOptions.Add(new NamedOptionViewModel(account.Id, account.Name));
            }

            RebuildRows();
        });
    }

    private void RebuildRows()
    {
        Rows.Clear();

        var matching = string.IsNullOrWhiteSpace(SearchText)
            ? allRules
            : allRules.Where(rule =>
                rule.MatchText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (categoriesById.TryGetValue(rule.CategoryId, out var category) && category.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

        foreach (var rule in matching.OrderByDescending(rule => rule.UpdatedAt))
        {
            var categoryName = categoriesById.TryGetValue(rule.CategoryId, out var foundCategory) ? foundCategory.Name : "Unknown category";
            var destinationAccountName = rule.DestinationAccountId is { } destinationAccountId && accountsById.TryGetValue(destinationAccountId, out var destinationAccount)
                ? destinationAccount.Name
                : null;

            Rows.Add(new CategoryLearningRuleRowViewModel(
                rule,
                categoryName,
                destinationAccountName,
                CategoryOptions,
                AccountOptions,
                ForgetRuleAsync,
                SaveEditAsync));
        }
    }

    private async Task ForgetRuleAsync(Guid ruleId)
    {
        await categoryLearningRuleRepository.DeleteAsync(ruleId);
        await InitializeAsync();
    }

    private async Task SaveEditAsync(CategoryLearningRuleRowViewModel row)
    {
        var existing = allRules.FirstOrDefault(rule => rule.Id == row.Id);
        if (existing is null || string.IsNullOrWhiteSpace(row.EditMatchText) || row.EditCategory is null)
        {
            return;
        }

        if (row.EditType == TransactionType.Transfer && row.EditDestinationAccount is null)
        {
            return;
        }

        var normalizedMatchText = row.EditMatchText.Trim();
        var updated = existing with
        {
            MatchText = normalizedMatchText,
            NormalizedMatchText = new CategorySuggestionService().Normalize(normalizedMatchText),
            Type = row.EditType,
            CategoryId = row.EditCategory.Id,
            DestinationAccountId = row.EditType == TransactionType.Transfer ? row.EditDestinationAccount?.Id : null,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await categoryLearningRuleRepository.SaveAsync(updated);
        await InitializeAsync();
    }
}
