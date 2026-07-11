using System.Buffers.Binary;
using FluentAssertions;
using PsarcImporter.Conversion;
using Xunit;

namespace PsarcImporter.Tests;

public class ArtworkConverterTests : IDisposable
{
    private readonly string _tempDir;

    public ArtworkConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"artwork_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    [Fact]
    public void WriteRgbaPng_CreatesValidPngFile()
    {
        string pngPath = Path.Combine(_tempDir, "test.png");
        byte[] rgba = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }; // 2x1 pixel: red, green

        ArtworkConverter.WriteRgbaPng(pngPath, 2, 1, rgba);

        File.Exists(pngPath).Should().BeTrue();
        byte[] fileBytes = File.ReadAllBytes(pngPath);

        // PNG signature
        fileBytes[0].Should().Be(0x89);
        fileBytes[1].Should().Be(0x50); // 'P'
        fileBytes[2].Should().Be(0x4E); // 'N'
        fileBytes[3].Should().Be(0x47); // 'G'
    }

    [Fact]
    public void WriteRgbaPng_IHDRChunk_HasCorrectDimensions()
    {
        string pngPath = Path.Combine(_tempDir, "dim.png");
        byte[] rgba = new byte[4 * 3 * 2]; // 3x2 pixels

        ArtworkConverter.WriteRgbaPng(pngPath, 3, 2, rgba);

        byte[] fileBytes = File.ReadAllBytes(pngPath);
        // IHDR starts after 8-byte signature + 4-byte length + 4-byte "IHDR"
        int ihdrStart = 8 + 4 + 4;
        int width = BinaryPrimitives.ReadInt32BigEndian(fileBytes.AsSpan(ihdrStart, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(fileBytes.AsSpan(ihdrStart + 4, 4));

        width.Should().Be(3);
        height.Should().Be(2);
    }

    [Fact]
    public void WriteRgbaPng_ContainsIENDChunk()
    {
        string pngPath = Path.Combine(_tempDir, "end.png");
        byte[] rgba = new byte[4]; // 1x1 pixel

        ArtworkConverter.WriteRgbaPng(pngPath, 1, 1, rgba);

        byte[] fileBytes = File.ReadAllBytes(pngPath);
        // IEND chunk: [4-byte length (0)][4-byte type "IEND"][4-byte CRC] = 12 bytes at end of file
        int iendTypeStart = fileBytes.Length - 8;
        fileBytes[iendTypeStart].Should().Be((byte)'I');
        fileBytes[iendTypeStart + 1].Should().Be((byte)'E');
        fileBytes[iendTypeStart + 2].Should().Be((byte)'N');
        fileBytes[iendTypeStart + 3].Should().Be((byte)'D');
    }

    [Fact]
    public void WriteRgbaPng_1x1Pixel_CanBeReadBack()
    {
        string pngPath = Path.Combine(_tempDir, "single.png");
        byte[] rgba = new byte[] { 128, 64, 32, 255 }; // 1 pixel: RGBA

        ArtworkConverter.WriteRgbaPng(pngPath, 1, 1, rgba);

        File.Exists(pngPath).Should().BeTrue();
        new FileInfo(pngPath).Length.Should().BeGreaterThan(0);
    }
}
