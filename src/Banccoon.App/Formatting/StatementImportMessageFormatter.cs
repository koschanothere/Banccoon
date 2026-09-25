using Banccoon.App.Localization;
using Banccoon.Core.Statements;

namespace Banccoon.App.Formatting;

public static class StatementImportMessageFormatter
{
    public static string Format(StatementImportMessage message) => message.Code switch
    {
        StatementImportMessageCode.NoFileChosen => Translator.Get("StatementImportMessage_NoFileChosen"),
        StatementImportMessageCode.NoParserAvailable => Translator.Get("StatementImportMessage_NoParserAvailable"),
        StatementImportMessageCode.RowsFound => Translator.GetPlural("StatementImportMessage_RowsFound", message.Count ?? 0),
        StatementImportMessageCode.RowsReadyForReview => Translator.GetPlural("StatementImportMessage_RowsReadyForReview", message.Count ?? 0),
        StatementImportMessageCode.ImportNotFound => Translator.Get("StatementImportMessage_ImportNotFound"),
        StatementImportMessageCode.CannotCancelAfterApproval => Translator.Get("StatementImportMessage_CannotCancelAfterApproval"),
        StatementImportMessageCode.Cancelled => Translator.Get("StatementImportMessage_Cancelled"),
        _ => message.Code.ToString()
    };
}
