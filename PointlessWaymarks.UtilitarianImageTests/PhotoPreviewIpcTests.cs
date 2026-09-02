using PointlessWaymarks.WpfCommon.PhotoPreview;

namespace PointlessWaymarks.UtilitarianImageTests;

[TestFixture]
public class PhotoPreviewIpcTests
{
    [Test]
    public void PhotoPreviewIpcEnvelope_Serialization_RoundTripsCorrectly()
    {
        var originalDto = new PhotoPreviewRequestIpcDto(
            @"C:\Photos\Test.jpg",
            "Test Photo",
            4,
            [@"C:\Photos\Next1.jpg", @"C:\Photos\Next2.jpg"]
        );

        var senderId = Guid.NewGuid();
        var envelope = PhotoPreviewIpcEnvelope.Create(PhotoPreviewIpcMessageType.PreviewRequest, originalDto, senderId);

        Assert.Multiple(() =>
        {
            Assert.That(envelope.MessageType, Is.EqualTo(PhotoPreviewIpcMessageType.PreviewRequest));
            Assert.That(envelope.SenderId, Is.EqualTo(senderId));
        });

        var deserializedDto = envelope.DeserializePayload<PhotoPreviewRequestIpcDto>();

        Assert.Multiple(() =>
        {
            Assert.That(deserializedDto, Is.Not.Null);
            Assert.That(deserializedDto!.FilePath, Is.EqualTo(originalDto.FilePath));
            Assert.That(deserializedDto.Title, Is.EqualTo(originalDto.Title));
            Assert.That(deserializedDto.Rating, Is.EqualTo(originalDto.Rating));
            Assert.That(deserializedDto.UpcomingFilePaths, Has.Count.EqualTo(2));
            Assert.That(deserializedDto.UpcomingFilePaths![0], Is.EqualTo(originalDto.UpcomingFilePaths![0]));
        });
    }

    [Test]
    public async Task PhotoPreviewIpcChannel_BiDirectionalCommunication_Succeeds()
    {
        var testChannelId = $"TestChannel-{Guid.NewGuid():N}";

        using var hostChannel = new PhotoPreviewIpcChannel(testChannelId);
        using var previewChannel = new PhotoPreviewIpcChannel(testChannelId);

        var previewRequestReceivedTcs = new TaskCompletionSource<PhotoPreviewRequestIpcDto>();
        var ratingChangedReceivedTcs = new TaskCompletionSource<PhotoItemRatingChangedIpcDto>();
        var navigateReceivedTcs = new TaskCompletionSource<PhotoPreviewNavigateIpcDto>();

        previewChannel.PreviewRequestReceived += (_, dto) => previewRequestReceivedTcs.TrySetResult(dto);
        hostChannel.RatingChangedReceived += (_, dto) => ratingChangedReceivedTcs.TrySetResult(dto);
        hostChannel.NavigateReceived += (_, dto) => navigateReceivedTcs.TrySetResult(dto);

        // Host sends preview request to standalone preview
        hostChannel.PublishPreviewRequest(@"C:\Photos\Sample.dng", "Sample RAW", 5, [@"C:\Photos\Sample2.dng"]);

        var receivedPreview = await Task.WhenAny(previewRequestReceivedTcs.Task, Task.Delay(3000));
        Assert.That(receivedPreview, Is.EqualTo(previewRequestReceivedTcs.Task), "Timed out waiting for PreviewRequest");

        var previewDto = await previewRequestReceivedTcs.Task;
        Assert.Multiple(() =>
        {
            Assert.That(previewDto.FilePath, Is.EqualTo(@"C:\Photos\Sample.dng"));
            Assert.That(previewDto.Title, Is.EqualTo("Sample RAW"));
            Assert.That(previewDto.Rating, Is.EqualTo(5));
            Assert.That(previewDto.UpcomingFilePaths, Has.Count.EqualTo(1));
        });

        // Standalone preview sends rating change back to host
        previewChannel.PublishRatingChanged(@"C:\Photos\Sample.dng", 3);

        var receivedRating = await Task.WhenAny(ratingChangedReceivedTcs.Task, Task.Delay(3000));
        Assert.That(receivedRating, Is.EqualTo(ratingChangedReceivedTcs.Task), "Timed out waiting for RatingChanged");

        var ratingDto = await ratingChangedReceivedTcs.Task;
        Assert.Multiple(() =>
        {
            Assert.That(ratingDto.FilePath, Is.EqualTo(@"C:\Photos\Sample.dng"));
            Assert.That(ratingDto.Rating, Is.EqualTo(3));
        });

        // Standalone preview sends navigation command to host
        previewChannel.PublishNavigate("Next");

        var receivedNav = await Task.WhenAny(navigateReceivedTcs.Task, Task.Delay(3000));
        Assert.That(receivedNav, Is.EqualTo(navigateReceivedTcs.Task), "Timed out waiting for Navigate");

        var navDto = await navigateReceivedTcs.Task;
        Assert.That(navDto.Direction, Is.EqualTo("Next"));
    }

    [Test]
    public void PhotoPreviewLauncher_FindPreviewGuiExecutable_FindsBinaryInDevelopmentEnvironment()
    {
        var exe = PhotoPreviewLauncher.FindPreviewGuiExecutable();
        Assert.That(exe, Is.Not.Null, "PhotoPreviewLauncher should find PointlessWaymarks.PhotoPreviewGui.exe");
        Assert.That(exe!.Exists, Is.True, $"File {exe.FullName} should exist");
    }
}
