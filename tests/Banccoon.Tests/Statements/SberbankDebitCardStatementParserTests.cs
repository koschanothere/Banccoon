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
            "Номер счёта 40817 810 3 1234 5678901",
            "Карта МИР Золотая •••• 1234",
            "999 www.sberbank.ru Заказано в СберБанк Онлайн",
            "Выписка по счёту дебетовой карты",
            "За период 01.06.2026 — 30.06.2026",
            "Остаток на 01.06.2026 1 000,00",
            "Остаток на 30.06.2026 1 850,00",
            "ДАТА ОПЕРАЦИИ (МСК) КАТЕГОРИЯ СУММА В ВАЛЮТЕ СЧЁТА ОСТАТОК СРЕДСТВ",
            // Real Sberbank statements list operations newest-first, so the income (11.06) comes
            // before the expense (10.06) here too.
            "11.06.2026 09:15 Перевод на карту +1 000,00 1 849,50",
            "11.06.2026 654321 Перевод от И. ИВАН. Операция по карте ****1234",
            "10.06.2026 12:34 Рестораны и кафе 150,50 849,50",
            "10.06.2026 123456 CAFE TEST. Операция по карте ****1234"
        };

        var parsed = parser.ParseExtractedLines(lines, "sample.pdf");

        Assert.Equal("sberbank-debit-card-pdf", parsed.ParserId);
        Assert.Equal(new DateOnly(2026, 6, 1), parsed.PeriodStart);
        Assert.Equal(new DateOnly(2026, 6, 30), parsed.PeriodEnd);
        Assert.Equal(1000m, parsed.OpeningBalance);
        // Not the header's stated closing balance (1850) - the most recent operation's own
        // running balance, which is what's actually trustworthy (see ParseExtractedLines).
        Assert.Equal(1849.50m, parsed.ClosingBalance);
        Assert.Equal("40817810312345678901", parsed.AccountNumber);
        Assert.Equal("1234", parsed.CardLastFourDigits);

        Assert.Collection(
            parsed.Rows,
            income =>
            {
                Assert.Equal(new DateOnly(2026, 6, 11), income.Date);
                Assert.Equal(TransactionType.Income, income.Type);
                Assert.Equal(1000m, income.Amount);
                Assert.Equal("Перевод от И. ИВАН", income.Description);
                Assert.Equal("654321", income.ExternalReference);
                Assert.Equal(1849.50m, income.BalanceAfter);
            },
            expense =>
            {
                Assert.Equal(new DateOnly(2026, 6, 10), expense.Date);
                Assert.Equal(TransactionType.Expense, expense.Type);
                Assert.Equal(150.50m, expense.Amount);
                Assert.Equal("CAFE TEST", expense.Description);
                Assert.Equal("123456", expense.ExternalReference);
                Assert.Equal(849.50m, expense.BalanceAfter);
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

    [Fact]
    public void ParseExtractedLines_FlushesPendingRowAtPageBoundary()
    {
        // Regression test for a real bug: any line between the last real operation of one page
        // and the first operation of the next used to get silently appended to the pending row's
        // description, because nothing forced a flush at the page break itself.
        var parser = new SberbankDebitCardStatementParser();
        var lines = new[]
        {
            "Выписка по счёту дебетовой карты",
            "ДАТА ОПЕРАЦИИ (МСК) КАТЕГОРИЯ СУММА В ВАЛЮТЕ СЧЁТА ОСТАТОК СРЕДСТВ",
            "12.06.2026 10:00 Супермаркеты 200,00 800,00",
            "12.06.2026 111222 MARKET CITY. Операция по карте ****1234",
            "Продолжение на следующей странице",
            "Для проверки подлинности документа",
            "1. Зайдите в приложение СберБанк Онлайн",
            "Выписка по счёту дебетовой карты Страница 2 из 2",
            "ДАТА ОПЕРАЦИИ (МСК) КАТЕГОРИЯ СУММА В ВАЛЮТЕ СЧЁТА ОСТАТОК СРЕДСТВ",
            "11.06.2026 09:00 Рестораны и кафе 50,00 750,00",
            "11.06.2026 222333 CAFE. Операция по карте ****1234",
            "*",
            "Дата формирования документа 13.06.2026",
            "40601D00C08FCE2CD999F93A68651986",
            "ПАО Сбербанк. Генеральная лицензия."
        };

        var parsed = parser.ParseExtractedLines(lines, "boundary.pdf");

        Assert.Collection(
            parsed.Rows,
            first => Assert.Equal("MARKET CITY", first.Description),
            second => Assert.Equal("CAFE", second.Description));
    }
}
