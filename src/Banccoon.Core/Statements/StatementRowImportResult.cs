using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public sealed record StatementRowImportResult(
    StatementImportRow Row,
    Transaction? Transaction);
