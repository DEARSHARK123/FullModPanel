using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Transport.Steam;
using MegaCrit.Sts2.Core.Platform.Steam;
using Steamworks;

namespace FullModPanel;

internal readonly record struct WorkshopText(string? Title, IReadOnlyList<string> Tags);

/// <summary>
/// Asks SteamUGC for the subscribed item title and tags. Skipped when
/// <see cref="SteamInitializer.Initialized"/> is false (offline or Steam not up).
/// Results are published for the panel to read on the main thread.
/// </summary>
internal static class SteamWorkshopTitles
{
    private const int PageSize = 50;
    private static readonly object Gate = new();
    private static IReadOnlyDictionary<ulong, WorkshopText>? _pending;
    private static int _generation;

    public static bool TryConsume(out IReadOnlyDictionary<ulong, WorkshopText>? titles)
    {
        lock (Gate)
        {
            titles = _pending;
            _pending = null;
            return titles is not null;
        }
    }

    public static void Begin(IReadOnlyCollection<ulong> workshopIds)
    {
        if (workshopIds.Count == 0 || !SteamInitializer.Initialized)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _generation);
        var ids = workshopIds.Where(id => id != 0).Distinct().ToArray();
        _ = QueryAsync(ids, generation);
    }

    private static async Task QueryAsync(ulong[] ids, int generation)
    {
        var map = new Dictionary<ulong, WorkshopText>();
        try
        {
            for (var offset = 0; offset < ids.Length; offset += PageSize)
            {
                var slice = ids.Skip(offset).Take(PageSize).Select(id => new PublishedFileId_t(id)).ToArray();
                if (slice.Length == 0)
                {
                    continue;
                }

                var handle = SteamUGC.CreateQueryUGCDetailsRequest(slice, (uint)slice.Length);
                try
                {
                    using var call = new SteamCallResult<SteamUGCQueryCompleted_t>(
                        SteamUGC.SendQueryUGCRequest(handle),
                        SteamInitializer.DisconnectToken);
                    var completed = await call.Task.ConfigureAwait(false);
                    if (completed.m_eResult != EResult.k_EResultOK)
                    {
                        Log.Warn($"FullModPanel workshop query failed: {completed.m_eResult}");
                        continue;
                    }

                    for (uint index = 0; index < completed.m_unNumResultsReturned; index++)
                    {
                        if (!SteamUGC.GetQueryUGCResult(completed.m_handle, index, out var details))
                        {
                            continue;
                        }

                        var tags = (details.m_rgchTags ?? "")
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        map[details.m_nPublishedFileId.m_PublishedFileId] = new WorkshopText(details.m_rgchTitle, tags);
                    }
                }
                finally
                {
                    SteamUGC.ReleaseQueryUGCRequest(handle);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel workshop query failed: {ex.Message}");
        }

        lock (Gate)
        {
            if (generation == _generation)
            {
                _pending = map;
            }
        }
    }
}
