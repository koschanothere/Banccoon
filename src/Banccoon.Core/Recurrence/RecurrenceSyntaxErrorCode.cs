namespace Banccoon.Core.Recurrence;

public enum RecurrenceSyntaxErrorCode
{
    Empty,
    InvalidField,
    FieldRequired,
    UnsupportedValue,
    NotWholeNumber,
    InvalidMonthDay,

    // The syntax parsed, but the rule it describes failed RecurrenceValidationService -
    // RecurrenceSyntaxError.ValidationError carries which rule.
    RuleInvalid
}
