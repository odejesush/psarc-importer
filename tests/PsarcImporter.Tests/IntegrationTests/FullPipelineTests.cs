using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PsarcImporter.Tests.IntegrationTests;

public class FullPipelineTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _outputDir;
    private readonly string _outputPath;
    private readonly string _psarcPath;

    public FullPipelineTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"integration_work_{Guid.NewGuid():N}");
        _outputDir = Path.Combine(Path.GetTempPath(), $"integration_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        Directory.CreateDirectory(_outputDir);
        _outputPath = Path.Combine(_outputDir, "output.theory");
        _psarcPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "test_p.psarc");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true); }
        catch { }
        try { if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, true); }
        catch { }
    }

    private static bool AreAudioToolsAvailable()
    {
        string toolsDir = Path.Combine(AppContext.BaseDirectory, "Tools");
        string ww2ogg = Path.Combine(toolsDir, "ww2ogg");
        string revorb = Path.Combine(toolsDir, "revorb");

        if (OperatingSystem.IsWindows())
            return File.Exists(ww2ogg + ".exe") || File.Exists(ww2ogg);

        return File.Exists(ww2ogg) && File.Exists(revorb);
    }

    private bool HasFixturePsarc()
    {
        return File.Exists(_psarcPath);
    }

    private bool SkipIfNotSupported()
    {
        return !AreAudioToolsAvailable() || !HasFixturePsarc();
    }

    [Fact]
    public async Task ConvertPsarc_ProducesValidTheoryFile()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);
        File.Exists(_outputPath).Should().BeTrue();
        new FileInfo(_outputPath).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConvertPsarc_TheoryContainsManifest()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);

        using ZipArchive archive = ZipFile.OpenRead(_outputPath);
        ZipArchiveEntry? manifestEntry = archive.GetEntry("manifest.json");
        manifestEntry.Should().NotBeNull();

        using Stream stream = manifestEntry!.Open();
        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        root.GetProperty("formatId").GetString().Should().Be("string-theory-song");
        root.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        root.TryGetProperty("title", out _).Should().BeTrue();
        root.TryGetProperty("artist", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertPsarc_TheoryContainsArrangements()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);

        using ZipArchive archive = ZipFile.OpenRead(_outputPath);
        var arrangementEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("arrangements/") && e.FullName.EndsWith(".json"))
            .ToList();

        arrangementEntries.Should().NotBeEmpty("theory file should contain arrangement data");

        foreach (var entry in arrangementEntries)
        {
            using Stream stream = entry.Open();
            using JsonDocument doc = JsonDocument.Parse(stream);
            doc.RootElement.TryGetProperty("arrangementId", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ConvertPsarc_TheoryContainsAudio()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);

        using ZipArchive archive = ZipFile.OpenRead(_outputPath);
        var audioEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("audio/") && e.FullName.EndsWith(".ogg"))
            .ToList();

        audioEntries.Should().NotBeEmpty("theory file should contain audio");
    }

    [Fact]
    public async Task ConvertPsarc_TheoryContainsCoverArt()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);

        using ZipArchive archive = ZipFile.OpenRead(_outputPath);
        ZipArchiveEntry? coverEntry = archive.GetEntry("assets/cover.png");
        coverEntry.Should().NotBeNull("theory file should contain cover art");
        coverEntry!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConvertPsarc_ManifestHasCorrectMetadata()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);

        using ZipArchive archive = ZipFile.OpenRead(_outputPath);
        ZipArchiveEntry? manifestEntry = archive.GetEntry("manifest.json");
        manifestEntry.Should().NotBeNull();

        using Stream stream = manifestEntry!.Open();
        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        root.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("artist").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("durationSeconds").GetSingle().Should().BeGreaterThan(0f);
        root.GetProperty("arrangements").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConvertPsarc_ArrangementsHaveNotes()
    {
        if (SkipIfNotSupported()) return;

        int exitCode = await Program.Main(new[]
        {
            "--source", _psarcPath,
            "--output", _outputPath,
            "--work", _workDir
        });

        exitCode.Should().Be(0);

        using ZipArchive archive = ZipFile.OpenRead(_outputPath);
        var arrangementEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("arrangements/") && e.FullName.EndsWith(".json"))
            .ToList();

        arrangementEntries.Should().NotBeEmpty();

        foreach (var entry in arrangementEntries)
        {
            using Stream stream = entry.Open();
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;

            root.TryGetProperty("notes", out JsonElement notes).Should().BeTrue();
            notes.GetArrayLength().Should().BeGreaterThan(0, "arrangement should contain note data");
        }
    }
}
