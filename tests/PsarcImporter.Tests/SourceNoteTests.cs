using FluentAssertions;
using PsarcImporter.Models;
using Rocksmith2014.XML;
using Xunit;

namespace PsarcImporter.Tests;

public class SourceNoteTests
{
    [Fact]
    public void FromNote_SlideToPositive_SetsSlideTargetFret()
    {
        var note = new Note { Time = 1000, SlideTo = 7 };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.SlideTargetFret.Should().Be(7);
    }

    [Fact]
    public void FromNote_SlideToNegative_SlideUnpitchToPositive_SetsSlideTargetFret()
    {
        var note = new Note { Time = 1000, SlideTo = -1, SlideUnpitchTo = 5 };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.SlideTargetFret.Should().Be(5);
    }

    [Fact]
    public void FromNote_BothSlideNegative_SetsNegativeOne()
    {
        var note = new Note { Time = 1000, SlideTo = -1, SlideUnpitchTo = -1 };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.SlideTargetFret.Should().Be(-1);
    }

    [Fact]
    public void FromNote_ChordPalmMute_SetsPalmMute()
    {
        var note = new Note { Time = 1000, IsPalmMute = false };

        var source = SourceNote.FromNote(note, chordPalmMute: true, false, false, "C");

        source.IsPalmMute.Should().BeTrue();
    }

    [Fact]
    public void FromNote_ChordFretHandMute_SetsFretHandMute()
    {
        var note = new Note { Time = 1000, IsFretHandMute = false };

        var source = SourceNote.FromNote(note, false, chordFretHandMute: true, false, "C");

        source.IsFretHandMute.Should().BeTrue();
    }

    [Fact]
    public void FromNote_ChordHopo_SetsHammerOnAndHopo()
    {
        var note = new Note { Time = 1000, Mask = NoteMask.None };

        var source = SourceNote.FromNote(note, false, false, chordHopo: true, "C");

        source.IsHammerOn.Should().BeTrue();
        source.IsHopo.Should().BeTrue();
    }

    [Fact]
    public void FromNote_Harmonic_SetsIsHarmonic()
    {
        var note = new Note { Time = 1000, IsHarmonic = true };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.IsHarmonic.Should().BeTrue();
    }

    [Fact]
    public void FromNote_PinchHarmonic_SetsIsHarmonic()
    {
        var note = new Note { Time = 1000, IsPinchHarmonic = true };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.IsHarmonic.Should().BeTrue();
        source.IsPinchHarmonic.Should().BeTrue();
    }

    [Fact]
    public void FromNote_VibratoPositive_SetsHasVibrato()
    {
        var note = new Note { Time = 1000, Vibrato = 80 };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.HasVibrato.Should().BeTrue();
        source.VibratoStrength.Should().Be(80);
    }

    [Fact]
    public void FromNote_NullBendValues_ReturnsEmptyList()
    {
        var note = new Note { Time = 1000, BendValues = null };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.BendValues.Should().BeEmpty();
    }

    [Fact]
    public void FromNote_WithBendValues_BuildsBendPoints()
    {
        var note = new Note
        {
            Time = 1000,
            BendValues = new List<BendValue>
            {
                new BendValue(1000, 1.0f),
                new BendValue(1500, 2.0f)
            }
        };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.BendValues.Should().HaveCount(2);
        source.BendValues[0].TimeMs.Should().Be(0);
        source.BendValues[0].Step.Should().Be(1.0f);
        source.BendValues[1].TimeMs.Should().Be(500);
        source.BendValues[1].Step.Should().Be(2.0f);
    }

    [Fact]
    public void FromNote_NegativeRelativeTime_ClampedToZero()
    {
        var note = new Note
        {
            Time = 1000,
            BendValues = new List<BendValue>
            {
                new BendValue(500, 1.0f)
            }
        };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.BendValues[0].TimeMs.Should().Be(0);
    }

    [Fact]
    public void FromTemplate_HopoAndLinkNext_SetsHammerOn()
    {
        var source = SourceNote.FromTemplate(1000, 0, 5, false, false, hopo: true, linkNext: true, "C");

        source.IsHammerOn.Should().BeTrue();
        source.IsHopo.Should().BeTrue();
    }

    [Fact]
    public void FromTemplate_HopoAndNotLinkNext_DoesNotSetHammerOn()
    {
        var source = SourceNote.FromTemplate(1000, 0, 5, false, false, hopo: true, linkNext: false, "C");

        source.IsHammerOn.Should().BeFalse();
        source.IsHopo.Should().BeTrue();
    }

    [Fact]
    public void FromTemplate_PalmMute_SetsPalmMute()
    {
        var source = SourceNote.FromTemplate(1000, 0, 5, palmMute: true, false, false, false, "C");

        source.IsPalmMute.Should().BeTrue();
    }

    [Fact]
    public void FromTemplate_FretHandMute_SetsFretHandMute()
    {
        var source = SourceNote.FromTemplate(1000, 0, 5, false, fretHandMute: true, false, false, "C");

        source.IsFretHandMute.Should().BeTrue();
    }

    [Fact]
    public void FromTemplate_DefaultValues_AreCorrect()
    {
        var source = SourceNote.FromTemplate(1000, 2, 7, false, false, false, false, null);

        source.TimeMs.Should().Be(1000);
        source.String.Should().Be(2);
        source.Fret.Should().Be(7);
        source.ChordName.Should().BeEmpty();
        source.SlideTargetFret.Should().Be(-1);
        source.IsAccent.Should().BeFalse();
        source.IsTap.Should().BeFalse();
        source.IsTremolo.Should().BeFalse();
        source.IsPullOff.Should().BeFalse();
        source.IsHarmonic.Should().BeFalse();
        source.IsPinchHarmonic.Should().BeFalse();
        source.HasVibrato.Should().BeFalse();
        source.VibratoStrength.Should().Be(0);
        source.MaxBend.Should().Be(0f);
        source.BendValues.Should().BeEmpty();
    }

    [Fact]
    public void FromNote_MaxBend_SetsMaxBend()
    {
        var note = new Note { Time = 1000, MaxBend = 2.5f };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.MaxBend.Should().Be(2.5f);
    }

    [Fact]
    public void FromNote_Accent_SetsIsAccent()
    {
        var note = new Note { Time = 1000, IsAccent = true };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.IsAccent.Should().BeTrue();
    }

    [Fact]
    public void FromNote_Tap_SetsIsTap()
    {
        var note = new Note { Time = 1000, Tap = 1 };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.IsTap.Should().BeTrue();
    }

    [Fact]
    public void FromNote_Tremolo_SetsIsTremolo()
    {
        var note = new Note { Time = 1000, IsTremolo = true };

        var source = SourceNote.FromNote(note, false, false, false, null);

        source.IsTremolo.Should().BeTrue();
    }
}
