internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Parse arguments
        string? sourcePath = null;
        string? outputPath = null;
        string? workDirectory = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--source", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                sourcePath = args[++i];
            else if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                outputPath = args[++i];
            else if (string.Equals(args[i], "--work", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                workDirectory = args[++i];
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("Usage: PsarcImporter --source <psarcPath> --output <outputTheoryPath> [--work <workDir>]");
            return 2;
        }

        // Default work directory
        if (string.IsNullOrWhiteSpace(workDirectory))
        {
            workDirectory = Path.Combine(Path.GetTempPath(), $"PsarcImporter_{Guid.NewGuid():N}");
        }

        // Ensure output directory exists
        string? outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        try
        {
            await PsarcImporter.PsarcConverter.ConvertAsync(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(outputPath),
                Path.GetFullPath(workDirectory));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PsarcImporter] Fatal error: {ex}");
            return 1;
        }
        finally
        {
            // Clean up work directory
            try
            {
                if (Directory.Exists(workDirectory))
                    Directory.Delete(workDirectory, true);
            }
            catch
            {
            }
        }
    }
}
