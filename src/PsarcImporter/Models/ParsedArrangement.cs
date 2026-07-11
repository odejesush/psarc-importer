using Rocksmith2014.XML;
using PsarcImporter.Models;

namespace PsarcImporter.Models;

internal sealed class ArrangementContext
{
    public required InstrumentalArrangement Arrangement;
    public required string Route;
    public required string DisplayName;
    public required string PartId;
    public required int[] TuningPitches;
    public required string TuningDisplayName;
    public CachedArrangementToneData Tones = new();

    public static ArrangementContext From(InstrumentalArrangement arrangement, string xmlPath, int index)
    {
        string route = string.IsNullOrWhiteSpace(arrangement.MetaData.Arrangement)
            ? InferRouteFromFileName(xmlPath)
            : arrangement.MetaData.Arrangement!;
        short partNumber = arrangement.MetaData.Part <= 0 ? (short)1 : arrangement.MetaData.Part;
        string displayName = partNumber > 1 ? $"{route} {partNumber}" : route;
        int[] tuningPitches = BuildTuningPitches(route, arrangement.MetaData.Tuning);
        return new ArrangementContext
        {
            Arrangement = arrangement,
            Route = route,
            DisplayName = displayName,
            PartId = $"{route.ToLowerInvariant()}::{partNumber}",
            TuningPitches = tuningPitches,
            TuningDisplayName = FormatTuningDisplayName(tuningPitches),
            Tones = new CachedArrangementToneData()
        };
    }

    private static string InferRouteFromFileName(string xmlPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(xmlPath) ?? string.Empty;
        if (fileName.Contains("bass", StringComparison.OrdinalIgnoreCase))
            return "Bass";
        if (fileName.Contains("rhythm", StringComparison.OrdinalIgnoreCase))
            return "Rhythm";
        if (fileName.Contains("combo", StringComparison.OrdinalIgnoreCase))
            return "Combo";
        return "Lead";
    }

    private static int[] BuildTuningPitches(string route, Tuning tuning)
    {
        bool isBassRoute = !string.IsNullOrWhiteSpace(route) && route.Contains("Bass", StringComparison.OrdinalIgnoreCase);
        int[] baseTuning = isBassRoute ? StandardBassTuning : StandardGuitarTuning;
        int stringCount = Math.Min(baseTuning.Length, tuning?.Strings?.Length ?? 0);
        if (stringCount <= 0)
            return (int[])baseTuning.Clone();

        int[] pitches = new int[stringCount];
        for (int i = 0; i < pitches.Length; i++)
            pitches[i] = baseTuning[i] + tuning.Strings[i];
        return pitches;
    }

    private static string FormatTuningDisplayName(int[] tuningPitches)
    {
        if (Matches(tuningPitches, new[] { 40, 45, 50, 55, 59, 64 })) return "E Standard";
        if (Matches(tuningPitches, new[] { 39, 44, 49, 54, 58, 63 })) return "Eb Standard";
        if (Matches(tuningPitches, new[] { 38, 43, 48, 53, 57, 62 })) return "D Standard";
        if (Matches(tuningPitches, new[] { 38, 45, 50, 55, 59, 64 })) return "Drop D";
        if (Matches(tuningPitches, new[] { 37, 44, 49, 54, 58, 63 })) return "Drop Db";
        if (Matches(tuningPitches, new[] { 36, 43, 48, 53, 57, 62 })) return "Drop C";
        if (Matches(tuningPitches, new[] { 28, 33, 38, 43 })) return "E Standard Bass";
        if (Matches(tuningPitches, new[] { 27, 32, 37, 42 })) return "Eb Standard Bass";
        if (Matches(tuningPitches, new[] { 26, 31, 36, 41 })) return "D Standard Bass";
        if (Matches(tuningPitches, new[] { 26, 33, 38, 43 })) return "Drop D Bass";
        if (Matches(tuningPitches, new[] { 25, 32, 37, 42 })) return "Drop Db Bass";
        if (Matches(tuningPitches, new[] { 24, 31, 36, 41 })) return "Drop C Bass";
        return $"Custom ({string.Join(" ", tuningPitches.Select(GetNoteName))})";
    }

