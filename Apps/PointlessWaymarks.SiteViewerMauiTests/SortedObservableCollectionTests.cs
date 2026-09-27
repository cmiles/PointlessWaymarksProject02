using NUnit.Framework;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.Tools;

namespace PointlessWaymarks.SiteViewerMauiTests;

[TestFixture]
public class SortedObservableCollectionTests
{
    [Test]
    public void SortedObservableCollection_WithString_MaintainsSortOrderOnAdd()
    {
        var collection = new SortedObservableCollection<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Delta",
            "Alpha",
            "Charlie",
            "Bravo"
        };

        Assert.That(collection.ToList(), Is.EqualTo(new[] { "Alpha", "Bravo", "Charlie", "Delta" }));
    }

    [Test]
    public void SortedObservableCollection_WithCloudViewerProfile_MaintainsAlphabeticalSortByName()
    {
        var collection = new SortedObservableCollection<CloudViewerProfile>(
            Comparer<CloudViewerProfile>.Create((a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a is null) return -1;
                if (b is null) return 1;
                var nameComparison = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty);
                return nameComparison != 0 ? nameComparison : a.Id.CompareTo(b.Id);
            }));

        var zebra = new CloudViewerProfile { Id = Guid.NewGuid(), Name = "Zebra" };
        var apple = new CloudViewerProfile { Id = Guid.NewGuid(), Name = "Apple" };
        var mango = new CloudViewerProfile { Id = Guid.NewGuid(), Name = "mango" };
        var banana = new CloudViewerProfile { Id = Guid.NewGuid(), Name = "Banana" };

        collection.Add(zebra);
        collection.Add(apple);
        collection.Add(mango);
        collection.Add(banana);

        Assert.That(collection.Select(x => x.Name).ToList(),
            Is.EqualTo(new[] { "Apple", "Banana", "mango", "Zebra" }));
    }

    [Test]
    public void SortedObservableCollection_WithDuplicateNames_MaintainsDeterministicSortById()
    {
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var collection = new SortedObservableCollection<CloudViewerProfile>(
            Comparer<CloudViewerProfile>.Create((a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a is null) return -1;
                if (b is null) return 1;
                var nameComparison = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty);
                return nameComparison != 0 ? nameComparison : a.Id.CompareTo(b.Id);
            }));

        var item2 = new CloudViewerProfile { Id = id2, Name = "Duplicate" };
        var item1 = new CloudViewerProfile { Id = id1, Name = "Duplicate" };

        collection.Add(item2);
        collection.Add(item1);

        Assert.That(collection.Select(x => x.Id).ToList(), Is.EqualTo(new[] { id1, id2 }));
    }
}
