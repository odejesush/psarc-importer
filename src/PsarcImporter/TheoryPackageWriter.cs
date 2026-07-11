using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using PsarcImporter.Models;

namespace PsarcImporter;

internal static class TheoryPackageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(
        string outputPath,
        TheorySongManifest manifest,
        List<TheoryArrangementData> arrangements,
        string? primaryAudioPath,
        string? previewAudioPath,
        string? coverArtPath,
        CachedSongManifest cachedManifest,
        string audioEntryDir)
    {
        using FileStream stream = File.Create(outputPath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        // Write manifest
        string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using (Stream writer = manifestEntry.Open())
        using (StreamWriter sw = new(writer))
        {
            sw.Write(manifestJson);
        }

        // Write arrangements
        foreach (TheoryArrangementData arrangement in arrangements)
        {
            string entryName = $"arrangements/{SanitizeEntryName(arrangement.arrangementId)}.json";
            string arrangementJson = JsonSerializer.Serialize(arrangement, JsonOptions);
            ZipArchiveEntry arrEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream writer = arrEntry.Open())
            using (StreamWriter sw = new(writer))
            {
                sw.Write(arrangementJson);
            }
        }

        // Write primary audio
        if (!string.IsNullOrWhiteSpace(primaryAudioPath) && File.Exists(primaryAudioPath))
        {
            string audioEntry = string.IsNullOrWhiteSpace(manifest.primaryAudioEntry)
                ? $"audio/{Path.GetFileName(primaryAudioPath)}"
                : manifest.primaryAudioEntry;
            archive.CreateEntryFromFile(primaryAudioPath, audioEntry, CompressionLevel.NoCompression);
        }

        // Write preview audio
        if (!string.IsNullOrWhiteSpace(previewAudioPath) && File.Exists(previewAudioPath))
        {
            archive.CreateEntryFromFile(previewAudioPath, "audio/song_preview.ogg", CompressionLevel.NoCompression);
        }

        // Write cover art
        if (!string.IsNullOrWhiteSpace(coverArtPath) && File.Exists(coverArtPath))
        {
            archive.CreateEntryFromFile(coverArtPath, "assets/cover.png", CompressionLevel.Optimal);
        }
    }

    private static string SanitizeEntryName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "arrangement";

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (System.Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
                chars[i] = '_';
        }

        string result = new string(chars).Trim('_', '.', ' ');
        return string.IsNullOrWhiteSpace(result) ? "arrangement" : result;
    }
}

internal sealed class TheorySongManifest
{
    public string formatId { get; set; } = "string-theory-song";
    public int schemaVersion { get; set; } = 2;
    public string packageId { get; set; } = Guid.NewGuid().ToString("N");
    public long createdAtUtcTicks { get; set; }
    public long modifiedAtUtcTicks { get; set; }
    public string title { get; set; } = string.Empty;
    public string artist { get; set; } = string.Empty;
    public string album { get; set; } = string.Empty;
    public string subtitle { get; set; } = string.Empty;
    public string genre { get; set; } = string.Empty;
    public string year { get; set; } = string.Empty;
    public string defaultArrangementId { get; set; } = string.Empty;
    public string primaryAudioEntry { get; set; } = string.Empty;
    public string coverArtEntry { get; set; } = string.Empty;
    public float durationSeconds { get; set; }
    public int difficultyRating { get; set; }
    public TheoryImportProvenance provenance { get; set; } = new();
    public List<TheoryAudioAsset> audio { get; set; } = new();
    public List<TheoryStemAsset> stems { get; set; } = new();
    public List<TheoryArrangementSummary> arrangements { get; set; } = new();
}

internal sealed class TheoryImportProvenance
{
    public string sourceType { get; set; } = string.Empty;
    public string sourceDisplayName { get; set; } = string.Empty;
    public string sourcePath { get; set; } = string.Empty;
    public long sourceLastWriteUtcTicks { get; set; }
    public long sourceSizeBytes { get; set; }
    public string sourceContentFingerprint { get; set; } = string.Empty;
    public long importedAtUtcTicks { get; set; }
    public string converterName { get; set; } = string.Empty;
    public string converterVersion { get; set; } = string.Empty;
}

