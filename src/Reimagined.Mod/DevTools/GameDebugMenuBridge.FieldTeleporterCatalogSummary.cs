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
            private const string WarpCatalogSummaryFileName = "warp_catalog_summary.txt";
            private const string WarpCatalogSelectedRouteTextFileName = "warp_catalog_selected_route.txt";
            private const string WarpCatalogSelectedRouteJsonFileName = "warp_catalog_selected_route.json";
            private const string WarpCatalogSelectedFavoriteSnippetLatestFileName = "warp_catalog_selected_favorite_snippet.txt";

            private static bool s_warpCatalogBrowseIncoming = false;
            private static string s_warpCatalogBrowseContextKey = string.Empty;
            private static int s_warpCatalogBrowseOutgoingIndex = 0;
            private static int s_warpCatalogBrowseIncomingIndex = 0;

            private sealed class WarpCatalogRoute
            {
                public string Kind = "";
                public int SrcF = -1;
                public int SrcA = -1;
                public int SrcS = -1;
                public string SrcPointRes = "";
                public string SrcCamName = "";
                public int DstF = -1;
                public int DstA = -1;
                public int DstS = -1;
                public string DstPointRes = "";
                public string DstCamName = "";
                public int DoorIdx = -1;
                public int TransportTerminalType = -1;
                public int TransportTerminalNo = -1;
                public int TransportJumpNo = -1;
                public int TransportEventStat = -1;
                public string RouteId = "";
                public int Count = 1;
                public string LastTimestamp = "";
                public HashSet<string> SourcePointVariants = new HashSet<string>(StringComparer.Ordinal);
                public HashSet<string> SourceCamVariants = new HashSet<string>(StringComparer.Ordinal);
            }

            public static void HotkeyDumpWarpCatalogSummary()
            {
                try
                {
                    Directory.CreateDirectory(DumpsDir);
                    string path = Path.Combine(DumpsDir, WarpCatalogSummaryFileName);
                    string text = BuildWarpCatalogSummaryText();
                    File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    MelonLogger.Msg($"[WarpCatalog] Summary -> {Path.GetFileName(path)}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalog] Failed to dump summary: {ex}");
                }
            }

            public static void HotkeyWarpCatalogBrowseToggleMode()
            {
                s_warpCatalogBrowseIncoming = !s_warpCatalogBrowseIncoming;
                string mode = s_warpCatalogBrowseIncoming ? "incoming" : "outgoing";
                if (TryDescribeWarpCatalogBrowserSelection(out string desc))
                    MelonLogger.Msg($"[WarpCatalog] {desc}");
                else
                    MelonLogger.Msg($"[WarpCatalog] mode={mode} none");
            }

            public static void HotkeyDumpWarpCatalogSelectedRoute()
            {
                try
                {
                    if (!TryGetSelectedWarpCatalogRoute(out var route, out string mode, out int index, out int count) || route == null)
                    {
                        MelonLogger.Msg("[WarpCatalog] Dump selected route: no selection for current context.");
                        return;
                    }

                    Directory.CreateDirectory(DumpsDir);

                    string textPath = Path.Combine(DumpsDir, WarpCatalogSelectedRouteTextFileName);
                    string jsonPath = Path.Combine(DumpsDir, WarpCatalogSelectedRouteJsonFileName);

                    File.WriteAllText(textPath, BuildWarpCatalogSelectedRouteText(route, mode, index, count), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    var payload = new
                    {
                        generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        browseMode = mode,
                        selectedIndex1 = index + 1,
                        selectedCount = count,
                        routeId = route.RouteId,
                        kind = route.Kind,
                        src = new { F = route.SrcF, A = route.SrcA, S = route.SrcS, pointRes = route.SrcPointRes, camName = route.SrcCamName },
                        dst = new { F = route.DstF, A = route.DstA, S = route.DstS, pointRes = route.DstPointRes, camName = route.DstCamName },
                        door = new { idx = route.DoorIdx },
                        terminal = new
                        {
                            transportTerminalType = route.TransportTerminalType,
                            transportTerminalNo = route.TransportTerminalNo,
                            transportJumpNo = route.TransportJumpNo,
                            transportEventStat = route.TransportEventStat
                        },
                        sourcePointVariants = route.SourcePointVariants.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                        sourceCamVariants = route.SourceCamVariants.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                        seenCount = route.Count,
                        lastTimestamp = route.LastTimestamp,
                    };

                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    MelonLogger.Msg($"[WarpCatalog] Selected route -> {Path.GetFileName(textPath)} / {Path.GetFileName(jsonPath)} ({route.RouteId})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalog] Failed to dump selected route: {ex}");
                }
            }

            public static void HotkeyDumpWarpCatalogSelectedFavoriteSnippet()
            {
                try
                {
                    if (!TryGetSelectedWarpCatalogRoute(out var route, out string mode, out int index, out int count) || route == null)
                    {
                        MelonLogger.Msg("[WarpCatalog] Dump favorite snippet: no selection for current context.");
                        return;
                    }

                    Directory.CreateDirectory(DumpsDir);

                    string text = BuildWarpCatalogSelectedFavoriteSnippetText(route, mode, index, count);
                    string latestPath = Path.Combine(DumpsDir, WarpCatalogSelectedFavoriteSnippetLatestFileName);
                    string archivePath = BuildWarpCatalogSelectedFavoriteSnippetArchivePath(route);

                    File.WriteAllText(latestPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    File.WriteAllText(archivePath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    MelonLogger.Msg($"[WarpCatalog] Favorite snippet -> latest={Path.GetFileName(latestPath)} archive={Path.GetFileName(archivePath)} ({route.RouteId})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalog] Failed to dump favorite snippet: {ex}");
                }
            }

            public static void HotkeyAppendSelectedWarpCatalogDoorToFavorites()
            {
                try
                {
                    if (!TryGetSelectedWarpCatalogRoute(out var route, out _, out _, out _) || route == null)
                    {
                        MelonLogger.Msg("[WarpCatalog] Append selected route to favorites: no selection for current context.");
                        return;
                    }

                    if (!string.Equals(route.Kind, "door", StringComparison.Ordinal) || route.DoorIdx < 0)
                    {
                        if (string.Equals(route.Kind, "terminal", StringComparison.Ordinal))
                        {
                            MelonLogger.Msg($"[WarpCatalog] Append selected route to favorites: {route.RouteId} is terminal-only; use Ctrl+Alt+Shift+V for a template export.");
                            s_warpLastActionSummary = $"catalog append skipped: {route.RouteId} is terminal-only";
                        }
                        else
                        {
                            MelonLogger.Msg($"[WarpCatalog] Append selected route to favorites: {route.RouteId} is unsupported kind=\"{San(route.Kind)}\".");
                            s_warpLastActionSummary = $"catalog append skipped: unsupported kind {San(route.Kind)}";
                        }
                        return;
                    }

                    string path = Path.Combine(DumpsDir, WarpFavoritesFileName);
                    string label = BuildWarpCatalogDoorFavoriteLabel(route);
                    if (!TryUpsertWarpFavoriteInFile(path, route.DoorIdx, route.SrcF, route.SrcA, route.SrcS, string.Empty, label, out bool updatedExisting, out string actionSummary))
                    {
                        MelonLogger.Warning($"[WarpCatalog] Append selected route to favorites failed: {route.RouteId}");
                        s_warpLastActionSummary = $"catalog append failed: {route.RouteId}";
                        return;
                    }

                    ReloadWarpFavorites();

                    bool currentContextMatches = TryCurrentFieldContextMatches(route.SrcF, route.SrcA, route.SrcS);
                    bool selected = false;
                    if (currentContextMatches)
                        selected = TrySelectWarpFavorite(route.DoorIdx, route.SrcF, route.SrcA, route.SrcS);

                    if (selected)
                    {
                        s_warpLastActionSummary = $"catalog append OK ({(updatedExisting ? "updated" : "appended")}): {route.RouteId}";
                        MelonLogger.Msg($"[WarpCatalog] Added door route to warp favorites: {route.RouteId} -> selected current-context favorite.");
                    }
                    else if (currentContextMatches)
                    {
                        s_warpLastActionSummary = $"catalog append OK ({(updatedExisting ? "updated" : "appended")}): {route.RouteId}";
                        MelonLogger.Msg($"[WarpCatalog] Added door route to warp favorites: {route.RouteId} ({actionSummary})");
                    }
                    else
                    {
                        s_warpLastActionSummary = $"catalog append OK ({(updatedExisting ? "updated" : "appended")}): {route.RouteId} src=F{route.SrcF} A{route.SrcA} S{route.SrcS}";
                        MelonLogger.Msg($"[WarpCatalog] Added door route to warp favorites: {route.RouteId} ({actionSummary}); source context differs from current, so current selection was left unchanged.");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalog] Failed to append selected route to favorites: {ex}");
                }
            }

            public static void HotkeyWarpCatalogBrowsePrev()
            {
                if (!TryGetWarpCatalogStatsForCurrentContext(out _, out var outgoingRoutes, out var incomingRoutes))
                {
                    MelonLogger.Msg("[WarpCatalog] Browse prev: catalog unavailable or no current field context.");
                    return;
                }

                EnsureWarpCatalogBrowserState(outgoingRoutes.Count, incomingRoutes.Count);
                bool incomingMode = s_warpCatalogBrowseIncoming;
                int count = incomingMode ? incomingRoutes.Count : outgoingRoutes.Count;
                if (count <= 0)
                {
                    string mode = incomingMode ? "incoming" : "outgoing";
                    MelonLogger.Msg($"[WarpCatalog] Browse prev: no {mode} routes for current context.");
                    return;
                }

                if (incomingMode)
                    s_warpCatalogBrowseIncomingIndex = WrapIndex(s_warpCatalogBrowseIncomingIndex - 1, count);
                else
                    s_warpCatalogBrowseOutgoingIndex = WrapIndex(s_warpCatalogBrowseOutgoingIndex - 1, count);

                if (TryDescribeWarpCatalogBrowserSelection(out string desc))
                    MelonLogger.Msg($"[WarpCatalog] {desc}");
            }

            public static void HotkeyWarpCatalogBrowseNext()
            {
                if (!TryGetWarpCatalogStatsForCurrentContext(out _, out var outgoingRoutes, out var incomingRoutes))
                {
                    MelonLogger.Msg("[WarpCatalog] Browse next: catalog unavailable or no current field context.");
                    return;
                }

                EnsureWarpCatalogBrowserState(outgoingRoutes.Count, incomingRoutes.Count);
                bool incomingMode = s_warpCatalogBrowseIncoming;
                int count = incomingMode ? incomingRoutes.Count : outgoingRoutes.Count;
                if (count <= 0)
                {
                    string mode = incomingMode ? "incoming" : "outgoing";
                    MelonLogger.Msg($"[WarpCatalog] Browse next: no {mode} routes for current context.");
                    return;
                }

                if (incomingMode)
                    s_warpCatalogBrowseIncomingIndex = WrapIndex(s_warpCatalogBrowseIncomingIndex + 1, count);
                else
                    s_warpCatalogBrowseOutgoingIndex = WrapIndex(s_warpCatalogBrowseOutgoingIndex + 1, count);

                if (TryDescribeWarpCatalogBrowserSelection(out string desc))
                    MelonLogger.Msg($"[WarpCatalog] {desc}");
            }

            public static bool TryGetWarpCatalogHudLines(int loadField, int loadArea, int loadScript, out string[] lines)
            {
                var outLines = new List<string>(capacity: 9);
                lines = Array.Empty<string>();

                try
                {
                    if (!TryLoadWarpCatalogStats(out var exactRoutes, out var contextRoutes, out int rawCount, out string path))
                        return false;

                    var outgoingRoutes = new List<WarpCatalogRoute>();
                    var incomingRoutes = new List<WarpCatalogRoute>();
                    for (int i = 0; i < contextRoutes.Count; i++)
                    {
                        var r = contextRoutes[i];
                        if (r.SrcF == loadField && r.SrcA == loadArea && r.SrcS == loadScript)
                            outgoingRoutes.Add(r);
                        if (r.DstF == loadField && r.DstA == loadArea && r.DstS == loadScript)
                            incomingRoutes.Add(r);
                    }

                    EnsureWarpCatalogBrowserState(loadField, loadArea, loadScript, outgoingRoutes.Count, incomingRoutes.Count);

                    outLines.Add($"warpCatalog: file=\"{San(path)}\" rows={rawCount} exact={exactRoutes.Count} context={contextRoutes.Count} outgoingHere={outgoingRoutes.Count} incomingHere={incomingRoutes.Count}");

                    int shown = 0;
                    for (int i = 0; i < outgoingRoutes.Count && shown < 3; i++)
                    {
                        shown++;
                        outLines.Add($"warpCatalogOut[{shown}]: {FormatWarpCatalogRouteInline(outgoingRoutes[i])}");
                    }

                    shown = 0;
                    for (int i = 0; i < incomingRoutes.Count && shown < 3; i++)
                    {
                        shown++;
                        outLines.Add($"warpCatalogIn[{shown}]: {FormatWarpCatalogRouteInlineIncoming(incomingRoutes[i])}");
                    }

                    if (TryDescribeWarpCatalogBrowserSelection(out string browseLine))
                        outLines.Add($"warpCatalogBrowse: {browseLine}");
                    else
                        outLines.Add($"warpCatalogBrowse: mode={(s_warpCatalogBrowseIncoming ? "incoming" : "outgoing")} none");

                    lines = outLines.ToArray();
                    return lines.Length > 0;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[WarpCatalog] TryGetWarpCatalogHudLines failed: {ex.GetType().Name}: {ex.Message}");
                    lines = Array.Empty<string>();
                    return false;
                }
            }

            private static string BuildWarpCatalogSummaryText()
            {
                var sb = new StringBuilder(4096);
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                string path = Path.Combine(DumpsDir, WarpCatalogFileName);
                sb.AppendLine($"Source: {path}");
                sb.AppendLine();

                if (!TryLoadWarpCatalogStats(out var exactRoutes, out var contextRoutes, out int rawCount, out _))
                {
                    sb.AppendLine("warp catalog unavailable (missing file or no readable rows)");
                    return sb.ToString();
                }

                sb.AppendLine($"Rows: {rawCount}");
                sb.AppendLine($"Exact unique routes: {exactRoutes.Count}");
                sb.AppendLine($"Context routes: {contextRoutes.Count}");

                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out string curPointRes, out string curCamName);
                sb.AppendLine($"Current context: F{curF} A{curA} S{curS} point=\"{San(curPointRes)}\" cam=\"{San(curCamName)}\"");
                sb.AppendLine();

                var outgoingHere = new List<WarpCatalogRoute>();
                var incomingHere = new List<WarpCatalogRoute>();
                for (int i = 0; i < contextRoutes.Count; i++)
                {
                    var r = contextRoutes[i];
                    if (r.SrcF == curF && r.SrcA == curA && r.SrcS == curS)
                        outgoingHere.Add(r);
                    if (r.DstF == curF && r.DstA == curA && r.DstS == curS)
                        incomingHere.Add(r);
                }

                EnsureWarpCatalogBrowserState(curF, curA, curS, outgoingHere.Count, incomingHere.Count);

                sb.AppendLine("[Current context · outgoing routes (context-collapsed)]");
                if (outgoingHere.Count == 0)
                {
                    sb.AppendLine("none");
                }
                else
                {
                    for (int i = 0; i < outgoingHere.Count; i++)
                        sb.AppendLine($"- {FormatWarpCatalogRouteInline(outgoingHere[i])}");
                }

                sb.AppendLine();
                sb.AppendLine("[Current context · incoming routes (context-collapsed)]");
                if (incomingHere.Count == 0)
                {
                    sb.AppendLine("none");
                }
                else
                {
                    for (int i = 0; i < incomingHere.Count; i++)
                        sb.AppendLine($"- {FormatWarpCatalogRouteInlineIncoming(incomingHere[i])}");
                }

                if (TryDescribeWarpCatalogBrowserSelection(out string browseDesc))
                {
                    sb.AppendLine();
                    sb.AppendLine($"[Current browser selection] {browseDesc}");
                }

                sb.AppendLine();
                sb.AppendLine("[All context routes by source]");

                string currentSourceKey = "";
                for (int i = 0; i < contextRoutes.Count; i++)
                {
                    var r = contextRoutes[i];
                    string sourceKey = BuildSourceContextKey(r.SrcF, r.SrcA, r.SrcS);
                    if (!string.Equals(sourceKey, currentSourceKey, StringComparison.Ordinal))
                    {
                        currentSourceKey = sourceKey;
                        sb.AppendLine(sourceKey);
                    }

                    sb.AppendLine($"  - {FormatWarpCatalogRouteInline(r)}");
                }

                sb.AppendLine();
                sb.AppendLine("[Exact source variants by context]");
                currentSourceKey = "";
                for (int i = 0; i < exactRoutes.Count; i++)
                {
                    var r = exactRoutes[i];
                    string sourceKey = BuildExactSourceKey(r.SrcF, r.SrcA, r.SrcS, r.SrcPointRes, r.SrcCamName);
                    if (!string.Equals(sourceKey, currentSourceKey, StringComparison.Ordinal))
                    {
                        currentSourceKey = sourceKey;
                        sb.AppendLine(sourceKey);
                    }

                    sb.AppendLine($"  - {FormatWarpCatalogRouteInline(r)}");
                }

                return sb.ToString();
            }

            private static bool TryLoadWarpCatalogStats(out List<WarpCatalogRoute> exactRoutes, out List<WarpCatalogRoute> contextRoutes, out int rawCount, out string path)
            {
                exactRoutes = new List<WarpCatalogRoute>();
                contextRoutes = new List<WarpCatalogRoute>();
                rawCount = 0;
                path = Path.Combine(DumpsDir, WarpCatalogFileName);

                if (!File.Exists(path))
                    return false;

                var exactByKey = new Dictionary<string, WarpCatalogRoute>(StringComparer.Ordinal);
                var contextByKey = new Dictionary<string, WarpCatalogRoute>(StringComparer.Ordinal);

                foreach (string rawLine in File.ReadLines(path))
                {
                    string line = (rawLine ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.Length > 0 && line[0] == '\ufeff')
                        line = line.Substring(1);

                    if (!TryParseWarpCatalogRoute(line, out WarpCatalogRoute? parsed) || parsed == null)
                        continue;

                    rawCount++;
                    MergeRoute(exactByKey, BuildExactRouteKey(parsed), parsed);
                    MergeRoute(contextByKey, BuildContextRouteKey(parsed), parsed);
                }

                exactRoutes = SortRoutes(exactByKey.Values);
                contextRoutes = SortRoutes(contextByKey.Values);
                return rawCount > 0;
            }

            private static List<WarpCatalogRoute> SortRoutes(IEnumerable<WarpCatalogRoute> routes)
            {
                return routes
                    .OrderBy(r => r.SrcF)
                    .ThenBy(r => r.SrcA)
                    .ThenBy(r => r.SrcS)
                    .ThenBy(r => r.Kind)
                    .ThenBy(r => r.DstF)
                    .ThenBy(r => r.DstA)
                    .ThenBy(r => r.DstS)
                    .ThenBy(r => r.DoorIdx)
                    .ThenBy(r => r.TransportTerminalType)
                    .ThenBy(r => r.TransportTerminalNo)
                    .ThenBy(r => r.TransportJumpNo)
                    .ThenBy(r => r.DstPointRes)
                    .ThenBy(r => r.SrcPointRes)
                    .ToList();
            }

            private static void MergeRoute(Dictionary<string, WarpCatalogRoute> map, string key, WarpCatalogRoute incoming)
            {
                if (map.TryGetValue(key, out WarpCatalogRoute? existing) && existing != null)
                {
                    existing.Count++;
                    if (string.CompareOrdinal(incoming.LastTimestamp, existing.LastTimestamp) > 0)
                    {
                        existing.LastTimestamp = incoming.LastTimestamp;
                        if (!string.IsNullOrEmpty(incoming.SrcPointRes))
                            existing.SrcPointRes = incoming.SrcPointRes;
                        if (!string.IsNullOrEmpty(incoming.SrcCamName))
                            existing.SrcCamName = incoming.SrcCamName;
                        if (!string.IsNullOrEmpty(incoming.DstPointRes))
                            existing.DstPointRes = incoming.DstPointRes;
                        if (!string.IsNullOrEmpty(incoming.DstCamName))
                            existing.DstCamName = incoming.DstCamName;
                    }

                    if (!string.IsNullOrEmpty(incoming.SrcPointRes))
                        existing.SourcePointVariants.Add(incoming.SrcPointRes);
                    if (!string.IsNullOrEmpty(incoming.SrcCamName))
                        existing.SourceCamVariants.Add(incoming.SrcCamName);
                    return;
                }

                var clone = CloneRoute(incoming);
                clone.RouteId = ComputeStableRouteId(key);
                map[key] = clone;
            }

            private static WarpCatalogRoute CloneRoute(WarpCatalogRoute r)
            {
                var clone = new WarpCatalogRoute
                {
                    Kind = r.Kind,
                    SrcF = r.SrcF,
                    SrcA = r.SrcA,
                    SrcS = r.SrcS,
                    SrcPointRes = r.SrcPointRes,
                    SrcCamName = r.SrcCamName,
                    DstF = r.DstF,
                    DstA = r.DstA,
                    DstS = r.DstS,
                    DstPointRes = r.DstPointRes,
                    DstCamName = r.DstCamName,
                    DoorIdx = r.DoorIdx,
                    TransportTerminalType = r.TransportTerminalType,
                    TransportTerminalNo = r.TransportTerminalNo,
                    TransportJumpNo = r.TransportJumpNo,
                    TransportEventStat = r.TransportEventStat,
                    RouteId = r.RouteId,
                    Count = r.Count,
                    LastTimestamp = r.LastTimestamp,
                };

                if (!string.IsNullOrEmpty(r.SrcPointRes))
                    clone.SourcePointVariants.Add(r.SrcPointRes);
                if (!string.IsNullOrEmpty(r.SrcCamName))
                    clone.SourceCamVariants.Add(r.SrcCamName);
                return clone;
            }

            private static bool TryParseWarpCatalogRoute(string jsonLine, out WarpCatalogRoute? route)
            {
                route = null;

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonLine);
                    JsonElement root = doc.RootElement;

                    var r = new WarpCatalogRoute();
                    r.Kind = GetJsonString(root, "kind");
                    r.LastTimestamp = GetJsonString(root, "t");

                    if (root.TryGetProperty("src", out JsonElement src))
                    {
                        r.SrcF = GetJsonInt(src, "F");
                        r.SrcA = GetJsonInt(src, "A");
                        r.SrcS = GetJsonInt(src, "S");
                        r.SrcPointRes = GetJsonString(src, "pointRes");
                        r.SrcCamName = GetJsonString(src, "camName");
                    }

                    if (root.TryGetProperty("dst", out JsonElement dst))
                    {
                        r.DstF = GetJsonInt(dst, "F");
                        r.DstA = GetJsonInt(dst, "A");
                        r.DstS = GetJsonInt(dst, "S");
                        r.DstPointRes = GetJsonString(dst, "pointRes");
                        r.DstCamName = GetJsonString(dst, "camName");
                    }

                    if (root.TryGetProperty("door", out JsonElement door))
                        r.DoorIdx = GetJsonInt(door, "idx");

                    if (root.TryGetProperty("terminal", out JsonElement terminal))
                    {
                        r.TransportTerminalType = GetJsonInt(terminal, "transportTerminalType");
                        r.TransportTerminalNo = GetJsonInt(terminal, "transportTerminalNo");
                        r.TransportJumpNo = GetJsonInt(terminal, "transportJumpNo");
                        r.TransportEventStat = GetJsonInt(terminal, "transportEventStat");
                    }

                    if (!string.IsNullOrEmpty(r.SrcPointRes))
                        r.SourcePointVariants.Add(r.SrcPointRes);
                    if (!string.IsNullOrEmpty(r.SrcCamName))
                        r.SourceCamVariants.Add(r.SrcCamName);

                    route = r;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static string BuildExactRouteKey(WarpCatalogRoute r)
            {
                return string.Join("|", new string[]
                {
                    r.Kind,
                    r.SrcF.ToString(CultureInfo.InvariantCulture),
                    r.SrcA.ToString(CultureInfo.InvariantCulture),
                    r.SrcS.ToString(CultureInfo.InvariantCulture),
                    r.DstF.ToString(CultureInfo.InvariantCulture),
                    r.DstA.ToString(CultureInfo.InvariantCulture),
                    r.DstS.ToString(CultureInfo.InvariantCulture),
                    r.DoorIdx.ToString(CultureInfo.InvariantCulture),
                    r.TransportTerminalType.ToString(CultureInfo.InvariantCulture),
                    r.TransportTerminalNo.ToString(CultureInfo.InvariantCulture),
                    r.TransportJumpNo.ToString(CultureInfo.InvariantCulture),
                    r.SrcPointRes,
                    r.DstPointRes
                });
            }

            private static string BuildContextRouteKey(WarpCatalogRoute r)
            {
                return string.Join("|", new string[]
                {
                    r.Kind,
                    r.SrcF.ToString(CultureInfo.InvariantCulture),
                    r.SrcA.ToString(CultureInfo.InvariantCulture),
                    r.SrcS.ToString(CultureInfo.InvariantCulture),
                    r.DstF.ToString(CultureInfo.InvariantCulture),
                    r.DstA.ToString(CultureInfo.InvariantCulture),
                    r.DstS.ToString(CultureInfo.InvariantCulture),
                    r.DoorIdx.ToString(CultureInfo.InvariantCulture),
                    r.TransportTerminalType.ToString(CultureInfo.InvariantCulture),
                    r.TransportTerminalNo.ToString(CultureInfo.InvariantCulture),
                    r.TransportJumpNo.ToString(CultureInfo.InvariantCulture),
                    r.DstPointRes
                });
            }

            private static string BuildSourceContextKey(int f, int a, int s)
            {
                return $"[F{f} A{a} S{s}]";
            }

            private static string BuildExactSourceKey(int f, int a, int s, string pointRes, string camName)
            {
                return $"[F{f} A{a} S{s}] point=\"{San(pointRes)}\" cam=\"{San(camName)}\"";
            }

            private static string FormatWarpCatalogRouteInline(WarpCatalogRoute r)
            {
                var sb = new StringBuilder(224);
                if (!string.IsNullOrEmpty(r.RouteId))
                    sb.Append('[').Append(r.RouteId).Append("] ");
                sb.Append(r.Kind);
                sb.Append(" -> F").Append(r.DstF).Append(" A").Append(r.DstA).Append(" S").Append(r.DstS);

                if (!string.IsNullOrEmpty(r.DstPointRes))
                    sb.Append(" point=\"").Append(San(r.DstPointRes)).Append("\"");

                if (string.Equals(r.Kind, "terminal", StringComparison.Ordinal))
                {
                    sb.Append(" transport(type=").Append(r.TransportTerminalType.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" no=").Append(r.TransportTerminalNo.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" jump=").Append(r.TransportJumpNo.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" evt=").Append(r.TransportEventStat.ToString(CultureInfo.InvariantCulture)).Append(')');
                }
                else
                {
                    sb.Append(" door=").Append(r.DoorIdx.ToString(CultureInfo.InvariantCulture));
                }

                AppendSourcePointVariantSummary(sb, r);
                sb.Append(" seen=").Append(r.Count.ToString(CultureInfo.InvariantCulture));
                return sb.ToString();
            }

            private static string FormatWarpCatalogRouteInlineIncoming(WarpCatalogRoute r)
            {
                var sb = new StringBuilder(224);
                if (!string.IsNullOrEmpty(r.RouteId))
                    sb.Append('[').Append(r.RouteId).Append("] ");
                sb.Append(r.Kind);
                sb.Append(" <- F").Append(r.SrcF).Append(" A").Append(r.SrcA).Append(" S").Append(r.SrcS);

                if (!string.IsNullOrEmpty(r.DstPointRes))
                    sb.Append(" point=\"").Append(San(r.DstPointRes)).Append("\"");

                if (string.Equals(r.Kind, "terminal", StringComparison.Ordinal))
                {
                    sb.Append(" transport(type=").Append(r.TransportTerminalType.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" no=").Append(r.TransportTerminalNo.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" jump=").Append(r.TransportJumpNo.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" evt=").Append(r.TransportEventStat.ToString(CultureInfo.InvariantCulture)).Append(')');
                }
                else
                {
                    sb.Append(" door=").Append(r.DoorIdx.ToString(CultureInfo.InvariantCulture));
                }

                AppendSourcePointVariantSummary(sb, r);
                sb.Append(" seen=").Append(r.Count.ToString(CultureInfo.InvariantCulture));
                return sb.ToString();
            }

            private static void AppendSourcePointVariantSummary(StringBuilder sb, WarpCatalogRoute r)
            {
                if (r.SourcePointVariants.Count == 1)
                {
                    foreach (string point in r.SourcePointVariants)
                    {
                        if (!string.IsNullOrEmpty(point))
                            sb.Append(" srcPoint=\"").Append(San(point)).Append("\"");
                        break;
                    }
                    return;
                }

                if (r.SourcePointVariants.Count <= 1)
                    return;

                sb.Append(" srcPoints=").Append(r.SourcePointVariants.Count.ToString(CultureInfo.InvariantCulture)).Append('[');
                bool first = true;
                foreach (string point in r.SourcePointVariants.OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (!first)
                        sb.Append('|');
                    sb.Append(San(point));
                    first = false;
                }
                sb.Append(']');
            }



            private static string BuildWarpCatalogSelectedRouteText(WarpCatalogRoute route, string mode, int index, int count)
            {
                var sb = new StringBuilder(512);
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source: {Path.Combine(DumpsDir, WarpCatalogFileName)}");
                sb.AppendLine($"Selection: mode={mode} [{index + 1}/{count}]");
                sb.AppendLine($"RouteId: {route.RouteId}");
                sb.AppendLine();
                sb.AppendLine($"Route: {(string.Equals(mode, "incoming", StringComparison.Ordinal) ? FormatWarpCatalogRouteInlineIncoming(route) : FormatWarpCatalogRouteInline(route))}");
                sb.AppendLine($"Kind: {route.Kind}");
                sb.AppendLine($"Src: F{route.SrcF} A{route.SrcA} S{route.SrcS} point=\"{San(route.SrcPointRes)}\" cam=\"{San(route.SrcCamName)}\"");
                sb.AppendLine($"Dst: F{route.DstF} A{route.DstA} S{route.DstS} point=\"{San(route.DstPointRes)}\" cam=\"{San(route.DstCamName)}\"");
                sb.AppendLine($"DoorIdx: {route.DoorIdx}");
                sb.AppendLine($"Transport: type={route.TransportTerminalType} no={route.TransportTerminalNo} jump={route.TransportJumpNo} evt={route.TransportEventStat}");
                sb.AppendLine($"SeenCount: {route.Count}");
                sb.AppendLine($"LastTimestamp: {route.LastTimestamp}");
                sb.AppendLine($"SourcePoints: {string.Join(" | ", route.SourcePointVariants.OrderBy(p => p, StringComparer.Ordinal))}");
                sb.AppendLine($"SourceCams: {string.Join(" | ", route.SourceCamVariants.OrderBy(p => p, StringComparer.Ordinal))}");
                return sb.ToString();
            }

            private static string BuildWarpCatalogSelectedFavoriteSnippetText(WarpCatalogRoute route, string mode, int index, int count)
            {
                var sb = new StringBuilder(768);
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source: {Path.Combine(DumpsDir, WarpCatalogFileName)}");
                sb.AppendLine($"FavoritesFile: {Path.Combine(DumpsDir, WarpFavoritesFileName)}");
                sb.AppendLine($"Selection: mode={mode} [{index + 1}/{count}]");
                sb.AppendLine($"RouteId: {route.RouteId}");
                sb.AppendLine();
                sb.AppendLine($"Route: {(string.Equals(mode, "incoming", StringComparison.Ordinal) ? FormatWarpCatalogRouteInlineIncoming(route) : FormatWarpCatalogRouteInline(route))}");
                sb.AppendLine();

                if (string.Equals(route.Kind, "door", StringComparison.Ordinal) && route.DoorIdx >= 0)
                {
                    string line = FormatWarpFavoriteLine(route.SrcF, route.SrcA, route.SrcS, route.DoorIdx, string.Empty, BuildWarpCatalogDoorFavoriteLabel(route));
                    sb.AppendLine("KindSupport: direct-door favorite (compatible with current warp_favorites executor)");
                    sb.AppendLine("Action: copy the line below into dumps/warp_favorites.txt, then press Ctrl+Alt+Shift+F7 to reload.");
                    sb.AppendLine();
                    sb.AppendLine(line);
                }
                else if (string.Equals(route.Kind, "terminal", StringComparison.Ordinal))
                {
                    sb.AppendLine("KindSupport: terminal-template only (NOT executable by current warp_favorites door-index executor)");
                    sb.AppendLine("Action: keep this as a catalog reference for future terminal-route execution work.");
                    sb.AppendLine();
                    sb.AppendLine($"# terminal routeId={route.RouteId}");
                    sb.AppendLine($"# src=F{route.SrcF} A{route.SrcA} S{route.SrcS} point=\"{San(route.SrcPointRes)}\"");
                    sb.AppendLine($"# dst=F{route.DstF} A{route.DstA} S{route.DstS} point=\"{San(route.DstPointRes)}\"");
                    sb.AppendLine($"# transport(type={route.TransportTerminalType} no={route.TransportTerminalNo} jump={route.TransportJumpNo} evt={route.TransportEventStat})");
                    if (route.SourcePointVariants.Count > 0)
                        sb.AppendLine($"# sourcePoints={string.Join(" | ", route.SourcePointVariants.OrderBy(p => p, StringComparer.Ordinal))}");
                }
                else
                {
                    sb.AppendLine("KindSupport: unsupported route kind for favorite snippet export");
                }

                return sb.ToString();
            }

            private static string BuildWarpCatalogSelectedFavoriteSnippetArchivePath(WarpCatalogRoute route)
            {
                string kind = route.Kind;
                if (string.IsNullOrWhiteSpace(kind))
                    kind = "route";

                kind = SanitizeFileNameToken(kind);
                string routeId = SanitizeFileNameToken(route.RouteId);
                return MakeDumpPath($"warp_catalog_selected_favorite_snippet_{routeId}_{kind}", "txt");
            }

            private static string SanitizeFileNameToken(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return "unknown";

                var sb = new StringBuilder(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                        sb.Append(c);
                    else
                        sb.Append('_');
                }

                return sb.Length > 0 ? sb.ToString() : "unknown";
            }

            private static string BuildWarpCatalogDoorFavoriteLabel(WarpCatalogRoute route)
            {
                string label = $"catalog {route.RouteId} -> F{route.DstF} A{route.DstA} S{route.DstS}";
                if (!string.IsNullOrEmpty(route.DstPointRes))
                    label += $" point={SanFavValue(route.DstPointRes)}";
                return label;
            }

            private static bool TryCurrentFieldContextMatches(int srcF, int srcA, int srcS)
            {
                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out _, out _);
                return curF == srcF && curA == srcA && curS == srcS;
            }

            private static bool TrySelectWarpFavorite(int doorIndex, int loadField, int loadArea, int loadScript)
            {
                lock (s_warpFavLock)
                {
                    if (s_warpFavs == null || s_warpFavs.Count <= 0)
                        return false;

                    for (int i = 0; i < s_warpFavs.Count; i++)
                    {
                        var fav = s_warpFavs[i];
                        if (fav.DoorIndex != doorIndex)
                            continue;
                        if (!fav.HasContext)
                            continue;
                        if (fav.LoadField != loadField || fav.LoadArea != loadArea || fav.LoadScript != loadScript)
                            continue;

                        s_warpFavSel = i;
                        return true;
                    }
                }

                return false;
            }

            private static bool TryGetSelectedWarpCatalogRoute(out WarpCatalogRoute? route, out string mode, out int index, out int count)
            {
                route = null;
                mode = s_warpCatalogBrowseIncoming ? "incoming" : "outgoing";
                index = 0;
                count = 0;

                if (!TryGetWarpCatalogStatsForCurrentContext(out _, out var outgoingRoutes, out var incomingRoutes))
                    return false;

                EnsureWarpCatalogBrowserState(outgoingRoutes.Count, incomingRoutes.Count);
                bool incomingMode = s_warpCatalogBrowseIncoming;
                var list = incomingMode ? incomingRoutes : outgoingRoutes;
                if (list.Count <= 0)
                    return false;

                index = incomingMode ? s_warpCatalogBrowseIncomingIndex : s_warpCatalogBrowseOutgoingIndex;
                index = WrapIndex(index, list.Count);
                count = list.Count;
                mode = incomingMode ? "incoming" : "outgoing";
                route = list[index];
                return true;
            }

            private static string ComputeStableRouteId(string key)
            {
                unchecked
                {
                    uint hash = 2166136261u;
                    for (int i = 0; i < key.Length; i++)
                    {
                        hash ^= key[i];
                        hash *= 16777619u;
                    }
                    return "R" + hash.ToString("X8", CultureInfo.InvariantCulture);
                }
            }

            private static bool TryDescribeWarpCatalogBrowserSelection(out string text)
            {
                text = string.Empty;
                if (!TryGetWarpCatalogStatsForCurrentContext(out _, out var outgoingRoutes, out var incomingRoutes))
                    return false;

                EnsureWarpCatalogBrowserState(outgoingRoutes.Count, incomingRoutes.Count);
                bool incomingMode = s_warpCatalogBrowseIncoming;
                var list = incomingMode ? incomingRoutes : outgoingRoutes;
                if (list.Count <= 0)
                    return false;

                int index = incomingMode ? s_warpCatalogBrowseIncomingIndex : s_warpCatalogBrowseOutgoingIndex;
                index = WrapIndex(index, list.Count);
                var route = list[index];
                string mode = incomingMode ? "incoming" : "outgoing";
                string body = incomingMode ? FormatWarpCatalogRouteInlineIncoming(route) : FormatWarpCatalogRouteInline(route);
                text = $"mode={mode} [{index + 1}/{list.Count}] {body}";
                return true;
            }

            private static bool TryGetWarpCatalogStatsForCurrentContext(out List<WarpCatalogRoute> contextRoutes, out List<WarpCatalogRoute> outgoingRoutes, out List<WarpCatalogRoute> incomingRoutes)
            {
                contextRoutes = new List<WarpCatalogRoute>();
                outgoingRoutes = new List<WarpCatalogRoute>();
                incomingRoutes = new List<WarpCatalogRoute>();

                if (!TryLoadWarpCatalogStats(out _, out contextRoutes, out _, out _))
                    return false;

                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out _, out _);
                if (curF < 0 || curA < 0 || curS < 0)
                    return false;

                for (int i = 0; i < contextRoutes.Count; i++)
                {
                    var r = contextRoutes[i];
                    if (r.SrcF == curF && r.SrcA == curA && r.SrcS == curS)
                        outgoingRoutes.Add(r);
                    if (r.DstF == curF && r.DstA == curA && r.DstS == curS)
                        incomingRoutes.Add(r);
                }

                EnsureWarpCatalogBrowserState(curF, curA, curS, outgoingRoutes.Count, incomingRoutes.Count);
                return true;
            }

            private static void EnsureWarpCatalogBrowserState(int outgoingCount, int incomingCount)
            {
                s_warpCatalogBrowseOutgoingIndex = ClampRouteIndex(s_warpCatalogBrowseOutgoingIndex, outgoingCount);
                s_warpCatalogBrowseIncomingIndex = ClampRouteIndex(s_warpCatalogBrowseIncomingIndex, incomingCount);
            }

            private static void EnsureWarpCatalogBrowserState(int f, int a, int s, int outgoingCount, int incomingCount)
            {
                string contextKey = BuildSourceContextKey(f, a, s);
                if (!string.Equals(contextKey, s_warpCatalogBrowseContextKey, StringComparison.Ordinal))
                {
                    s_warpCatalogBrowseContextKey = contextKey;
                    s_warpCatalogBrowseOutgoingIndex = 0;
                    s_warpCatalogBrowseIncomingIndex = 0;
                }

                EnsureWarpCatalogBrowserState(outgoingCount, incomingCount);
            }

            private static int ClampRouteIndex(int index, int count)
            {
                if (count <= 0)
                    return 0;
                if (index < 0)
                    return 0;
                if (index >= count)
                    return count - 1;
                return index;
            }

            private static int WrapIndex(int index, int count)
            {
                if (count <= 0)
                    return 0;
                int wrapped = index % count;
                if (wrapped < 0)
                    wrapped += count;
                return wrapped;
            }

            private static int GetJsonInt(JsonElement parent, string name)
            {
                try
                {
                    if (!parent.TryGetProperty(name, out JsonElement elem))
                        return -1;

                    if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out int n))
                        return n;

                    if (elem.ValueKind == JsonValueKind.String)
                    {
                        string s = elem.GetString() ?? "";
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                            return parsed;
                    }
                }
                catch { }
                return -1;
            }

            private static string GetJsonString(JsonElement parent, string name)
            {
                try
                {
                    if (!parent.TryGetProperty(name, out JsonElement elem))
                        return "";

                    if (elem.ValueKind == JsonValueKind.String)
                        return elem.GetString() ?? "";

                    return elem.ToString() ?? "";
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}
