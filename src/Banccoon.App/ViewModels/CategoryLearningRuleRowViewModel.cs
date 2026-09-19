using System.Windows.Input;

namespace Banccoon.App.ViewModels;

public sealed class CategoryLearningRuleRowViewModel
{
    public CategoryLearningRuleRowViewModel(
        Guid id,
        string matchText,
        string typeText,
        string categoryName,
        string? destinationAccountName,
        int matchCount,
        string lastUpdatedText,
        Func<Guid, Task> onForget)
    {
        Id = id;
        MatchText = matchText;
        TypeText = typeText;
        CategoryName = categoryName;
        DestinationAccountName = destinationAccountName;
        HasDestinationAccount = !string.IsNullOrWhiteSpace(destinationAccountName);
        MatchCountText = matchCount == 1 ? "Learned from 1 correction" : $"Learned from {matchCount} corrections";
        LastUpdatedText = lastUpdatedText;

        ForgetCommand = new RelayCommand(() => _ = onForget(Id));
    }

    public Guid Id { get; }

    public string MatchText { get; }

    public string TypeText { get; }

    public string CategoryName { get; }

    public string? DestinationAccountName { get; }

    public bool HasDestinationAccount { get; }

    public string MatchCountText { get; }

    public string LastUpdatedText { get; }

    public ICommand ForgetCommand { get; }
}
