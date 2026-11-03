using NUnit.Framework;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.Utility;
using SkiaSharp;

namespace PointlessWaymarks.UtilitarianImageTests;

[TestFixture]
public class SingleColumnImageStitcherTests
{
    private static SKBitmap CreateSyntheticDocumentImage(int width, int height, int headerHeight, int footerHeight,
        int pageOffset, SKColor headerColor, SKColor footerColor)
    {
        return CreateSyntheticDocumentImageWithInnerBars(width, height, headerHeight, footerHeight, 0, 0, pageOffset,
            headerColor, footerColor, SKColors.Transparent, SKColors.Transparent);
    }

    private static SKBitmap CreateSyntheticDocumentImageWithInnerBars(int width, int height,
        int globalHeaderHeight, int globalFooterHeight,
        int innerHeaderHeight, int innerFooterHeight,
        int pageOffset, SKColor globalHeaderColor, SKColor globalFooterColor,
        SKColor innerHeaderColor, SKColor innerFooterColor)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // Draw global header
        if (globalHeaderHeight > 0)
        {
            using var headerPaint = new SKPaint { Color = globalHeaderColor };
            canvas.DrawRect(new SKRect(0, 0, width, globalHeaderHeight), headerPaint);
            // Draw a distinct pattern in header
            using var linePaint = new SKPaint { Color = SKColors.Yellow, StrokeWidth = 2 };
            canvas.DrawLine(5, globalHeaderHeight / 2f, width - 5, globalHeaderHeight / 2f, linePaint);
        }

        // Draw inner header
        if (innerHeaderHeight > 0)
        {
            using var innerHeaderPaint = new SKPaint { Color = innerHeaderColor };
            canvas.DrawRect(new SKRect(0, globalHeaderHeight, width, globalHeaderHeight + innerHeaderHeight), innerHeaderPaint);
            using var linePaint = new SKPaint { Color = SKColors.White, StrokeWidth = 1 };
            canvas.DrawLine(2, globalHeaderHeight + innerHeaderHeight / 2f, width - 2, globalHeaderHeight + innerHeaderHeight / 2f, linePaint);
        }

        // Draw body content that varies with pageOffset
        var bodyTop = globalHeaderHeight + innerHeaderHeight;
        var bodyBottom = height - (globalFooterHeight + innerFooterHeight);

        for (var y = bodyTop; y < bodyBottom; y++)
        {
            var pageY = pageOffset + (y - bodyTop);

            // Draw horizontal stripe patterns every 20 page pixels with alternating colors
            var stripeIndex = pageY / 20;
            var colorInStripe = (byte)((stripeIndex * 37) % 200 + 30);
            var lineColor = new SKColor(colorInStripe, (byte)(255 - colorInStripe), (byte)((pageY * 7) % 255));

            using var paint = new SKPaint { Color = lineColor };
            // Draw a non-uniform pattern across x
            canvas.DrawPoint((pageY * 13) % width, y, paint);
            canvas.DrawLine(0, y, width / 2f, y, paint);
        }

        // Draw inner footer
        if (innerFooterHeight > 0)
        {
            var innerFooterTop = height - (globalFooterHeight + innerFooterHeight);
            using var innerFooterPaint = new SKPaint { Color = innerFooterColor };
            canvas.DrawRect(new SKRect(0, innerFooterTop, width, innerFooterTop + innerFooterHeight), innerFooterPaint);
        }

        // Draw global footer
        if (globalFooterHeight > 0)
        {
            using var footerPaint = new SKPaint { Color = globalFooterColor };
            canvas.DrawRect(new SKRect(0, height - globalFooterHeight, width, height), footerPaint);
            using var linePaint = new SKPaint { Color = SKColors.Cyan, StrokeWidth = 2 };
            canvas.DrawLine(10, height - globalFooterHeight / 2f, width - 10, height - globalFooterHeight / 2f, linePaint);
        }