internal sealed class TheoryAudioAsset
{
    public string id { get; set; } = string.Empty;
    public string entry { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string role { get; set; } = string.Empty;
    public string contentType { get; set; } = string.Empty;
    public long sourceSizeBytes { get; set; }
    public bool defaultForPlayback { get; set; } = true;
}

internal sealed class TheoryStemAsset
{
    public string id { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string entry { get; set; } = string.Empty;
    public string contentType { get; set; } = string.Empty;
    public long sourceSizeBytes { get; set; }
    public string provider { get; set; } = "demucs";
    public string model { get; set; } = string.Empty;
    public long generatedAtUtcTicks { get; set; }
}

internal sealed class TheoryArrangementSummary
{
    public string arrangementId { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string instrumentType { get; set; } = string.Empty;
    public string route { get; set; } = string.Empty;
    public string groupId { get; set; } = string.Empty;
    public string groupDisplayName { get; set; } = string.Empty;
    public string difficultyLabel { get; set; } = string.Empty;
    public int difficultyUiIndex { get; set; } = -1;
    public bool hasDifficultyVariants { get; set; }
    public string entry { get; set; } = string.Empty;
    public int noteCount { get; set; }
    public int tabCount { get; set; }
    public int score { get; set; }
    public int difficultyRating { get; set; }
    public bool preserveImportedRuntimeNotes { get; set; }
    public int[]? tuningPitches { get; set; }
    public string tuningDisplayName { get; set; } = string.Empty;
}

internal sealed class TheoryArrangementData
{
    public int schemaVersion { get; set; } = 2;
    public string arrangementId { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string instrumentType { get; set; } = string.Empty;
    public string route { get; set; } = string.Empty;
    public string groupId { get; set; } = string.Empty;
    public string groupDisplayName { get; set; } = string.Empty;
    public string difficultyLabel { get; set; } = string.Empty;
    public int difficultyUiIndex { get; set; } = -1;
    public bool hasDifficultyVariants { get; set; }
    public float durationSeconds { get; set; }
    public int difficultyRating { get; set; }
    public bool preserveImportedRuntimeNotes { get; set; }
    public int[]? tuningPitches { get; set; }
    public string tuningDisplayName { get; set; } = string.Empty;
    public TheoryTimingData timing { get; set; } = new();
    public TheoryToneData tones { get; set; } = new();
    public TheoryGeneratedPartInfo generatedPart { get; set; } = new();
    public List<TheoryNoteData> notes { get; set; } = new();
    public List<TheoryArpeggioGuide> arpeggioGuides { get; set; } = new();
    public List<TheoryGeneratedChannelAssignment> generatedChannels { get; set; } = new();
    public List<TheoryGeneratedNoteEvent> generatedNotes { get; set; } = new();
}

internal sealed class TheoryTimingData
{
    public float averageTempoBpm { get; set; } = 120f;
    public int capo { get; set; }
    public List<TheoryBeatData> beats { get; set; } = new();
    public List<TheorySectionData> sections { get; set; } = new();
}

internal sealed class TheoryBeatData
{
    public float timeSeconds { get; set; }
    public short measure { get; set; } = -1;
}

internal sealed class TheorySectionData
{
    public string name { get; set; } = string.Empty;
    public short number { get; set; }
    public float timeSeconds { get; set; }
}

internal sealed class TheoryToneData
{
    public string baseToneName { get; set; } = string.Empty;
    public List<TheoryToneChangeData> changes { get; set; } = new();
    public List<TheoryToneDefinitionData> definitions { get; set; } = new();
}

internal sealed class TheoryToneChangeData
{
    public float timeSeconds { get; set; }
    public string toneName { get; set; } = string.Empty;
    public int toneId { get; set; } = -1;
}

internal sealed class TheoryToneDefinitionData
{
    public string name { get; set; } = string.Empty;
    public string key { get; set; } = string.Empty;
    public string rawToneEntry { get; set; } = string.Empty;
    public string preferredPresetName { get; set; } = string.Empty;
    public string fallbackSearchText { get; set; } = string.Empty;
    public TheoryTonePresetData preset { get; set; } = new();
    public TheoryToneFallbackData fallback { get; set; } = new();
}

internal sealed class TheoryTonePresetData
{
    public string presetId { get; set; } = string.Empty;
    public string presetName { get; set; } = string.Empty;
    public float inputGainDb { get; set; }
    public float outputGainDb { get; set; }
    public List<TheoryTonePedalSlotData> pedalChain { get; set; } = new();
}

internal sealed class TheoryTonePedalSlotData
{
    public string instanceId { get; set; } = string.Empty;
    public string pedalType { get; set; } = string.Empty;
    public string descriptorId { get; set; } = string.Empty;
    public bool enabled { get; set; } = true;
    public string settingsJson { get; set; } = string.Empty;
}

internal sealed class TheoryToneFallbackData
{
    public string preferredPresetName { get; set; } = string.Empty;
    public string searchText { get; set; } = string.Empty;
}

internal sealed class TheoryNoteData
{
    public int id { get; set; }
    public float time { get; set; }
    public float duration { get; set; }
    public int stringIndex { get; set; }
    public int fret { get; set; }
    public string noteName { get; set; } = string.Empty;
    public int chordId { get; set; } = -1;
    public string chordName { get; set; } = string.Empty;
    public int primaryTechnique { get; set; }
    public int slideTargetFret { get; set; } = -1;
    public float bendStep { get; set; }
    public float bendVisualStartTime { get; set; } = -1f;
    public float bendVisualDuration { get; set; }
    public bool bendPreBend { get; set; }
    public bool bendRelease { get; set; }
    public bool muted { get; set; }
    public bool palmMute { get; set; }
    public bool fretHandMute { get; set; }
    public bool harmonic { get; set; }
    public bool accent { get; set; }
    public bool tap { get; set; }
    public bool tremolo { get; set; }
    public bool pinchHarmonic { get; set; }
    public bool hammerOn { get; set; }
    public bool pullOff { get; set; }
    public bool hopo { get; set; }
    public bool vibrato { get; set; }
    public int vibratoStrength { get; set; }
    public float maxBend { get; set; }
    public bool legato { get; set; }
    public bool requiresPluck { get; set; } = true;
    public int linkedFromNoteId { get; set; } = -1;
    public List<TheoryBendPointData> bendPoints { get; set; } = new();
    public List<TheoryTechniqueSegmentData> techniqueSegments { get; set; } = new();
}

internal sealed class TheoryBendPointData
{
    public float timeSeconds { get; set; }
    public float step { get; set; }
}

internal sealed class TheoryTechniqueSegmentData
{
    public int type { get; set; }
    public float startOffset { get; set; }
    public float endOffset { get; set; }
    public int startFret { get; set; }
    public int endFret { get; set; }
    public float startBend { get; set; }
    public float endBend { get; set; }
}

internal sealed class TheoryArpeggioGuide
{
    public int id { get; set; }
    public float startTime { get; set; }
    public float endTime { get; set; }
    public string chordName { get; set; } = string.Empty;
    public int[] stringFrets { get; set; } = System.Array.Empty<int>();
}

internal sealed class TheoryGeneratedPartInfo
{
    public string partId { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string instrumentName { get; set; } = string.Empty;
    public int sourceMidiChannel { get; set; } = -1;
    public int sourceMidiProgram { get; set; } = 29;
    public int preferredBank { get; set; } = -1;
    public bool isDrum { get; set; }
    public bool isGuitarFamily { get; set; } = true;
    public bool isExplicitHarmonicPart { get; set; }
}

internal sealed class TheoryGeneratedChannelAssignment
{
    public int channel { get; set; }
    public int bank { get; set; } = -1;
    public int preset { get; set; } = 29;
    public bool isDrum { get; set; }
    public string label { get; set; } = string.Empty;
    public string sourcePartId { get; set; } = string.Empty;
    public string sourcePartName { get; set; } = string.Empty;
    public int pitchBendRangeSemitones { get; set; }
}

internal sealed class TheoryGeneratedNoteEvent
{
    public float startTimeSeconds { get; set; }
    public float durationSeconds { get; set; }
    public float pitchPreRollSeconds { get; set; }
    public int midiNote { get; set; }
    public int velocity { get; set; }
    public int channel { get; set; }
    public string partId { get; set; } = string.Empty;
    public string partName { get; set; } = string.Empty;
    public int techniqueVariant { get; set; }
    public int legatoTransitionKind { get; set; }
    public float attackVelocityScale { get; set; } = 1f;
    public float vibratoDepthSemitones { get; set; }
    public float vibratoRateHz { get; set; }
    public float vibratoDelayNormalized { get; set; }
    public float vibratoFadeNormalized { get; set; }
    public int pitchBendRangeSemitones { get; set; }
    public List<TheoryGeneratedPitchPoint> pitchCurve { get; set; } = new();
}

internal sealed class TheoryGeneratedPitchPoint
{
    public float normalizedTime { get; set; }
    public float semitoneOffset { get; set; }
}
