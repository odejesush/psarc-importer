namespace PsarcImporter.Models;

internal static class CachedSongFormat
{
    public const int SchemaVersion = 20;
    public const string ManifestFileName = "song.rs2song.json";
    public const string ContentDirectoryName = "psarc_content";
}

internal sealed class CachedSongManifest
{
    public int schemaVersion;
    public string sourcePsarcPath = string.Empty;
    public long sourcePsarcLastWriteUtcTicks;
    public long importedAtUtcTicks;
    public string displayName = string.Empty;
    public string artist = string.Empty;
    public string album = string.Empty;
    public string subtitle = string.Empty;
    public string artworkPath = string.Empty;
    public string audioPath = string.Empty;
    public string previewAudioPath = string.Empty;
    public float durationSeconds;
    public int difficultyRating;
    public int toneDefinitionScanVersion;
    public int toneDefinitionCount;
    public List<CachedArrangementSummary> arrangements = new();
}

internal sealed class CachedArrangementSummary
{
    public string partId = string.Empty;
    public string displayName = string.Empty;
    public string route = string.Empty;
    public string arrangementGroupId = string.Empty;
    public string arrangementDisplayName = string.Empty;
    public string difficultyLabel = string.Empty;
    public int difficultyUiIndex = -1;
    public bool hasDifficultyVariants;
    public string partFilePath = string.Empty;
    public int noteCount;
    public int tabCount;
    public int score;
    public int difficultyRating;
    public int[]? tuningPitches;
    public string tuningDisplayName = string.Empty;
}

internal sealed class CachedArrangementPart
{
    public int schemaVersion;
    public string partId = string.Empty;
    public string displayName = string.Empty;
    public string route = string.Empty;
    public string arrangementGroupId = string.Empty;
    public string arrangementDisplayName = string.Empty;
    public string difficultyLabel = string.Empty;
    public int difficultyUiIndex = -1;
    public bool hasDifficultyVariants;
    public float durationSeconds;
    public int difficultyRating;
    public int[]? tuningPitches;
    public string tuningDisplayName = string.Empty;
    public CachedArrangementTimingData timing = new();
    public CachedArrangementToneData tones = new();
    public CachedGeneratedPartInfo generatedPart = new();
    public List<CachedNoteData> notes = new();
    public List<CachedArpeggioGuideData> arpeggioGuides = new();
    public List<CachedGeneratedNoteEvent> generatedNotes = new();
}

internal sealed class CachedArrangementTimingData
{
    public float averageTempoBpm = 120f;
    public int capo;
    public List<CachedEbeatData> ebeats = new();
    public List<CachedSectionData> sections = new();
}

internal sealed class CachedEbeatData
{
    public float timeSeconds;
    public short measure = -1;
}

internal sealed class CachedSectionData
{
    public string name = string.Empty;
    public short number;
    public float timeSeconds;
}

internal sealed class CachedArrangementToneData
{
    public string baseToneName = string.Empty;
    public List<CachedToneChangeData> changes = new();
    public List<CachedToneDefinitionData> definitions = new();
}

internal sealed class CachedToneChangeData
{
    public float timeSeconds;
    public string toneName = string.Empty;
    public int toneId = -1;
}

internal sealed class CachedToneDefinitionData
{
    public string name = string.Empty;
    public string key = string.Empty;
    public string rawJson = string.Empty;
}

internal sealed class ManifestToneData
{
    public List<CachedToneDefinitionData> Definitions = new();
    public Dictionary<int, string> IdNameMap = new();
}

internal sealed class CachedNoteData
{
    public int id;
    public float time;
    public float duration;
    public int stringIdx;
    public int fret;
    public string note = string.Empty;
    public int chordId;
    public string chordName = string.Empty;
    public int technique;
    public int slideTargetFret = -1;
    public float bendStep;
    public float bendVisualStartTime = -1f;
    public float bendVisualDuration;
    public bool bendPreBend;
    public bool bendRelease;
    public bool isMuted;
    public bool isPalmMute;
    public bool isFretHandMute;
    public bool isHarmonic;
    public bool isAccent;
    public bool isTap;
    public bool isTremolo;
    public bool isPinchHarmonic;
    public bool isHammerOn;
    public bool isPullOff;
    public bool isHopo;
    public bool hasVibrato;
    public int vibratoStrength;
    public float maxBend;
    public bool isLegato;
    public bool requiresPluck = true;
    public int linkedFromNoteId = -1;
    public List<CachedBendPointData> bendPoints = new();
    public List<CachedTechniqueSegmentData> techniqueSegments = new();
}

internal sealed class CachedBendPointData
{
    public float timeSeconds;
    public float step;
}

internal sealed class CachedTechniqueSegmentData
{
    public int type;
    public float startOffset;
    public float endOffset;
    public int startFret;
    public int endFret;
    public float startBend;
    public float endBend;
}

internal sealed class CachedArpeggioGuideData
{
    public int id;
    public float startTime;
    public float endTime;
    public string chordName = string.Empty;
    public int[] stringFrets = System.Array.Empty<int>();
}

internal sealed class CachedGeneratedPartInfo
{
    public string partId = string.Empty;
    public string displayName = string.Empty;
    public string instrumentName = string.Empty;
    public int sourceMidiChannel = -1;
    public int sourceMidiProgram = 29;
    public int preferredBank = -1;
    public bool isDrum;
    public bool isGuitarFamily = true;
    public bool isExplicitHarmonicPart;
}

internal sealed class CachedGeneratedNoteEvent
{
    public float startTimeSeconds;
    public float durationSeconds;
    public float pitchPreRollSeconds;
    public int midiNote;
    public int velocity;
    public int channel;
    public string partId = string.Empty;
    public string partName = string.Empty;
    public int techniqueVariant;
    public int legatoTransitionKind;
    public float attackVelocityScale = 1f;
    public float vibratoDepthSemitones;
    public float vibratoRateHz;
    public float vibratoDelayNormalized;
    public float vibratoFadeNormalized;
    public int pitchBendRangeSemitones;
    public List<CachedGeneratedPitchPoint> pitchCurve = new();
}

internal sealed class CachedGeneratedPitchPoint
{
    public float normalizedTime;
    public float semitoneOffset;
}
