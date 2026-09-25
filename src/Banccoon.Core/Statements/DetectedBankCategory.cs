namespace Banccoon.Core.Statements;

// A bank category seen for the first time in a statement, with how many of its operations use it.
public sealed record DetectedBankCategory(string Name, int OperationCount);
