using Banccoon.Core.Appearance;
using Banccoon.Core.Models;

namespace Banccoon.Core.Setup;

// One suggested category offered at first-run setup. Core stays UI-language-agnostic: Key is a
// stable identifier, and the App turns it into a name in the chosen language (resx key
// "DefaultCategory_{Key}"). Once created, a category's name is the user's own data and doesn't
// follow later language changes.
public sealed record DefaultCategoryDefinition(
    string Key,
    TransactionType Type,
    CategoryColor Color,
    IReadOnlyList<string> ChildKeys);

// The default categories agreed with the user 2026-09-28 (see docs/development-phases.md,
// "Default categories"): two levels, expense then income. Children take their parent's color.
public static class DefaultCategories
{
    // The catch-all: always created (setup doesn't let it be unticked), and set as the import's
    // fallback category (AppSettings.FallbackCategoryId).
    public const string OtherKey = "Other";

    public static IReadOnlyList<DefaultCategoryDefinition> All { get; } =
    [
        Expense("Groceries", CategoryColor.Teal, "Supermarket", "Market", "GroceryDelivery"),
        Expense("EatingOut", CategoryColor.Amber, "Restaurants", "Cafes", "FastFood", "FoodDelivery"),
        Expense("Transport", CategoryColor.Blue, "Fuel", "PublicTransport", "Taxi", "CarSharing", "ScootersBikes"),
        Expense("Home", CategoryColor.Brown, "Bills", "Rent", "Mortgage", "Repairs", "FurnitureAppliances", "HouseholdGoods"),
        Expense("Health", CategoryColor.Pink, "PersonalCare", "Pharmacy", "Doctors", "Dentist", "Sport"),
        Expense("Shopping", CategoryColor.Violet, "ClothesShoes", "Electronics", "Marketplaces"),
        Expense("Leisure", CategoryColor.Amber, "Travel", "CinemaEvents", "Hobbies", "Games", "Books"),
        Expense("Education", CategoryColor.Blue, "Courses", "SchoolUniversity", "KidsActivities"),
        Expense("FinanceFees", CategoryColor.Slate, "BankFees", "LoanInterest", "Taxes", "Insurance"),
        Expense(OtherKey, CategoryColor.Slate, "Gifts", "Charity"),
        Income("Salary", CategoryColor.Teal, "Advance", "Bonus"),
        Income("SideIncome", CategoryColor.Teal),
        Income("InterestCashback", CategoryColor.Blue, "DepositInterest", "Cashback"),
        Income("GiftsReceived", CategoryColor.Pink),
        Income("OtherIncome", CategoryColor.Slate)
    ];

    // Child keys are "{ParentKey}_{Child}" so the same word under two parents stays two keys.
    private static DefaultCategoryDefinition Expense(string key, CategoryColor color, params string[] children) =>
        new(key, TransactionType.Expense, color, children.Select(child => $"{key}_{child}").ToList());

    private static DefaultCategoryDefinition Income(string key, CategoryColor color, params string[] children) =>
        new(key, TransactionType.Income, color, children.Select(child => $"{key}_{child}").ToList());
}
