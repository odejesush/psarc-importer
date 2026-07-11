using FluentAssertions;
using PsarcImporter.Models;
using Rocksmith2014.XML;
using Xunit;

namespace PsarcImporter.Tests;

public class TimingExporterTests
{
    [Fact]
    public void Build_NullArrangement_ReturnsEmptyTiming()
    {
        var result = TimingExporter.Build(null!);

        result.Should().NotBeNull();
        result.averageTempoBpm.Should().Be(120f);
        result.ebeats.Should().BeEmpty();
        result.sections.Should().BeEmpty();
    }

    [Fact]
    public void Build_UsesAverageTempo()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.AverageTempo = 140f;

        var result = TimingExporter.Build(arrangement);

        result.averageTempoBpm.Should().Be(140f);
    }

    [Fact]
    public void Build_ZeroTempo_DefaultsTo120()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.AverageTempo = 0f;

        var result = TimingExporter.Build(arrangement);

        result.averageTempoBpm.Should().Be(120f);
    }

    [Fact]
    public void Build_CapoValue_SetCorrectly()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Capo = 2;

        var result = TimingExporter.Build(arrangement);

        result.capo.Should().Be(2);
    }

    [Fact]
    public void Build_Ebeats_ConvertedToSeconds()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.Ebeats = new List<Ebeat>
        {
            new() { Time = 1000, Measure = 1 },
            new() { Time = 2000, Measure = -1 },
            new() { Time = 3000, Measure = 1 }
        };

        var result = TimingExporter.Build(arrangement);

        result.ebeats.Should().HaveCount(3);
        result.ebeats[0].timeSeconds.Should().Be(1.0f);
        result.ebeats[0].measure.Should().Be(1);
        result.ebeats[1].timeSeconds.Should().Be(2.0f);
        result.ebeats[1].measure.Should().Be(-1);
    }

    [Fact]
    public void Build_Sections_ConvertedToSeconds()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.Sections = new List<Section>
        {
            new() { Name = "Intro", Number = 1, Time = 0 },
            new() { Name = "Verse", Number = 1, Time = 15000 },
            new() { Name = "Chorus", Number = 1, Time = 30000 }
        };

        var result = TimingExporter.Build(arrangement);

        result.sections.Should().HaveCount(3);
        result.sections[0].name.Should().Be("Intro");
        result.sections[0].timeSeconds.Should().Be(0f);
        result.sections[1].name.Should().Be("Verse");
        result.sections[1].timeSeconds.Should().Be(15.0f);
    }

    [Fact]
    public void Build_Sections_SortedByTime()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.Sections = new List<Section>
        {
            new() { Name = "Chorus", Number = 1, Time = 30000 },
            new() { Name = "Intro", Number = 1, Time = 0 },
            new() { Name = "Verse", Number = 1, Time = 15000 }
        };

        var result = TimingExporter.Build(arrangement);

        result.sections[0].name.Should().Be("Intro");
        result.sections[1].name.Should().Be("Verse");
        result.sections[2].name.Should().Be("Chorus");
    }

    [Fact]
    public void Build_NullEbeatsAndSections_ReturnsEmptyLists()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.Ebeats = null;
        arrangement.Sections = null;

        var result = TimingExporter.Build(arrangement);

        result.ebeats.Should().BeEmpty();
        result.sections.Should().BeEmpty();
    }
}
