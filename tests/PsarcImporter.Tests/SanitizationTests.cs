using FluentAssertions;
using PsarcImporter;
using Xunit;

namespace PsarcImporter.Tests;

public class SanitizationTests
{
    [Fact]
    public void SanitizeFileName_RemovesInvalidChars()
    {
        string result = PsarcConverter.SanitizeFileName("hello<>:\"/\\|?*world");

        result.Should().Be("hello_________world");
    }

    [Fact]
    public void SanitizeFileName_TrimsDotsSpacesAndUnderscores()
    {
        string result = PsarcConverter.SanitizeFileName("..name..");

        result.Should().Be("name");
    }

    [Fact]
    public void SanitizeFileName_EmptyString_ReturnsArrangement()
    {
        string result = PsarcConverter.SanitizeFileName("");

        result.Should().Be("arrangement");
    }

    [Fact]
    public void SanitizeFileName_WhitespaceOnly_ReturnsArrangement()
    {
        string result = PsarcConverter.SanitizeFileName("   ");

        result.Should().Be("arrangement");
    }

    [Fact]
    public void SanitizeFileName_AllInvalidChars_ReturnsArrangement()
    {
        string result = PsarcConverter.SanitizeFileName("<>:\"|?*");

        result.Should().Be("arrangement");
    }

    [Fact]
    public void SanitizeFileName_ValidName_ReturnsUnchanged()
    {
        string result = PsarcConverter.SanitizeFileName("MySong");

        result.Should().Be("MySong");
    }

    [Fact]
    public void SanitizeFileName_LeadWithColons_ReplacesWithUnderscores()
    {
        string result = PsarcConverter.SanitizeFileName("lead::1");

        result.Should().Be("lead__1");
    }

    [Fact]
    public void SanitizeFileName_Slash_ReplacedWithUnderscore()
    {
        string result = PsarcConverter.SanitizeFileName("combo" + "/" + "verse");

        result.Should().Be("combo" + "_" + "verse");
    }

    [Fact]
    public void TheoryPackageWriter_SanitizeEntryName_RemovesInvalidChars()
    {
        string result = TheoryPackageWriter.SanitizeEntryName("test<>file");

        result.Should().Be("test__file");
    }
}