    private static bool Matches(int[] left, int[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }

    private static string GetNoteName(int midiNote)
    {
        return NoteNames[Math.Abs(midiNote) % 12];
    }

    private static readonly int[] StandardGuitarTuning = { 40, 45, 50, 55, 59, 64 };
    private static readonly int[] StandardBassTuning = { 28, 33, 38, 43 };
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
}

internal sealed class ArrangementVariantSource
{
    public required Level Level;
    public bool IsExpertSource;
}

internal sealed class ArrangementVariantContext
{
    public required ArrangementContext Arrangement;
    public required Level SourceLevel;
    public required string PartId;
    public required string DisplayName;
    public required string DifficultyLabel;
    public int DifficultyUiIndex;
    public bool HasDifficultyVariants;
}

internal sealed class ArrangementVariantBuildResult
{
    public required ArrangementVariantContext Context;
    public required CachedArrangementPart Part;
}

internal sealed class SourceEvent
{
    public int TimeMs;
    public List<SourceNote> Notes = new();
}

internal sealed class SourceNote
{
    public int TimeMs;
    public int SustainMs;
    public int String;
    public int Fret;
    public string ChordName = string.Empty;
    public int SlideTargetFret = -1;
    public bool IsPalmMute;
    public bool IsFretHandMute;
    public bool IsAccent;
    public bool IsTap;
    public bool IsTremolo;
    public bool IsHammerOn;
    public bool IsPullOff;
    public bool IsHopo;
    public bool IsHarmonic;
    public bool IsPinchHarmonic;
    public bool HasVibrato;
    public int VibratoStrength;
    public float MaxBend;
    public List<BendPoint> BendValues = new();

    public static SourceNote FromNote(Note note, bool chordPalmMute, bool chordFretHandMute, bool chordHopo, string chordName)
    {
        return new SourceNote
        {
            TimeMs = note.Time,
            SustainMs = note.Sustain,
            String = note.String,
            Fret = note.Fret,
            ChordName = chordName ?? string.Empty,
            SlideTargetFret = note.SlideTo >= 0 ? note.SlideTo : note.SlideUnpitchTo >= 0 ? note.SlideUnpitchTo : -1,
            IsPalmMute = note.IsPalmMute || chordPalmMute,
            IsFretHandMute = note.IsFretHandMute || chordFretHandMute,
            IsAccent = note.IsAccent,
            IsTap = note.IsTap,
            IsTremolo = note.IsTremolo,
            IsHammerOn = note.IsHammerOn || chordHopo,
            IsPullOff = note.IsPullOff,
            IsHopo = note.IsHopo || chordHopo,
            IsHarmonic = note.IsHarmonic || note.IsPinchHarmonic,
            IsPinchHarmonic = note.IsPinchHarmonic,
            HasVibrato = note.Vibrato > 0,
            VibratoStrength = note.Vibrato,
            MaxBend = note.MaxBend,
            BendValues = BuildBendPoints(note.BendValues, note.Time)
        };
    }

    public static SourceNote FromTemplate(int timeMs, int stringIndex, int fret, bool palmMute, bool fretHandMute, bool hopo, bool linkNext, string chordName)
    {
        return new SourceNote
        {
            TimeMs = timeMs,
            SustainMs = 0,
            String = stringIndex,
            Fret = fret,
            ChordName = chordName ?? string.Empty,
            SlideTargetFret = -1,
            IsPalmMute = palmMute,
            IsFretHandMute = fretHandMute,
            IsAccent = false,
            IsTap = false,
            IsTremolo = false,
            IsHammerOn = hopo && linkNext,
            IsPullOff = false,
            IsHopo = hopo,
            IsHarmonic = false,
            IsPinchHarmonic = false,
            HasVibrato = false,
            VibratoStrength = 0,
            MaxBend = 0f,
            BendValues = new List<BendPoint>()
        };
    }

    private static List<BendPoint> BuildBendPoints(List<BendValue>? bendValues, int noteTimeMs)
    {
        List<BendPoint> result = new();
        if (bendValues == null)
            return result;

        for (int i = 0; i < bendValues.Count; i++)
        {
            int relativeTimeMs = Math.Max(0, bendValues[i].Time - noteTimeMs);
            result.Add(new BendPoint { TimeMs = relativeTimeMs, Step = bendValues[i].Step });
        }

        return result;
    }
}

internal sealed class BendPoint
{
    public int TimeMs;
    public float Step;
}
