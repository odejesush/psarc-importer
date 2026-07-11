using System.Globalization;
using Rocksmith2014.XML;
using PsarcImporter.Models;

namespace PsarcImporter.Models;

internal static class NoteBuilder
{
    private const float RocksmithVibratoCyclesPerSecond = 5f;
    private const float RocksmithBendDrivenVibratoMinimumHoldSeconds = 0.12f;

    private static readonly int[] StandardGuitarTuning = { 40, 45, 50, 55, 59, 64 };
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static CachedNoteData BuildGameplayNote(SourceNote source, ArrangementVariantContext context, int noteId, int chordId)
    {
        float startSeconds = source.TimeMs / 1000f;
        float durationSeconds = Math.Max(0f, source.SustainMs / 1000f);
        float bendStep = source.BendValues.Count > 0
            ? source.BendValues.Max(value => Math.Abs(value.Step))
            : Math.Max(0f, source.MaxBend);
        List<CachedTechniqueSegmentData> segments = BuildTechniqueSegments(source, durationSeconds);
        bool hasBend = segments.Any(segment => segment.type == 1);
        float bendVisualStartTime = hasBend ? startSeconds : -1f;
        float bendVisualDuration = hasBend ? durationSeconds : 0f;

        return new CachedNoteData
        {
            id = noteId,
            time = startSeconds,
            duration = durationSeconds,
            stringIdx = source.String,
            fret = source.Fret,
            note = GetNoteName(ComputeMidiNote(context.Arrangement.TuningPitches, source.String, source.Fret)),
            chordId = chordId,
            chordName = source.ChordName,
            technique = DetermineGameplayTechnique(source),
            slideTargetFret = source.SlideTargetFret,
            bendStep = bendStep,
            bendVisualStartTime = bendVisualStartTime,
            bendVisualDuration = bendVisualDuration,
            bendPreBend = StartsWithPreBend(source.BendValues),
            bendRelease = HasBendRelease(source.BendValues),
            isMuted = source.IsPalmMute || source.IsFretHandMute,
            isPalmMute = source.IsPalmMute,
            isFretHandMute = source.IsFretHandMute,
            isHarmonic = source.IsHarmonic,
            isAccent = source.IsAccent,
            isTap = source.IsTap,
            isTremolo = source.IsTremolo,
            isPinchHarmonic = source.IsPinchHarmonic,
            isHammerOn = source.IsHammerOn,
            isPullOff = source.IsPullOff,
            isHopo = source.IsHopo,
            hasVibrato = source.HasVibrato,
            vibratoStrength = source.VibratoStrength,
            maxBend = source.MaxBend,
            isLegato = false,
            requiresPluck = true,
            linkedFromNoteId = -1,
            bendPoints = source.BendValues
                .Select(point => new CachedBendPointData
                {
                    timeSeconds = Math.Max(0f, point.TimeMs / 1000f),
                    step = point.Step
                })
                .ToList(),
            techniqueSegments = segments
        };
    }

    public static void ApplyLegatoLink(CachedNoteData note, SourceNote source, Dictionary<string, CachedNoteData> previousByStringRoute)
    {
        string key = source.String.ToString(CultureInfo.InvariantCulture);
        if (!previousByStringRoute.TryGetValue(key, out CachedNoteData? previous))
            return;

        if (!(source.SlideTargetFret >= 0 || source.IsHammerOn || source.IsPullOff || source.IsHopo))
            return;

        note.isLegato = true;
        note.requiresPluck = false;
        note.linkedFromNoteId = previous.id;

        if (note.technique == 0)
        {
            if (source.SlideTargetFret >= 0)
                note.technique = 3;
            else if (source.IsHammerOn)
                note.technique = 1;
            else if (source.IsPullOff)
                note.technique = 2;
            else if (source.IsHopo)
            {
                if (note.fret > previous.fret)
                    note.technique = 1;
                else if (note.fret < previous.fret)
                    note.technique = 2;
            }
        }
    }

