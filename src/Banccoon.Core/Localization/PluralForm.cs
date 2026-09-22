namespace Banccoon.Core.Localization;

// The CLDR cardinal-plural categories actually needed for English (One/Other) and Russian
// (One/Few/Many) integer counts. Not a full CLDR category set (no Zero/Two) - neither language
// this app supports uses them for cardinal numbers.
public enum PluralForm
{
    One,
    Few,
    Many,
    Other
}
