// Usage: dotnet run [check-name]   (runs every check group when no name is given)
var only = args.Length > 0 ? args[0] : null;
foreach (var (name, run) in new (string, Action)[]
{
    ("recurrence-syntax-errors", Checks.RunRecurrenceSyntaxErrors),
    ("import-messages", Checks.RunImportMessages),
    ("recurrence-descriptions", Checks.RunRecurrenceDescriptions),
    ("chart-event-summaries", Checks.RunChartEventSummaries),
})
{
    if (only is null || only == name) { Console.WriteLine($"## {name}"); run(); }
}
Console.WriteLine(Checks.Failures == 0 ? "ALL PASSED" : $"{Checks.Failures} FAILED");
return Checks.Failures == 0 ? 0 : 1;
