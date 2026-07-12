using FluentAssertions;
using Xunit;

namespace PsarcImporter.Tests;

public class BatchProcessingTests
{
    [Fact]
    public async Task Main_DirectoryWithNoPsarcFiles_Returns1()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            int exitCode = await Program.Main(new[]
            {
                "--source", tempDir,
                "--output", outputDir
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_EmptyDirectory_CreatesOutputDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            await Program.Main(new[]
            {
                "--source", tempDir,
                "--output", outputDir
            });

            Directory.Exists(outputDir).Should().BeTrue();
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_NonexistentDirectory_TreatedAsFile_Returns1()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            int exitCode = await Program.Main(new[]
            {
                "--source", "/nonexistent/directory",
                "--output", outputDir
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_DirectoryWithNonPsarcFiles_Returns1()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "not a psarc");
            File.WriteAllText(Path.Combine(tempDir, "song.zip"), "not a psarc");

            int exitCode = await Program.Main(new[]
            {
                "--source", tempDir,
                "--output", outputDir
            });

            exitCode.Should().Be(1);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Main_DirectoryMode_CaseInsensitiveOutput()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            int exitCode = await Program.Main(new[]
            {
                "--SOURCE", tempDir,
                "--OUTPUT", outputDir
            });

            exitCode.Should().Be(1);
            Directory.Exists(outputDir).Should().BeTrue();
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
            try { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
            catch { }
        }
    }
}
