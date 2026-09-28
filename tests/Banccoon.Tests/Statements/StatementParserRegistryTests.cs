using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Xunit;

namespace Banccoon.Tests.Statements;

public sealed class StatementParserRegistryTests
{
    [Fact]
    public void FindParser_WhenNoParserSupportsFile_ReturnsNull()
    {
        var registry = new StatementParserRegistry(Array.Empty<IStatementParser>());

        var parser = registry.FindParser(new StatementParseRequest("statement.pdf", Guid.NewGuid()));

        Assert.Null(parser);
        Assert.Empty(registry.AvailableParsers);
    }

    [Fact]
    public void FindParser_WhenParserSupportsFile_ReturnsParser()
    {
        var parser = new FakeStatementParser(".fake");
        var registry = new StatementParserRegistry([parser]);

        var selected = registry.FindParser(new StatementParseRequest("statement.fake", Guid.NewGuid()));

        Assert.Same(parser, selected);
        Assert.Equal("Fake parser", Assert.Single(registry.AvailableParsers).Name);
    }

    [Fact]
    public void FindParser_WithPreferredParsers_TriesThemFirst_ThenDetectsAcrossTheRest()
    {
        var first = new FakeStatementParser(".pdf", "first");
        var second = new FakeStatementParser(".pdf", "second");
        var csvOnly = new FakeStatementParser(".csv", "csv");
        var registry = new StatementParserRegistry([first, second, csvOnly]);

        Assert.Same(first, registry.FindParser(new StatementParseRequest("s.pdf", Guid.NewGuid()), []));
        Assert.Same(second, registry.FindParser(new StatementParseRequest("s.pdf", Guid.NewGuid()), ["second"]));
        // A preferred parser that can't read the file doesn't stop detection.
        Assert.Same(first, registry.FindParser(new StatementParseRequest("s.pdf", Guid.NewGuid()), ["csv", "unknown"]));
        Assert.Same(csvOnly, registry.FindParser(new StatementParseRequest("s.csv", Guid.NewGuid()), ["second"]));
    }

    private sealed class FakeStatementParser : IStatementParser
    {
        private readonly string extension;

        public FakeStatementParser(string extension, string id = "fake")
        {
            this.extension = extension;
            Descriptor = new StatementParserDescriptor(id, "Fake parser", [".fake"]);
        }

        public StatementParserDescriptor Descriptor { get; }

        public bool CanParse(StatementParseRequest request)
        {
            return request.FilePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedStatement> ParseAsync(
            StatementParseRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ParsedStatement(
                Descriptor.Id,
                Descriptor.Name,
                Path.GetFileName(request.FilePath),
                [
                    new ParsedStatementRow(
                        new DateOnly(2026, 6, 10),
                        12.50m,
                        TransactionType.Expense,
                        "Coffee shop")
                ]));
        }
    }
}
