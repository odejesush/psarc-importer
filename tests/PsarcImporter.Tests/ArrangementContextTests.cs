using FluentAssertions;
using PsarcImporter.Models;
using Rocksmith2014.XML;
using Xunit;

namespace PsarcImporter.Tests;

public class ArrangementContextTests
{
    [Fact]
    public void From_ArrangementSet_UsesProvidedArrangement()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        arrangement.MetaData.Part = 1;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.Route.Should().Be("Lead");
        context.DisplayName.Should().Be("Lead");
    }

    [Fact]
    public void From_ArrangementEmpty_InfersFromFileName()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "";

        var context = ArrangementContext.From(arrangement, "arr_bass_RS2.xml", 0);

        context.Route.Should().Be("Bass");
    }

    [Fact]
    public void From_ArrangementNull_InfersFromFileName()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = null;

        var context = ArrangementContext.From(arrangement, "arr_rhythm.xml", 0);

        context.Route.Should().Be("Rhythm");
    }

    [Fact]
    public void From_FileNameContainsCombo_ReturnsCombo()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "";

        var context = ArrangementContext.From(arrangement, "arr_combo.xml", 0);

        context.Route.Should().Be("Combo");
    }

    [Fact]
    public void From_FileNameNoMatch_ReturnsLead()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "";

        var context = ArrangementContext.From(arrangement, "arrangement.xml", 0);

        context.Route.Should().Be("Lead");
    }

    [Fact]
    public void From_PartGreaterThanOne_IncludesPartInDisplayName()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        arrangement.MetaData.Part = 2;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.DisplayName.Should().Be("Lead 2");
        context.PartId.Should().Be("lead::2");
    }

    [Fact]
    public void From_PartZero_DefaultsToOne()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        arrangement.MetaData.Part = 0;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.PartId.Should().Be("lead::1");
    }

    [Fact]
    public void From_BassRoute_UsesStandardBassTuning()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Bass";

        var context = ArrangementContext.From(arrangement, "arr_bass.xml", 0);

        context.TuningPitches.Should().BeEquivalentTo(new[] { 28, 33, 38, 43 });
    }

    [Fact]
    public void From_GuitarRoute_UsesStandardGuitarTuning()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningPitches.Should().BeEquivalentTo(new[] { 40, 45, 50, 55, 59, 64 });
    }

    [Fact]
    public void From_NullTuning_ClonesBaseTuning()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        arrangement.MetaData.Tuning = null;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningPitches.Should().BeEquivalentTo(new[] { 40, 45, 50, 55, 59, 64 });
    }

    [Fact]
    public void From_WithTuningOffsets_AppliesOffsets()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        var tuning = new Tuning();
        tuning.SetTuning(-1, -1, -1, -1, -1, -1);
        arrangement.MetaData.Tuning = tuning;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningPitches.Should().BeEquivalentTo(new[] { 39, 44, 49, 54, 58, 63 });
        context.TuningDisplayName.Should().Be("Eb Standard");
    }

    [Fact]
    public void FormatTuningDisplayName_EStandard()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningDisplayName.Should().Be("E Standard");
    }

    [Fact]
    public void FormatTuningDisplayName_DropD()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        var tuning = new Tuning();
        tuning.SetTuning(-2, 0, 0, 0, 0, 0);
        arrangement.MetaData.Tuning = tuning;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningDisplayName.Should().Be("Drop D");
    }

    [Fact]
    public void FormatTuningDisplayName_DStandard()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        var tuning = new Tuning();
        tuning.SetTuning(-2, -2, -2, -2, -2, -2);
        arrangement.MetaData.Tuning = tuning;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningDisplayName.Should().Be("D Standard");
    }

    [Fact]
    public void FormatTuningDisplayName_EStandardBass()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Bass";

        var context = ArrangementContext.From(arrangement, "arr_bass.xml", 0);

        context.TuningDisplayName.Should().Be("E Standard Bass");
    }

    [Fact]
    public void FormatTuningDisplayName_DropDBass()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Bass";
        var tuning = new Tuning();
        tuning.SetTuning(-2, 0, 0, 0, 0, 0);
        arrangement.MetaData.Tuning = tuning;

        var context = ArrangementContext.From(arrangement, "arr_bass.xml", 0);

        context.TuningDisplayName.Should().Be("Drop D Bass");
    }

    [Fact]
    public void FormatTuningDisplayName_Custom()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";
        var tuning = new Tuning();
        tuning.SetTuning(1, 0, 0, 0, 0, 0);
        arrangement.MetaData.Tuning = tuning;

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.TuningDisplayName.Should().StartWith("Custom");
    }

    [Fact]
    public void From_TonesInitialized()
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.Arrangement = "Lead";

        var context = ArrangementContext.From(arrangement, "arr_lead.xml", 0);

        context.Tones.Should().NotBeNull();
    }
}
