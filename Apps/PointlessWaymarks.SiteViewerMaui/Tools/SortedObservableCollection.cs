using System.Collections.ObjectModel;

namespace PointlessWaymarks.SiteViewerMaui.Tools;

/// <summary>
///     An <see cref="ObservableCollection{T}" /> that keeps its items sorted according to an <see cref="IComparer{T}" />.
///     When items are added or inserted, they are placed at the sorted index.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class SortedObservableCollection<T> : ObservableCollection<T>
{
    private readonly IComparer<T> _comparer;

    public SortedObservableCollection(IComparer<T> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    public SortedObservableCollection(Comparison<T> comparison)
        : this(Comparer<T>.Create(comparison))
    {
    }

    protected override void InsertItem(int index, T item)
    {
        var targetIndex = 0;
        while (targetIndex < Count && _comparer.Compare(item, this[targetIndex]) > 0)
        {
            targetIndex++;
        }

        base.InsertItem(targetIndex, item);
    }

    protected override void SetItem(int index, T item)
    {
        base.RemoveAt(index);
        InsertItem(0, item);
    }
}
