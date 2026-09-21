using Banccoon.App.Localization;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.ViewModels;

public sealed class RecurrenceSyntaxExampleRowViewModel
{
    public RecurrenceSyntaxExampleRowViewModel(RecurrenceSyntaxExample example)
    {
        LabelText = Translator.Get($"RecurrenceSyntax_Example_{example.Key}");
        Syntax = example.Syntax;
    }

    public string LabelText { get; }

    public string Syntax { get; }
}
