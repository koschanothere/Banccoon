using System.Collections.ObjectModel;

namespace Banccoon.App.ViewModels;

// Brings a bound collection in line with the list it should show, touching only what differs:
// items no longer wanted are removed, missing ones inserted at their place. Rows already on screen
// are never removed and re-added, so their views (and the pickers in them) aren't rebuilt just
// because a neighbour came or went. UI-thread only.
public static class CollectionSync
{
    public static void Apply<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
        where T : class
    {
        var wanted = new HashSet<T>(desired, ReferenceEqualityComparer.Instance);
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(target[index]))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            if (index < target.Count && ReferenceEquals(target[index], desired[index]))
            {
                continue;
            }

            var current = target.IndexOf(desired[index]);
            if (current >= 0)
            {
                target.Move(current, index);
            }
            else
            {
                target.Insert(index, desired[index]);
            }
        }
    }
}