    public static CachedGeneratedNoteEvent BuildGeneratedNote(SourceNote source, ArrangementVariantContext context, CachedNoteData note)
    {
        List<CachedGeneratedPitchPoint> pitchCurve = BuildPitchCurve(source, note.duration);
        int pitchBendRange = 0;
        for (int i = 0; i < pitchCurve.Count; i++)
            pitchBendRange = Math.Max(pitchBendRange, (int)Math.Ceiling(Math.Abs(pitchCurve[i].semitoneOffset)));

        bool usesBendDrivenVibrato = UsesBendDrivenVibrato(source);
        float genericVibratoDepth = !usesBendDrivenVibrato && source.HasVibrato
            ? ResolveRocksmithVibratoDepthSemitones(source.VibratoStrength, bendDriven: false)
            : 0f;

        return new CachedGeneratedNoteEvent
        {
            startTimeSeconds = note.time,
            durationSeconds = Math.Max(0.05f, note.duration),
            midiNote = ComputeMidiNote(context.Arrangement.TuningPitches, source.String, source.Fret),
            velocity = source.IsPalmMute || source.IsFretHandMute ? 86 : 112,
            channel = 0,
            partId = context.PartId,
            partName = context.DisplayName,
            techniqueVariant = DetermineGeneratedTechniqueVariant(source),
            legatoTransitionKind = DetermineGeneratedLegatoTransitionKind(source),
            attackVelocityScale = source.IsPalmMute || source.IsFretHandMute ? 0.82f : 1f,
            vibratoDepthSemitones = genericVibratoDepth,
            vibratoRateHz = genericVibratoDepth > 0.01f ? RocksmithVibratoCyclesPerSecond : 0f,
            vibratoDelayNormalized = genericVibratoDepth > 0.01f ? 0.05f : 0f,
            vibratoFadeNormalized = genericVibratoDepth > 0.01f ? 0.35f : 0f,
            pitchBendRangeSemitones = pitchBendRange,
            pitchCurve = pitchCurve
        };
    }

    public static List<CachedTechniqueSegmentData> BuildTechniqueSegments(SourceNote source, float durationSeconds)
    {
        List<CachedTechniqueSegmentData> segments = new();
        if (durationSeconds > 0.35f)
        {
            segments.Add(new CachedTechniqueSegmentData
            {
                type = 2,
                startOffset = 0f,
                endOffset = durationSeconds,
                startFret = source.Fret,
                endFret = source.Fret,
                startBend = 0f,
                endBend = 0f
            });
        }

        if (source.SlideTargetFret >= 0)
        {
            segments.Add(new CachedTechniqueSegmentData
            {
                type = 0,
                startOffset = 0f,
                endOffset = Math.Max(0.05f, durationSeconds > 0f ? durationSeconds : 0.15f),
                startFret = source.Fret,
                endFret = source.SlideTargetFret,
                startBend = 0f,
                endBend = 0f
            });
        }

        if (source.BendValues.Count > 0)
        {
            BendPoint firstPoint = source.BendValues[0];
            float firstPointTime = Math.Clamp(firstPoint.TimeMs / 1000f, 0f, Math.Max(durationSeconds, 0.001f));
            bool startsWithPreBend = StartsWithPreBend(source.BendValues);
            if (firstPointTime > 0.0001f && Math.Abs(firstPoint.Step) > 0.01f)
            {
                segments.Add(new CachedTechniqueSegmentData
                {
                    type = 1,
                    startOffset = 0f,
                    endOffset = firstPointTime,
                    startFret = source.Fret,
                    endFret = source.Fret,
                    startBend = startsWithPreBend ? firstPoint.Step : 0f,
                    endBend = firstPoint.Step
                });
            }

            for (int i = 1; i < source.BendValues.Count; i++)
            {
                BendPoint previous = source.BendValues[i - 1];
                BendPoint current = source.BendValues[i];
                float startOffset = Math.Clamp(previous.TimeMs / 1000f, 0f, Math.Max(durationSeconds, 0.001f));
                float endOffset = Math.Clamp(current.TimeMs / 1000f, 0f, Math.Max(durationSeconds, 0.001f));
                if (endOffset <= startOffset + 0.0001f)
                    continue;

                bool useBendDrivenVibrato =
                    source.HasVibrato &&
                    IsFlatBendHold(previous, current) &&
                    (endOffset - startOffset) >= RocksmithBendDrivenVibratoMinimumHoldSeconds;
                segments.Add(new CachedTechniqueSegmentData
                {
                    type = useBendDrivenVibrato ? 3 : 1,
                    startOffset = startOffset,
                    endOffset = endOffset,
                    startFret = source.Fret,
                    endFret = source.Fret,
                    startBend = previous.Step,
                    endBend = current.Step
                });
            }

            if (source.HasVibrato &&
                source.BendValues.Count == 1 &&
                Math.Abs(source.BendValues[0].Step) > 0.01f &&
                Math.Max(0f, durationSeconds) >= RocksmithBendDrivenVibratoMinimumHoldSeconds)
            {
                segments.Add(new CachedTechniqueSegmentData
                {
                    type = 3,
                    startOffset = 0f,
                    endOffset = Math.Max(0.1f, durationSeconds),
                    startFret = source.Fret,
                    endFret = source.Fret,
                    startBend = source.BendValues[0].Step,
                    endBend = source.BendValues[0].Step
                });
            }
        }

        if (source.HasVibrato && !UsesBendDrivenVibrato(source))
        {
            segments.Add(new CachedTechniqueSegmentData
            {
                type = 3,
                startOffset = 0f,
                endOffset = Math.Max(0.1f, durationSeconds),
                startFret = source.Fret,
                endFret = source.Fret,
                startBend = 0f,
                endBend = 0f
            });
        }

        return segments;
    }

