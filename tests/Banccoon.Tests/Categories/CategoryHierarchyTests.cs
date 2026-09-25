using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Infrastructure.Repositories;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Categories;

public sealed class CategoryHierarchyTests
{
    [Fact]
    public void Tree_ListsOnlyParentsAtTopLevel_AndEachParentsOwnChildrenByName()
    {
        var food = new Category(Guid.NewGuid(), "Food");
        var transport = new Category(Guid.NewGuid(), "Transport");
        var restaurants = new Category(Guid.NewGuid(), "Restaurants", ParentCategoryId: food.Id);
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);

        var tree = new CategoryTree([restaurants, transport, groceries, food]);

        Assert.Equal(["Food", "Transport"], tree.TopLevel.Select(category => category.Name));
        Assert.Equal(["Groceries", "Restaurants"], tree.ChildrenOf(food.Id).Select(category => category.Name));
        Assert.Empty(tree.ChildrenOf(transport.Id));
        Assert.True(tree.HasChildren(food.Id));
        Assert.False(tree.HasChildren(transport.Id));
        Assert.Equal((food.Id, groceries.Id), tree.Split(groceries.Id));
        Assert.Equal((transport.Id, (Guid?)null), tree.Split(transport.Id));
        Assert.Equal(food.Id, tree.RootIdOf(restaurants.Id));
    }

    [Fact]
    public void Tree_TreatsAChildWithAMissingOrNestedParentAsTopLevel()
    {
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var orphan = new Category(Guid.NewGuid(), "Orphan", ParentCategoryId: Guid.NewGuid());
        var grandchild = new Category(Guid.NewGuid(), "Grandchild", ParentCategoryId: groceries.Id);

        var tree = new CategoryTree([food, groceries, orphan, grandchild]);

        Assert.Equal(["Food", "Grandchild", "Orphan"], tree.TopLevel.Select(category => category.Name));
        Assert.Equal(orphan.Id, tree.RootIdOf(orphan.Id));
        Assert.False(tree.IsChild(grandchild.Id));
    }

    [Fact]
    public async Task Save_Child_TakesItsParentsColor_AndRoundTripsItsParent()
    {
        await using var store = new SqliteTestStore();
        var repository = new HierarchicalCategoryRepository(store.Categories);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Teal);
        await repository.SaveAsync(food);

        var groceries = new Category(Guid.NewGuid(), "Groceries", Color: CategoryColor.Pink, ParentCategoryId: food.Id);
        await repository.SaveAsync(groceries);

        var loaded = await store.Categories.GetByIdAsync(groceries.Id);
        Assert.Equal(food.Id, loaded!.ParentCategoryId);
        Assert.Equal(CategoryColor.Teal, loaded.Color);
        Assert.Equal(food.Id, loaded.ColorSourceId);
    }

    [Fact]
    public async Task Save_ParentWithNewColor_RecolorsItsChildren()
    {
        await using var store = new SqliteTestStore();
        var repository = new HierarchicalCategoryRepository(new CachedCategoryRepository(store.Categories));
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        await repository.SaveAsync(food);
        await repository.SaveAsync(groceries);

        await repository.SaveAsync(food with { Color = CategoryColor.Violet });

        Assert.Equal(CategoryColor.Violet, (await repository.GetByIdAsync(groceries.Id))!.Color);
        Assert.Equal(CategoryColor.Violet, (await store.Categories.GetByIdAsync(groceries.Id))!.Color);
    }

    [Fact]
    public async Task Save_RejectsAThirdLevel_ACategoryWithChildrenBecomingAChild_SelfAndMissingParents()
    {
        await using var store = new SqliteTestStore();
        var repository = new HierarchicalCategoryRepository(store.Categories);
        var food = new Category(Guid.NewGuid(), "Food");
        var transport = new Category(Guid.NewGuid(), "Transport");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        await repository.SaveAsync(food);
        await repository.SaveAsync(transport);
        await repository.SaveAsync(groceries);

        // Child of a child.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveAsync(new Category(Guid.NewGuid(), "Fruit", ParentCategoryId: groceries.Id)));
        // A parent that has children can't be put under another parent.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveAsync(food with { ParentCategoryId = transport.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveAsync(transport with { ParentCategoryId = transport.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveAsync(transport with { ParentCategoryId = Guid.NewGuid() }));

        // Nothing was changed by the rejected saves.
        Assert.Null((await store.Categories.GetByIdAsync(food.Id))!.ParentCategoryId);
        Assert.Null((await store.Categories.GetByIdAsync(transport.Id))!.ParentCategoryId);
        Assert.Equal(3, (await store.Categories.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Save_ChildMovedToAnotherParent_AndBackToTopLevel()
    {
        await using var store = new SqliteTestStore();
        var repository = new HierarchicalCategoryRepository(store.Categories);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Teal);
        var home = new Category(Guid.NewGuid(), "Home", Color: CategoryColor.Brown);
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        await repository.SaveAsync(food);
        await repository.SaveAsync(home);
        await repository.SaveAsync(groceries);

        await repository.SaveAsync(groceries with { ParentCategoryId = home.Id });
        Assert.Equal(CategoryColor.Brown, (await store.Categories.GetByIdAsync(groceries.Id))!.Color);

        await repository.SaveAsync((await store.Categories.GetByIdAsync(groceries.Id))! with { ParentCategoryId = null });
        var loaded = await store.Categories.GetByIdAsync(groceries.Id);
        Assert.Null(loaded!.ParentCategoryId);
        Assert.Equal(CategoryColor.Brown, loaded.Color);
    }

    [Fact]
    public async Task Delete_Parent_MakesItsChildrenTopLevel_InTheCacheToo()
    {
        await using var store = new SqliteTestStore();
        var cached = new CachedCategoryRepository(store.Categories);
        var repository = new HierarchicalCategoryRepository(cached);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Amber);
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        await repository.SaveAsync(food);
        await repository.SaveAsync(groceries);

        await repository.DeleteAsync(food.Id);

        var fromCache = Assert.Single(await cached.GetAllAsync());
        Assert.Equal(groceries.Id, fromCache.Id);
        Assert.Null(fromCache.ParentCategoryId);
        Assert.Equal(CategoryColor.Amber, fromCache.Color);
        Assert.Null((await store.Categories.GetByIdAsync(groceries.Id))!.ParentCategoryId);
    }

    [Fact]
    public async Task Database_DeletingAParentDirectly_SetsItsChildrensParentToNull()
    {
        await using var store = new SqliteTestStore();
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        await store.Categories.SaveAsync(food);
        await store.Categories.SaveAsync(groceries);

        await store.Categories.DeleteAsync(food.Id);

        Assert.Null((await store.Categories.GetByIdAsync(groceries.Id))!.ParentCategoryId);
    }

    [Fact]
    public async Task Migration_AddsParentCategoryId_ToAnOldCategoriesTable_LeavingEveryCategoryTopLevel()
    {
        await using var store = new SqliteTestStore();
        var oldId = Guid.NewGuid();
        await using (var connection = await store.ConnectionFactory.OpenConnectionAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE TABLE Categories (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Type TEXT NULL, Color TEXT NULL);
                INSERT INTO Categories (Id, Name) VALUES ('{oldId}', 'Old');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var old = Assert.Single(await store.Categories.GetAllAsync());
        Assert.Null(old.ParentCategoryId);

        var repository = new HierarchicalCategoryRepository(store.Categories);
        var child = new Category(Guid.NewGuid(), "Child", ParentCategoryId: oldId);
        await repository.SaveAsync(child);
        Assert.Equal(oldId, (await store.Categories.GetByIdAsync(child.Id))!.ParentCategoryId);
    }
}
