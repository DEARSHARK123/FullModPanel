using FullModPanel.Core;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace FullModPanel;

internal static class Sts2Origins
{
    public static ModOrigin From(ModSource source) => source switch
    {
        ModSource.None => ModOrigin.Unknown,
        ModSource.ModsDirectory => ModOrigin.ModsDirectory,
        ModSource.SteamWorkshop => ModOrigin.SteamWorkshop,
    };

    public static ModSource? ToSource(ModOrigin origin) => origin switch
    {
        ModOrigin.ModsDirectory => ModSource.ModsDirectory,
        ModOrigin.SteamWorkshop => ModSource.SteamWorkshop,
        ModOrigin.Unknown => null,
    };
}

internal readonly record struct ModIdentity(string Key, string StableId, ModOrigin Origin, bool CanPersist);

/// <summary>
/// Reads <see cref="ModManager.Mods"/>. Enablement comes from
/// <see cref="ModSettings.IsModDisabled"/>, the same check the vanilla row uses.
/// </summary>
internal static class Sts2ModCatalog
{
    internal static ModIdentity Identify(Mod mod)
    {
        var manifestId = mod.manifest?.id;
        var canPersist = !string.IsNullOrWhiteSpace(manifestId);
        var origin = Sts2Origins.From(mod.modSource);
        var stableId = canPersist ? manifestId! : string.IsNullOrWhiteSpace(mod.path) ? "missing" : mod.path;
        return new ModIdentity(
            ModKeys.Create(canPersist ? origin : ModOrigin.Unknown, stableId),
            stableId,
            origin,
            canPersist);
    }

    public static IReadOnlyList<ModFacts> Read(IReadOnlyDictionary<ulong, WorkshopText> workshop)
    {
        var facts = new List<ModFacts>();
        foreach (var mod in ModManager.Mods)
        {
            var manifest = mod.manifest;
            var identity = Identify(mod);
            WorkshopText? text = null;
            ulong? workshopId = TryGetWorkshopId(mod.path, out var parsedId) ? parsedId : null;
            if (workshopId is ulong publishedId && workshop.TryGetValue(publishedId, out var found))
            {
                text = found;
            }

            facts.Add(new ModFacts
            {
                Key = identity.Key,
                Id = identity.StableId,
                Origin = identity.Origin,
                Name = manifest?.name,
                Description = manifest?.description,
                AffectsGameplay = manifest?.affectsGameplay ?? true,
                DependencyIds = manifest?.dependencies?
                    .Select(dependency => dependency.id)
                    .Where(dependencyId => !string.IsNullOrWhiteSpace(dependencyId))
                    .Select(dependencyId => dependencyId!)
                    .ToArray() ?? [],
                WorkshopTitle = text?.Title,
                WorkshopTags = text?.Tags ?? [],
                WorkshopId = workshopId,
                Enabled = ReadEnabled(mod),
                CanPersist = identity.CanPersist,
            });
        }

        return facts;
    }

    /// <summary>
    /// This STS2 <see cref="Mod"/> build has no workshop id property. Steam
    /// installs the item at <c>…/workshop/content/&lt;appId&gt;/&lt;id&gt;/</c>.
    /// </summary>
    internal static bool TryGetWorkshopId(string? path, out ulong workshopId)
    {
        workshopId = 0;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 3 < parts.Length; i++)
        {
            if (!parts[i].Equals("workshop", StringComparison.OrdinalIgnoreCase)
                || !parts[i + 1].Equals("content", StringComparison.OrdinalIgnoreCase)
                || !ulong.TryParse(parts[i + 2], out _)
                || !ulong.TryParse(parts[i + 3], out var id)
                || id == 0)
            {
                continue;
            }

            workshopId = id;
            return true;
        }

        return false;
    }

    private static bool ReadEnabled(Mod mod)
    {
        try
        {
            return !(SaveManager.Instance.SettingsSave?.ModSettings?.IsModDisabled(mod) ?? false);
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel could not read enable state for '{mod.manifest?.id}': {ex.Message}");
            return true;
        }
    }
}

/// <summary>
/// Writes <see cref="SettingsSaveMod.IsEnabled"/> on
/// <c>SaveManager.Instance.SettingsSave.ModSettings.ModList</c> and flushes with
/// <see cref="SaveManager.SaveSettings"/>. List order is left alone. The game still
/// loads mods only at process start, so a full restart is required.
/// </summary>
internal sealed class Sts2EnableStore : IModEnableStore
{
    public bool TryApply(IReadOnlyList<ModEnableChange> changes, out string? error)
    {
        try
        {
            var save = SaveManager.Instance.SettingsSave;
            if (save is null)
            {
                error = "游戏设置尚未就绪，请稍后重试。";
                return false;
            }

            save.ModSettings ??= new ModSettings();
            var list = save.ModSettings.ModList;
            if (list is null)
            {
                list = new List<SettingsSaveMod>();
                save.ModSettings.ModList = list;
            }

            var planned = new List<PlannedChange>(changes.Count);
            foreach (var change in changes)
            {
                if (!ModKeys.TryParse(change.Key, out var origin, out var id))
                {
                    error = "模组标识符无效。";
                    return false;
                }

                var source = Sts2Origins.ToSource(origin);
                if (source is null)
                {
                    error = "该模组缺少 Manifest ID，无法保存设置。";
                    return false;
                }

                var matches = list.Where(entry => entry.Id == id && entry.Source == source.Value).ToList();
                Mod? created = null;
                if (matches.Count == 0)
                {
                    created = ModManager.Mods.FirstOrDefault(mod => mod.manifest?.id == id && mod.modSource == source.Value);
                    if (created?.manifest?.id is null)
                    {
                        error = $"未找到模组 {id}。";
                        return false;
                    }
                }

                planned.Add(new PlannedChange(matches, created, change.Enabled));
            }

            foreach (var change in planned)
            {
                if (change.Matches.Count == 0)
                {
                    list.Add(new SettingsSaveMod(change.Created!) { IsEnabled = change.Enabled });
                    continue;
                }

                foreach (var entry in change.Matches)
                {
                    entry.IsEnabled = change.Enabled;
                }
            }

            SaveManager.Instance.SaveSettings();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"FullModPanel failed to save mod settings: {ex}");
            error = "保存设置失败，请重试。";
            return false;
        }
    }

    private readonly record struct PlannedChange(List<SettingsSaveMod> Matches, Mod? Created, bool Enabled);
}

internal static class Sts2ModOverrides
{
    public static IReadOnlyList<ModOverride> Read()
    {
        try
        {
            var path = ProjectSettings.GlobalizePath("user://FullModPanel/overrides.json");
            if (!File.Exists(path))
            {
                return [];
            }

            return ModOverrideParser.Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel could not read overrides.json: {ex.Message}");
            return [];
        }
    }
}
