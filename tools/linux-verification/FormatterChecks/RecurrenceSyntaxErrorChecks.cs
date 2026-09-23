using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Recurrence;
static partial class Checks
{
    public static void RunRecurrenceSyntaxErrors()
    {
        var svc = new RecurrenceSyntaxService();
        string First(string syntax) => RecurrenceSyntaxErrorFormatter.Format(svc.TryParse(syntax).Errors[0]);
        
        Translator.SetLanguage("en");
        Check("en empty", First(" "), "Syntax is empty.");
        Check("en invalid field", First("garbage;FREQ=DAILY;START=2026-06-07"), "Invalid field 'garbage'. Use KEY=VALUE.");
        Check("en required", First("INTERVAL=1;START=2026-06-07"), "FREQ is required.");
        Check("en unsupported", First("FREQ=HOURLY;START=2026-06-07"), "FREQ has an unsupported value.");
        Check("en whole", First("FREQ=DAILY;INTERVAL=x;START=2026-06-07"), "INTERVAL must be a whole number.");
        Check("en monthday", First("FREQ=MONTHLY;START=2026-06-07;BYMONTHDAY=x"), "BYMONTHDAY must be a number between 1 and 31, or LAST.");
        Check("en rule invalid", First("FREQ=DAILY;INTERVAL=0;START=2026-06-07"), "Recurrence interval must be at least 1.");
        Translator.SetLanguage("ru");
        Check("ru empty", First(" "), "Синтаксис не указан.");
        Check("ru invalid field", First("garbage;FREQ=DAILY;START=2026-06-07"), "Некорректное поле «garbage». Используйте формат KEY=VALUE.");
        Check("ru required", First("INTERVAL=1;START=2026-06-07"), "Поле FREQ обязательно.");
        Check("ru unsupported", First("FREQ=HOURLY;START=2026-06-07"), "Поле FREQ содержит неподдерживаемое значение.");
        Check("ru whole", First("FREQ=DAILY;INTERVAL=x;START=2026-06-07"), "Поле INTERVAL должно быть целым числом.");
        Check("ru monthday", First("FREQ=MONTHLY;START=2026-06-07;BYMONTHDAY=x"), "Поле BYMONTHDAY должно быть числом от 1 до 31 или LAST.");
        Check("ru rule invalid", First("FREQ=DAILY;INTERVAL=0;START=2026-06-07"), "Интервал повторения должен быть не менее 1.");
    }
}
