namespace PsarcImporter.Conversion;

internal static class AudioConverter
{
    public static void ConvertAll(string contentDirectory)
    {
        foreach (string wemPath in Directory.GetFiles(contentDirectory, "*.wem", SearchOption.TopDirectoryOnly))
        {
            if (File.Exists(Path.ChangeExtension(wemPath, ".ogg")))
                continue;

            if (OperatingSystem.IsMacOS())
            {
                MacWemAudioConverter.ConvertWithFallback(wemPath);
                continue;
            }

            Rocksmith2014.Audio.Conversion.wemToOgg(wemPath);
        }
    }

    public static string? SelectPrimaryAudioPath(string contentDirectory)
    {
        return Directory.GetFiles(contentDirectory, "*.ogg", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("preview", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static string? SelectPreviewAudioPath(string contentDirectory)
    {
        return Directory.GetFiles(contentDirectory, "*.ogg", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileNameWithoutExtension(path).Contains("preview", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
