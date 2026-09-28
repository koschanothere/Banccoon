namespace Banccoon.Core.Statements;

public interface IStatementParserRegistry
{
    IReadOnlyList<StatementParserDescriptor> AvailableParsers { get; }

    IStatementParser? FindParser(StatementParseRequest request);

    // Tries the preferred parsers first, in that order (the banks chosen at first-run setup,
    // AppSettings.PreferredParserIds), then every other parser - so a preference only changes
    // which parser wins when more than one could read the file, and never stops detection.
    IStatementParser? FindParser(StatementParseRequest request, IReadOnlyList<string> preferredParserIds);
}
