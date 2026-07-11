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

    [Fact]
    public void BuildGameplayNote_EmptyBendValues_MaxBendPositive_UsesMaxBend()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 200,
            String = 1,
            Fret = 5,
            MaxBend = 2.0f,
            BendValues = new List<BendPoint>()
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.bendStep.Should().Be(2.0f);
        note.technique.Should().Be(4); // bend
    }

    [Fact]
    public void ApplyLegatoLink_HopoHigherFret_SetsHammerOn()
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
            IsHopo = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeTrue();
        note.requiresPluck.Should().BeFalse();
        note.technique.Should().Be(1); // hammer-on
    }

    [Fact]
    public void ApplyLegatoLink_HopoLowerFret_SetsPullOff()
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
            IsHopo = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeTrue();
        note.requiresPluck.Should().BeFalse();
        note.technique.Should().Be(2); // pull-off
    }

    [Fact]
    public void ApplyLegatoLink_PreviousNotFound_DoesNotSetLegato()
    {
        var context = CreateContext();
        var previousDict = new Dictionary<string, CachedNoteData>();
        var source = new SourceNote
        {
            TimeMs = 500,
            SustainMs = 0,
            String = 1,
            Fret = 5,
            SlideTargetFret = 7
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeFalse();
    }

    [Fact]
    public void ApplyLegatoLink_ExistingTechnique_DoesNotOverwrite()
    {
        var context = CreateContext();
        var previous = new CachedNoteData { id = 0, fret = 3 };
        var previousDict = new Dictionary<string, CachedNoteData> { ["1"] = previous };
        var source = new SourceNote
        {
            TimeMs = 500,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsHammerOn = true,
            BendValues = new List<BendPoint> { new() { TimeMs = 0, Step = 1.0f } }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 1, chordId: 0);
        // technique is already 4 (bend) from BuildGameplayNote

        NoteBuilder.ApplyLegatoLink(note, source, previousDict);

        note.isLegato.Should().BeTrue();
        note.technique.Should().Be(4); // bend, not overwritten
    }

    [Fact]
    public void BuildGeneratedNote_FretHandMute_LowersVelocity()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsFretHandMute = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.velocity.Should().Be(86);
        generated.attackVelocityScale.Should().Be(0.82f);
    }

    [Fact]
    public void BuildGeneratedNote_GenericVibrato_SetsVibratoFields()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80,
            BendValues = new List<BendPoint>()
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.vibratoDepthSemitones.Should().BeGreaterThan(0f);
        generated.vibratoRateHz.Should().Be(5f);
        generated.vibratoDelayNormalized.Should().Be(0.05f);
        generated.vibratoFadeNormalized.Should().Be(0.35f);
    }

    [Fact]
    public void BuildGeneratedNote_BendDrivenVibrato_ZeroGenericDepth()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f },
                new() { TimeMs = 250, Step = 1.0f }
            }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.vibratoDepthSemitones.Should().Be(0f);
    }

    [Fact]
    public void BuildGeneratedNote_MinDuration_CapsAt005()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 10,
            String = 1,
            Fret = 5
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.durationSeconds.Should().BeGreaterThanOrEqualTo(0.05f);
    }

    [Fact]
    public void DetermineGameplayTechnique_MaxBendOnly_Returns4()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            MaxBend = 1.5f,
            BendValues = new List<BendPoint>()
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.technique.Should().Be(4);
    }

    [Fact]
    public void BuildGeneratedNote_FretHandMute_VariantIs2()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsFretHandMute = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.techniqueVariant.Should().Be(2);
    }

    [Fact]
    public void BuildGeneratedNote_Harmonic_VariantIs3()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsHarmonic = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.techniqueVariant.Should().Be(3);
    }

    [Fact]
    public void BuildGeneratedNote_HammerOn_LegatoKindIs2()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsHammerOn = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.legatoTransitionKind.Should().Be(2);
    }

    [Fact]
    public void BuildGeneratedNote_PullOff_LegatoKindIs3()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            IsPullOff = true
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.legatoTransitionKind.Should().Be(3);
    }

    [Fact]
    public void BuildGeneratedNote_Slide_LegatoKindIs1()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 100,
            String = 1,
            Fret = 5,
            SlideTargetFret = 7
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.legatoTransitionKind.Should().Be(1);
    }

    [Fact]
    public void BuildTechniqueSegments_SingleBendWithVibrato_CreatesType3Segment()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 300,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f }
            }
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0.3f);

        segments.Should().Contain(s => s.type == 3); // bend-driven vibrato
    }

    [Fact]
    public void BuildTechniqueSegments_GenericVibrato_CreatesType3Segment()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 300,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80,
            BendValues = new List<BendPoint>()
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0.3f);

        segments.Should().Contain(s => s.type == 3); // vibrato
    }

    [Fact]
    public void BuildTechniqueSegments_FlatBendHold_CreatesType3Segment()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f },
                new() { TimeMs = 200, Step = 1.0f },
                new() { TimeMs = 400, Step = 1.0f }
            }
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0.5f);

        segments.Should().Contain(s => s.type == 3); // bend-driven vibrato from flat hold
    }

    [Fact]
    public void BuildTechniqueSegments_ZeroDurationSlide_Uses015()
    {
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 0,
            Fret = 5,
            SlideTargetFret = 7
        };

        var segments = NoteBuilder.BuildTechniqueSegments(source, 0f);

        var slide = segments.First(s => s.type == 0);
        slide.endOffset.Should().Be(0.15f);
    }

    [Fact]
    public void BuildPitchCurve_WithBendRelease_EndsAtLowerStep()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 2.0f },
                new() { TimeMs = 250, Step = 0.5f }
            }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.bendRelease.Should().BeTrue();
        note.bendPoints.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildPitchCurve_PreBend_SetsInitialOffset()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 2.0f }
            }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.bendPreBend.Should().BeTrue();
        note.bendPoints.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildPitchCurve_ClosesAtTime1()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f },
                new() { TimeMs = 200, Step = 2.0f }
            }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.pitchCurve[^1].normalizedTime.Should().Be(1f);
    }

    [Fact]
    public void ResolveRocksmithVibratoDepthSemitones_HighStrength()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 120,
            BendValues = new List<BendPoint>()
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.vibratoDepthSemitones.Should().Be(0.26f);
    }

    [Fact]
    public void ResolveRocksmithVibratoDepthSemitones_MediumStrength()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 80,
            BendValues = new List<BendPoint>()
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.vibratoDepthSemitones.Should().Be(0.22f);
    }

    [Fact]
    public void ResolveRocksmithVibratoDepthSemitones_LowStrength()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            HasVibrato = true,
            VibratoStrength = 50,
            BendValues = new List<BendPoint>()
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        var generated = NoteBuilder.BuildGeneratedNote(source, context, note);

        generated.vibratoDepthSemitones.Should().Be(0.18f);
    }

    [Fact]
    public void ComputeMidiNote_NullTuning_FallsBackToStandard()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 0,
            String = 0,
            Fret = 5
        };
        context.Arrangement.TuningPitches = null!;

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.note.Should().Be("A"); // 40 + 5 = 45 = A
    }

    [Fact]
    public void ComputeMidiNote_OutOfRangeString_ClampsToStandard()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 0,
            String = 10,
            Fret = 0
        };

        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.note.Should().Be("E"); // StandardGuitarTuning[5] = 64 = E
    }

    [Fact]
    public void HasBendRelease_WithRelease_ReturnsTrue()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 2.0f },
                new() { TimeMs = 200, Step = 0.0f }
            }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.bendRelease.Should().BeTrue();
    }

    [Fact]
    public void HasBendRelease_NoRelease_ReturnsFalse()
    {
        var context = CreateContext();
        var source = new SourceNote
        {
            TimeMs = 0,
            SustainMs = 500,
            String = 1,
            Fret = 5,
            BendValues = new List<BendPoint>
            {
                new() { TimeMs = 0, Step = 1.0f },
                new() { TimeMs = 200, Step = 2.0f }
            }
        };
        var note = NoteBuilder.BuildGameplayNote(source, context, noteId: 0, chordId: 0);

        note.bendRelease.Should().BeFalse();
    }
}
