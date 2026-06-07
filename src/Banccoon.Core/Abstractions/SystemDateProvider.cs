namespace Banccoon.Core.Abstractions;

public sealed class SystemDateProvider : IDateProvider
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
