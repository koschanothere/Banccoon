namespace Banccoon.Core.Statements;

// One of a bank's own operation categories (e.g. Sberbank's "Рестораны и кафе"), as its statement
// parser reads it, and the app category it's linked to - null when the user skipped it (it's then
// ignored until they link it in Settings). A row exists once the category has been seen in a
// statement, so each bank's list grows from its statements rather than being written in advance.
public sealed record BankCategoryLink(
    string ParserId,
    string BankCategory,
    Guid? CategoryId,
    DateTimeOffset FirstSeenAt);
