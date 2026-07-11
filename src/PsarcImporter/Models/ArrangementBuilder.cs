using System.Globalization;
using Rocksmith2014.XML;
using Rocksmith2014.XML.Processing;
using PsarcImporter.Models;

namespace PsarcImporter.Models;

internal static class ArrangementBuilder
{
    public static CachedArrangementPart BuildPart(ArrangementVariantContext context)
    {
        CachedArrangementPart part = new()
        {
            schemaVersion = CachedSongFormat.SchemaVersion,
            partId = context.PartId,
            displayName = context.DisplayName,
            route = context.Arrangement.Route,
            arrangementGroupId = context.Arrangement.PartId,
            arrangementDisplayName = context.Arrangement.DisplayName,
            difficultyLabel = context.DifficultyLabel,
            difficultyUiIndex = context.DifficultyUiIndex,
            hasDifficultyVariants = context.HasDifficultyVariants,
            durationSeconds = Math.Max(context.Arrangement.Arrangement.MetaData.SongLength / 1000f, GetLevelDurationSeconds(context.SourceLevel)),
            difficultyRating = CalculateDifficultyRating(context.SourceLevel, context.Arrangement.Arrangement.MetaData.SongLength / 1000f),
            tuningPitches = context.Arrangement.TuningPitches,
            tuningDisplayName = context.Arrangement.TuningDisplayName,
            timing = TimingExporter.Build(context.Arrangement.Arrangement),
            tones = CloneToneData(context.Arrangement.Tones),
            generatedPart = new CachedGeneratedPartInfo
            {
                partId = context.PartId,
                displayName = context.DisplayName,
                instrumentName = context.Arrangement.Route,
                sourceMidiChannel = -1,
                sourceMidiProgram = string.Equals(context.Arrangement.Route, "Bass", StringComparison.OrdinalIgnoreCase) ? 33 : 29,
                preferredBank = -1,
                isDrum = false,
                isGuitarFamily = true,
                isExplicitHarmonicPart = false
            }
        };

        List<SourceEvent> events = BuildSourceEvents(context.SourceLevel, context.Arrangement.Arrangement.ChordTemplates, context.Arrangement.Arrangement.MetaData.Tuning);
        part.arpeggioGuides = BuildArpeggioGuides(context.SourceLevel, context.Arrangement.Arrangement.ChordTemplates);
        int noteId = 0;
        int chordId = 0;
        Dictionary<string, CachedNoteData> previousByStringRoute = new(StringComparer.OrdinalIgnoreCase);

        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            SourceEvent sourceEvent = events[eventIndex];
            int assignedChordId = chordId++;

            foreach (SourceNote source in sourceEvent.Notes.OrderBy(note => note.String))
            {
                CachedNoteData note = NoteBuilder.BuildGameplayNote(source, context, noteId++, assignedChordId);
                NoteBuilder.ApplyLegatoLink(note, source, previousByStringRoute);
                part.notes.Add(note);
                part.generatedNotes.Add(NoteBuilder.BuildGeneratedNote(source, context, note));
                previousByStringRoute[note.stringIdx.ToString(CultureInfo.InvariantCulture)] = note;
            }
        }

        part.durationSeconds = Math.Max(part.durationSeconds, part.notes.Count > 0
            ? part.notes.Max(note => note.time + Math.Max(0.05f, note.duration))
            : 0f);

