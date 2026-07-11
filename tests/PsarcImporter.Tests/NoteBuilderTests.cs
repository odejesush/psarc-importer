using FluentAssertions;
using PsarcImporter.Models;
using Rocksmith2014.XML;
using Xunit;

namespace PsarcImporter.Tests;

public class NoteBuilderTests
{
    private static ArrangementVariantContext CreateContext(int[]? tuningPitches = null)
    {
        var arrangement = new InstrumentalArrangement();
        arrangement.MetaData.SongLength = 120000;
        arrangement.MetaData.Arrangement = "Lead";

        var context = new ArrangementContext
        {
            Arrangement = arrangement,
            Route = "Lead",
            DisplayName = "Lead",
            PartId = "lead::1",
            TuningPitches = tuningPitches ?? new[] { 40, 45, 50, 55, 59, 64 },
            TuningDisplayName = "E Standard",
            Tones = new CachedArrangementToneData()
        };

        return new ArrangementVariantContext
        {
            Arrangement = context,
            SourceLevel = new Level(0),
            PartId = "lead::1",
            DisplayName = "Lead",
            DifficultyLabel = "Full",
            DifficultyUiIndex = 0,
            HasDifficultyVariants = false
        };
    }

    [Fact]
    public void BuildGameplayNote_SetsBasicFields()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 1000,
            SustainMs = 500,
            String = 2,
            Fret = 5,
            ChordName = "A"
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.time.Should().Be(1.0f);
        note.duration.Should().Be(0.5f);
        note.stringIdx.Should().Be(2);
        note.fret.Should().Be(5);
        note.chordName.Should().Be("A");
    }

    [Fact]
    public void BuildGameplayNote_ComputesMidiNote()
    {
        var context = CreateContext(new[] { 40, 45, 50, 55, 59, 64 });
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 0,
            String = 0,
            Fret = 5
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        // MIDI note = tuning[0] + fret = 40 + 5 = 45 = "A"
        note.note.Should().Be("A");
    }

    [Fact]
    public void BuildGameplayNote_SlideNote_SetsSlideTargetFret()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 3,
            SlideTargetFret = 7
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.slideTargetFret.Should().Be(7);
        note.technique.Should().Be(3); // slide
    }

    [Fact]
    public void BuildGameplayNote_BendNote_SetsBendStep()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 200,
            String = 1,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f },
                new() { TimeMs = 100, Step = 2.0f }
            }
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.bendStep.Should().Be(2.0f); // max of abs(steps)
        note.technique.Should().Be(4); // bend
    }

    [Fact]
    public void BuildGameplayNote_HammerOn_SetsTechnique()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 0,
            String = 1,
            Fret = 5,
            IsHammerOn = true
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.technique.Should().Be(1); // hammer-on
    }

    [Fact]
    public void BuildGameplayNote_PullOff_SetsTechnique()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 0,
            String = 1,
            Fret = 5,
            IsPullOff = true
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.technique.Should().Be(2); // pull-off
    }

    [Fact]
    public void BuildGameplayNote_Vibrato_SetsTechnique()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 200,
            String = 1,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.technique.Should().Be(5); // vibrato
        note.hasVibrato.Should().BeTrue();
    }

    [Fact]
    public void ApplyLegatoLink_SlideToPreviousNote_SetsLegato()
    {
        var context = CreateContext();
        var previous = new CachedNoteData { id = 0, fret = 3 };
        var previousDict = new Dictionary<string, CachedNoteData> { ["1"] = previous };
        var source = new SourceNote
        {
            TimeMs = 500,
            SustainMs = 0,
            String = 1,
            Fret = 7,
            SlideTargetFret = 7
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeTrue();
        note.requiresPluck.Should().BeFalse();
        note.linkedFromNoteId.Should().Be(0);
        note.technique.Should().Be(3); // slide
    }

    [Fact]
    public void ApplyLegatoLink_HammerOnToPreviousNote_SetsLegato()
    {
        var context = CreateContext();
        var previous = new CachedNoteData { id = 0, fret = 3 };
        var previousDict = new Dictionary<string, CachedNoteData> { ["1"] = previous };
        var source = new SourceNote
        {
            TimeMs = 500,
            SustainMs = 0,
            String = 1,
            Fret = 5,
            IsHammerOn = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeTrue();
        note.requiresPluck.Should().BeFalse();
        note.linkedFromNoteId.Should().Be(0);
        note.technique.Should().Be(1); // hammer-on
    }

    [Fact]
    public void ApplyLegatoLink_PullOffToPreviousNote_SetsLegato()
    {
        var context = CreateContext();
        var previous = new CachedNoteData { id = 0, fret = 7 };
        var previousDict = new Dictionary<string, CachedNoteData> { ["1"] = previous };
        var source = new SourceNote
        {
            TimeMs = 500,
            SustainMs = 0,
            String = 1,
            Fret = 3,
            IsPullOff = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeTrue();
        note.requiresPluck.Should().BeFalse();
        note.technique.Should().Be(2); // pull-off
    }

    [Fact]
    public void ApplyLegatoLink_NoSlideOrHopo_DoesNotSetLegato()
    {
        var context = CreateContext();
        var previous = new CachedNoteData { id = 0, fret = 3 };
        var previousDict = new Dictionary<string, CachedNoteData> { ["1"] = previous };
        var source = new SourceNote
        {
            TimeMs = 500,
            SustainMs = 0,
            String = 1,
            Fret = 5
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeFalse();
        note.requiresPluck.Should().BeTrue();
    }

    [Fact]
    public void BuildGeneratedNote_SetsMidiNote()
    {
        var context = CreateContext(new[] { 40, 45, 50, 55, 59, 64 });
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 0,
            Fret = 3
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.midiNote.Should().Be(43); // 40 + 3
    }

    [Fact]
    public void BuildGeneratedNote_PalmMute_LowersVelocity()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsPalmMute = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.velocity.Should().Be(86);
        generated.attackVelocityScale.Should().Be(0.82f);
    }

    [Fact]
    public void BuildGeneratedNote_NormalNote_FullVelocity()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.velocity.Should().Be(112);
        generated.attackVelocityScale.Should().Be(1.0f);
    }

    [Fact]
    public void BuildTechniqueSegments_Slide_CreatesSlideSegment()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 200,
            Fret = 3,
            SlideTargetFret = 7
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0.2f);

        segments.Should().Contain(s => s.type == 0); // slide segment
        var slide = segments.First(s => s.type == 0);
        slide.startFret.Should().Be(3);
        slide.endFret.Should().Be(7);
    }

    [Fact]
    public void BuildTechniqueSegments_LongNote_CreatesSustainSegment()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            Fret = 5
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0.5f);

        segments.Should().Contain(s => s.type == 2); // sustain segment
    }

    [Fact]
    public void BuildTechniqueSegments_BendNote_CreatesBendSegment()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 200,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f },
                new() { TimeMs = 100, Step = 2.0f }
            }
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0.2f);

        segments.Should().Contain(s => s.type == 1); // bend segment
    }

    [Fact]
    public void ResolveChordDisplayName_NullTemplate_ReturnsEmpty()
    {
        string result = NoteBuilder.ResolveChordDisplayName(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveChordDisplayName_ReplacesMinWithM()
    {
        var template = new ChordTemplate { DisplayName = "Amin" };

        string result = NoteBuilder.ResolveChordDisplayName(template);

        result.Should().Be("Am");
    }

    [Fact]
    public void ResolveChordDisplayName_RemovesConv()
    {
        var template = new ChordTemplate { DisplayName = "ECONV" };

        string result = NoteBuilder.ResolveChordDisplayName(template);

        result.Should().Be("E");
    }

    [Fact]
    public void ResolveChordDisplayName_RemovesNop()
    {
        var template = new ChordTemplate { DisplayName = "G-nop" };

        string result = NoteBuilder.ResolveChordDisplayName(template);

        result.Should().Be("G");
    }

    [Fact]
    public void ResolveChordDisplayName_RemovesArp()
    {
        var template = new ChordTemplate { DisplayName = "D-arp" };

        string result = NoteBuilder.ResolveChordDisplayName(template);

        result.Should().Be("D");
    }

    [Fact]
    public void ResolveChordDisplayName_FallsBackToName()
    {
        var template = new ChordTemplate { DisplayName = "", Name = "F#" };

        string result = NoteBuilder.ResolveChordDisplayName(template);

        result.Should().Be("F#");
    }
}
