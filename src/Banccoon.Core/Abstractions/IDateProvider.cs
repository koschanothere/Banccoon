namespace Banccoon.Core.Abstractions;

public interface IDateProvider
{
    DateOnly Today { get; }
}
