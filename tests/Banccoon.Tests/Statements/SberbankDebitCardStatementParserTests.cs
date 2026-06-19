using Banccoon.Core.Models;
using Banccoon.Infrastructure.Statements;
using Xunit;

namespace Banccoon.Tests.Statements;

public sealed class SberbankDebitCardStatementParserTests
{
    [Fact]
    public void ParseExtractedLines_ParsesSberbankDebitCardRows()
    {
        var parser = new SberbankDebitCardStatementParser();
        var lines = new[]
        {
            "РќРѕРјРµСЂ СЃС‡С‘С‚Р° 40817 810 3 1234 5678901",
            "РљР°СЂС‚Р° РњРР  Р—РѕР»РѕС‚Р°СЏ •••• 1234",
            "999 www.sberbank.ru Заказано в СберБанк Онлайн",
            "Выписка по счёту дебетовой карты",
            "За период 01.06.2026 — 30.06.2026",
            "Остаток на 01.06.2026 1 000,00",
            "Остаток на 30.06.2026 1 850,00",
            "ДАТА ОПЕРАЦИИ (МСК) КАТЕГОРИЯ СУММА В ВАЛЮТЕ СЧЁТА ОСТАТОК СРЕДСТВ",
            "10.06.2026 12:34 Рестораны и кафе 150,50 849,50",
            "10.06.2026 123456 CAFE TEST. Операция по карте ****1234",
            "11.06.2026 09:15 Перевод на карту +1 000,00 1 849,50",
            "11.06.2026 654321 Перевод от И. ИВАН. Операция по карте ****1234"
        };

        var parsed = parser.ParseExtractedLines(lines, "sample.pdf");

        Assert.Equal("sberbank-debit-card-pdf", parsed.ParserId);
        Assert.Equal(new DateOnly(2026, 6, 1), parsed.PeriodStart);
        Assert.Equal(new DateOnly(2026, 6, 30), parsed.PeriodEnd);
        Assert.Equal(1000m, parsed.OpeningBalance);
        Assert.Equal(1850m, parsed.ClosingBalance);
        Assert.Equal("40817810312345678901", parsed.AccountNumber);
        Assert.Equal("1234", parsed.CardLastFourDigits);

        Assert.Collection(
            parsed.Rows,
            expense =>
            {
                Assert.Equal(new DateOnly(2026, 6, 10), expense.Date);
                Assert.Equal(TransactionType.Expense, expense.Type);
                Assert.Equal(150.50m, expense.Amount);
                Assert.Equal("CAFE TEST", expense.Description);
                Assert.Equal("123456", expense.ExternalReference);
            },
            income =>
            {
                Assert.Equal(new DateOnly(2026, 6, 11), income.Date);
                Assert.Equal(TransactionType.Income, income.Type);
                Assert.Equal(1000m, income.Amount);
                Assert.Equal("Перевод от И. ИВАН", income.Description);
                Assert.Equal("654321", income.ExternalReference);
            });
    }

    [Fact]
    public void ParseExtractedLines_JoinsWrappedDescriptions()
    {
        var parser = new SberbankDebitCardStatementParser();
        var lines = new[]
        {
            "СберБанк Онлайн",
            "Выписка по счёту дебетовой карты",
            "12.06.2026 10:00 Супермаркеты 200,00 800,00",
            "12.06.2026 111222 MARKET",
            "CITY. Операция по карте",
            "****1234"
        };

        var parsed = parser.ParseExtractedLines(lines, "wrapped.pdf");

        var row = Assert.Single(parsed.Rows);
        Assert.Equal("MARKET CITY", row.Description);
    }
}