    private static List<CachedGeneratedPitchPoint> BuildPitchCurve(SourceNote source, float durationSeconds)
    {
        List<CachedGeneratedPitchPoint> curve = new()
        {
            new CachedGeneratedPitchPoint { normalizedTime = 0f, semitoneOffset = 0f }
        };

        if (source.BendValues.Count > 0 && durationSeconds > 0.0001f)
        {
            if (StartsWithPreBend(source.BendValues) && source.BendValues[0].TimeMs > 0)
                curve[0].semitoneOffset = source.BendValues[0].Step;

            for (int i = 0; i < source.BendValues.Count; i++)
            {
                BendPoint point = source.BendValues[i];
                AddOrReplacePitchPoint(
                    curve,
                    Math.Clamp(point.TimeMs / 1000f / Math.Max(durationSeconds, 0.0001f), 0f, 1f),
                    point.Step);

                if (source.HasVibrato && i + 1 < source.BendValues.Count)
                {
                    BendPoint nextPoint = source.BendValues[i + 1];
                    AppendBendDrivenVibratoPoints(curve, source, point, nextPoint, durationSeconds);
                }
            }

            if (UsesBendDrivenVibrato(source) &&
                source.BendValues.Count == 1 &&
                Math.Abs(source.BendValues[0].Step) > 0.01f)
            {
                AppendBendDrivenVibratoHold(
                    curve,
                    source,
                    0f,
                    1f,
                    source.BendValues[0].Step,
                    durationSeconds);
            }
        }

        if (curve[^1].normalizedTime < 1f)
        {
            curve.Add(new CachedGeneratedPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = curve[^1].semitoneOffset
            });
        }

