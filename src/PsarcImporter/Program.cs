using System.IO.Compression;
using System.Text.Json;

namespace PsarcImporter;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Parse arguments
        string? sourcePath = null;
        string? outputPath = null;
        string? workDirectory = null;
        bool force = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--source", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                sourcePath = args[++i];
            else if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                outputPath = args[++i];
            else if (string.Equals(args[i], "--work", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                workDirectory = args[++i];
            else if (string.Equals(args[i], "--force", StringComparison.OrdinalIgnoreCase))
                force = true;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("Usage: PsarcImporter --source <psarcPath|sourceDir> --output <outputTheoryPath|outputDir> [--work <workDir>] [--force]");
            return 2;
        }

        string fullPath = Path.GetFullPath(sourcePath);

        if (Directory.Exists(fullPath))
            return await RunBatch(fullPath, Path.GetFullPath(outputPath), workDirectory, force);
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

    private static async Task<int> RunBatch(string sourceDir, string outputDir, string? workDirectory, bool force)
    {
        Directory.CreateDirectory(outputDir);

        string[] psarcFiles = Directory.GetFiles(sourceDir, "*.psarc", SearchOption.TopDirectoryOnly);

        if (psarcFiles.Length == 0)
        {
            Console.Error.WriteLine($"[PsarcImporter] No .psarc files found in {sourceDir}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(workDirectory))
            workDirectory = Path.Combine(Path.GetTempPath(), $"PsarcImporter_{Guid.NewGuid():N}");

        Console.WriteLine($"[PsarcImporter] Found {psarcFiles.Length} .psarc file(s) in {sourceDir}");

        // Build set of existing songs from output directory metadata
        var existingSongs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!force)
            existingSongs = LoadExistingSongMetadata(outputDir);

        int succeeded = 0;
        int failed = 0;
        int skipped = 0;
        var failedFiles = new List<(string File, string Error)>();

        foreach (string psarcPath in psarcFiles)
        {
            string fileWorkDir = Path.Combine(workDirectory, Guid.NewGuid().ToString("N"));
            string tempOutputPath = Path.Combine(fileWorkDir, "temp.theory");

            try
            {
                Directory.CreateDirectory(fileWorkDir);
                var (title, artist) = await PsarcImporter.PsarcConverter.ConvertAsync(
                    psarcPath, tempOutputPath, fileWorkDir);

                // Check for duplicates by metadata
                if (!force && existingSongs.Contains(GetSongKey(title, artist)))
                {
                    Console.WriteLine($"[PsarcImporter] Skipping '{title}' by '{artist}' (already in output)");
                    skipped++;
                    continue;
                }

                string sanitizedTitle = PsarcImporter.PsarcConverter.SanitizeFileName(title);
                string sanitizedArtist = PsarcImporter.PsarcConverter.SanitizeFileName(artist);
                string fileName = string.IsNullOrWhiteSpace(artist)
                    ? $"{sanitizedTitle}.theory"
                    : $"{sanitizedArtist} - {sanitizedTitle}.theory";
                string finalPath = Path.Combine(outputDir, fileName);

                if (File.Exists(finalPath))
                {
                    Console.WriteLine($"[PsarcImporter] Skipping '{title}' (file already exists)");
                    skipped++;
                }
                else
                {
                    File.Move(tempOutputPath, finalPath);
                    Console.WriteLine($"[PsarcImporter] Imported '{title}' -> {fileName}");
                    succeeded++;
                }
            }
            catch (Exception ex)
            {
                string error = ex.InnerException?.Message ?? ex.Message;
                Console.Error.WriteLine($"[PsarcImporter] Failed to import '{Path.GetFileName(psarcPath)}': {error}");
                failedFiles.Add((Path.GetFileName(psarcPath), error));
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

        // Print summary
        Console.WriteLine($"[PsarcImporter] Done: {succeeded} imported, {skipped} skipped, {failed} failed");

        if (failedFiles.Count > 0)
        {
            Console.Error.WriteLine($"[PsarcImporter] Failed files:");
            foreach (var (file, error) in failedFiles)
                Console.Error.WriteLine($"  - {file}: {error}");
        }

        return failed > 0 ? 1 : 0;
    }

    internal static HashSet<string> LoadExistingSongMetadata(string outputDir)
    {
        var songs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] theoryFiles = Directory.GetFiles(outputDir, "*.theory", SearchOption.TopDirectoryOnly);

        foreach (string theoryPath in theoryFiles)
        {
            try
            {
                using var stream = File.OpenRead(theoryPath);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null) continue;

                using var manifestStream = manifestEntry.Open();
                var manifest = JsonSerializer.Deserialize<TheorySongManifest>(manifestStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest != null)
                    songs.Add(GetSongKey(manifest.title, manifest.artist));
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        return songs;
    }

    internal static string GetSongKey(string title, string artist)
    {
        return $"{title.Trim()}|{artist.Trim()}";
    }
}
