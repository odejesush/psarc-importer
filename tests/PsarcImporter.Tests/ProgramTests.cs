using FluentAssertions;
using Xunit;

namespace PsarcImporter.Tests;

public class ProgramTests
{
    [Fact]
    public async Task Main_MissingArgs_Returns2()
    {
        int exitCode = await Program.Main(new string[0]);

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task Main_MissingOutput_Returns2()
    {
        int exitCode = await Program.Main(new[] { "--source", "/fake/path.psarc" });

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task Main_MissingSource_Returns2()
    {
        int exitCode = await Program.Main(new[] { "--output", "/fake/output.theory" });

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task Main_SourceOnly_Returns2()
    {
        int exitCode = await Program.Main(new[] { "--source", "/fake/path.psarc", "--output" });

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task Main_CaseInsensitiveArgs_MissingSource_Returns2()
    {
        int exitCode = await Program.Main(new[] { "--SOURCE", "/fake/path.psarc" });

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task Main_NonexistentSource_Returns1()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"prog_test_{Guid.NewGuid():N}");
        string outputFile = Path.Combine(tempDir, "out.theory");
        try
        {
            int exitCode = await Program.Main(new[]
            {
                "--source", "/nonexistent/file.psarc",
                "--output", outputFile
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
        }
    }
}
