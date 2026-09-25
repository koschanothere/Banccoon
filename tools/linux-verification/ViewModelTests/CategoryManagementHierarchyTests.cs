using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Tests.Infrastructure;
using Xunit;

// Settings -> Manage categories with two-level categories, over the real hierarchy-enforcing
// repository the app registers (HierarchicalCategoryRepository).
public sealed class CategoryManagementHierarchyTests
{
    [Fact]
    public async Task Boxes_PutEachParentsChildrenRightAfterIt_AndAChildsPopupHasNoSwatches()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var (vm, repository) = Create(store);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Teal);
        var bills = new Category(Guid.NewGuid(), "Bills");
        await repository.SaveAsync(food);
        await repository.SaveAsync(bills);
        await repository.SaveAsync(new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id));
        await repository.SaveAsync(new Category(Guid.NewGuid(), "Cafes", ParentCategoryId: food.Id));

        await vm.InitializeAsync();

        Assert.Equal(["Bills", "Food", "Cafes", "Groceries"], vm.Boxes.Select(box => box.Name));
        Assert.Equal([false, false, true, true], vm.Boxes.Select(box => box.IsChild));
        var foodBox = vm.Boxes.Single(box => box.Name == "Food");
        Assert.All(vm.Boxes.Where(box => box.IsChild), child => Assert.Equal(foodBox.Color, child.Color));

        var groceriesBox = vm.Boxes.Single(box => box.Name == "Groceries");
        groceriesBox.TapCommand.Execute(null);
        Assert.Same(groceriesBox, vm.ActiveColorPickerBox);
        Assert.False(vm.CanChooseActiveColor);
        Assert.Equal("Its color follows Food.", vm.ActiveBoxColorFollowsText);
        Assert.True(vm.CanChangeActiveParent);
        Assert.Equal("Food", vm.ActiveBoxParent!.Name);
        Assert.DoesNotContain(vm.ActiveBoxParentOptions, option => option.Name == "Groceries" || option.Name == "Cafes");

        foodBox.TapCommand.Execute(null);
        Assert.True(vm.CanChooseActiveColor);
        Assert.False(vm.CanChangeActiveParent);

        // Recoloring the parent recolors its children.
        foodBox.ColorSwatches.Single(swatch => swatch.Color == Banccoon.App.Formatting.CategoryColorPalette.GetColor(CategoryColor.Pink)).SelectCommand.Execute(null);
        await Task.Delay(300);
        Assert.All(vm.Boxes.Where(box => box.IsChild), child => Assert.Equal(Banccoon.App.Formatting.CategoryColorPalette.GetColor(CategoryColor.Pink), child.Color));
    }

    [Fact]
    public async Task PopupParentPicker_MovesACategoryUnderAParent_AndBack()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var (vm, repository) = Create(store);
        var food = new Category(Guid.NewGuid(), "Food");
        var coffee = new Category(Guid.NewGuid(), "Coffee");
        await repository.SaveAsync(food);
        await repository.SaveAsync(coffee);
        await vm.InitializeAsync();

        vm.Boxes.Single(box => box.Name == "Coffee").TapCommand.Execute(null);
        Assert.Equal("None (top level)", vm.ActiveBoxParent!.Name);
        vm.ActiveBoxParent = vm.ActiveBoxParentOptions.Single(option => option.Name == "Food");
        await Task.Delay(300);

        Assert.Equal(food.Id, (await store.Categories.GetByIdAsync(coffee.Id))!.ParentCategoryId);
        Assert.Equal(["Food", "Coffee"], vm.Boxes.Select(box => box.Name));
        Assert.True(vm.Boxes[1].IsChild);
        Assert.True(vm.Boxes[0].HasChildren);

        vm.Boxes[1].TapCommand.Execute(null);
        vm.ActiveBoxParent = vm.ActiveBoxParentOptions[0];
        await Task.Delay(300);
        Assert.Null((await store.Categories.GetByIdAsync(coffee.Id))!.ParentCategoryId);
        Assert.Equal(["Coffee", "Food"], vm.Boxes.Select(box => box.Name));
    }

    [Fact]
    public async Task AddCategory_WithAParent_CreatesAChildInItsParentsColor()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var (vm, repository) = Create(store);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Amber);
        await repository.SaveAsync(food);
        await vm.InitializeAsync();

        vm.StartAddCategoryCommand.Execute(null);
        Assert.Equal("None (top level)", vm.NewCategoryParent!.Name);
        vm.NewCategoryName = "Snacks";
        vm.NewCategoryParent = vm.NewCategoryParentOptions.Single(option => option.Name == "Food");
        vm.ConfirmAddCategoryCommand.Execute(null);
        await Task.Delay(300);

        var snacks = (await store.Categories.GetAllAsync()).Single(category => category.Name == "Snacks");
        Assert.Equal(food.Id, snacks.ParentCategoryId);
        Assert.Equal(CategoryColor.Amber, snacks.Color);
        Assert.Equal(["Food", "Snacks"], vm.Boxes.Select(box => box.Name));
    }

    private static (CategoryManagementViewModel Vm, HierarchicalCategoryRepository Repository) Create(SqliteTestStore store)
    {
        var repository = new HierarchicalCategoryRepository(store.Categories);
        var service = new CategoryManagementService(repository, store.Transactions, store.BankCategoryLinks);
        return (new CategoryManagementViewModel(repository, service, () => Task.CompletedTask), repository);
    }
}
