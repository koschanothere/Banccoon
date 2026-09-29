using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// Surfaces what the app has auto-learned from statement-import corrections (recipient -> category/
// type/destination-account), with search and the ability to fix a rule that was learned wrong -
// previously the only option was Forget (delete) and let it re-learn from scratch on a future
// correction; now a rule can be corrected directly.
//
// Shown RulesPerPage at a time (2026-09-25): years of imports can teach hundreds of rules, and
// drawing every one of them made Settings slow to open. Search covers every rule and starts again
// at the first page; editing or forgetting a rule stays on the current page.
public sealed class CategoryLearningRulesViewModel : ViewModelBase
{
    public const int RulesPerPage = 25;

    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAccountRepository accountRepository;

    private IReadOnlyList<CategoryLearningRule> allRules = [];
    private IReadOnlyDictionary<Guid, Category> categoriesById = new Dictionary<Guid, Category>();
    private CategoryTree categoryTree = CategoryTree.Empty;
    private IReadOnlyDictionary<Guid, Account> accountsById = new Dictionary<Guid, Account>();
    private string searchText = string.Empty;
    private int pageIndex;
    private int matchingCount;

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

        PreviousPageCommand = new RelayCommand(() => GoToPage(pageIndex - 1));
        NextPageCommand = new RelayCommand(() => GoToPage(pageIndex + 1));
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
                pageIndex = 0;
                RebuildRows();
            }
        }
    }

    public bool HasPages => matchingCount > RulesPerPage;

    public bool CanGoToPreviousPage => pageIndex > 0;

    public bool CanGoToNextPage => (pageIndex + 1) * RulesPerPage < matchingCount;

    // "26–50 of 312".
    public string PageText => matchingCount == 0
        ? string.Empty
        : string.Format(
            Translator.Get("Settings_LearnedRulesPageFormat"),
            pageIndex * RulesPerPage + 1,
            Math.Min((pageIndex + 1) * RulesPerPage, matchingCount),
            matchingCount);

    public ICommand PreviousPageCommand { get; }

    public ICommand NextPageCommand { get; }

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
            // Parents only; a rule's edit mode offers the chosen parent's children next to it.
            categoryTree = new CategoryTree(categories);
            CategoryOptions.Clear();
            foreach (var category in categoryTree.TopLevel)
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

    private void GoToPage(int page)
    {
        pageIndex = page;
        RebuildRows();
    }

    // UI-thread only.
    private void RebuildRows()
    {
        Rows.Clear();

        var matching = (string.IsNullOrWhiteSpace(SearchText)
            ? allRules
            : allRules.Where(rule =>
                rule.MatchText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || CategoryDisplay.PathName(categoryTree, rule.CategoryId).Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(rule => rule.UpdatedAt)
            .ToList();

        // Forgetting the last rule on the last page steps back a page rather than showing nothing.
        matchingCount = matching.Count;
        var lastPage = Math.Max(0, (matchingCount - 1) / RulesPerPage);
        pageIndex = Math.Clamp(pageIndex, 0, lastPage);
        OnPropertyChanged(nameof(HasPages));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        OnPropertyChanged(nameof(PageText));

        foreach (var rule in matching.Skip(pageIndex * RulesPerPage).Take(RulesPerPage))
        {
            var categoryName = categoriesById.ContainsKey(rule.CategoryId)
                ? CategoryDisplay.PathName(categoryTree, rule.CategoryId)
                : Translator.Get("Settings_UnknownCategory");
            var destinationAccountName = rule.DestinationAccountId is { } destinationAccountId && accountsById.TryGetValue(destinationAccountId, out var destinationAccount)
                ? destinationAccount.Name
                : null;

            Rows.Add(new CategoryLearningRuleRowViewModel(
                rule,
                categoryName,
                destinationAccountName,
                CategoryOptions,
                categoryTree,
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
        if (existing is null || string.IsNullOrWhiteSpace(row.EditMatchText) || row.EditCategoryId is not { } editCategoryId)
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
            CategoryId = editCategoryId,
            DestinationAccountId = row.EditType == TransactionType.Transfer ? row.EditDestinationAccount?.Id : null,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await categoryLearningRuleRepository.SaveAsync(updated);
        await InitializeAsync();
    }
}
