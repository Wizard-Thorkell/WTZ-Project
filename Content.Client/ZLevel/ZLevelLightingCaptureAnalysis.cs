// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.ZLevel;

internal readonly record struct ZLevelLightingCaptureColorSample(
    double Red,
    double Green,
    double Blue,
    double Alpha)
{
    public double Luminance => Red * 0.2126d + Green * 0.7152d + Blue * 0.0722d;

    public double DominanceMargin(char channel)
    {
        return channel switch
        {
            'R' => Red - Math.Max(Green, Blue),
            'G' => Green - Math.Max(Red, Blue),
            'B' => Blue - Math.Max(Red, Green),
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };
    }
}

/// <summary>
/// Deterministic, renderer-independent measurements used by the real-client lighting capture.
/// </summary>
internal static class ZLevelLightingCaptureAnalysis
{
    public const int SignatureWidth = 48;
    public const int SignatureHeight = 32;
    private const float ProbeHalfSize = 0.28f;

    public static ZLevelLightingCaptureColorSample SampleWorldRegion(
        Image<Rgba32> image,
        Vector2 worldPosition,
        Vector2 eyePosition,
        Vector2i logicalViewportSize)
    {
        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("Capture image must not be empty.", nameof(image));

        if (logicalViewportSize.X <= 0 || logicalViewportSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(logicalViewportSize));

        var renderScaleX = image.Width / (float) logicalViewportSize.X;
        var renderScaleY = image.Height / (float) logicalViewportSize.Y;
        var centerX = image.Width / 2f +
                      (worldPosition.X - eyePosition.X) * EyeManager.PixelsPerMeter * renderScaleX;
        var centerY = image.Height / 2f -
                      (worldPosition.Y - eyePosition.Y) * EyeManager.PixelsPerMeter * renderScaleY;
        var halfWidth = Math.Max(1, (int) MathF.Round(
            ProbeHalfSize * EyeManager.PixelsPerMeter * renderScaleX));
        var halfHeight = Math.Max(1, (int) MathF.Round(
            ProbeHalfSize * EyeManager.PixelsPerMeter * renderScaleY));

        var left = Math.Clamp((int) MathF.Floor(centerX) - halfWidth, 0, image.Width - 1);
        var right = Math.Clamp((int) MathF.Ceiling(centerX) + halfWidth, 0, image.Width - 1);
        var top = Math.Clamp((int) MathF.Floor(centerY) - halfHeight, 0, image.Height - 1);
        var bottom = Math.Clamp((int) MathF.Ceiling(centerY) + halfHeight, 0, image.Height - 1);

        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        var count = 0;

        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var pixel = image[x, y];
                red += pixel.R;
                green += pixel.G;
                blue += pixel.B;
                alpha += pixel.A;
                count++;
            }
        }

        if (count == 0)
            return default;

        return new ZLevelLightingCaptureColorSample(
            red / (double) count,
            green / (double) count,
            blue / (double) count,
            alpha / (double) count);
    }

    public static byte[] BuildSignature(Image<Rgba32> image)
    {
        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("Capture image must not be empty.", nameof(image));

        var signature = new byte[SignatureWidth * SignatureHeight * 3];
        var index = 0;
        for (var y = 0; y < SignatureHeight; y++)
        {
            var sourceY = Math.Min(
                image.Height - 1,
                (int) ((y + 0.5f) * image.Height / SignatureHeight));
            for (var x = 0; x < SignatureWidth; x++)
            {
                var sourceX = Math.Min(
                    image.Width - 1,
                    (int) ((x + 0.5f) * image.Width / SignatureWidth));
                var pixel = image[sourceX, sourceY];
                signature[index++] = pixel.R;
                signature[index++] = pixel.G;
                signature[index++] = pixel.B;
            }
        }

        return signature;
    }

    public static byte[] BuildGridRegionSignature(
        Image<Rgba32> image,
        Box2 localBounds,
        Matrix3x2 gridWorldMatrix,
        Vector2 eyePosition,
        Vector2i logicalViewportSize)
    {
        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("Capture image must not be empty.", nameof(image));

        if (!localBounds.IsValid() || localBounds.Width <= 0f || localBounds.Height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(localBounds));

        if (logicalViewportSize.X <= 0 || logicalViewportSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(logicalViewportSize));

        var renderScaleX = image.Width / (float) logicalViewportSize.X;
        var renderScaleY = image.Height / (float) logicalViewportSize.Y;
        var signature = new byte[SignatureWidth * SignatureHeight * 3];
        var index = 0;
        for (var y = 0; y < SignatureHeight; y++)
        {
            var localY = localBounds.Bottom +
                         (y + 0.5f) * localBounds.Height / SignatureHeight;
            for (var x = 0; x < SignatureWidth; x++)
            {
                var localX = localBounds.Left +
                             (x + 0.5f) * localBounds.Width / SignatureWidth;
                var world = Vector2.Transform(new Vector2(localX, localY), gridWorldMatrix);
                var pixelX = (int) MathF.Round(
                    image.Width / 2f +
                    (world.X - eyePosition.X) * EyeManager.PixelsPerMeter * renderScaleX);
                var pixelY = (int) MathF.Round(
                    image.Height / 2f -
                    (world.Y - eyePosition.Y) * EyeManager.PixelsPerMeter * renderScaleY);

                if (pixelX < 0 || pixelX >= image.Width || pixelY < 0 || pixelY >= image.Height)
                {
                    index += 3;
                    continue;
                }

                var pixel = image[pixelX, pixelY];
                signature[index++] = pixel.R;
                signature[index++] = pixel.G;
                signature[index++] = pixel.B;
            }
        }

        return signature;
    }

    public static double SignatureDifference(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.Length == 0 || first.Length != second.Length)
            throw new ArgumentException("Capture signatures must have the same non-zero length.");

        double squared = 0d;
        for (var i = 0; i < first.Length; i++)
        {
            var difference = first[i] - second[i];
            squared += difference * difference;
        }

        return Math.Sqrt(squared / first.Length) / byte.MaxValue;
    }

    public static double SignatureLuminance(ReadOnlySpan<byte> signature)
    {
        if (signature.Length == 0 || signature.Length % 3 != 0)
            throw new ArgumentException("Capture signature must contain RGB triplets.", nameof(signature));

        double luminance = 0d;
        for (var i = 0; i < signature.Length; i += 3)
        {
            luminance += signature[i] * 0.2126d +
                         signature[i + 1] * 0.7152d +
                         signature[i + 2] * 0.0722d;
        }

        return luminance / (signature.Length / 3);
    }
}
