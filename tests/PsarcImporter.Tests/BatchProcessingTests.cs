using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PsarcImporter.Tests;

public class BatchProcessingTests
{
    [Fact]
    public async Task Main_DirectoryWithNoPsarcFiles_Returns1()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            int exitCode = await Program.Main(new[]
            {
                "--source", tempDir,
                "--output", outputDir
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_EmptyDirectory_CreatesOutputDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            await Program.Main(new[]
            {
                "--source", tempDir,
                "--output", outputDir
            });

            Directory.Exists(outputDir).Should().BeTrue();
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_NonexistentDirectory_TreatedAsFile_Returns1()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            int exitCode = await Program.Main(new[]
            {
                "--source", "/nonexistent/directory",
                "--output", outputDir
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_DirectoryWithNonPsarcFiles_Returns1()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "not a psarc");
            File.WriteAllText(Path.Combine(tempDir, "song.zip"), "not a psarc");

            int exitCode = await Program.Main(new[]
            {
                "--source", tempDir,
                "--output", outputDir
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_DirectoryMode_CaseInsensitiveOutput()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            int exitCode = await Program.Main(new[]
            {
                "--SOURCE", tempDir,
                "--OUTPUT", outputDir
            });

            exitCode.Should().Be(1);
            Directory.Exists(outputDir).Should().BeTrue();
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public void GetSongKey_ReturnsTitleAndArtist()
    {
        string key = Program.GetSongKey("My Song", "My Artist");
        key.Should().Be("My Song|My Artist");
    }

    [Fact]
    public void GetSongKey_TrimsWhitespace()
    {
        string key = Program.GetSongKey("  Song  ", "  Artist  ");
        key.Should().Be("Song|Artist");
    }

    [Fact]
    public void LoadExistingSongMetadata_EmptyDirectory_ReturnsEmptySet()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_meta_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            var songs = Program.LoadExistingSongMetadata(outputDir);
            songs.Should().BeEmpty();
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public void LoadExistingSongMetadata_WithValidTheoryFile_ContainsMetadata()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_meta_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            CreateTestTheoryFile(Path.Combine(outputDir, "TestArtist - TestSong.theory"), "TestSong", "TestArtist");

            var songs = Program.LoadExistingSongMetadata(outputDir);

            songs.Should().Contain("TestSong|TestArtist");
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public void LoadExistingSongMetadata_WithMultipleTheoryFiles_ContainsAllMetadata()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_meta_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            CreateTestTheoryFile(Path.Combine(outputDir, "A - Song1.theory"), "Song1", "A");
            CreateTestTheoryFile(Path.Combine(outputDir, "B - Song2.theory"), "Song2", "B");

            var songs = Program.LoadExistingSongMetadata(outputDir);

            songs.Should().HaveCount(2);
            songs.Should().Contain("Song1|A");
            songs.Should().Contain("Song2|B");
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public void LoadExistingSongMetadata_WithCorruptFile_SkipsGracefully()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_meta_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "corrupt.theory"), "not a zip file");
            CreateTestTheoryFile(Path.Combine(outputDir, "valid.theory"), "ValidSong", "ValidArtist");

            var songs = Program.LoadExistingSongMetadata(outputDir);

            songs.Should().HaveCount(1);
            songs.Should().Contain("ValidSong|ValidArtist");
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public void LoadExistingSongMetadata_WithNoManifest_SkipsGracefully()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_meta_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            string theoryPath = Path.Combine(outputDir, "no_manifest.theory");
            using (var stream = File.Create(theoryPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                archive.CreateEntry("dummy.txt");
            }

            var songs = Program.LoadExistingSongMetadata(outputDir);

            songs.Should().BeEmpty();
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public void LoadExistingSongMetadata_IgnoresNonTheoryFiles()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_meta_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "readme.txt"), "not a theory file");
            CreateTestTheoryFile(Path.Combine(outputDir, "valid.theory"), "Song", "Artist");

            var songs = Program.LoadExistingSongMetadata(outputDir);

            songs.Should().HaveCount(1);
            songs.Should().Contain("Song|Artist");
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    private static void CreateTestTheoryFile(string path, string title, string artist)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("manifest.json");
        using var entryStream = entry.Open();
        var manifest = new { title, artist };
        JsonSerializer.Serialize(entryStream, manifest);
    }
}
