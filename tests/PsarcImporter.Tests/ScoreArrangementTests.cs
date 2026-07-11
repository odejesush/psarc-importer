using FluentAssertions;
using PsarcImporter.Models;
using Xunit;

namespace PsarcImporter.Tests;

public class ScoreArrangementTests
{
    [Fact]
    public void ScoreArrangement_LeadRoute_Adds160()
    {
        int score = ArrangementBuilder.ScoreArrangement("Lead", 50);

        score.Should().Be(50 * 2 + 160);
    }

    [Fact]
    public void ScoreArrangement_RhythmRoute_Adds120()
    {
        int score = ArrangementBuilder.ScoreArrangement("Rhythm", 50);

        score.Should().Be(50 * 2 + 120);
    }

    [Fact]
    public void ScoreArrangement_BassRoute_Adds60()
    {
        int score = ArrangementBuilder.ScoreArrangement("Bass", 50);

        score.Should().Be(50 * 2 + 60);
    }

    [Fact]
    public void ScoreArrangement_ComboRoute_Adds90()
    {
        int score = ArrangementBuilder.ScoreArrangement("Combo", 50);

        score.Should().Be(50 * 2 + 90);
    }

    [Fact]
    public void ScoreArrangement_UnknownRoute_AddsNoBonus()
    {
        int score = ArrangementBuilder.ScoreArrangement("Custom", 50);

        score.Should().Be(50 * 2);
    }

    [Theory]
    [InlineData("lead", 160)]
    [InlineData("LEAD", 160)]
    [InlineData("Lead Guitar", 160)]
    public void ScoreArrangement_CaseInsensitiveLead(string route, int expectedBonus)
    {
        int score = ArrangementBuilder.ScoreArrangement(route, 10);

        score.Should().Be(10 * 2 + expectedBonus);
    }

    [Fact]
    public void ScoreArrangement_ZeroNotes_ReturnsOnlyBonus()
    {
        int score = ArrangementBuilder.ScoreArrangement("Bass", 0);

        score.Should().Be(60);
    }
}
