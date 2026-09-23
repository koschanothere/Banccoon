using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Statements;
static partial class Checks
{
    public static void RunImportMessages()
    {
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string M(StatementImportMessageCode c, int? n = null) => StatementImportMessageFormatter.Format(new StatementImportMessage(c, n));
        Translator.SetLanguage("en");
        Check("en nofile", M(StatementImportMessageCode.NoFileChosen), "Choose a bank statement file first.");
        Check("en found 1", M(StatementImportMessageCode.RowsFound, 1), "1 statement row found.");
        Check("en found 21", M(StatementImportMessageCode.RowsFound, 21), "21 statement rows found.");
        Check("en ready 1", M(StatementImportMessageCode.RowsReadyForReview, 1), "1 statement row is ready for review.");
        Check("en ready 3", M(StatementImportMessageCode.RowsReadyForReview, 3), "3 statement rows are ready for review.");
        Check("en cancelled", M(StatementImportMessageCode.Cancelled), "Statement import cancelled.");
        Check("en version", ImportValidationErrorFormatter.Format(ImportValidationError.UnsupportedFormatVersion(99)), "Unsupported export format version: 99.");
        Check("en dup", ImportValidationErrorFormatter.Format(ImportValidationError.DuplicateId(ImportEntityType.ScheduledTransaction, id1)), $"Scheduled transaction id {id1} appears more than once.");
        Check("en missing", ImportValidationErrorFormatter.Format(ImportValidationError.MissingReference(ImportEntityType.StatementImportRow, id1, ImportReferenceKind.SuggestedCategory, id2)), $"Statement import row {id1} references missing suggested category {id2}.");
        Check("en missing fallback", ImportValidationErrorFormatter.Format(ImportValidationError.MissingReference(ImportEntityType.Account, id1, ImportReferenceKind.Batch, id2)), $"Record {id1} references a missing record {id2}.");
        Translator.SetLanguage("ru");
        Check("ru nofile", M(StatementImportMessageCode.NoFileChosen), "Сначала выберите файл банковской выписки.");
        Check("ru found 1", M(StatementImportMessageCode.RowsFound, 1), "Найдена 1 строка выписки.");
        Check("ru found 21", M(StatementImportMessageCode.RowsFound, 21), "Найдена 21 строка выписки.");
        Check("ru found 3", M(StatementImportMessageCode.RowsFound, 3), "Найдено 3 строки выписки.");
        Check("ru found 11", M(StatementImportMessageCode.RowsFound, 11), "Найдено 11 строк выписки.");
        Check("ru ready 1", M(StatementImportMessageCode.RowsReadyForReview, 1), "1 строка выписки готова к проверке.");
        Check("ru ready 24", M(StatementImportMessageCode.RowsReadyForReview, 24), "24 строки выписки готовы к проверке.");
        Check("ru ready 5", M(StatementImportMessageCode.RowsReadyForReview, 5), "5 строк выписки готово к проверке.");
        Check("ru cannot cancel", M(StatementImportMessageCode.CannotCancelAfterApproval), "По этой выписке уже созданы транзакции, поэтому импорт нельзя отменить.");
        Check("ru dup", ImportValidationErrorFormatter.Format(ImportValidationError.DuplicateId(ImportEntityType.Category, id1)), $"Категория с идентификатором {id1} встречается более одного раза.");
        Check("ru missing acc", ImportValidationErrorFormatter.Format(ImportValidationError.MissingReference(ImportEntityType.Transaction, id1, ImportReferenceKind.Account, id2)), $"Транзакция {id1} ссылается на отсутствующий счёт {id2}.");
        Check("ru missing cat", ImportValidationErrorFormatter.Format(ImportValidationError.MissingReference(ImportEntityType.Transaction, id1, ImportReferenceKind.Category, id2)), $"Транзакция {id1} ссылается на отсутствующую категорию {id2}.");

        // Every (entity, reference) pair ExportValidator can actually emit must have its own key in both
        // languages - scan the validator source so a new check added later can't silently hit the fallback.
        var src = File.ReadAllText(Path.Combine(RepoRoot.Find(), "src/Banccoon.Infrastructure/ImportExport/ExportValidator.cs"));
        var pairs = System.Text.RegularExpressions.Regex.Matches(src, @"MissingReference\(ImportEntityType\.(\w+), [^,]+, ImportReferenceKind\.(\w+)");
        foreach (var lang in new[] { "en", "ru" })
        {
            Translator.SetLanguage(lang);
            foreach (System.Text.RegularExpressions.Match m in pairs)
            {
                var key = $"ImportValidation_MissingReference_{m.Groups[1].Value}_{m.Groups[2].Value}";
                Check($"{lang} key {key}", Translator.Get(key) == key ? "MISSING" : "present", "present");
            }
            foreach (var entity in Enum.GetValues<ImportEntityType>())
            {
                var key = $"ImportValidation_DuplicateId_{entity}";
                Check($"{lang} key {key}", Translator.Get(key) == key ? "MISSING" : "present", "present");
            }
        }
        Console.WriteLine($"   (validator emits {pairs.Count} missing-reference pairs)");
    }
}