        return part;
    }

    public static List<ArrangementVariantBuildResult> BuildVariants(ArrangementContext context)
    {
        List<ArrangementVariantSource> sources = BuildVariantSources(context.Arrangement);
        if (sources.Count == 0)
        {
            ArrangementVariantContext fallbackContext = new()
            {
                Arrangement = context,
                SourceLevel = ChooseExpertLevel(context.Arrangement),
                PartId = context.PartId,
                DisplayName = context.DisplayName,
                DifficultyLabel = "Full",
                DifficultyUiIndex = 0,
                HasDifficultyVariants = false
            };

            return new List<ArrangementVariantBuildResult>
            {
                new()
                {
                    Context = fallbackContext,
                    Part = BuildPart(fallbackContext)
                }
            };
        }

        bool hasDifficultyVariants = sources.Count > 1;
        ArrangementVariantBuildResult? expertResult = null;
        List<ArrangementVariantBuildResult> nonExpertResults = new(Math.Max(0, sources.Count - 1));
        for (int i = 0; i < sources.Count; i++)
        {
            ArrangementVariantSource source = sources[i];
            ArrangementVariantContext variantContext = new()
            {
                Arrangement = context,
                SourceLevel = source.Level,
                PartId = context.PartId,
                DisplayName = context.DisplayName,
                DifficultyLabel = string.Empty,
                DifficultyUiIndex = -1,
                HasDifficultyVariants = hasDifficultyVariants
            };

            CachedArrangementPart part = BuildPart(variantContext);
            ArrangementVariantBuildResult result = new()
            {
                Context = variantContext,
                Part = part
            };

            if (source.IsExpertSource)
                expertResult = result;
            else
                nonExpertResults.Add(result);
        }

        nonExpertResults = nonExpertResults
            .OrderByDescending(result => result?.Part?.notes?.Count ?? 0)
            .ThenByDescending(result => result?.Part?.generatedNotes?.Count ?? 0)
            .ThenByDescending(result => result?.Context?.SourceLevel?.Difficulty ?? int.MinValue)
            .ToList();

        List<ArrangementVariantBuildResult> results = new(sources.Count);
        if (expertResult != null)
            results.Add(expertResult);
        results.AddRange(nonExpertResults);

        for (int i = 0; i < results.Count; i++)
        {
            ArrangementVariantBuildResult result = results[i];
            ArrangementVariantContext variantContext = result.Context;
            int uiIndex = i;
            string difficultyLabel = GetDifficultyLabelForOrderedIndex(uiIndex, results.Count);
            string partId = uiIndex == 0
                ? context.PartId
                : $"{context.PartId}::level-{variantContext.SourceLevel.Difficulty:D3}-{uiIndex:D3}";
            string displayName = hasDifficultyVariants ? $"{context.DisplayName} - {difficultyLabel}" : context.DisplayName;

            variantContext.PartId = partId;
            variantContext.DisplayName = displayName;
            variantContext.DifficultyLabel = difficultyLabel;
            variantContext.DifficultyUiIndex = uiIndex;
            ApplyVariantMetadata(result.Part, variantContext);
        }

        return results;
    }

    private static void ApplyVariantMetadata(CachedArrangementPart part, ArrangementVariantContext context)
    {
        if (part == null || context == null)
            return;

        part.partId = context.PartId;
        part.displayName = context.DisplayName;
        part.route = context.Arrangement.Route;
        part.arrangementGroupId = context.Arrangement.PartId;
        part.arrangementDisplayName = context.Arrangement.DisplayName;
        part.difficultyLabel = context.DifficultyLabel;
        part.difficultyUiIndex = context.DifficultyUiIndex;
        part.hasDifficultyVariants = context.HasDifficultyVariants;

        if (part.generatedPart != null)
        {
            part.generatedPart.partId = context.PartId;
            part.generatedPart.displayName = context.DisplayName;
            part.generatedPart.instrumentName = context.Arrangement.Route;
        }

        if (part.generatedNotes == null)
            return;

        for (int i = 0; i < part.generatedNotes.Count; i++)
        {
            part.generatedNotes[i].partId = context.PartId;
            part.generatedNotes[i].partName = context.DisplayName;
        }
    }

    private static List<ArrangementVariantSource> BuildVariantSources(InstrumentalArrangement arrangement)
    {
        List<ArrangementVariantSource> results = new();
        Level expertLevel = ChooseExpertLevel(arrangement);
        if (HasPlayableContent(expertLevel))
            results.Add(new ArrangementVariantSource { Level = expertLevel, IsExpertSource = true });

        List<Level> playableLevels = (arrangement.Levels ?? new List<Level>())
            .Where(HasPlayableContent)
            .OrderByDescending(level => level.Difficulty)
            .ToList();

        for (int i = 0; i < playableLevels.Count; i++)
            results.Add(new ArrangementVariantSource { Level = playableLevels[i] });

        return results;
    }

    private static Level ChooseExpertLevel(InstrumentalArrangement arrangement)
    {
        if (HasPlayableContent(arrangement.TranscriptionTrack))
            return arrangement.TranscriptionTrack!;

        if ((arrangement.Levels?.Count ?? 0) > 1)
        {
            try
            {
                Level generated = arrangement.GenerateTranscriptionTrack().GetAwaiter().GetResult();
                if (HasPlayableContent(generated))
                    return generated;
            }
            catch
            {
            }
        }

        Level? bestLevel = arrangement.Levels?
            .Where(HasPlayableContent)
            .OrderByDescending(level => (level.Notes?.Count ?? 0) + (level.Chords?.Count ?? 0))
            .ThenByDescending(level => level.Difficulty)
            .FirstOrDefault();
        if (bestLevel != null)
            return bestLevel;

        return arrangement.TranscriptionTrack
               ?? arrangement.Levels?.OrderByDescending(level => level.Difficulty).FirstOrDefault()
               ?? new Level(0);
    }

    private static bool HasPlayableContent(Level? level)
    {
        return level != null &&
               ((level.Notes?.Count ?? 0) > 0 || (level.Chords?.Count ?? 0) > 0);
    }

    private static List<SourceEvent> BuildSourceEvents(Level level, List<ChordTemplate> chordTemplates, Tuning tuning)
    {
        List<SourceEvent> events = new();

        if (level?.Notes != null)
        {
            for (int i = 0; i < level.Notes.Count; i++)
            {
                Note note = level.Notes[i];
                if (note == null || note.IsIgnore || !IsValidStringIndex(note.String))
                    continue;

                events.Add(new SourceEvent
                {
                    TimeMs = note.Time,
                    Notes = new List<SourceNote>
                    {
                        SourceNote.FromNote(note, false, false, false, string.Empty)
                    }
                });
            }
        }

        if (level?.Chords != null)
        {
            for (int i = 0; i < level.Chords.Count; i++)
            {
                Chord chord = level.Chords[i];
                if (chord == null || chord.IsIgnore)
                    continue;

                List<SourceNote> sourceNotes = new();
                if (chord.HasChordNotes)
                {
                    string chordDisplayName = ResolveChordDisplayNameFromChord(chord, chordTemplates);
                    foreach (Note chordNote in chord.ChordNotes!)
                    {
                        if (chordNote == null || chordNote.IsIgnore || !IsValidStringIndex(chordNote.String))
                            continue;

                        sourceNotes.Add(SourceNote.FromNote(chordNote, chord.IsPalmMute, chord.IsFretHandMute, chord.IsHopo, chordDisplayName));
                    }
                }
                else if (chord.ChordId >= 0 && chord.ChordId < chordTemplates.Count)
                {
                    ChordTemplate template = chordTemplates[chord.ChordId];
                    string chordDisplayName = NoteBuilder.ResolveChordDisplayName(template);
                    for (int stringIndex = 0; stringIndex < template.Frets.Length; stringIndex++)
                    {
                        sbyte fret = template.Frets[stringIndex];
                        if (fret < 0)
                            continue;

                        sourceNotes.Add(SourceNote.FromTemplate(chord.Time, stringIndex, fret, chord.IsPalmMute, chord.IsFretHandMute, chord.IsHopo, chord.IsLinkNext, chordDisplayName));
                    }
                }

                if (sourceNotes.Count == 0)
                    continue;

                events.Add(new SourceEvent
                {
                    TimeMs = chord.Time,
                    Notes = sourceNotes
                });
            }
        }

        events.Sort((left, right) =>
        {
            int cmp = left.TimeMs.CompareTo(right.TimeMs);
            if (cmp != 0)
                return cmp;
            return right.Notes.Count.CompareTo(left.Notes.Count);
        });

        return events;
    }

    private static List<CachedArpeggioGuideData> BuildArpeggioGuides(Level level, List<ChordTemplate> chordTemplates)
    {
        List<CachedArpeggioGuideData> guides = new();
        if (level?.HandShapes == null || chordTemplates == null)
            return guides;

        for (int i = 0; i < level.HandShapes.Count; i++)
        {
            HandShape handShape = level.HandShapes[i];
            if (handShape == null)
                continue;

            if (handShape.ChordId < 0 || handShape.ChordId >= chordTemplates.Count)
                continue;

            ChordTemplate template = chordTemplates[handShape.ChordId];
            if (template == null || !template.IsArpeggio)
                continue;

            int[] stringFrets = new int[template.Frets.Length];
            int activeStrings = 0;
            for (int stringIndex = 0; stringIndex < template.Frets.Length; stringIndex++)
            {
                int fret = template.Frets[stringIndex];
                stringFrets[stringIndex] = fret;
                if (fret >= 0)
                    activeStrings++;
            }

            if (activeStrings < 2)
                continue;

            float startTime = handShape.StartTime / 1000f;
            float endTime = Math.Max(handShape.EndTime, handShape.StartTime) / 1000f;
            if (endTime <= startTime + 0.01f)
                endTime = startTime + 0.01f;

            guides.Add(new CachedArpeggioGuideData
            {
                id = guides.Count,
                startTime = startTime,
                endTime = endTime,
                chordName = NoteBuilder.ResolveChordDisplayName(template),
                stringFrets = stringFrets
            });
        }

        return guides;
    }

    internal static void ImproveArrangementForImport(InstrumentalArrangement arrangement)
    {
        if (arrangement == null)
            return;

        BasicFixes.fixLinkNexts(arrangement);
        BasicFixes.removeOverlappingBendValues(arrangement);
    }

    private static int CalculateDifficultyRating(Level level, float durationSeconds)
    {
        int noteCount = (level?.Notes?.Count ?? 0) + (level?.Chords?.Count ?? 0);
        float safeDuration = Math.Max(1f, durationSeconds);
        float density = noteCount / safeDuration;
        if (density < 0.5f) return 1;
        if (density < 1.25f) return 2;
        if (density < 2.25f) return 3;
        if (density < 3.5f) return 4;
        return 5;
    }

    internal static int ScoreArrangement(string route, int noteCount)
    {
        int score = noteCount * 2;
        if (route.Contains("Lead", StringComparison.OrdinalIgnoreCase))
            score += 160;
        else if (route.Contains("Rhythm", StringComparison.OrdinalIgnoreCase))
            score += 120;
        else if (route.Contains("Bass", StringComparison.OrdinalIgnoreCase))
            score += 60;
        else if (route.Contains("Combo", StringComparison.OrdinalIgnoreCase))
            score += 90;
        return score;
    }

    private static float GetLevelDurationSeconds(Level level)
    {
        float maxSeconds = 0f;
        if (level?.Notes != null)
        {
            for (int i = 0; i < level.Notes.Count; i++)
            {
                Note note = level.Notes[i];
                maxSeconds = Math.Max(maxSeconds, (note.Time + Math.Max(note.Sustain, 50)) / 1000f);
            }
        }

        if (level?.Chords != null)
        {
            for (int i = 0; i < level.Chords.Count; i++)
            {
                Chord chord = level.Chords[i];
                maxSeconds = Math.Max(maxSeconds, chord.Time / 1000f);
                if (chord.ChordNotes != null)
                {
                    for (int j = 0; j < chord.ChordNotes.Count; j++)
                    {
                        Note note = chord.ChordNotes[j];
                        maxSeconds = Math.Max(maxSeconds, (chord.Time + Math.Max(note.Sustain, 50)) / 1000f);
                    }
                }
            }
        }

        return maxSeconds;
    }

    private static string GetDifficultyLabelForOrderedIndex(int orderedIndex, int totalVariantCount)
    {
        if (orderedIndex <= 0 || totalVariantCount <= 1)
            return "Full";

        int numericLevel = Math.Max(1, totalVariantCount - orderedIndex);
        return numericLevel.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsValidStringIndex(int stringIndex)
    {
        return stringIndex >= 0 && stringIndex < 6;
    }

    private static string ResolveChordDisplayNameFromChord(Chord chord, List<ChordTemplate> chordTemplates)
    {
        if (chord == null || chordTemplates == null)
            return string.Empty;

        if (chord.ChordId < 0 || chord.ChordId >= chordTemplates.Count)
            return string.Empty;

        return NoteBuilder.ResolveChordDisplayName(chordTemplates[chord.ChordId]);
    }

    private static CachedArrangementToneData CloneToneData(CachedArrangementToneData source)
    {
        return NormalizeToneData(source ?? new CachedArrangementToneData());
    }

    internal static CachedArrangementToneData NormalizeToneData(CachedArrangementToneData? source)
    {
        return new CachedArrangementToneData
        {
            baseToneName = source?.baseToneName?.Trim() ?? string.Empty,
            changes = source?.changes != null
                ? source.changes
                    .Where(change => change != null && !string.IsNullOrWhiteSpace(change.toneName))
                    .OrderBy(change => change.timeSeconds)
                    .Select(change => new CachedToneChangeData
                    {
                        timeSeconds = MathF.Round(Math.Max(0f, change.timeSeconds), 3),
                        toneName = change.toneName.Trim(),
                        toneId = change.toneId
                    })
                    .ToList()
                : new List<CachedToneChangeData>(),
            definitions = source?.definitions != null
                ? source.definitions
                    .Where(d => d != null && (!string.IsNullOrWhiteSpace(d.name) || !string.IsNullOrWhiteSpace(d.key) || !string.IsNullOrWhiteSpace(d.rawJson)))
                    .Select(d => new CachedToneDefinitionData
                    {
                        name = d.name?.Trim() ?? string.Empty,
                        key = d.key?.Trim() ?? string.Empty,
                        rawJson = d.rawJson ?? string.Empty
                    })
                    .ToList()
                : new List<CachedToneDefinitionData>()
        };
    }
}
