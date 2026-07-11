using FluentAssertions;
using PsarcImporter.Conversion;
using PsarcImporter.Models;
using Xunit;

namespace PsarcImporter.Tests;

public class ToneExtractorTests
{
    [Fact]
    public void AddToneDefinitionKeys_NullDefinitions_DoesNotThrow()
    {
        HashSet<string> keys = new();
        ToneExtractor.AddToneDefinitionKeys(null, keys);

        keys.Should().BeEmpty();
    }

    [Fact]
    public void AddToneDefinitionKeys_NullKeys_DoesNotThrow()
    {
        var definitions = new List<CachedToneDefinitionData>
        {
            new() { key = "tone1" }
        };

        ToneExtractor.AddToneDefinitionKeys(definitions, null);
    }

    [Fact]
    public void AddToneDefinitionKeys_AddsKeysFromDefinitions()
    {
        var definitions = new List<CachedToneDefinitionData>
        {
            new() { key = "tone1", name = "Tone 1" },
            new() { key = "tone2", name = "Tone 2" }
        };
        HashSet<string> keys = new();

        ToneExtractor.AddToneDefinitionKeys(definitions, keys);

        keys.Should().Contain("tone1");
        keys.Should().Contain("tone2");
    }

    [Fact]
    public void AddToneDefinitionKeys_FallsBackToName_WhenKeyEmpty()
    {
        var definitions = new List<CachedToneDefinitionData>
        {
            new() { key = "", name = "FallbackTone" }
        };
        HashSet<string> keys = new();

        ToneExtractor.AddToneDefinitionKeys(definitions, keys);

        keys.Should().Contain("FallbackTone");
    }

    [Fact]
    public void AddToneDefinitionKeys_FallsBackToRawJson_WhenKeyAndNameEmpty()
    {
        var definitions = new List<CachedToneDefinitionData>
        {
            new() { key = "", name = "", rawJson = "{\"Name\":\"JsonTone\"}" }
        };
        HashSet<string> keys = new();

        ToneExtractor.AddToneDefinitionKeys(definitions, keys);

        keys.Should().Contain("{\"Name\":\"JsonTone\"}");
    }

    [Fact]
    public void AddToneDefinitionKeys_DoesNotAddEmptyKeys()
    {
        var definitions = new List<CachedToneDefinitionData>
        {
            new() { key = "", name = "", rawJson = "" }
        };
        HashSet<string> keys = new();

        ToneExtractor.AddToneDefinitionKeys(definitions, keys);

        keys.Should().BeEmpty();
    }

    [Fact]
    public void ExtractProjectToneDefinitions_NullPath_ReturnsEmpty()
    {
        var result = ToneExtractor.ExtractProjectToneDefinitions(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractProjectToneDefinitions_EmptyPath_ReturnsEmpty()
    {
        var result = ToneExtractor.ExtractProjectToneDefinitions("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractProjectToneDefinitions_NonexistentFile_ReturnsEmpty()
    {
        var result = ToneExtractor.ExtractProjectToneDefinitions("/nonexistent/file.json");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractProjectToneDefinitions_ValidJsonWithTones_ReturnsDefinitions()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_tones_{Guid.NewGuid():N}.json");
        try
        {
            string json = """
            {
                "Tones": [
                    { "Key": "tone1", "Name": "Clean" },
                    { "Key": "tone2", "Name": "Distortion" }
                ]
            }
            """;
            File.WriteAllText(tempFile, json);

            var result = ToneExtractor.ExtractProjectToneDefinitions(tempFile);

            result.Should().HaveCount(2);
            result[0].key.Should().Be("tone1");
            result[0].name.Should().Be("Clean");
            result[1].key.Should().Be("tone2");
            result[1].name.Should().Be("Distortion");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExtractProjectToneDefinitions_InvalidJson_ReturnsEmpty()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_bad_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, "not valid json {{{");

            var result = ToneExtractor.ExtractProjectToneDefinitions(tempFile);

            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExtractProjectToneDefinitions_NoTonesProperty_ReturnsEmpty()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_notones_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, """{ "Name": "Test" }""");

            var result = ToneExtractor.ExtractProjectToneDefinitions(tempFile);

            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void NormalizeToneData_NullSource_ReturnsEmpty()
    {
        var result = ArrangementBuilder.NormalizeToneData(null);

        result.Should().NotBeNull();
        result.baseToneName.Should().BeEmpty();
        result.changes.Should().BeEmpty();
        result.definitions.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeToneData_TrimsWhitespace()
    {
        var source = new CachedArrangementToneData
        {
            baseToneName = "  clean  ",
            changes = new List<CachedToneChangeData>
            {
                new() { timeSeconds = 1.5f, toneName = "  Tone A  ", toneId = 0 }
            },
            definitions = new List<CachedToneDefinitionData>
            {
                new() { name = "  Def  ", key = "  key  ", rawJson = "{}" }
            }
        };

        var result = ArrangementBuilder.NormalizeToneData(source);

        result.baseToneName.Should().Be("clean");
        result.changes[0].toneName.Should().Be("Tone A");
        result.definitions[0].name.Should().Be("Def");
        result.definitions[0].key.Should().Be("key");
    }

    [Fact]
    public void NormalizeToneData_SortsChangesByTime()
    {
        var source = new CachedArrangementToneData
        {
            changes = new List<CachedToneChangeData>
            {
                new() { timeSeconds = 5.0f, toneName = "B" },
                new() { timeSeconds = 1.0f, toneName = "A" },
                new() { timeSeconds = 3.0f, toneName = "C" }
            }
        };

        var result = ArrangementBuilder.NormalizeToneData(source);

        result.changes.Should().HaveCount(3);
        result.changes[0].toneName.Should().Be("A");
        result.changes[1].toneName.Should().Be("C");
        result.changes[2].toneName.Should().Be("B");
    }

    [Fact]
    public void NormalizeToneData_FiltersOutChangesWithEmptyName()
    {
        var source = new CachedArrangementToneData
        {
            changes = new List<CachedToneChangeData>
            {
                new() { timeSeconds = 1.0f, toneName = "Valid" },
                new() { timeSeconds = 2.0f, toneName = "" },
                new() { timeSeconds = 3.0f, toneName = "   " }
            }
        };

        var result = ArrangementBuilder.NormalizeToneData(source);

        result.changes.Should().HaveCount(1);
        result.changes[0].toneName.Should().Be("Valid");
    }

    [Fact]
    public void NormalizeToneData_FiltersOutNullChanges()
    {
        var source = new CachedArrangementToneData
        {
            changes = new List<CachedToneChangeData>
            {
                new() { timeSeconds = 1.0f, toneName = "Valid" },
                null!
            }
        };

        var result = ArrangementBuilder.NormalizeToneData(source);

        result.changes.Should().HaveCount(1);
    }
}
