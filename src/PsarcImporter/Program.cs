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
            Console.Error.WriteLine("Usage: PsarcImporter --source <psarcPath|sourceDir> --output <outputTheoryPath|outputDir> [--work <workDir>]");
            return 2;
        }

        string fullPath = Path.GetFullPath(sourcePath);

        if (Directory.Exists(fullPath))
            return await RunBatch(fullPath, Path.GetFullPath(outputPath), workDirectory);
        else
            return await RunSingle(fullPath, Path.GetFullPath(outputPath), workDirectory);
    }

    private static async Task<int> RunSingle(string sourcePath, string outputPath, string? workDirectory)
    {
        if (string.IsNullOrWhiteSpace(workDirectory))
            workDirectory = Path.Combine(Path.GetTempPath(), $"PsarcImporter_{Guid.NewGuid():N}");

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        try
        {
            await PsarcImporter.PsarcConverter.ConvertAsync(sourcePath, outputPath, workDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PsarcImporter] Fatal error: {ex}");
            return 1;
        }
        finally
        {
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

    private static async Task<int> RunBatch(string sourceDir, string outputDir, string? workDirectory)
    {
        string[] psarcFiles = Directory.GetFiles(sourceDir, "*.psarc", SearchOption.TopDirectoryOnly);

        if (psarcFiles.Length == 0)
        {
            Console.Error.WriteLine($"[PsarcImporter] No .psarc files found in {sourceDir}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        if (string.IsNullOrWhiteSpace(workDirectory))
            workDirectory = Path.Combine(Path.GetTempPath(), $"PsarcImporter_{Guid.NewGuid():N}");

        Console.WriteLine($"[PsarcImporter] Found {psarcFiles.Length} .psarc file(s) in {sourceDir}");

        int succeeded = 0;
        int failed = 0;
        int skipped = 0;

        foreach (string psarcPath in psarcFiles)
        {
            string fileWorkDir = Path.Combine(workDirectory, Guid.NewGuid().ToString("N"));
            string tempOutputPath = Path.Combine(fileWorkDir, "temp.theory");

            try
            {
                Directory.CreateDirectory(fileWorkDir);
                var (title, artist) = await PsarcImporter.PsarcConverter.ConvertAsync(
                    psarcPath, tempOutputPath, fileWorkDir);

                string sanitizedTitle = PsarcImporter.PsarcConverter.SanitizeFileName(title);
                string sanitizedArtist = PsarcImporter.PsarcConverter.SanitizeFileName(artist);
                string fileName = string.IsNullOrWhiteSpace(artist)
                    ? $"{sanitizedTitle}.theory"
                    : $"{sanitizedArtist} - {sanitizedTitle}.theory";
                string finalPath = Path.Combine(outputDir, fileName);

                if (File.Exists(finalPath))
                {
                    Console.WriteLine($"[PsarcImporter] Skipping '{title}' (already exists)");
                    skipped++;
                }
                else
                {
                    File.Move(tempOutputPath, finalPath);
                    Console.WriteLine($"[PsarcImporter] Imported '{title}' -> {finalPath}");
                    succeeded++;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PsarcImporter] Failed to import '{Path.GetFileName(psarcPath)}': {ex.Message}");
                failed++;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(fileWorkDir))
                        Directory.Delete(fileWorkDir, true);
                }
                catch
                {
                }
            }
        }

        Console.WriteLine($"[PsarcImporter] Done: {succeeded} imported, {skipped} skipped, {failed} failed");
        return failed > 0 ? 1 : 0;
    }
}
