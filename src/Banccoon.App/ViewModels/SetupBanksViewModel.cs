using System.Collections.ObjectModel;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// First-run setup, step 4: which banks' statements the user will import. Built from every
// registered statement parser, so a new parser shows up here with no setup change. The ticked
// ones are tried first when reading a statement (AppSettings.PreferredParserIds); none ticked
// (or Skip) means detecting the bank from each file, as before.
public sealed class SetupBanksViewModel : ViewModelBase
{
    public SetupBanksViewModel(IStatementParserRegistry parserRegistry)
    {
        Banks = new ObservableCollection<SetupBankOptionViewModel>(
            parserRegistry.AvailableParsers
                .OrderBy(parser => parser.Name, StringComparer.CurrentCulture)
                .Select(parser => new SetupBankOptionViewModel(parser.Id, parser.Name)));
    }

    public ObservableCollection<SetupBankOptionViewModel> Banks { get; }

    public IReadOnlyList<string> SelectedParserIds => Banks.Where(bank => bank.IsSelected).Select(bank => bank.ParserId).ToList();

    public void ClearSelection()
    {
        foreach (var bank in Banks)
        {
            bank.IsSelected = false;
        }
    }
}
