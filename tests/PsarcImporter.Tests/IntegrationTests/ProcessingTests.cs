using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using PsarcImporter.Models;
using Rocksmith2014.XML;
using Xunit;

namespace PsarcImporter.Tests.IntegrationTests;

public class ProcessingTests : IDisposable
{
    private readonly string _workDir;

    public ProcessingTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"processing_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true); }
        catch { }
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
    }

    [Fact]
    public void ProcessArrangements_BuildsVariants()
    {
        string leadXml = GetFixturePath("arr_lead_RS2.xml");
        if (!File.Exists(leadXml))
            return;

        InstrumentalArrangement arrangement = InstrumentalArrangement.Load(leadXml);
        ArrangementContext context = ArrangementContext.From(arrangement, leadXml, 0);

        List<ArrangementVariantBuildResult> variants = ArrangementBuilder.BuildVariants(context);

        variants.Should().NotBeEmpty("lead arrangement should produce at least one variant");
        variants[0].Part.Should().NotBeNull();
        variants[0].Part.notes.Should().NotBeNull();
    }

    [Fact]
    public void ProcessArrangements_SetsCorrectMetadata()
    {
        string leadXml = GetFixturePath("arr_lead_RS2.xml");
        if (!File.Exists(leadXml))
            return;

        InstrumentalArrangement arrangement = InstrumentalArrangement.Load(leadXml);
        ArrangementContext context = ArrangementContext.From(arrangement, leadXml, 0);

        context.Route.Should().Be("Lead");
        context.PartId.Should().StartWith("lead::");
        context.TuningPitches.Should().NotBeEmpty();
        context.TuningDisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ProcessArrangements_BuildsTimingData()
    {
        string leadXml = GetFixturePath("arr_lead_RS2.xml");
        if (!File.Exists(leadXml))
            return;

        InstrumentalArrangement arrangement = InstrumentalArrangement.Load(leadXml);
        CachedArrangementTimingData timing = TimingExporter.Build(arrangement);

        timing.ebeats.Should().NotBeEmpty("arrangement should have ebeats");
        timing.sections.Should().NotBeEmpty("arrangement should have sections");
        timing.averageTempoBpm.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ProcessBassArrangement_SetsBassRoute()
    {
        string bassXml = GetFixturePath("arr_bass_RS2.xml");
        if (!File.Exists(bassXml))
            return;

        InstrumentalArrangement arrangement = InstrumentalArrangement.Load(bassXml);
        ArrangementContext context = ArrangementContext.From(arrangement, bassXml, 0);

        context.Route.Should().Be("Bass");
        context.PartId.Should().StartWith("bass::");
    }

    [Fact]
    public void ProcessArrangements_NormalizeToneData()
    {
        string leadXml = GetFixturePath("arr_lead_RS2.xml");
        if (!File.Exists(leadXml))
            return;

        InstrumentalArrangement arrangement = InstrumentalArrangement.Load(leadXml);
        ArrangementContext context = ArrangementContext.From(arrangement, leadXml, 0);

        CachedArrangementToneData tones = ArrangementBuilder.NormalizeToneData(context.Tones);

        tones.Should().NotBeNull();
        tones.baseToneName.Should().NotBeNull();
        tones.changes.Should().NotBeNull();
        tones.definitions.Should().NotBeNull();
    }

    [Fact]
    public void BuildTheoryPackage_ProducesValidZip()
    {
        string outputPath = Path.Combine(_workDir, "test.theory");

        var manifest = new TheorySongManifest
        {
            title = "Test Song",
            artist = "Test Artist",
            album = "Test Album",
            durationSeconds = 180f,
            difficultyRating = 3,
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            modifiedAtUtcTicks = DateTime.UtcNow.Ticks
        };

        var arrangements = new List<TheoryArrangementData>();
        var cachedManifest = new CachedSongManifest();

        TheoryPackageWriter.Write(
            outputPath,
            manifest,
            arrangements,
            primaryAudioPath: null,
            previewAudioPath: null,
            coverArtPath: null,
            cachedManifest,
            audioEntryDir: "audio");

        File.Exists(outputPath).Should().BeTrue();
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0);

        using ZipArchive archive = ZipFile.OpenRead(outputPath);
        archive.GetEntry("manifest.json").Should().NotBeNull();
    }

    [Fact]
    public void BuildTheoryPackage_ManifestStructure()
    {
        string outputPath = Path.Combine(_workDir, "test.theory");

        var manifest = new TheorySongManifest
        {
            title = "Integration Test Song",
            artist = "Test Artist",
            album = "Test Album",
            durationSeconds = 240.5f,
            difficultyRating = 4,
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            modifiedAtUtcTicks = DateTime.UtcNow.Ticks,
            defaultArrangementId = "lead::1"
        };

        manifest.arrangements.Add(new TheoryArrangementSummary
        {
            arrangementId = "lead::1",
            displayName = "Lead",
            instrumentType = "guitar",
            route = "Lead",
            noteCount = 150,
            difficultyRating = 4
        });

        var arrangements = new List<TheoryArrangementData>();
        var cachedManifest = new CachedSongManifest();

        TheoryPackageWriter.Write(
            outputPath,
            manifest,
            arrangements,
            primaryAudioPath: null,
            previewAudioPath: null,
            coverArtPath: null,
            cachedManifest,
            audioEntryDir: "audio");

        using ZipArchive archive = ZipFile.OpenRead(outputPath);
        ZipArchiveEntry? manifestEntry = archive.GetEntry("manifest.json");
        manifestEntry.Should().NotBeNull();

        using Stream stream = manifestEntry!.Open();
        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        root.GetProperty("formatId").GetString().Should().Be("string-theory-song");
        root.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        root.GetProperty("title").GetString().Should().Be("Integration Test Song");
        root.GetProperty("artist").GetString().Should().Be("Test Artist");
        root.GetProperty("durationSeconds").GetSingle().Should().Be(240.5f);
        root.GetProperty("difficultyRating").GetInt32().Should().Be(4);
        root.GetProperty("defaultArrangementId").GetString().Should().Be("lead::1");
        root.GetProperty("arrangements").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void BuildTheoryPackage_IncludesArrangements()
    {
        string outputPath = Path.Combine(_workDir, "test.theory");

        var manifest = new TheorySongManifest
        {
            title = "Test",
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            modifiedAtUtcTicks = DateTime.UtcNow.Ticks
        };

        var arrangement = new TheoryArrangementData
        {
            arrangementId = "lead::1",
            displayName = "Lead",
            instrumentType = "guitar",
            route = "Lead",
            durationSeconds = 120f,
            difficultyRating = 3,
            notes = new List<TheoryNoteData>
            {
                new() { id = 0, time = 1.0f, duration = 0.5f, stringIndex = 1, fret = 5 }
            }
        };

        var arrangements = new List<TheoryArrangementData> { arrangement };
        var cachedManifest = new CachedSongManifest();

        TheoryPackageWriter.Write(
            outputPath,
            manifest,
            arrangements,
            primaryAudioPath: null,
            previewAudioPath: null,
            coverArtPath: null,
            cachedManifest,
            audioEntryDir: "audio");

        using ZipArchive archive = ZipFile.OpenRead(outputPath);
        var arrangementEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("arrangements/") && e.FullName.EndsWith(".json"))
            .ToList();

        arrangementEntries.Should().HaveCount(1);

        using Stream stream = arrangementEntries[0].Open();
        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        root.GetProperty("arrangementId").GetString().Should().Be("lead::1");
        root.GetProperty("notes").GetArrayLength().Should().Be(1);
        root.GetProperty("notes")[0].GetProperty("fret").GetInt32().Should().Be(5);
    }
}
