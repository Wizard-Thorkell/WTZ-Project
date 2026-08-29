// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.Client.ZLevel;
using NUnit.Framework;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ZLevelLightingCaptureAnalysisTest
{
    [Test]
    public void WorldProbeMapsThroughLogicalViewportAndRenderScale()
    {
        using var image = new Image<Rgba32>(640, 640);
        Fill(image, new Rgba32(0, 0, 255, 255));

        // Logical (1, 1) maps to physical (384, 256) at render scale two.
        for (var y = 236; y <= 276; y++)
        {
            for (var x = 364; x <= 404; x++)
                image[x, y] = new Rgba32(255, 0, 0, 255);
        }

        var sample = ZLevelLightingCaptureAnalysis.SampleWorldRegion(
            image,
            new Vector2(1f, 1f),
            Vector2.Zero,
            new Vector2i(320, 320));

        Assert.Multiple(() =>
        {
            Assert.That(sample.Red, Is.EqualTo(255d));
            Assert.That(sample.Green, Is.Zero);
            Assert.That(sample.Blue, Is.Zero);
            Assert.That(sample.DominanceMargin('R'), Is.EqualTo(255d));
        });
    }

    [Test]
    public void SignaturesAreDeterministicAndNormalized()
    {
        using var black = new Image<Rgba32>(96, 64);
        using var white = new Image<Rgba32>(96, 64);
        Fill(black, new Rgba32(0, 0, 0, 255));
        Fill(white, new Rgba32(255, 255, 255, 255));

        var blackSignature = ZLevelLightingCaptureAnalysis.BuildSignature(black);
        var blackAgain = ZLevelLightingCaptureAnalysis.BuildSignature(black);
        var whiteSignature = ZLevelLightingCaptureAnalysis.BuildSignature(white);

        Assert.Multiple(() =>
        {
            Assert.That(blackSignature, Has.Length.EqualTo(
                ZLevelLightingCaptureAnalysis.SignatureWidth *
                ZLevelLightingCaptureAnalysis.SignatureHeight * 3));
            Assert.That(ZLevelLightingCaptureAnalysis.SignatureDifference(
                blackSignature, blackAgain), Is.Zero);
            Assert.That(ZLevelLightingCaptureAnalysis.SignatureDifference(
                blackSignature, whiteSignature), Is.EqualTo(1d).Within(1e-9));
            Assert.That(ZLevelLightingCaptureAnalysis.SignatureLuminance(blackSignature), Is.Zero);
            Assert.That(ZLevelLightingCaptureAnalysis.SignatureLuminance(whiteSignature),
                Is.EqualTo(255d).Within(1e-9));
        });
    }

    [Test]
    public void GridRegionSignatureTracksTheGridWorldTransform()
    {
        using var image = new Image<Rgba32>(320, 320);
        Fill(image, new Rgba32(0, 0, 255, 255));

        // Local 0..1 translated to world X 1..2 maps to this screen rectangle.
        for (var y = 127; y <= 161; y++)
        {
            for (var x = 191; x <= 225; x++)
                image[x, y] = new Rgba32(255, 0, 0, 255);
        }

        var signature = ZLevelLightingCaptureAnalysis.BuildGridRegionSignature(
            image,
            new Box2(0f, 0f, 1f, 1f),
            Matrix3x2.CreateTranslation(1f, 0f),
            Vector2.Zero,
            new Vector2i(320, 320));

        Assert.That(signature.Where((_, index) => index % 3 == 0), Is.All.EqualTo(255));
        Assert.That(signature.Where((_, index) => index % 3 != 0), Is.All.Zero);
    }

    private static void Fill(Image<Rgba32> image, Rgba32 color)
    {
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
                rows.GetRowSpan(y).Fill(color);
        });
    }
}