        return bitmap;
    }

    [Test]
    public void DetectStaticHeaderHeight_WithKnownHeader_DetectsCorrectHeight()
    {
        const int width = 200;
        const int height = 400;
        const int headerHeight = 35;
        const int footerHeight = 25;

        using var img1 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 0, SKColors.Red, SKColors.Blue);
        using var img2 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 80, SKColors.Red, SKColors.Blue);
        using var img3 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 160, SKColors.Red, SKColors.Blue);

        var detectedHeader = SingleColumnImageStitcher.DetectStaticHeaderHeight([img1, img2, img3]);
        Assert.That(detectedHeader, Is.EqualTo(headerHeight));
    }

    [Test]
    public void DetectStaticFooterHeight_WithKnownFooter_DetectsCorrectHeight()
    {
        const int width = 200;
        const int height = 400;
        const int headerHeight = 35;
        const int footerHeight = 28;

        using var img1 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 0, SKColors.Red, SKColors.Blue);
        using var img2 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 80, SKColors.Red, SKColors.Blue);
        using var img3 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 160, SKColors.Red, SKColors.Blue);

        var detectedFooter = SingleColumnImageStitcher.DetectStaticFooterHeight([img1, img2, img3]);
        Assert.That(detectedFooter, Is.EqualTo(footerHeight));
    }

    [Test]
    public void DetectStaticHeaderAndFooter_WhenNone_ReturnsZero()
    {
        const int width = 200;
        const int height = 400;

        using var img1 = CreateSyntheticDocumentImage(width, height, 0, 0, 0, SKColors.Transparent, SKColors.Transparent);
        using var img2 = CreateSyntheticDocumentImage(width, height, 0, 0, 100, SKColors.Transparent, SKColors.Transparent);

        var detectedHeader = SingleColumnImageStitcher.DetectStaticHeaderHeight([img1, img2]);
        var detectedFooter = SingleColumnImageStitcher.DetectStaticFooterHeight([img1, img2]);

        Assert.That(detectedHeader, Is.EqualTo(0));
        Assert.That(detectedFooter, Is.EqualTo(0));
    }

    [Test]
    public void FindBestVerticalOffset_WithKnownShift_ReturnsExpectedShift()
    {
        const int width = 200;
        const int height = 400;
        const int headerHeight = 30;
        const int footerHeight = 20;
        const int scrollDelta = 65;

        using var img1 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 0, SKColors.Red, SKColors.Blue);
        using var img2 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, scrollDelta, SKColors.Red, SKColors.Blue);

        var activeTopA = 0;
        var activeBottomA = height - footerHeight;
        var activeTopB = headerHeight;
        var activeBottomB = height;

        var shift = SingleColumnImageStitcher.FindBestVerticalOffset(img1, img2, activeTopA, activeBottomA, activeTopB, activeBottomB);

        Assert.That(shift, Is.EqualTo(scrollDelta));
    }

    [Test]
    public void StitchVertical_TwoImagesWithHeaderAndFooter_HeaderAppearsOnlyOnceAndFooterOnlyOnce()
    {
        const int width = 200;
        const int height = 400;
        const int headerHeight = 30;
        const int footerHeight = 25;
        const int scrollDelta = 100;

        using var img1 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 0, SKColors.Red, SKColors.Blue);
        using var img2 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, scrollDelta, SKColors.Red, SKColors.Blue);

        using var stitched = SingleColumnImageStitcher.StitchVertical([img1, img2]);

        Assert.That(stitched.Width, Is.EqualTo(width));
        // Expected height = height + scrollDelta = 400 + 100 = 500
        Assert.That(stitched.Height, Is.EqualTo(height + scrollDelta));

        // Top header rows should match headerColor (Red)
        for (var y = 0; y < headerHeight; y++)
        {
            var pixel = stitched.GetPixel(5, y);
            Assert.That(pixel.Red, Is.GreaterThan(200));
            Assert.That(pixel.Blue, Is.LessThan(50));
        }

        // Just after header (row 31) should NOT be header
        var justAfterHeader = stitched.GetPixel(5, headerHeight + 1);
        Assert.That(justAfterHeader, Is.Not.EqualTo(SKColors.Red));

        // Bottom footer rows should match footerColor (Blue)
        for (var y = stitched.Height - footerHeight; y < stitched.Height; y++)
        {
            var pixel = stitched.GetPixel(5, y);
            Assert.That(pixel.Blue, Is.GreaterThan(200));
            Assert.That(pixel.Red, Is.LessThan(50));
        }

        // Just above footer should NOT be footer
        var justAboveFooter = stitched.GetPixel(5, stitched.Height - footerHeight - 2);
        Assert.That(justAboveFooter, Is.Not.EqualTo(SKColors.Blue));
    }

    [Test]
    public void StitchVertical_ThreeImages_StitchesAllSequences()
    {
        const int width = 150;
        const int height = 300;
        const int headerHeight = 20;
        const int footerHeight = 15;
        const int shift1 = 70;
        const int shift2 = 80;

        using var img1 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, 0, SKColors.Green, SKColors.Magenta);
        using var img2 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, shift1, SKColors.Green, SKColors.Magenta);
        using var img3 = CreateSyntheticDocumentImage(width, height, headerHeight, footerHeight, shift1 + shift2, SKColors.Green, SKColors.Magenta);

        using var stitched = SingleColumnImageStitcher.StitchVertical([img1, img2, img3]);

        Assert.That(stitched.Width, Is.EqualTo(width));
        Assert.That(stitched.Height, Is.EqualTo(height + shift1 + shift2));
    }

    [Test]
    public void StitchVertical_SingleImage_ReturnsMatchingClone()
    {
        const int width = 100;
        const int height = 150;
        using var img = CreateSyntheticDocumentImage(width, height, 10, 10, 0, SKColors.Red, SKColors.Blue);

        using var stitched = SingleColumnImageStitcher.StitchVertical([img]);

        Assert.That(stitched.Width, Is.EqualTo(width));
        Assert.That(stitched.Height, Is.EqualTo(height));
        Assert.That(stitched.GetPixel(10, 5), Is.EqualTo(img.GetPixel(10, 5)));
    }

    [Test]
    public async Task StitchImageBytesVertical_EncodesAndReturnsValidJpegBytes()
    {
        const int width = 120;
        const int height = 200;
        using var img1 = CreateSyntheticDocumentImage(width, height, 20, 20, 0, SKColors.Green, SKColors.Blue);
        using var img2 = CreateSyntheticDocumentImage(width, height, 20, 20, 50, SKColors.Green, SKColors.Blue);

        using var skImg1 = SKImage.FromBitmap(img1);
        using var data1 = skImg1.Encode(SKEncodedImageFormat.Png, 100);
        using var skImg2 = SKImage.FromBitmap(img2);
        using var data2 = skImg2.Encode(SKEncodedImageFormat.Png, 100);

        var stitchedBytes = await SingleColumnImageStitcher.StitchImageBytesVertical([data1.ToArray(), data2.ToArray()]);
        Assert.That(stitchedBytes, Is.Not.Empty);

        using var decoded = SKBitmap.Decode(stitchedBytes);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded.Width, Is.EqualTo(width));
        Assert.That(decoded.Height, Is.EqualTo(250));
    }

    [Test]
    public async Task StitchImagesVertical_FileCombiner_CreatesExpectedOutputFile()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var file1 = Path.Combine(tempDir.FullName, "img1.png");
            var file2 = Path.Combine(tempDir.FullName, "img2.png");
            var output = Path.Combine(tempDir.FullName, "stitched.jpg");

            const int width = 120;
            const int height = 200;
            using var img1 = CreateSyntheticDocumentImage(width, height, 15, 15, 0, SKColors.Red, SKColors.Yellow);
            using var img2 = CreateSyntheticDocumentImage(width, height, 15, 15, 40, SKColors.Red, SKColors.Yellow);

            using (var skImg1 = SKImage.FromBitmap(img1))
            using (var data1 = skImg1.Encode(SKEncodedImageFormat.Png, 100))
            await using (var stream = File.OpenWrite(file1))
            {
                data1.SaveTo(stream);
            }

            using (var skImg2 = SKImage.FromBitmap(img2))
            using (var data2 = skImg2.Encode(SKEncodedImageFormat.Png, 100))
            await using (var stream = File.OpenWrite(file2))
            {
                data2.SaveTo(stream);
            }

            var result = await PointlessWaymarks.UtilitarianImage.Combiner.StitchImagesVertical([file1, file2], output, 100, new ConsoleProgress());

            Assert.That(File.Exists(result), Is.True);
            using var decoded = SKBitmap.Decode(result);
            Assert.That(decoded.Width, Is.EqualTo(width));
            Assert.That(decoded.Height, Is.EqualTo(240));
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    [Test]
    public void DetectInnerHeaderHeight_WithKnownInnerHeader_DetectsCorrectHeight()
    {
        const int width = 200;
        const int height = 400;
        const int globalHeaderHeight = 30;
        const int innerHeaderHeight = 45;
        const int globalFooterHeight = 20;

        using var img1 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, 0, 0, 0,
            SKColors.Red, SKColors.Blue, SKColors.Magenta, SKColors.Transparent);
        using var img2 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, innerHeaderHeight, 0, 80,
            SKColors.Red, SKColors.Blue, SKColors.Magenta, SKColors.Transparent);
        using var img3 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, innerHeaderHeight, 0, 160,
            SKColors.Red, SKColors.Blue, SKColors.Magenta, SKColors.Transparent);
        using var img4 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, innerHeaderHeight, 0, 240,
            SKColors.Red, SKColors.Blue, SKColors.Magenta, SKColors.Transparent);

        var detectedGlobalHeader = SingleColumnImageStitcher.DetectStaticHeaderHeight([img1, img2, img3, img4]);
        var detectedInnerHeader = SingleColumnImageStitcher.DetectInnerHeaderHeight([img1, img2, img3, img4], detectedGlobalHeader);

        Assert.That(detectedGlobalHeader, Is.EqualTo(globalHeaderHeight));
        Assert.That(detectedInnerHeader, Is.EqualTo(innerHeaderHeight));
    }

    [Test]
    public void DetectInnerFooterHeight_WithKnownInnerFooter_DetectsCorrectHeight()
    {
        const int width = 200;
        const int height = 400;
        const int globalHeaderHeight = 25;
        const int innerFooterHeight = 35;
        const int globalFooterHeight = 0;

        using var img1 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, 0, innerFooterHeight, 0,
            SKColors.Red, SKColors.Transparent, SKColors.Transparent, SKColors.Green);
        using var img2 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, 0, innerFooterHeight, 80,
            SKColors.Red, SKColors.Transparent, SKColors.Transparent, SKColors.Green);
        using var img3 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, 0, innerFooterHeight, 160,
            SKColors.Red, SKColors.Transparent, SKColors.Transparent, SKColors.Green);
        using var img4 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, 40, 0, 0, 240,
            SKColors.Red, SKColors.Blue, SKColors.Transparent, SKColors.Transparent);

        var detectedGlobalFooter = SingleColumnImageStitcher.DetectStaticFooterHeight([img1, img2, img3, img4]);
        var detectedInnerFooter = SingleColumnImageStitcher.DetectInnerFooterHeight([img1, img2, img3, img4], detectedGlobalFooter);

        Assert.That(detectedGlobalFooter, Is.EqualTo(0));
        Assert.That(detectedInnerFooter, Is.EqualTo(innerFooterHeight));
    }

    [Test]
    public void StitchVertical_WithInnerHeader_EliminatesInnerHeaderFromFinalImage()
    {
        const int width = 200;
        const int height = 400;
        const int globalHeaderHeight = 30;
        const int innerHeaderHeight = 40;
        const int globalFooterHeight = 20;
        const int shift1 = 70;
        const int shift2 = 70;
        var innerHeaderColor = new SKColor(255, 0, 128); // Distinct Deep Pink

        // Image 1: top of page (no inner scroll header)
        using var img1 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, 0, 0, 0,
            SKColors.Red, SKColors.Blue, innerHeaderColor, SKColors.Transparent);
        // Image 2: scrolled down (sticky inner header present)
        using var img2 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, innerHeaderHeight, 0, shift1,
            SKColors.Red, SKColors.Blue, innerHeaderColor, SKColors.Transparent);
        // Image 3: scrolled down further (sticky inner header present)
        using var img3 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, innerHeaderHeight, 0, shift1 + shift2,
            SKColors.Red, SKColors.Blue, innerHeaderColor, SKColors.Transparent);

        using var stitched = SingleColumnImageStitcher.StitchVertical([img1, img2, img3]);

        Assert.That(stitched.Width, Is.EqualTo(width));

        // Top rows should contain global header (Red)
        for (var y = 0; y < globalHeaderHeight; y++)
        {
            var pixel = stitched.GetPixel(2, y);
            Assert.That(pixel.Red, Is.GreaterThan(200));
            Assert.That(pixel.Blue, Is.LessThan(50));
        }

        // Search the ENTIRE stitched image for the inner header color - it should be completely eliminated!
        for (var y = 0; y < stitched.Height; y++)
        {
            var pixel = stitched.GetPixel(2, y);
            var isInnerHeader = pixel.Red == innerHeaderColor.Red &&
                                pixel.Green == innerHeaderColor.Green &&
                                pixel.Blue == innerHeaderColor.Blue;
            Assert.That(isInnerHeader, Is.False, $"Found inner header pixel at row {y} - it should have been eliminated!");
        }

        // Bottom rows should contain global footer (Blue)
        for (var y = stitched.Height - globalFooterHeight; y < stitched.Height; y++)
        {
            var pixel = stitched.GetPixel(2, y);
            Assert.That(pixel.Blue, Is.GreaterThan(200));
            Assert.That(pixel.Red, Is.LessThan(50));
        }
    }

    [Test]
    public void StitchVertical_WithInnerFooter_EliminatesInnerFooterFromFinalImage()
    {
        const int width = 200;
        const int height = 400;
        const int globalHeaderHeight = 30;
        const int innerFooterHeight = 35;
        const int globalFooterHeight = 20;
        const int shift1 = 70;
        const int shift2 = 70;
        var innerFooterColor = new SKColor(128, 0, 255); // Distinct Purple

        // Image 1 and 2 have a floating inner footer
        using var img1 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, 0, 0, innerFooterHeight, 0,
            SKColors.Red, SKColors.Transparent, SKColors.Transparent, innerFooterColor);
        using var img2 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, 0, 0, innerFooterHeight, shift1,
            SKColors.Red, SKColors.Transparent, SKColors.Transparent, innerFooterColor);
        // Image 3 has the actual global footer (Blue)
        using var img3 = CreateSyntheticDocumentImageWithInnerBars(width, height, globalHeaderHeight, globalFooterHeight, 0, 0, shift1 + shift2,
            SKColors.Red, SKColors.Blue, SKColors.Transparent, SKColors.Transparent);

        using var stitched = SingleColumnImageStitcher.StitchVertical([img1, img2, img3]);

        Assert.That(stitched.Width, Is.EqualTo(width));

        // Search the ENTIRE stitched image for the inner footer color - it should be completely eliminated!
        for (var y = 0; y < stitched.Height; y++)
        {
            var pixel = stitched.GetPixel(2, y);
            var isInnerFooter = pixel.Red == innerFooterColor.Red &&
                                pixel.Green == innerFooterColor.Green &&
                                pixel.Blue == innerFooterColor.Blue;
            Assert.That(isInnerFooter, Is.False, $"Found inner footer pixel at row {y} - it should have been eliminated!");
        }

        // Bottom rows should contain global footer (Blue)
        for (var y = stitched.Height - globalFooterHeight; y < stitched.Height; y++)
        {
            var pixel = stitched.GetPixel(2, y);
            Assert.That(pixel.Blue, Is.GreaterThan(200));
            Assert.That(pixel.Red, Is.LessThan(50));
        }
    }
}
