using System.Text.Json;
using Microsoft.FSharp.Core;
using PsarcImporter.Conversion;
using PsarcImporter.Models;
using Rocksmith2014.DLCProject;
using Rocksmith2014.XML;

namespace PsarcImporter;

internal static class PsarcConverter
{
    public static async Task ConvertAsync(string psarcPath, string outputPath, string workDirectory)
    {
        if (!File.Exists(psarcPath))
            throw new FileNotFoundException("PSARC file not found.", psarcPath);

        string contentDirectory = Path.Combine(workDirectory, "psarc_content");
        ResetDirectory(contentDirectory);

        Console.WriteLine($"[PsarcImporter] Importing {psarcPath}");

        // Extract PSARC archive via Rocksmith2014 library
        FSharpFunc<Unit, Unit> progress = new ConsoleProgressFunc();
        var importResult = await Rocksmith2014.DLCProject.PsarcImporter.import(
            progress, psarcPath, contentDirectory);

        // Convert WEM audio to OGG
        AudioConverter.ConvertAll(contentDirectory);

        // Find instrumental arrangement XMLs (exclude showlights, vocals)
        List<string> arrangementXmlPaths = Directory.GetFiles(contentDirectory, "arr_*_RS2.xml", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string fileName = Path.GetFileName(path);
                return !fileName.Contains("showlights", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Contains("vocals", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (arrangementXmlPaths.Count == 0)
            throw new InvalidOperationException("No instrumental Rocksmith arrangements were extracted.");

        // Locate manifest JSONs for tone definitions
        List<string> manifestJsonPaths = Directory.GetFiles(contentDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<CachedToneDefinitionData> projectToneDefinitions =
            ToneExtractor.ExtractProjectToneDefinitions(importResult.ProjectPath);

        // Process each arrangement: load, fix, build variants, collect parts
        var allVariantParts = new List<(ArrangementVariantBuildResult Variant, ArrangementContext Context)>();
        HashSet<string> toneDefinitionKeys = new(StringComparer.OrdinalIgnoreCase);
        InstrumentalArrangement? firstArrangement = null;

        for (int i = 0; i < arrangementXmlPaths.Count; i++)
        {
            string xmlPath = arrangementXmlPaths[i];
            InstrumentalArrangement arrangement = InstrumentalArrangement.Load(xmlPath);
            firstArrangement ??= arrangement;
            ArrangementBuilder.ImproveArrangementForImport(arrangement);

            ArrangementContext context = ArrangementContext.From(arrangement, xmlPath, i);
            context.Tones = ToneExtractor.ExtractArrangementTones(
                manifestJsonPaths, xmlPath, context.Route, projectToneDefinitions);
            ToneExtractor.AddToneDefinitionKeys(context.Tones.definitions, toneDefinitionKeys);

            List<ArrangementVariantBuildResult> variants = ArrangementBuilder.BuildVariants(context);
            foreach (var variant in variants)
                allVariantParts.Add((variant, context));
        }

        // Build the theory output
        string title = firstArrangement?.MetaData?.Title ?? Path.GetFileNameWithoutExtension(psarcPath);
        string artist = firstArrangement?.MetaData?.ArtistName ?? string.Empty;
        string albumName = firstArrangement?.MetaData?.AlbumName ?? string.Empty;

        // Find audio and cover art
        string? mainAudioPath = AudioConverter.SelectPrimaryAudioPath(contentDirectory);
        string? previewAudioPath = AudioConverter.SelectPreviewAudioPath(contentDirectory);
        string? coverArtPath = ArtworkConverter.ExtractArtworkPath(contentDirectory);

        BuildTheoryPackage(
            outputPath,
            title,
            artist,
            albumName,
            mainAudioPath,
            previewAudioPath,
            coverArtPath,
            psarcPath,
            allVariantParts);

        Console.WriteLine($"[PsarcImporter] Successfully imported '{title}' -> {outputPath}");
    }

    private static void BuildTheoryPackage(
        string outputPath,
        string title,
        string artist,
        string album,
        string? mainAudioPath,
        string? previewAudioPath,
        string? coverArtPath,
        string psarcPath,
        List<(ArrangementVariantBuildResult Variant, ArrangementContext Context)> variantParts)
    {
        long now = DateTime.UtcNow.Ticks;
        float maxDuration = variantParts.Count > 0
            ? variantParts.Max(v => v.Variant.Part.durationSeconds)
            : 0f;
        int maxDifficulty = variantParts.Count > 0
            ? variantParts.Max(v => v.Variant.Part.difficultyRating)
            : 0;

        TheorySongManifest manifest = new()
        {
            title = title,
            artist = artist,
            album = album,
            durationSeconds = maxDuration,
            difficultyRating = maxDifficulty,
            createdAtUtcTicks = now,
            modifiedAtUtcTicks = now,
            provenance = new TheoryImportProvenance
            {
                sourceType = "psarc-importer",
                sourceDisplayName = Path.GetFileNameWithoutExtension(psarcPath),
                sourcePath = Path.GetFullPath(psarcPath),
                sourceLastWriteUtcTicks = File.GetLastWriteTimeUtc(psarcPath).Ticks,
                sourceSizeBytes = new FileInfo(psarcPath).Length,
                importedAtUtcTicks = now,
                converterName = "PSARC Importer",
                converterVersion = "1.0.0"
            }
        };

        // Convert variant parts to theory arrangement data
        var theoryArrangements = new List<TheoryArrangementData>();
        var arrangementSummaries = new List<TheoryArrangementSummary>();

        foreach (var (variant, context) in variantParts)
        {
            CachedArrangementPart part = variant.Part;
            ArrangementVariantContext vctx = variant.Context;

            string arrangementEntry = $"arrangements/{SanitizeFileName(part.partId)}.json";

            // Convert cached note data to theory note data
            var theoryNotes = part.notes.Select(n => new TheoryNoteData
            {
                id = n.id,
                time = n.time,
                duration = n.duration,
                stringIndex = n.stringIdx,
                fret = n.fret,
                noteName = n.note,
                chordId = n.chordId,
                chordName = n.chordName,
                primaryTechnique = n.technique,
                slideTargetFret = n.slideTargetFret,
                bendStep = n.bendStep,
                bendVisualStartTime = n.bendVisualStartTime,
                bendVisualDuration = n.bendVisualDuration,
                bendPreBend = n.bendPreBend,
                bendRelease = n.bendRelease,
                muted = n.isMuted,
                palmMute = n.isPalmMute,
                fretHandMute = n.isFretHandMute,
                harmonic = n.isHarmonic,
                accent = n.isAccent,
                tap = n.isTap,
                tremolo = n.isTremolo,
                pinchHarmonic = n.isPinchHarmonic,
                hammerOn = n.isHammerOn,
                pullOff = n.isPullOff,
                hopo = n.isHopo,
                vibrato = n.hasVibrato,
                vibratoStrength = n.vibratoStrength,
                maxBend = n.maxBend,
                legato = n.isLegato,
                requiresPluck = n.requiresPluck,
                linkedFromNoteId = n.linkedFromNoteId,
                bendPoints = n.bendPoints.Select(bp => new TheoryBendPointData
                {
                    timeSeconds = bp.timeSeconds,
                    step = bp.step
                }).ToList(),
                techniqueSegments = n.techniqueSegments.Select(ts => new TheoryTechniqueSegmentData
                {
                    type = ts.type,
                    startOffset = ts.startOffset,
                    endOffset = ts.endOffset,
                    startFret = ts.startFret,
                    endFret = ts.endFret,
                    startBend = ts.startBend,
                    endBend = ts.endBend
                }).ToList()
            }).ToList();

            // Convert generated notes
            var theoryGeneratedNotes = part.generatedNotes.Select(gn => new TheoryGeneratedNoteEvent
            {
                startTimeSeconds = gn.startTimeSeconds,
                durationSeconds = gn.durationSeconds,
                pitchPreRollSeconds = gn.pitchPreRollSeconds,
                midiNote = gn.midiNote,
                velocity = gn.velocity,
                channel = gn.channel,
                partId = gn.partId,
                partName = gn.partName,
                techniqueVariant = gn.techniqueVariant,
                legatoTransitionKind = gn.legatoTransitionKind,
                attackVelocityScale = gn.attackVelocityScale,
                vibratoDepthSemitones = gn.vibratoDepthSemitones,
                vibratoRateHz = gn.vibratoRateHz,
                vibratoDelayNormalized = gn.vibratoDelayNormalized,
                vibratoFadeNormalized = gn.vibratoFadeNormalized,
                pitchBendRangeSemitones = gn.pitchBendRangeSemitones,
                pitchCurve = gn.pitchCurve.Select(pc => new TheoryGeneratedPitchPoint
                {
                    normalizedTime = pc.normalizedTime,
                    semitoneOffset = pc.semitoneOffset
                }).ToList()
            }).ToList();

            // Convert arpeggio guides
            var theoryArpeggios = part.arpeggioGuides.Select(ag => new TheoryArpeggioGuide
            {
                id = ag.id,
                startTime = ag.startTime,
                endTime = ag.endTime,
                chordName = ag.chordName,
                stringFrets = ag.stringFrets
            }).ToList();

            // Convert timing
            var beats = part.timing.ebeats.Select(e => new TheoryBeatData
            {
                timeSeconds = e.timeSeconds,
                measure = e.measure
            }).ToList();

            var sections = part.timing.sections.Select(s => new TheorySectionData
            {
                name = s.name,
                number = s.number,
                timeSeconds = s.timeSeconds
            }).ToList();

            // Convert tones
            var toneChanges = part.tones.changes.Select(tc => new TheoryToneChangeData
            {
                timeSeconds = tc.timeSeconds,
                toneName = tc.toneName,
                toneId = tc.toneId
            }).ToList();

            var toneDefs = part.tones.definitions.Select(td => new TheoryToneDefinitionData
            {
                name = td.name,
                key = td.key,
                rawToneEntry = td.rawJson
            }).ToList();

            // Build arrangement
            var theoryArrangement = new TheoryArrangementData
            {
                arrangementId = part.partId,
                displayName = part.displayName,
                instrumentType = "guitar",
                route = part.route,
                groupId = part.arrangementGroupId,
                groupDisplayName = part.arrangementDisplayName,
                difficultyLabel = part.difficultyLabel,
                difficultyUiIndex = part.difficultyUiIndex,
                hasDifficultyVariants = part.hasDifficultyVariants,
                durationSeconds = part.durationSeconds,
                difficultyRating = part.difficultyRating,
                tuningPitches = part.tuningPitches,
                tuningDisplayName = part.tuningDisplayName,
                timing = new TheoryTimingData
                {
                    averageTempoBpm = part.timing.averageTempoBpm,
                    capo = part.timing.capo,
                    beats = beats,
                    sections = sections
                },
                tones = new TheoryToneData
                {
                    baseToneName = part.tones.baseToneName,
                    changes = toneChanges,
                    definitions = toneDefs
                },
                generatedPart = new TheoryGeneratedPartInfo
                {
                    partId = part.generatedPart.partId,
                    displayName = part.generatedPart.displayName,
                    instrumentName = part.generatedPart.instrumentName,
                    sourceMidiChannel = part.generatedPart.sourceMidiChannel,
                    sourceMidiProgram = part.generatedPart.sourceMidiProgram,
                    preferredBank = part.generatedPart.preferredBank,
                    isDrum = part.generatedPart.isDrum,
                    isGuitarFamily = part.generatedPart.isGuitarFamily,
                    isExplicitHarmonicPart = part.generatedPart.isExplicitHarmonicPart
                },
                notes = theoryNotes,
                generatedNotes = theoryGeneratedNotes,
                arpeggioGuides = theoryArpeggios
            };

            theoryArrangements.Add(theoryArrangement);

            // Build summary
            arrangementSummaries.Add(new TheoryArrangementSummary
            {
                arrangementId = part.partId,
                displayName = part.displayName,
                instrumentType = "guitar",
                route = part.route,
                groupId = part.arrangementGroupId,
                groupDisplayName = part.arrangementDisplayName,
                difficultyLabel = part.difficultyLabel,
                difficultyUiIndex = part.difficultyUiIndex,
                hasDifficultyVariants = part.hasDifficultyVariants,
                entry = arrangementEntry,
                noteCount = part.notes.Count,
                tabCount = part.notes.Count,
                score = ArrangementBuilder.ScoreArrangement(part.route, part.notes.Count),
                difficultyRating = part.difficultyRating,
                tuningPitches = part.tuningPitches,
                tuningDisplayName = part.tuningDisplayName
            });
        }

        // Set default arrangement
        if (arrangementSummaries.Count > 0)
            manifest.defaultArrangementId = arrangementSummaries[0].arrangementId;

        manifest.arrangements = arrangementSummaries;

        // Add audio assets
        if (!string.IsNullOrWhiteSpace(mainAudioPath) && File.Exists(mainAudioPath))
        {
            string audioEntry = $"audio/{Path.GetFileName(mainAudioPath)}";
            manifest.primaryAudioEntry = audioEntry;
            manifest.audio.Add(new TheoryAudioAsset
            {
                id = "primary",
                entry = audioEntry,
                displayName = "Primary",
                role = "primary",
                contentType = "audio/ogg",
                sourceSizeBytes = new FileInfo(mainAudioPath).Length,
                defaultForPlayback = true
            });
        }

        if (!string.IsNullOrWhiteSpace(previewAudioPath) && File.Exists(previewAudioPath))
        {
            manifest.audio.Add(new TheoryAudioAsset
            {
                id = "preview",
                entry = $"audio/{Path.GetFileName(previewAudioPath)}",
                displayName = "Preview",
                role = "preview",
                contentType = "audio/ogg",
                sourceSizeBytes = new FileInfo(previewAudioPath).Length,
                defaultForPlayback = false
            });
        }

        // Add cover art
        if (!string.IsNullOrWhiteSpace(coverArtPath) && File.Exists(coverArtPath))
            manifest.coverArtEntry = "assets/cover.png";

        // Write the .theory ZIP package
        var cachedManifest = new Models.CachedSongManifest();
        TheoryPackageWriter.Write(
            outputPath,
            manifest,
            theoryArrangements,
            mainAudioPath,
            previewAudioPath,
            coverArtPath,
            cachedManifest,
            audioEntryDir: "audio");
    }

    private static string SanitizeFileName(string value)
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

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    private sealed class ConsoleProgressFunc : FSharpFunc<Unit, Unit>
    {
        public override Unit Invoke(Unit arg)
        {
            Console.WriteLine("[PsarcImporter] import stage advanced");
            return default;
        }
    }
}
