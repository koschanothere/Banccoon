namespace Banccoon.Core.Recurrence;

// Key is a stable, language-neutral identifier (e.g. "EveryDay"), not display text - Core stays
// UI-language-agnostic, same treatment as RecurrenceDescriptionData. The App layer resolves it to
// a translated label via "RecurrenceSyntax_Example_{Key}".
public sealed record RecurrenceSyntaxExample(string Key, string Syntax);
