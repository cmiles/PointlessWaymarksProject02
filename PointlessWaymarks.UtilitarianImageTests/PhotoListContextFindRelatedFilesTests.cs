using NUnit.Framework;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

namespace PointlessWaymarks.UtilitarianImageTests;

public class PhotoListContextFindRelatedFilesTests
{
    private DirectoryInfo? _testDirectory;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = FileLocationTools.TempStorageDirectorySubdirectory();
    }

    [TearDown]
    public void TearDown()
    {
        if (_testDirectory is { Exists: true })
        {
            try
            {
                _testDirectory.Delete(true);
            }
            catch
            {
                // ignored
            }
        }
    }

    [Test]
    public void FindRelatedFiles_PixelPhotosWithDatePrefix_AreNotGrouped()
    {
        var file1 = new FileInfo(Path.Combine(_testDirectory!.FullName, "PXL_20260817_161611359.jpg"));
        var file2 = new FileInfo(Path.Combine(_testDirectory.FullName, "PXL_20260817_185128224.jpg"));
        File.WriteAllText(file1.FullName, "test1");
        File.WriteAllText(file2.FullName, "test2");

        var relatedToFile1 = PhotoListContext.FindRelatedFiles(file1);
        Assert.That(relatedToFile1.Select(x => x.Name).ToList(), Is.EquivalentTo(new[] { file1.Name }));

        var relatedToFile2 = PhotoListContext.FindRelatedFiles(file2);
        Assert.That(relatedToFile2.Select(x => x.Name).ToList(), Is.EquivalentTo(new[] { file2.Name }));
    }

    [Test]
    public void FindRelatedFiles_SameBaseNameDifferentExtension_AreGrouped()
    {
        var jpg1 = new FileInfo(Path.Combine(_testDirectory!.FullName, "PXL_20260817_161611359.jpg"));
        var dng1 = new FileInfo(Path.Combine(_testDirectory.FullName, "PXL_20260817_161611359.dng"));
        var xmp1 = new FileInfo(Path.Combine(_testDirectory.FullName, "PXL_20260817_161611359.xmp"));
        var jpg2 = new FileInfo(Path.Combine(_testDirectory.FullName, "PXL_20260817_185128224.jpg"));

        File.WriteAllText(jpg1.FullName, "test");
        File.WriteAllText(dng1.FullName, "test");
        File.WriteAllText(xmp1.FullName, "test");
        File.WriteAllText(jpg2.FullName, "test");

        var related = PhotoListContext.FindRelatedFiles(jpg1);
        Assert.That(related.Select(x => x.Name).ToList(),
            Is.EquivalentTo(new[] { jpg1.Name, dng1.Name, xmp1.Name }));
    }

    [Test]
    public void FindRelatedFiles_CameraSequenceNumberFiles_AreNotGrouped()
    {
        var dsc1 = new FileInfo(Path.Combine(_testDirectory!.FullName, "_DSC1234.NEF"));
        var dsc2 = new FileInfo(Path.Combine(_testDirectory.FullName, "_DSC1235.NEF"));
        var img1 = new FileInfo(Path.Combine(_testDirectory.FullName, "IMG_0001.CR3"));
        var img2 = new FileInfo(Path.Combine(_testDirectory.FullName, "IMG_0002.CR3"));

        File.WriteAllText(dsc1.FullName, "test");
        File.WriteAllText(dsc2.FullName, "test");
        File.WriteAllText(img1.FullName, "test");
        File.WriteAllText(img2.FullName, "test");

        var relatedDsc1 = PhotoListContext.FindRelatedFiles(dsc1);
        Assert.That(relatedDsc1.Select(x => x.Name).ToList(), Is.EquivalentTo(new[] { dsc1.Name }));

        var relatedImg1 = PhotoListContext.FindRelatedFiles(img1);
        Assert.That(relatedImg1.Select(x => x.Name).ToList(), Is.EquivalentTo(new[] { img1.Name }));
    }

    [Test]
    public void FindRelatedFiles_NumberedSuffixVariants_WithRootFile_AreGrouped()
    {
        var root = new FileInfo(Path.Combine(_testDirectory!.FullName, "photo.jpg"));
        var variant1 = new FileInfo(Path.Combine(_testDirectory.FullName, "photo_1.jpg"));
        var variant2 = new FileInfo(Path.Combine(_testDirectory.FullName, "photo-2.jpg"));
        var variant3 = new FileInfo(Path.Combine(_testDirectory.FullName, "photo_03.jpg"));
        var other = new FileInfo(Path.Combine(_testDirectory.FullName, "other.jpg"));

        File.WriteAllText(root.FullName, "test");
        File.WriteAllText(variant1.FullName, "test");
        File.WriteAllText(variant2.FullName, "test");
        File.WriteAllText(variant3.FullName, "test");
        File.WriteAllText(other.FullName, "test");

        var relatedFromRoot = PhotoListContext.FindRelatedFiles(root);
        Assert.That(relatedFromRoot.Select(x => x.Name).ToList(),
            Is.EquivalentTo(new[] { root.Name, variant1.Name, variant2.Name, variant3.Name }));

        var relatedFromVariant = PhotoListContext.FindRelatedFiles(variant1);
        Assert.That(relatedFromVariant.Select(x => x.Name).ToList(),
            Is.EquivalentTo(new[] { root.Name, variant1.Name, variant2.Name, variant3.Name }));
    }

    [Test]
    public void FindRelatedFiles_NumberedSuffixMoreThan2Digits_AreNotGrouped()
    {
        var root = new FileInfo(Path.Combine(_testDirectory!.FullName, "photo.jpg"));
        var file3Digits = new FileInfo(Path.Combine(_testDirectory.FullName, "photo_123.jpg"));
        var file3DigitsDash = new FileInfo(Path.Combine(_testDirectory.FullName, "photo-123.jpg"));

        File.WriteAllText(root.FullName, "test");
        File.WriteAllText(file3Digits.FullName, "test");
        File.WriteAllText(file3DigitsDash.FullName, "test");

        var related = PhotoListContext.FindRelatedFiles(root);
        Assert.That(related.Select(x => x.Name).ToList(), Is.EquivalentTo(new[] { root.Name }));
    }

    [Test]
    public void FindRelatedFiles_BaseAndEditedVariants_AreGrouped()
    {
        var baseFile = new FileInfo(Path.Combine(_testDirectory!.FullName, "2026_Base.orf"));
        var edited = new FileInfo(Path.Combine(_testDirectory.FullName, "2026_Base-Deep Prime 3.jpg"));
        var other = new FileInfo(Path.Combine(_testDirectory.FullName, "2026_Other.orf"));

        File.WriteAllText(baseFile.FullName, "test");
        File.WriteAllText(edited.FullName, "test");
        File.WriteAllText(other.FullName, "test");

        var relatedFromBase = PhotoListContext.FindRelatedFiles(baseFile);
        Assert.That(relatedFromBase.Select(x => x.Name).ToList(),
            Is.EquivalentTo(new[] { baseFile.Name, edited.Name }));

        var relatedFromEdited = PhotoListContext.FindRelatedFiles(edited);
        Assert.That(relatedFromEdited.Select(x => x.Name).ToList(),
            Is.EquivalentTo(new[] { baseFile.Name, edited.Name }));
    }
}
