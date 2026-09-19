using System.Collections.ObjectModel;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// Surfaces what the app has auto-learned from statement-import corrections (recipient -> category/
// type/destination-account), with a way to forget a rule that was learned wrong - previously the
// only way to fix a bad learned rule was to keep re-correcting it by hand on every future import.
public sealed class CategoryLearningRulesViewModel : ViewModelBase
{
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAccountRepository accountRepository;

    public CategoryLearningRulesViewModel(
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository)
    {
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
        this.categoryRepository = categoryRepository;
        this.accountRepository = accountRepository;

        Rows = [];
    }

    public ObservableCollection<CategoryLearningRuleRowViewModel> Rows { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var rules = await categoryLearningRuleRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(category => category.Id);
        var accountsById = accounts.ToDictionary(account => account.Id);

        // Mutates a collection bound to live UI - must run on the UI thread, which the awaits
        // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            Rows.Clear();
            foreach (var rule in rules.OrderByDescending(rule => rule.UpdatedAt))
            {
                var categoryName = categoriesById.TryGetValue(rule.CategoryId, out var category) ? category.Name : "Unknown category";
                var destinationAccountName = rule.DestinationAccountId is { } destinationAccountId && accountsById.TryGetValue(destinationAccountId, out var destinationAccount)
                    ? destinationAccount.Name
                    : null;

                Rows.Add(new CategoryLearningRuleRowViewModel(
                    rule.Id,
                    rule.MatchText,
                    rule.Type.ToString(),
                    categoryName,
                    destinationAccountName,
                    rule.MatchCount,
                    rule.UpdatedAt.ToString("dd MMM yyyy"),
                    ForgetRuleAsync));
            }
        });
    }

    private async Task ForgetRuleAsync(Guid ruleId)
    {
        await categoryLearningRuleRepository.DeleteAsync(ruleId);
        await InitializeAsync();
    }
}