        return curve;
    }

    private static void AppendBendDrivenVibratoPoints(
        List<CachedGeneratedPitchPoint> curve,
        SourceNote source,
        BendPoint start,
        BendPoint end,
        float durationSeconds)
    {
        if (!source.HasVibrato || !UsesBendDrivenVibrato(source))
            return;

        if (!IsFlatBendHold(start, end))
            return;

        float spanSeconds = Math.Max(0f, (end.TimeMs - start.TimeMs) / 1000f);
        if (spanSeconds < RocksmithBendDrivenVibratoMinimumHoldSeconds || durationSeconds <= 0.0001f)
            return;
        float startNormalized = Math.Clamp(start.TimeMs / 1000f / durationSeconds, 0f, 1f);
        float endNormalized = Math.Clamp(end.TimeMs / 1000f / durationSeconds, 0f, 1f);
        AppendBendDrivenVibratoHold(curve, source, startNormalized, endNormalized, start.Step, durationSeconds);
    }

    private static void AddOrReplacePitchPoint(List<CachedGeneratedPitchPoint> curve, float normalizedTime, float semitoneOffset)
    {
        if (curve.Count > 0 && Math.Abs(curve[^1].normalizedTime - normalizedTime) <= 0.0005f)
        {
            curve[^1] = new CachedGeneratedPitchPoint
            {
                normalizedTime = normalizedTime,
                semitoneOffset = semitoneOffset
            };
            return;
        }

        curve.Add(new CachedGeneratedPitchPoint
        {
            normalizedTime = normalizedTime,
            semitoneOffset = semitoneOffset
        });
    }

    private static bool UsesBendDrivenVibrato(SourceNote source)
    {
        return source is { HasVibrato: true, BendValues.Count: > 0 } &&
               (source.BendValues.Count > 1 || Math.Abs(source.BendValues[0].Step) > 0.01f);
    }

    private static void AppendBendDrivenVibratoHold(
        List<CachedGeneratedPitchPoint> curve,
        SourceNote source,
        float startNormalized,
        float endNormalized,
        float baseline,
        float durationSeconds)
    {
        float spanNormalized = endNormalized - startNormalized;
        if (spanNormalized <= 0.0001f || durationSeconds <= 0.0001f)
            return;

        float amplitude = ResolveRocksmithVibratoDepthSemitones(source.VibratoStrength, bendDriven: true);
        if (amplitude <= 0.01f)
            return;

        float spanSeconds = spanNormalized * durationSeconds;
        int halfWaves = Math.Clamp(Math.Max(4, (int)Math.Round(spanSeconds * RocksmithVibratoCyclesPerSecond * 2f)), 4, 16);
        for (int index = 1; index < halfWaves; index++)
        {
            float normalizedT = index / (float)halfWaves;
            float pointTime = startNormalized + (spanNormalized * normalizedT);
            float offset = baseline + (MathF.Sin(normalizedT * MathF.PI * 2f) * amplitude);
            AddOrReplacePitchPoint(curve, pointTime, offset);
        }

        AddOrReplacePitchPoint(curve, endNormalized, baseline);
    }

    private static bool IsFlatBendHold(BendPoint start, BendPoint end)
    {
        return Math.Abs(start.Step - end.Step) <= 0.01f && end.TimeMs > start.TimeMs;
    }

    private static float ResolveRocksmithVibratoDepthSemitones(int rawStrength, bool bendDriven)
    {
        if (rawStrength >= 110)
            return bendDriven ? 0.18f : 0.26f;
        if (rawStrength >= 70)
            return bendDriven ? 0.15f : 0.22f;
        if (rawStrength > 0)
            return bendDriven ? 0.12f : 0.18f;
        return bendDriven ? 0.15f : 0.22f;
    }

    private static int DetermineGameplayTechnique(SourceNote source)
    {
        if (source.BendValues.Count > 0 || source.MaxBend > 0.01f)
            return 4;
        if (source.SlideTargetFret >= 0)
            return 3;
        if (source.IsHammerOn)
            return 1;
        if (source.IsPullOff)
            return 2;
        if (source.HasVibrato)
            return 5;
        return 0;
    }

    private static int DetermineGeneratedTechniqueVariant(SourceNote source)
    {
        if (source.IsPalmMute) return 1;
        if (source.IsFretHandMute) return 2;
        if (source.IsHarmonic) return 3;
        return 0;
    }

    private static int DetermineGeneratedLegatoTransitionKind(SourceNote source)
    {
        if (source.SlideTargetFret >= 0) return 1;
        if (source.IsHammerOn) return 2;
        if (source.IsPullOff) return 3;
        return 0;
    }

    private static int ComputeMidiNote(int[] tuningPitches, int stringIndex, int fret)
    {
        int basePitch = tuningPitches != null && stringIndex >= 0 && stringIndex < tuningPitches.Length
            ? tuningPitches[stringIndex]
            : StandardGuitarTuning[Math.Clamp(stringIndex, 0, StandardGuitarTuning.Length - 1)];
        return basePitch + fret;
    }

    private static string GetNoteName(int midiNote)
    {
        return NoteNames[Math.Abs(midiNote) % 12];
    }

    private static bool StartsWithPreBend(List<BendPoint> bendValues)
    {
        return bendValues.Count > 0 && bendValues[0].TimeMs <= 1 && Math.Abs(bendValues[0].Step) > 0.01f;
    }

    private static bool HasBendRelease(List<BendPoint> bendValues)
    {
        for (int i = 1; i < bendValues.Count; i++)
        {
            if (bendValues[i].Step < bendValues[i - 1].Step - 0.01f)
                return true;
        }
        return false;
    }

    public static string ResolveChordDisplayName(ChordTemplate? template)
    {
        if (template == null)
            return string.Empty;

        string candidate = string.IsNullOrWhiteSpace(template.DisplayName)
            ? template.Name
            : template.DisplayName;

        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        return candidate
            .Replace("min", "m", StringComparison.OrdinalIgnoreCase)
            .Replace("CONV", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-nop", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-arp", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
