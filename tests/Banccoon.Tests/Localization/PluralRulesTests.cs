using Banccoon.Core.Localization;
using Xunit;

namespace Banccoon.Tests.Localization;

public sealed class PluralRulesTests
{
    [Theory]
    [InlineData(1, PluralForm.One)]
    [InlineData(0, PluralForm.Other)]
    [InlineData(2, PluralForm.Other)]
    [InlineData(5, PluralForm.Other)]
    [InlineData(11, PluralForm.Other)]
    [InlineData(21, PluralForm.Other)]
    [InlineData(100, PluralForm.Other)]
    public void GetForm_English_HasOnlyOneAndOther(int count, PluralForm expected)
    {
        Assert.Equal(expected, PluralRules.GetForm(count, "en"));
    }

    [Theory]
    [InlineData(1, PluralForm.One)]
    [InlineData(21, PluralForm.One)]
    [InlineData(31, PluralForm.One)]
    [InlineData(101, PluralForm.One)]
    [InlineData(121, PluralForm.One)]
    public void GetForm_Russian_OneEndingInOneExceptEleven(int count, PluralForm expected)
    {
        Assert.Equal(expected, PluralRules.GetForm(count, "ru"));
    }

    [Theory]
    [InlineData(2, PluralForm.Few)]
    [InlineData(3, PluralForm.Few)]
    [InlineData(4, PluralForm.Few)]
    [InlineData(22, PluralForm.Few)]
    [InlineData(23, PluralForm.Few)]
    [InlineData(24, PluralForm.Few)]
    [InlineData(102, PluralForm.Few)]
    public void GetForm_Russian_FewEndingInTwoToFourExceptTwelveToFourteen(int count, PluralForm expected)
    {
        Assert.Equal(expected, PluralRules.GetForm(count, "ru"));
    }

    [Theory]
    [InlineData(0, PluralForm.Many)]
    [InlineData(5, PluralForm.Many)]
    [InlineData(6, PluralForm.Many)]
    [InlineData(9, PluralForm.Many)]
    [InlineData(10, PluralForm.Many)]
    [InlineData(11, PluralForm.Many)]
    [InlineData(12, PluralForm.Many)]
    [InlineData(13, PluralForm.Many)]
    [InlineData(14, PluralForm.Many)]
    [InlineData(20, PluralForm.Many)]
    [InlineData(25, PluralForm.Many)]
    [InlineData(100, PluralForm.Many)]
    [InlineData(111, PluralForm.Many)]
    [InlineData(112, PluralForm.Many)]
    [InlineData(114, PluralForm.Many)]
    public void GetForm_Russian_ManyForTeensAndZeroAndFivePlus(int count, PluralForm expected)
    {
        Assert.Equal(expected, PluralRules.GetForm(count, "ru"));
    }

    [Fact]
    public void GetForm_NegativeCount_UsesAbsoluteValue()
    {
        Assert.Equal(PluralForm.One, PluralRules.GetForm(-21, "ru"));
        Assert.Equal(PluralForm.Few, PluralRules.GetForm(-3, "ru"));
        Assert.Equal(PluralForm.Many, PluralRules.GetForm(-11, "ru"));
    }

    [Fact]
    public void GetForm_UnknownLanguageCode_FallsBackToEnglishRule()
    {
        Assert.Equal(PluralForm.One, PluralRules.GetForm(1, "fr"));
        Assert.Equal(PluralForm.Other, PluralRules.GetForm(21, "fr"));
    }
}
