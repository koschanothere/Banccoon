using System.Globalization;
using System.Data.Common;

namespace Banccoon.Infrastructure.Database;

internal static class SqliteData
{
    private const string DateFormat = "yyyy-MM-dd";

    public static object ToDbValue(string? value)
    {
        return value is null ? DBNull.Value : value;
    }

    public static object ToDbValue(Guid? value)
    {
        return value.HasValue ? value.Value.ToString() : DBNull.Value;
    }

    public static object ToDbValue(DateOnly? value)
    {
        return value.HasValue ? value.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : DBNull.Value;
    }

    public static object ToDbValue(decimal? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : DBNull.Value;
    }

    public static string DateToText(DateOnly value)
    {
        return value.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public static string DecimalToText(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static DateOnly ReadDate(DbDataReader reader, string name)
    {
        return DateOnly.ParseExact(ReadString(reader, name), DateFormat, CultureInfo.InvariantCulture);
    }

    public static DateOnly? ReadNullableDate(DbDataReader reader, string name)
    {
        var value = ReadNullableString(reader, name);
        return value is null ? null : DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);
    }

    public static decimal ReadDecimal(DbDataReader reader, string name)
    {
        return decimal.Parse(ReadString(reader, name), CultureInfo.InvariantCulture);
    }

    public static decimal? ReadNullableDecimal(DbDataReader reader, string name)
    {
        var value = ReadNullableString(reader, name);
        return value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public static Guid ReadGuid(DbDataReader reader, string name)
    {
        return Guid.Parse(ReadString(reader, name));
    }

    public static Guid? ReadNullableGuid(DbDataReader reader, string name)
    {
        var value = ReadNullableString(reader, name);
        return value is null ? null : Guid.Parse(value);
    }

    public static string ReadString(DbDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    public static string? ReadNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int? ReadNullableInt32(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static bool ReadBoolean(DbDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name)) != 0;
    }
}
