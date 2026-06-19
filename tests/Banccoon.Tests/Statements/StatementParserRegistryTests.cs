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

    private sealed class FakeStatementParser : IStatementParser
    {
        private readonly string extension;

        public FakeStatementParser(string extension)
        {
            this.extension = extension;
        }

        public StatementParserDescriptor Descriptor { get; } = new(
            "fake",
            "Fake parser",
            [".fake"]);

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
