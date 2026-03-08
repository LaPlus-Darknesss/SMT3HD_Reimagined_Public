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
                    string semanticKey = BuildWarpCatalogRouteFavoriteSemanticKey(route);

                    if (!TryUpsertWarpCatalogRouteFavoriteJsonLine(path, route.RouteId, semanticKey, jsonLine, out bool updatedExisting, out int entryCount, out int collapsedMatches))
                    {
                        s_warpCatalogRouteFavoritesLastActionSummary = $"save failed: {route.RouteId}";
                        MelonLogger.Warning($"[WarpCatalogRouteFavs] Save selected route failed: {route.RouteId}");
                        return;
                    }

                    string action = updatedExisting ? "updated" : "appended";
                    string collapseNote = collapsedMatches > 0 ? $"; collapsed={collapsedMatches}" : string.Empty;
                    s_warpCatalogRouteFavoritesLastActionSummary = $"{action} {route.Kind} {route.RouteId}{collapseNote}";
                    MelonLogger.Msg($"[WarpCatalogRouteFavs] Saved selected route: {route.RouteId} kind={San(route.Kind)} ({action}; entries={entryCount}{collapseNote})");
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
                    int legacy = 0;
                    int semanticDupes = 0;
                    var semanticCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = (raw ?? string.Empty).Trim();
                        if (line.Length == 0)
                            continue;

                        if (!TryGetWarpCatalogRouteFavoriteIdentity(line, out _, out string semanticKey, out int schemaVersion))
                        {
                            badLines++;
                            continue;
                        }

                        entries++;
                        if (schemaVersion > 0 && schemaVersion < 4)
                            legacy++;

                        if (!string.IsNullOrEmpty(semanticKey))
                        {
                            if (semanticCounts.TryGetValue(semanticKey, out int seen))
                            {
                                semanticCounts[semanticKey] = seen + 1;
                                semanticDupes++;
                            }
                            else
                            {
                                semanticCounts[semanticKey] = 1;
                            }
                        }
                    }

                    summary = $"file=\"{path}\" entries={entries} badLines={badLines} legacy={legacy} semDup={semanticDupes}";
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
                int[] observedTransportTerminalType = GetOrderedObservedTransportTerminalTypeVariants(route);
                int[] observedTransportTerminalNo = GetOrderedObservedTransportTerminalNoVariants(route);
                int[] observedTransportJumpNo = GetOrderedObservedTransportJumpNoVariants(route);
                int[] observedTransportEventStat = GetOrderedObservedTransportEventStatVariants(route);
                int[] observedTransportSeq = GetOrderedObservedTransportSeqVariants(route);
                int[] observedTransportCallMode = GetOrderedObservedTransportCallModeVariants(route);
                int[] observedTransportProcessStat = GetOrderedObservedTransportProcessStatVariants(route);
                int[] observedTransportTerminalCnt = GetOrderedObservedTransportTerminalCntVariants(route);
                string? currentDoorFavoriteLine = BuildCurrentDoorFavoriteLineOrNull(route);

                var payload = new
                {
                    schemaVersion = 4,
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
                        observed = new
                        {
                            transportTerminalType = observedTransportTerminalType,
                            transportTerminalNo = observedTransportTerminalNo,
                            transportJumpNo = observedTransportJumpNo,
                            transportEventStat = observedTransportEventStat,
                            transportSeq = observedTransportSeq,
                            transportCallMode = observedTransportCallMode,
                            transportProcessStat = observedTransportProcessStat,
                            transportTerminalCnt = observedTransportTerminalCnt,
                        },
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

            private static bool TryUpsertWarpCatalogRouteFavoriteJsonLine(string path, string routeId, string semanticKey, string jsonLine, out bool updatedExisting, out int entryCount, out int collapsedMatches)
            {
                updatedExisting = false;
                entryCount = 0;
                collapsedMatches = 0;

                List<string> lines = new List<string>();
                if (File.Exists(path))
                    lines.AddRange(File.ReadAllLines(path));

                List<int> matchLines = new List<int>();
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = (lines[i] ?? string.Empty).Trim();
                    if (t.Length == 0)
                        continue;

                    if (!TryGetWarpCatalogRouteFavoriteIdentity(t, out string existingRouteId, out string existingSemanticKey, out _))
                        continue;

                    bool routeIdMatch = !string.IsNullOrEmpty(routeId) && string.Equals(existingRouteId, routeId, StringComparison.Ordinal);
                    bool semanticMatch = !string.IsNullOrEmpty(semanticKey) && string.Equals(existingSemanticKey, semanticKey, StringComparison.Ordinal);
                    if (routeIdMatch || semanticMatch)
                        matchLines.Add(i);
                }

                if (matchLines.Count > 0)
                {
                    int keepLine = matchLines[0];
                    lines[keepLine] = jsonLine;
                    for (int i = matchLines.Count - 1; i >= 1; i--)
                        lines.RemoveAt(matchLines[i]);
                    updatedExisting = true;
                    collapsedMatches = matchLines.Count - 1;
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
                    if (TryGetWarpCatalogRouteFavoriteIdentity(lines[i] ?? string.Empty, out _, out _, out _))
                        entryCount++;
                }

                return true;
            }

            private static bool TryGetWarpCatalogRouteFavoriteRouteId(string jsonLine, out string routeId)
            {
                return TryGetWarpCatalogRouteFavoriteIdentity(jsonLine, out routeId, out _, out _);
            }

            private static bool TryGetWarpCatalogRouteFavoriteIdentity(string jsonLine, out string routeId, out string semanticKey, out int schemaVersion)
            {
                routeId = string.Empty;
                semanticKey = string.Empty;
                schemaVersion = 0;
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonLine);
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("schemaVersion", out JsonElement schemaElem) && schemaElem.ValueKind == JsonValueKind.Number)
                        schemaVersion = schemaElem.GetInt32();

                    if (!root.TryGetProperty("routeId", out JsonElement routeIdElem))
                        return false;
                    routeId = routeIdElem.GetString() ?? string.Empty;
                    if (string.IsNullOrEmpty(routeId))
                        return false;

                    semanticKey = BuildWarpCatalogRouteFavoriteSemanticKey(root);
                    return !string.IsNullOrEmpty(semanticKey);
                }
                catch
                {
                    return false;
                }
            }

            private static string BuildWarpCatalogRouteFavoriteSemanticKey(WarpCatalogRoute route)
            {
                if (route == null)
                    return string.Empty;

                int? doorIdx = GetDoorIdxForSerializableOutput(route);
                int? transportTerminalType = GetTransportFieldForSerializableOutput(route, route.TransportTerminalType);
                int? transportTerminalNo = GetTransportFieldForSerializableOutput(route, route.TransportTerminalNo);
                int? transportJumpNo = GetTransportFieldForSerializableOutput(route, route.TransportJumpNo);
                int? transportEventStat = GetTransportFieldForSerializableOutput(route, route.TransportEventStat);

                return string.Join("|", new string[]
                {
                    route.Kind ?? string.Empty,
                    route.SrcF.ToString(CultureInfo.InvariantCulture),
                    route.SrcA.ToString(CultureInfo.InvariantCulture),
                    route.SrcS.ToString(CultureInfo.InvariantCulture),
                    route.DstF.ToString(CultureInfo.InvariantCulture),
                    route.DstA.ToString(CultureInfo.InvariantCulture),
                    route.DstS.ToString(CultureInfo.InvariantCulture),
                    route.DstPointRes ?? string.Empty,
                    doorIdx.HasValue ? doorIdx.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    transportTerminalType.HasValue ? transportTerminalType.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    transportTerminalNo.HasValue ? transportTerminalNo.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    transportJumpNo.HasValue ? transportJumpNo.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    transportEventStat.HasValue ? transportEventStat.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                });
            }

            private static string BuildWarpCatalogRouteFavoriteSemanticKey(JsonElement root)
            {
                string kind = GetJsonString(root, "kind");
                JsonElement src = GetJsonObject(root, "src");
                JsonElement dst = GetJsonObject(root, "dst");
                JsonElement door = GetJsonObject(root, "door");
                JsonElement terminal = GetJsonObject(root, "terminal");

                return string.Join("|", new string[]
                {
                    kind,
                    GetJsonInt(src, "F").ToString(CultureInfo.InvariantCulture),
                    GetJsonInt(src, "A").ToString(CultureInfo.InvariantCulture),
                    GetJsonInt(src, "S").ToString(CultureInfo.InvariantCulture),
                    GetJsonInt(dst, "F").ToString(CultureInfo.InvariantCulture),
                    GetJsonInt(dst, "A").ToString(CultureInfo.InvariantCulture),
                    GetJsonInt(dst, "S").ToString(CultureInfo.InvariantCulture),
                    GetJsonString(dst, "pointRes"),
                    GetNullableJsonIntString(door, "idx"),
                    GetNullableJsonIntString(terminal, "transportTerminalType"),
                    GetNullableJsonIntString(terminal, "transportTerminalNo"),
                    GetNullableJsonIntString(terminal, "transportJumpNo"),
                    GetNullableJsonIntString(terminal, "transportEventStat"),
                });
            }

            private static JsonElement GetJsonObject(JsonElement parent, string name)
            {
                if (parent.TryGetProperty(name, out JsonElement prop) && prop.ValueKind == JsonValueKind.Object)
                    return prop;

                return default;
            }

            private static string GetNullableJsonIntString(JsonElement element, string propName)
            {
                if (!element.TryGetProperty(propName, out JsonElement prop))
                    return string.Empty;

                switch (prop.ValueKind)
                {
                    case JsonValueKind.Number:
                        return prop.GetInt32().ToString(CultureInfo.InvariantCulture);
                    case JsonValueKind.String:
                        return prop.GetString() ?? string.Empty;
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                    default:
                        return string.Empty;
                }
            }
        }
    }
}
