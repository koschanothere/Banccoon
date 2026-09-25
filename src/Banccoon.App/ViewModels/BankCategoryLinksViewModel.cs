using System.Collections.ObjectModel;
using Banccoon.App.Diagnostics;
using Banccoon.App.Localization;
using Banccoon.Core.Categories;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// Settings -> Bank categories (composed into SettingsViewModel as BankCategories, shown above the
// learned rules): every category a bank's statements have shown so far and the app category it
// fills in on import. Only banks whose statements have been imported appear; with more than one, a
// picker chooses the bank. Changes apply to future imports - past transactions keep their category.
public sealed class BankCategoryLinksViewModel : ViewModelBase
{
    private readonly IBankCategoryService bankCategoryService;
    private readonly ICategoryRepository categoryRepository;
    private readonly IStatementParserRegistry parserRegistry;
    private BankOptionViewModel? selectedBank;
    private CategoryTree categoryTree = CategoryTree.Empty;

    // True while a new category is being put into the shared option list (see AddOption): pickers
    // knocked off their pick by the insert must not save that as a new link.
    private bool isInsertingOption;

    public BankCategoryLinksViewModel(
        IBankCategoryService bankCategoryService,
        ICategoryRepository categoryRepository,
        IStatementParserRegistry parserRegistry)
    {
        this.bankCategoryService = bankCategoryService;
        this.categoryRepository = categoryRepository;
        this.parserRegistry = parserRegistry;
        Banks = [];
        Rows = [];
        CategoryOptions = [];
    }

    public ObservableCollection<BankOptionViewModel> Banks { get; }

    public ObservableCollection<BankCategoryLinkRowViewModel> Rows { get; }

    // "Not linked", then every category by name, then "+ New category".
    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    public bool HasBanks => Banks.Count > 0;

    public bool HasMultipleBanks => Banks.Count > 1;

    public bool HasSingleBank => Banks.Count == 1;

    public string SingleBankName => Banks.Count == 1 ? Banks[0].Name : string.Empty;

    public BankOptionViewModel? SelectedBank
    {
        get => selectedBank;
        set
        {
            if (SetProperty(ref selectedBank, value) && value is not null)
            {
                _ = LoadRowsAsync(value);
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var parserIds = await bankCategoryService.GetParsersWithCategoriesAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var names = parserRegistry.AvailableParsers.ToDictionary(parser => parser.Id, parser => parser.Name);
        var banks = parserIds
            .Select(id => new BankOptionViewModel(id, names.TryGetValue(id, out var name) ? name : id))
            .OrderBy(bank => bank.Name, StringComparer.CurrentCulture)
            .ToList();

        BankOptionViewModel? bankToShow = null;
        await RunOnMainThreadAsync(() =>
        {
            CategoryOptions.Clear();
            CategoryOptions.Add(CategoryOptionViewModel.NotLinked());
            // Parents only: a parent's children are offered next to it (SubcategoryPickerViewModel).
            categoryTree = new CategoryTree(categories);
            foreach (var category in categoryTree.TopLevel)
            {
                CategoryOptions.Add(CategoryOptionViewModel.ForCategory(category));
            }

            CategoryOptions.Add(CategoryOptionViewModel.CreateNewSentinel());

            var previous = selectedBank?.ParserId;
            Banks.Clear();
            foreach (var bank in banks)
            {
                Banks.Add(bank);
            }

            bankToShow = Banks.FirstOrDefault(bank => bank.ParserId == previous) ?? Banks.FirstOrDefault();
            selectedBank = bankToShow;
            OnPropertyChanged(nameof(SelectedBank));
            OnPropertyChanged(nameof(HasBanks));
            OnPropertyChanged(nameof(HasMultipleBanks));
            OnPropertyChanged(nameof(HasSingleBank));
            OnPropertyChanged(nameof(SingleBankName));
            Rows.Clear();
        });

        if (bankToShow is not null)
        {
            await LoadRowsAsync(bankToShow, cancellationToken);
        }
    }

    private async Task LoadRowsAsync(BankOptionViewModel bank, CancellationToken cancellationToken = default)
    {
        var links = await bankCategoryService.GetLinksAsync(bank.ParserId, cancellationToken);
        await RunOnMainThreadAsync(() =>
        {
            if (selectedBank != bank)
            {
                return;
            }

            Rows.Clear();
            foreach (var link in links.OrderBy(link => link.BankCategory, StringComparer.CurrentCulture))
            {
                Rows.Add(new BankCategoryLinkRowViewModel(link, CategoryOptions, categoryTree, SaveLinkAsync, CreateCategoryAsync));
            }
        });
    }

    private async Task SaveLinkAsync(BankCategoryLinkRowViewModel row, Guid? categoryId)
    {
        if (isInsertingOption || selectedBank is not { } bank)
        {
            return;
        }

        try
        {
            await bankCategoryService.SaveLinksAsync(bank.ParserId, new Dictionary<string, Guid?> { [row.Name] = categoryId });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Saving a bank category link failed: {ex}");
        }
    }

    private async Task CreateCategoryAsync(BankCategoryLinkRowViewModel row)
    {
        CategoryOptionViewModel? selected = null;
        var name = string.Empty;
        CategoryOptionViewModel? existing = null;
        await RunOnMainThreadAsync(() =>
        {
            selected = row.Category;
            name = row.NewCategoryName.Trim();
            existing = CategoryOptions.FirstOrDefault(option => option.IsCategory && string.Equals(option.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        });

        if (selected?.IsCreateNew != true || name.Length == 0)
        {
            return;
        }

        var option = existing;
        if (option is null)
        {
            var (_, created) = await CategoryOptionsHelper.ResolveOrCreateAsync(selected, name, categoryRepository);
            option = created!;
            await RunOnMainThreadAsync(() => AddOption(option));
        }

        var chosen = option;
        await RunOnMainThreadAsync(() =>
        {
            row.NewCategoryName = string.Empty;
            row.Category = chosen;
        });
    }

    // UI-thread only. Inserts in name order (after "Not linked") and puts every row's pick back:
    // MAUI's Picker re-reads the item at its old index after an insert before it and writes that
    // back (see StatementImportCategoriesViewModel.AddOption) - here that write would also have
    // saved a wrong link, hence isInsertingOption.
    private void AddOption(CategoryOptionViewModel option)
    {
        var selections = Rows.Select(row => (Row: row, Category: row.Category)).ToList();
        isInsertingOption = true;
        try
        {
            var index = 1;
            while (index < CategoryOptions.Count
                && CategoryOptions[index].IsCategory
                && string.Compare(CategoryOptions[index].Name, option.Name, StringComparison.CurrentCulture) <= 0)
            {
                index++;
            }

            CategoryOptions.Insert(index, option);
            foreach (var (row, category) in selections)
            {
                row.RestoreCategory(category);
            }
        }
        finally
        {
            isInsertingOption = false;
        }
    }
}

// A bank in the Settings picker - one statement parser that has shown categories.
public sealed class BankOptionViewModel(string parserId, string name)
{
    public string ParserId { get; } = parserId;

    public string Name { get; } = name;

    public override string ToString() => Name;
}
