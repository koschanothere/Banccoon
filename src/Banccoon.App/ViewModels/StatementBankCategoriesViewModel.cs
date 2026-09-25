using System.Collections.ObjectModel;
using Banccoon.App.Localization;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// The import's linking step (composed into StatementImportViewModel as BankCategories): shown only
// when a statement contains bank categories its parser has never shown before, and only for those.
// A linked one fills in the category of every row carrying it from then on - unless a learned rule
// for that recipient says otherwise. Skipped and untouched ones are saved unlinked, so they're not
// asked about again; Settings -> Bank categories changes any of them later.
public sealed class StatementBankCategoriesViewModel : ViewModelBase
{
    private readonly IBankCategoryService bankCategoryService;
    private readonly ICategoryRepository categoryRepository;
    private string bankName = string.Empty;

    public StatementBankCategoriesViewModel(IBankCategoryService bankCategoryService, ICategoryRepository categoryRepository)
    {
        this.bankCategoryService = bankCategoryService;
        this.categoryRepository = categoryRepository;
        Rows = [];
        CategoryOptions = [];
    }

    public ObservableCollection<StatementBankCategoryRowViewModel> Rows { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    public string HintText => string.Format(Translator.Get("StatementImport_BankCategoriesHintFormat"), bankName);

    // Whether the statement has categories its bank hasn't shown before (and so the step is needed).
    public async Task<bool> LoadAsync(ParsedStatement statement)
    {
        var detected = await bankCategoryService.GetNewCategoriesAsync(statement);
        if (detected.Count == 0)
        {
            return false;
        }

        var categories = await categoryRepository.GetAllAsync();
        await RunOnMainThreadAsync(() =>
        {
            bankName = statement.ParserName;
            CategoryOptionsHelper.Repopulate(CategoryOptions, categories);
            Rows.Clear();
            foreach (var category in detected)
            {
                Rows.Add(new StatementBankCategoryRowViewModel(category, CategoryOptions));
            }

            OnPropertyChanged(nameof(HintText));
        });

        return true;
    }

    // Links what was linked (creating any category named with "+ New category" - once per name,
    // reusing an existing category of that name), and saves the rest unlinked.
    public async Task SaveAsync(string parserId)
    {
        List<(string Name, CategoryOptionViewModel? Category, string NewName, bool Skipped)> choices = [];
        Dictionary<string, Guid> knownByName = new(StringComparer.OrdinalIgnoreCase);
        await RunOnMainThreadAsync(() =>
        {
            choices = Rows.Select(row => (row.Name, row.Category, row.NewCategoryName, row.IsSkipped)).ToList();
            foreach (var option in CategoryOptions.Where(option => option.IsCategory))
            {
                knownByName.TryAdd(option.Name.Trim(), option.Id);
            }
        });

        var links = new Dictionary<string, Guid?>();
        foreach (var (name, category, newName, skipped) in choices)
        {
            Guid? categoryId = null;
            if (!skipped && category is { IsCategory: true })
            {
                categoryId = category.Id;
            }
            else if (!skipped && category is { IsCreateNew: true } && !string.IsNullOrWhiteSpace(newName))
            {
                if (!knownByName.TryGetValue(newName.Trim(), out var id))
                {
                    var (createdId, _) = await CategoryOptionsHelper.ResolveOrCreateAsync(category, newName, categoryRepository);
                    id = createdId!.Value;
                    knownByName[newName.Trim()] = id;
                }

                categoryId = id;
            }

            links[name] = categoryId;
        }

        await bankCategoryService.SaveLinksAsync(parserId, links);
    }
}
