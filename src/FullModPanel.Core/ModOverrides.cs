using System.Text.Json;

namespace FullModPanel.Core;

/// <summary>
/// Parses <c>user://FullModPanel/overrides.json</c>. The file may set
/// <c>subtype</c> and <c>parent</c> only. A <c>group</c> field is ignored.
/// Invalid JSON yields an empty list so a broken file cannot blank the panel.
/// </summary>
public static class ModOverrideParser
{
    public static IReadOnlyList<ModOverride> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            JsonElement list;
            if (root.ValueKind == JsonValueKind.Array)
            {
                list = root;
            }
            else if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("mods", out var mods)
                && mods.ValueKind == JsonValueKind.Array)
            {
                list = mods;
            }
            else
            {
                return [];
            }

            var results = new List<ModOverride>();
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("id", out var idElement)
                    || idElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var id = idElement.GetString()?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var parentSpecified = false;
                string? parentId = null;
                if (item.TryGetProperty("parent", out var parentElement))
                {
                    parentSpecified = true;
                    if (parentElement.ValueKind == JsonValueKind.String)
                    {
                        var text = parentElement.GetString()?.Trim();
                        parentId = string.IsNullOrEmpty(text) ? null : text;
                    }
                }

                ModKind? subtype = null;
                if (item.TryGetProperty("subtype", out var subtypeElement)
                    && subtypeElement.ValueKind == JsonValueKind.String
                    && Enum.TryParse<ModKind>(subtypeElement.GetString(), ignoreCase: true, out var parsed)
                    && Enum.IsDefined(parsed))
                {
                    subtype = parsed;
                }

                results.Add(new ModOverride(id, parentSpecified, parentId, subtype));
            }

            return results;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
