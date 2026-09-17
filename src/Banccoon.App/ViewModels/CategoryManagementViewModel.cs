using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Categories;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class CategoryManagementViewModel : ViewModelBase
{
    private readonly ICategoryRepository categoryRepository;
    private readonly ICategoryManagementService categoryManagementService;
    private readonly Func<Task> onChanged;

    private bool isOpen;
    private NamedOptionViewModel? mergeSource;
    private NamedOptionViewModel? mergeTarget;
    private string statusText = string.Empty;

    public CategoryManagementViewModel(
        ICategoryRepository categoryRepository,
        ICategoryManagementService categoryManagementService,
        Func<Task> onChanged)
    {
        this.categoryRepository = categoryRepository;
        this.categoryManagementService = categoryManagementService;
        this.onChanged = onChanged;

        Rows = [];
        MergeOptions = [];

        ToggleCommand = new RelayCommand(() => _ = ToggleAsync());
        CloseCommand = new RelayCommand(Close);
        MergeCommand = new RelayCommand(() => _ = MergeAsync());
    }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public NamedOptionViewModel? MergeSource
    {
        get => mergeSource;
        set => SetProperty(ref mergeSource, value);
    }

    public NamedOptionViewModel? MergeTarget
    {
        get => mergeTarget;
        set => SetProperty(ref mergeTarget, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ObservableCollection<CategoryManagementRowViewModel> Rows { get; }

    public ObservableCollection<NamedOptionViewModel> MergeOptions { get; }

    public ICommand ToggleCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand MergeCommand { get; }

    public void Close()
    {
        IsOpen = false;
    }

    public async Task OpenAsync()
    {
        await RefreshAsync();
        IsOpen = true;
    }

    private async Task ToggleAsync()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        await OpenAsync();
    }

    private async Task RefreshAsync()
    {
        var categories = await categoryRepository.GetAllAsync();
        var ordered = categories.OrderBy(category => category.Name).ToList();

        // Mutates collections bound to live UI - must run on the UI thread, which the await above
        // may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            Rows.Clear();
            foreach (var category in ordered)
            {
                Rows.Add(new CategoryManagementRowViewModel(category, RenameAsync, DeleteAsync));
            }

            MergeOptions.Clear();
            foreach (var category in ordered)
            {
                MergeOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
            }

            MergeSource = MergeOptions.FirstOrDefault();
            MergeTarget = MergeOptions.Skip(1).FirstOrDefault();
            StatusText = string.Empty;
        });
    }

    private async Task RenameAsync(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        var category = await categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return;
        }

        await categoryRepository.SaveAsync(category with { Name = newName.Trim() });
        await onChanged();
    }

    private async Task DeleteAsync(Guid id)
    {
        await categoryRepository.DeleteAsync(id);
        await RefreshAsync();
        await onChanged();
    }

    private async Task MergeAsync()
    {
        if (MergeSource is null || MergeTarget is null)
        {
            StatusText = "Choose both categories.";
            return;
        }

        if (MergeSource.Id == MergeTarget.Id)
        {
            StatusText = "Choose two different categories.";
            return;
        }

        await categoryManagementService.MergeAsync(MergeSource.Id, MergeTarget.Id);
        await RefreshAsync();
        await onChanged();
    }
}
