using Banccoon.App.ViewModels;

// The ready block draws nothing while collapsed, and only the view that's showing when open - as on
// screen. Tests that look at its rows or groups open it first, the way the user would.
internal static class ReadyBlockTestExtensions
{
    public static void ShowReadyRows(this StatementImportReviewSectionsViewModel sections)
    {
        sections.IsGroupedByName = false;
        if (!sections.IsReadyExpanded)
        {
            sections.ToggleReadyExpandedCommand.Execute(null);
        }
    }

    public static void ShowReadyGroups(this StatementImportReviewSectionsViewModel sections)
    {
        sections.IsGroupedByName = true;
        if (!sections.IsReadyExpanded)
        {
            sections.ToggleReadyExpandedCommand.Execute(null);
        }
    }
}
