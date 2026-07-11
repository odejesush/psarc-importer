using FluentAssertions;
using PsarcImporter.Conversion;
using Xunit;

namespace PsarcImporter.Tests;

public class AudioConverterTests : IDisposable
{
    private readonly string _tempDir;

    public AudioConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"audio_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    [Fact]
    public void SelectPrimaryAudioPath_NoOggFiles_ReturnsNull()
    {
        string? result = AudioConverter.SelectPrimaryAudioPath(_tempDir);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectPrimaryAudioPath_SingleOgg_ReturnsIt()
    {
        File.WriteAllText(Path.Combine(_tempDir, "song.ogg"), "");

        string? result = AudioConverter.SelectPrimaryAudioPath(_tempDir);

        result.Should().NotBeNull();
        Path.GetFileName(result).Should().Be("song.ogg");
    }

    [Fact]
    public void SelectPrimaryAudioPath_ExcludesPreview()
    {
        File.WriteAllText(Path.Combine(_tempDir, "song.ogg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "song_preview.ogg"), "");

        string? result = AudioConverter.SelectPrimaryAudioPath(_tempDir);

        result.Should().NotBeNull();
        Path.GetFileName(result).Should().Be("song.ogg");
    }

    [Fact]
    public void SelectPrimaryAudioPath_OnlyPreview_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "song_preview.ogg"), "");

        string? result = AudioConverter.SelectPrimaryAudioPath(_tempDir);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectPreviewAudioPath_NoOggFiles_ReturnsNull()
    {
        string? result = AudioConverter.SelectPreviewAudioPath(_tempDir);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectPreviewAudioPath_SinglePreview_ReturnsIt()
    {
        File.WriteAllText(Path.Combine(_tempDir, "song_preview.ogg"), "");

        string? result = AudioConverter.SelectPreviewAudioPath(_tempDir);

        result.Should().NotBeNull();
        Path.GetFileName(result).Should().Be("song_preview.ogg");
    }

    [Fact]
    public void SelectPreviewAudioPath_ExcludesNonPreview()
    {
        File.WriteAllText(Path.Combine(_tempDir, "song.ogg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "song_preview.ogg"), "");

        string? result = AudioConverter.SelectPreviewAudioPath(_tempDir);

        result.Should().NotBeNull();
        Path.GetFileName(result).Should().Be("song_preview.ogg");
    }

    [Fact]
    public void SelectPrimaryAudioPath_MultipleOgg_SelectsFirst()
    {
        File.WriteAllText(Path.Combine(_tempDir, "b_song.ogg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "a_song.ogg"), "");

        string? result = AudioConverter.SelectPrimaryAudioPath(_tempDir);

        result.Should().NotBeNull();
        Path.GetFileName(result).Should().Be("a_song.ogg");
    }
}
