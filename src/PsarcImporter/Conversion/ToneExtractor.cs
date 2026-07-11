using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using PsarcImporter.Models;

namespace PsarcImporter.Conversion;

internal static class ToneExtractor
{
    public static CachedArrangementToneData ExtractArrangementTones(
        IReadOnlyList<string> manifestJsonPaths,
        string xmlPath,
        string arrangementName,
        IReadOnlyList<CachedToneDefinitionData> projectToneDefinitions)
    {
        ManifestToneData manifestToneData = FindManifestToneData(manifestJsonPaths, arrangementName);
        CachedArrangementToneData tones = ParseXmlToneData(xmlPath, manifestToneData.IdNameMap);
        if (string.IsNullOrWhiteSpace(tones.baseToneName) &&
            manifestToneData.IdNameMap.TryGetValue(0, out string? fallbackBaseTone) &&
            !string.IsNullOrWhiteSpace(fallbackBaseTone))
        {
            tones.baseToneName = fallbackBaseTone.Trim();
        }

        tones.definitions = manifestToneData.Definitions.Count > 0
            ? manifestToneData.Definitions
            : CloneToneDefinitions(projectToneDefinitions);
        return ArrangementBuilder.NormalizeToneData(tones);
    }

    public static List<CachedToneDefinitionData> ExtractProjectToneDefinitions(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            return new List<CachedToneDefinitionData>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(projectPath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Tones", out JsonElement tonesElement) ||
                tonesElement.ValueKind != JsonValueKind.Array)
            {
                return new List<CachedToneDefinitionData>();
            }

            return ExtractToneDefinitionsFromArray(tonesElement);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[PsarcImporter] Skipping project tone definitions from '{Path.GetFileName(projectPath)}': {ex.Message}");
            return new List<CachedToneDefinitionData>();
        }
    }

    private static ManifestToneData FindManifestToneData(IReadOnlyList<string> manifestJsonPaths, string arrangementName)
    {
        ManifestToneData result = new();
        if (manifestJsonPaths == null || string.IsNullOrWhiteSpace(arrangementName))
            return result;

        string targetName = arrangementName.Trim();
        for (int fileIndex = 0; fileIndex < manifestJsonPaths.Count; fileIndex++)
        {
            string jsonPath = manifestJsonPaths[fileIndex];
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                continue;

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath));
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("Entries", out JsonElement entries) ||
                    entries.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty entryProperty in entries.EnumerateObject())
                {
                    JsonElement entry = entryProperty.Value;
                    if (entry.ValueKind != JsonValueKind.Object ||
                        !entry.TryGetProperty("Attributes", out JsonElement attributes) ||
                        attributes.ValueKind != JsonValueKind.Object ||
                        !TryGetJsonString(attributes, "ArrangementName", out string? entryArrangementName) ||
                        !string.Equals(entryArrangementName.Trim(), targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Definitions = ExtractToneDefinitions(attributes);
                    result.IdNameMap = ExtractToneIdNameMap(attributes);
                    return result;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[PsarcImporter] Skipping unparseable manifest JSON '{Path.GetFileName(jsonPath)}': {ex.Message}");
            }
        }

        return result;
    }

    private static List<CachedToneDefinitionData> ExtractToneDefinitions(JsonElement attributes)
    {
        if (!attributes.TryGetProperty("Tones", out JsonElement tonesElement) ||
            tonesElement.ValueKind != JsonValueKind.Array)
        {
            return new List<CachedToneDefinitionData>();
        }

        return ExtractToneDefinitionsFromArray(tonesElement);
    }

    private static List<CachedToneDefinitionData> ExtractToneDefinitionsFromArray(JsonElement tonesElement)
    {
        List<CachedToneDefinitionData> definitions = new();
        if (tonesElement.ValueKind != JsonValueKind.Array)
            return definitions;

        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement toneElement in tonesElement.EnumerateArray())
        {
            if (toneElement.ValueKind != JsonValueKind.Object)
                continue;

            TryGetJsonString(toneElement, "Key", out string? key);
            if (!string.IsNullOrWhiteSpace(key) && !seenKeys.Add(key))
                continue;

            TryGetJsonString(toneElement, "Name", out string? name);
            definitions.Add(new CachedToneDefinitionData
            {
                name = name ?? string.Empty,
                key = key ?? string.Empty,
                rawJson = toneElement.GetRawText()
            });
        }

        return definitions;
    }

    private static List<CachedToneDefinitionData> CloneToneDefinitions(IReadOnlyList<CachedToneDefinitionData> definitions)
    {
        if (definitions == null || definitions.Count == 0)
            return new List<CachedToneDefinitionData>();

        List<CachedToneDefinitionData> clones = new(definitions.Count);
        for (int i = 0; i < definitions.Count; i++)
        {
            CachedToneDefinitionData? definition = definitions[i];
            if (definition == null)
                continue;

            clones.Add(new CachedToneDefinitionData
            {
                name = definition.name ?? string.Empty,
                key = definition.key ?? string.Empty,
                rawJson = definition.rawJson ?? string.Empty
            });
        }

        return clones;
    }

    public static void AddToneDefinitionKeys(IReadOnlyList<CachedToneDefinitionData>? definitions, HashSet<string> keys)
    {
        if (definitions == null || keys == null)
            return;

        for (int i = 0; i < definitions.Count; i++)
        {
            CachedToneDefinitionData? definition = definitions[i];
            if (definition == null)
                continue;

            string key = !string.IsNullOrWhiteSpace(definition.key)
                ? definition.key.Trim()
                : !string.IsNullOrWhiteSpace(definition.name)
                    ? definition.name.Trim()
                    : definition.rawJson ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
                keys.Add(key);
        }
    }

    private static Dictionary<int, string> ExtractToneIdNameMap(JsonElement attributes)
    {
        Dictionary<int, string> idNameMap = new();
        string[] keys = { "Tone_A", "Tone_B", "Tone_C", "Tone_D" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (TryGetJsonString(attributes, keys[i], out string? toneName) && !string.IsNullOrWhiteSpace(toneName))
                idNameMap[i] = toneName.Trim();
        }

        return idNameMap;
    }

    private static CachedArrangementToneData ParseXmlToneData(string xmlPath, IReadOnlyDictionary<int, string> idNameMap)
    {
        CachedArrangementToneData result = new();
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath))
            return result;

        try
        {
            XDocument document = XDocument.Load(xmlPath);
            XElement? root = document.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "song", StringComparison.OrdinalIgnoreCase))
                return result;

            XElement? toneBaseElement = root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "tonebase", StringComparison.OrdinalIgnoreCase));
            if (toneBaseElement != null && !string.IsNullOrWhiteSpace(toneBaseElement.Value))
                result.baseToneName = toneBaseElement.Value.Trim();

            XElement? tonesElement = root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "tones", StringComparison.OrdinalIgnoreCase));
            if (tonesElement == null)
                return result;

            foreach (XElement toneElement in tonesElement.Elements().Where(e => string.Equals(e.Name.LocalName, "tone", StringComparison.OrdinalIgnoreCase)))
            {
                string? timeText = toneElement.Attribute("time")?.Value;
                if (!float.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float timeSeconds) ||
                    float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds))
                {
                    continue;
                }

                string? idText = toneElement.Attribute("id")?.Value;
                int toneId = -1;
                if (!string.IsNullOrWhiteSpace(idText))
                    int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out toneId);

                string toneName = toneElement.Attribute("name")?.Value ?? string.Empty;
                if ((string.IsNullOrWhiteSpace(toneName) || string.Equals(toneName, "N/A", StringComparison.OrdinalIgnoreCase)) &&
                    toneId >= 0 && idNameMap != null && idNameMap.TryGetValue(toneId, out string? mappedName))
                {
                    toneName = mappedName;
                }

                if (string.IsNullOrWhiteSpace(toneName))
                    continue;

                result.changes.Add(new CachedToneChangeData
                {
                    timeSeconds = MathF.Round(Math.Max(0f, timeSeconds), 3),
                    toneName = toneName.Trim(),
                    toneId = toneId
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Console.WriteLine($"[PsarcImporter] Failed to parse arrangement tones from '{Path.GetFileName(xmlPath)}': {ex.Message}");
        }

        return result;
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return value != null;
    }
}
