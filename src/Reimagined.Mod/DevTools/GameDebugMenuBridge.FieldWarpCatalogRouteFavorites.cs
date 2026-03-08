#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            private const string WarpCatalogRouteFavoritesFileName = "warp_catalog_route_favorites.jsonl";
            private static string s_warpCatalogRouteFavoritesLastActionSummary = "";

            public static void HotkeySaveSelectedWarpCatalogRouteFavorite()
            {
                try
                {
                    if (!TryGetSelectedWarpCatalogRoute(out var route, out string mode, out int index, out int count) || route == null)
                    {
                        MelonLogger.Msg("[WarpCatalogRouteFavs] Save selected route: no selection for current context.");
                        return;
                    }

                    Directory.CreateDirectory(DumpsDir);
                    string path = Path.Combine(DumpsDir, WarpCatalogRouteFavoritesFileName);
                    string jsonLine = BuildWarpCatalogRouteFavoriteJsonLine(route, mode, index, count);

                    if (!TryUpsertWarpCatalogRouteFavoriteJsonLine(path, route.RouteId, jsonLine, out bool updatedExisting, out int entryCount))
                    {
                        s_warpCatalogRouteFavoritesLastActionSummary = $"save failed: {route.RouteId}";
                        MelonLogger.Warning($"[WarpCatalogRouteFavs] Save selected route failed: {route.RouteId}");
                        return;
                    }

                    string action = updatedExisting ? "updated" : "appended";
                    s_warpCatalogRouteFavoritesLastActionSummary = $"{action} {route.Kind} {route.RouteId}";
                    MelonLogger.Msg($"[WarpCatalogRouteFavs] Saved selected route: {route.RouteId} kind={San(route.Kind)} ({action}; entries={entryCount})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalogRouteFavs] Failed to save selected route: {ex}");
                }
            }

            public static bool TryGetWarpCatalogRouteFavoritesSummary(out string summary)
            {
                try
                {
                    string path = Path.Combine(DumpsDir, WarpCatalogRouteFavoritesFileName);
                    if (!File.Exists(path))
                    {
                        summary = "none (Ctrl+Alt+Shift+C saves the selected catalog route here)";
                        return true;
                    }

                    int entries = 0;
                    int badLines = 0;
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = (raw ?? string.Empty).Trim();
                        if (line.Length == 0)
                            continue;

                        if (TryGetWarpCatalogRouteFavoriteRouteId(line, out _))
                            entries++;
                        else
                            badLines++;
                    }

                    summary = $"file=\"{path}\" entries={entries} badLines={badLines}";
                    if (!string.IsNullOrEmpty(s_warpCatalogRouteFavoritesLastActionSummary))
                        summary += $" | last: {San(s_warpCatalogRouteFavoritesLastActionSummary)}";
                    return true;
                }
                catch (Exception ex)
                {
                    summary = $"error: {San(ex.GetType().Name)}";
                    return true;
                }
            }

            private static string BuildWarpCatalogRouteFavoriteJsonLine(WarpCatalogRoute route, string mode, int index, int count)
            {
                string[] sourcePoints = GetOrderedSourcePointVariants(route);
                string[] sourceCams = GetOrderedSourceCamVariants(route);
                int? doorIdx = GetDoorIdxForSerializableOutput(route);
                int? transportTerminalType = GetTransportFieldForSerializableOutput(route, route.TransportTerminalType);
                int? transportTerminalNo = GetTransportFieldForSerializableOutput(route, route.TransportTerminalNo);
                int? transportJumpNo = GetTransportFieldForSerializableOutput(route, route.TransportJumpNo);
                int? transportEventStat = GetTransportFieldForSerializableOutput(route, route.TransportEventStat);
                int? transportSeq = GetTransportFieldForSerializableOutput(route, route.TransportSeq);
                int? transportCallMode = GetTransportFieldForSerializableOutput(route, route.TransportCallMode);
                int? transportProcessStat = GetTransportFieldForSerializableOutput(route, route.TransportProcessStat);
                int? transportTerminalCnt = GetTransportFieldForSerializableOutput(route, route.TransportTerminalCnt);
                string? currentDoorFavoriteLine = BuildCurrentDoorFavoriteLineOrNull(route);

                var payload = new
                {
                    schemaVersion = 3,
                    savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    source = Path.Combine(DumpsDir, WarpCatalogFileName),
                    browseMode = mode,
                    selectedIndex1 = index + 1,
                    selectedCount = count,
                    routeId = route.RouteId,
                    kind = route.Kind,
                    src = new
                    {
                        F = route.SrcF,
                        A = route.SrcA,
                        S = route.SrcS,
                        pointRes = GetSingleObservedSourcePointOrNull(route),
                        camName = GetSingleObservedSourceCamOrNull(route),
                        sourcePoints = sourcePoints,
                        sourceCams = sourceCams,
                    },
                    dst = new
                    {
                        F = route.DstF,
                        A = route.DstA,
                        S = route.DstS,
                        pointRes = route.DstPointRes,
                        camName = route.DstCamName,
                    },
                    executor = new
                    {
                        currentDoorFavoritesCompatible = doorIdx.HasValue,
                        currentDoorFavoriteLine = currentDoorFavoriteLine,
                    },
                    door = new { idx = doorIdx },
                    terminal = new
                    {
                        transportTerminalType = transportTerminalType,
                        transportTerminalNo = transportTerminalNo,
                        transportJumpNo = transportJumpNo,
                        transportEventStat = transportEventStat,
                        transportSeq = transportSeq,
                        transportCallMode = transportCallMode,
                        transportProcessStat = transportProcessStat,
                        transportTerminalCnt = transportTerminalCnt,
                    },
                    catalogSeenCount = route.Count,
                    catalogLastTimestamp = route.LastTimestamp,
                };

                return JsonSerializer.Serialize(payload);
            }

            private static string[] GetOrderedSourcePointVariants(WarpCatalogRoute route)
            {
                return route.SourcePointVariants.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            }

            private static string[] GetOrderedSourceCamVariants(WarpCatalogRoute route)
            {
                return route.SourceCamVariants.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            }

            private static string? GetSingleObservedSourcePointOrNull(WarpCatalogRoute route)
            {
                string[] points = GetOrderedSourcePointVariants(route);
                if (points.Length == 1)
                    return points[0];

                return null;
            }

            private static string? GetSingleObservedSourceCamOrNull(WarpCatalogRoute route)
            {
                string[] cams = GetOrderedSourceCamVariants(route);
                if (cams.Length == 1)
                    return cams[0];

                return null;
            }

            private static int? GetDoorIdxForSerializableOutput(WarpCatalogRoute route)
            {
                if (string.Equals(route.Kind, "door", StringComparison.Ordinal) && route.DoorIdx >= 0)
                    return route.DoorIdx;

                return null;
            }

            private static int? GetTransportFieldForSerializableOutput(WarpCatalogRoute route, int value)
            {
                if (string.Equals(route.Kind, "terminal", StringComparison.Ordinal) && value >= 0)
                    return value;

                return null;
            }

            private static string? BuildCurrentDoorFavoriteLineOrNull(WarpCatalogRoute route)
            {
                int? doorIdx = GetDoorIdxForSerializableOutput(route);
                if (!doorIdx.HasValue)
                    return null;

                return FormatWarpFavoriteLine(route.SrcF, route.SrcA, route.SrcS, doorIdx.Value, string.Empty, BuildWarpCatalogDoorFavoriteLabel(route));
            }

            private static bool TryUpsertWarpCatalogRouteFavoriteJsonLine(string path, string routeId, string jsonLine, out bool updatedExisting, out int entryCount)
            {
                updatedExisting = false;
                entryCount = 0;

                List<string> lines = new List<string>();
                if (File.Exists(path))
                    lines.AddRange(File.ReadAllLines(path));

                int matchLine = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = (lines[i] ?? string.Empty).Trim();
                    if (t.Length == 0)
                        continue;

                    if (!TryGetWarpCatalogRouteFavoriteRouteId(t, out string existingRouteId))
                        continue;

                    if (string.Equals(existingRouteId, routeId, StringComparison.Ordinal))
                    {
                        matchLine = i;
                        break;
                    }
                }

                if (matchLine >= 0)
                {
                    lines[matchLine] = jsonLine;
                    updatedExisting = true;
                }
                else
                {
                    lines.Add(jsonLine);
                    updatedExisting = false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                entryCount = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (TryGetWarpCatalogRouteFavoriteRouteId(lines[i] ?? string.Empty, out _))
                        entryCount++;
                }

                return true;
            }

            private static bool TryGetWarpCatalogRouteFavoriteRouteId(string jsonLine, out string routeId)
            {
                routeId = string.Empty;
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonLine);
                    JsonElement root = doc.RootElement;
                    if (!root.TryGetProperty("routeId", out JsonElement routeIdElem))
                        return false;
                    routeId = routeIdElem.GetString() ?? string.Empty;
                    return !string.IsNullOrEmpty(routeId);
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
