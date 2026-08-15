using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using SkiaSharp;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ScreenCaptureServiceTests
{
    [Fact]
    public void SelectFocusedDisplayIndex_UsesDisplayWithLargestForegroundIntersection()
    {
        var displays = new[]
        {
            new NativeMethods.Rect { Left = 0, Top = 0, Right = 100, Bottom = 100 },
            new NativeMethods.Rect { Left = 100, Top = 0, Right = 220, Bottom = 100 }
        };
        var foregroundWindow = new NativeMethods.Rect { Left = 90, Top = 10, Right = 180, Bottom = 80 };

        var focusedDisplay = ScreenCaptureService.SelectFocusedDisplayIndex(displays, foregroundWindow);

        Assert.Equal(1, focusedDisplay);
    }

    [Fact]
    public void WriteWebpArtifacts_UsesOneBitmapAndCreatesNoTemporaryPng()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trackmeup-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var rawPath = Path.Combine(directory, "capture-raw.webp");
        var storedPath = Path.Combine(directory, "capture.webp");

        try
        {
            using var bitmap = new Bitmap(640, 180, PixelFormat.Format32bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(24, 80, 160));
            }

            ScreenCaptureService.WriteWebpArtifacts(
                bitmap,
                rawPath,
                storedPath,
                includeWatermark: true,
                watermarkText: "TEST MACHINE  ·  2026-08-15 16:30:00  ·  capture  ·  Monitor 1");

            Assert.True(File.Exists(rawPath));
            Assert.True(File.Exists(storedPath));
            Assert.Empty(Directory.GetFiles(directory, "*.png"));
            Assert.False(File.ReadAllBytes(rawPath).SequenceEqual(File.ReadAllBytes(storedPath)));

            using var raw = SKBitmap.Decode(rawPath);
            using var stored = SKBitmap.Decode(storedPath);
            Assert.NotNull(raw);
            Assert.NotNull(stored);
            Assert.Equal(640, raw.Width);
            Assert.Equal(180, raw.Height);
            Assert.Equal(raw.Width, stored.Width);
            Assert.Equal(raw.Height, stored.Height);
            var rawPixel = raw.GetPixel(32, 32);
            Assert.InRange(rawPixel.Red, (byte)16, (byte)32);
            Assert.InRange(rawPixel.Green, (byte)72, (byte)88);
            Assert.InRange(rawPixel.Blue, (byte)152, (byte)168);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteWebpArtifacts_WithoutWatermark_WritesOnlyRawArtifact()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trackmeup-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var rawPath = Path.Combine(directory, "capture-raw.webp");
        var storedPath = Path.Combine(directory, "capture.webp");

        try
        {
            using var bitmap = new Bitmap(64, 32, PixelFormat.Format32bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.CornflowerBlue);
            }

            ScreenCaptureService.WriteWebpArtifacts(
                bitmap,
                rawPath,
                storedPath,
                includeWatermark: false,
                watermarkText: string.Empty);

            Assert.True(File.Exists(rawPath));
            Assert.False(File.Exists(storedPath));
            using var decoded = SKBitmap.Decode(rawPath);
            Assert.NotNull(decoded);
            Assert.Equal(64, decoded.Width);
            Assert.Equal(32, decoded.Height);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
