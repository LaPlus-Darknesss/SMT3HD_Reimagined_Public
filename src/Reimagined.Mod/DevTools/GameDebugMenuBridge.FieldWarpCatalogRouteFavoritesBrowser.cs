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
            private const string WarpCatalogRouteFavoriteSelectedTextFileName = "warp_catalog_route_favorite_selected.txt";
            private const string WarpCatalogRouteFavoriteSelectedJsonFileName = "warp_catalog_route_favorite_selected.json";
            private static int s_warpCatalogRouteFavoritesBrowseIndex = 0;

            private sealed class WarpCatalogRouteFavoriteEntry
            {
                public int SchemaVersion = 0;
                public string SavedAt = "";
                public string Source = "";
                public string BrowseMode = "";
                public int SelectedIndex1 = 0;
                public int SelectedCount = 0;
                public string RawJsonLine = "";
                public WarpCatalogRoute Route = new WarpCatalogRoute();
                public bool CurrentDoorFavoritesCompatible = false;
                public string CurrentDoorFavoriteLine = "";
            }

            public static void HotkeyWarpCatalogRouteFavoritesBrowsePrev()
            {
                BrowseWarpCatalogRouteFavorites(delta: -1);
            }

            public static void HotkeyWarpCatalogRouteFavoritesBrowseNext()
            {
                BrowseWarpCatalogRouteFavorites(delta: +1);
            }

            public static void HotkeyDumpSelectedWarpCatalogRouteFavorite()
            {
                try
                {
                    if (!TryGetSelectedWarpCatalogRouteFavoriteEntry(out WarpCatalogRouteFavoriteEntry? entry, out int index, out int count) || entry == null)
                    {
                        MelonLogger.Msg("[WarpCatalogRouteFavs] Dump selected route-favorite: no saved entries.");
                        return;
                    }

                    Directory.CreateDirectory(DumpsDir);
                    string txtPath = Path.Combine(DumpsDir, WarpCatalogRouteFavoriteSelectedTextFileName);
                    string jsonPath = Path.Combine(DumpsDir, WarpCatalogRouteFavoriteSelectedJsonFileName);
                    File.WriteAllText(txtPath, BuildWarpCatalogRouteFavoriteSelectedText(entry, index, count), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    File.WriteAllText(jsonPath, PrettyPrintJson(entry.RawJsonLine), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    MelonLogger.Msg($"[WarpCatalogRouteFavs] Selected route-favorite -> {Path.GetFileName(txtPath)}, {Path.GetFileName(jsonPath)} ({BuildWarpCatalogRouteFavoriteBrowseInline(entry, index, count)})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalogRouteFavs] Failed to dump selected route-favorite: {ex}");
                }
            }

            public static bool TryGetWarpCatalogRouteFavoriteBrowseSummary(out string summary)
            {
                try
                {
                    if (!TryGetWarpCatalogRouteFavoriteEntries(out List<WarpCatalogRouteFavoriteEntry> entries) || entries.Count == 0)
                    {
                        summary = "none (Ctrl+Alt+Shift+C saves; Ctrl+Alt+Shift+;/' browse; Ctrl+Alt+Shift+\\ dumps selected)";
                        return true;
                    }

                    int index = ClampRouteIndex(s_warpCatalogRouteFavoritesBrowseIndex, entries.Count);
                    s_warpCatalogRouteFavoritesBrowseIndex = index;
                    summary = BuildWarpCatalogRouteFavoriteBrowseInline(entries[index], index, entries.Count);
                    return true;
                }
                catch (Exception ex)
                {
                    summary = $"error: {San(ex.GetType().Name)}";
                    return true;
                }
            }

            private static void BrowseWarpCatalogRouteFavorites(int delta)
            {
                try
                {
                    if (!TryGetWarpCatalogRouteFavoriteEntries(out List<WarpCatalogRouteFavoriteEntry> entries) || entries.Count == 0)
                    {
                        MelonLogger.Msg("[WarpCatalogRouteFavs] browse: no saved entries (Ctrl+Alt+Shift+C saves the selected catalog route).");
                        return;
                    }

                    s_warpCatalogRouteFavoritesBrowseIndex = WrapIndex(s_warpCatalogRouteFavoritesBrowseIndex + delta, entries.Count);
                    int index = ClampRouteIndex(s_warpCatalogRouteFavoritesBrowseIndex, entries.Count);
                    s_warpCatalogRouteFavoritesBrowseIndex = index;
                    MelonLogger.Msg($"[WarpCatalogRouteFavs] browse {BuildWarpCatalogRouteFavoriteBrowseInline(entries[index], index, entries.Count)}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalogRouteFavs] browse failed: {ex}");
                }
            }

            private static bool TryGetSelectedWarpCatalogRouteFavoriteEntry(out WarpCatalogRouteFavoriteEntry? entry, out int index, out int count)
            {
                entry = null;
                index = 0;
                count = 0;
                if (!TryGetWarpCatalogRouteFavoriteEntries(out List<WarpCatalogRouteFavoriteEntry> entries) || entries.Count == 0)
                    return false;

                count = entries.Count;
                index = ClampRouteIndex(s_warpCatalogRouteFavoritesBrowseIndex, count);
                s_warpCatalogRouteFavoritesBrowseIndex = index;
                entry = entries[index];
                return entry != null;
            }

            private static bool TryGetWarpCatalogRouteFavoriteEntries(out List<WarpCatalogRouteFavoriteEntry> entries)
            {
                entries = new List<WarpCatalogRouteFavoriteEntry>();
                try
                {
                    string path = Path.Combine(DumpsDir, WarpCatalogRouteFavoritesFileName);
                    if (!File.Exists(path))
                        return true;

                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = (raw ?? string.Empty).Trim();
                        if (line.Length == 0)
                            continue;

                        if (TryParseWarpCatalogRouteFavoriteEntry(line, out WarpCatalogRouteFavoriteEntry? entry) && entry != null)
                            entries.Add(entry);
                    }

                    s_warpCatalogRouteFavoritesBrowseIndex = ClampRouteIndex(s_warpCatalogRouteFavoritesBrowseIndex, entries.Count);
                    return true;
                }
                catch
                {
                    entries = new List<WarpCatalogRouteFavoriteEntry>();
                    return false;
                }
            }

            private static bool TryParseWarpCatalogRouteFavoriteEntry(string jsonLine, out WarpCatalogRouteFavoriteEntry? entry)
            {
                entry = null;
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonLine);
                    JsonElement root = doc.RootElement;
                    WarpCatalogRouteFavoriteEntry parsed = new WarpCatalogRouteFavoriteEntry();
                    parsed.RawJsonLine = jsonLine;
                    parsed.SchemaVersion = GetJsonInt(root, "schemaVersion");
                    parsed.SavedAt = GetJsonString(root, "savedAt");
                    parsed.Source = GetJsonString(root, "source");
                    parsed.BrowseMode = GetJsonString(root, "browseMode");
                    parsed.SelectedIndex1 = GetJsonInt(root, "selectedIndex1");
                    parsed.SelectedCount = GetJsonInt(root, "selectedCount");

                    JsonElement executor = GetJsonObject(root, "executor");
                    parsed.CurrentDoorFavoritesCompatible = GetJsonBool(executor, "currentDoorFavoritesCompatible");
                    parsed.CurrentDoorFavoriteLine = GetJsonString(executor, "currentDoorFavoriteLine");

                    WarpCatalogRoute route = new WarpCatalogRoute();
                    route.RouteId = GetJsonString(root, "routeId");
                    route.Kind = GetJsonString(root, "kind");
                    route.Count = Math.Max(1, GetJsonInt(root, "catalogSeenCount"));
                    route.LastTimestamp = GetJsonString(root, "catalogLastTimestamp");

                    JsonElement src = GetJsonObject(root, "src");
                    route.SrcF = GetJsonInt(src, "F");
                    route.SrcA = GetJsonInt(src, "A");
                    route.SrcS = GetJsonInt(src, "S");
                    route.SrcPointRes = GetJsonString(src, "pointRes");
                    route.SrcCamName = GetJsonString(src, "camName");
                    AddObservedStringVariants(route.SourcePointVariants, GetJsonStringArray(src, "sourcePoints"), route.SrcPointRes);
                    AddObservedStringVariants(route.SourceCamVariants, GetJsonStringArray(src, "sourceCams"), route.SrcCamName);

                    JsonElement dst = GetJsonObject(root, "dst");
                    route.DstF = GetJsonInt(dst, "F");
                    route.DstA = GetJsonInt(dst, "A");
                    route.DstS = GetJsonInt(dst, "S");
                    route.DstPointRes = GetJsonString(dst, "pointRes");
                    route.DstCamName = GetJsonString(dst, "camName");

                    JsonElement door = GetJsonObject(root, "door");
                    route.DoorIdx = GetJsonInt(door, "idx");

                    JsonElement terminal = GetJsonObject(root, "terminal");
                    route.TransportTerminalType = GetJsonInt(terminal, "transportTerminalType");
                    route.TransportTerminalNo = GetJsonInt(terminal, "transportTerminalNo");
                    route.TransportJumpNo = GetJsonInt(terminal, "transportJumpNo");
                    route.TransportEventStat = GetJsonInt(terminal, "transportEventStat");
                    route.TransportSeq = GetJsonInt(terminal, "transportSeq");
                    route.TransportCallMode = GetJsonInt(terminal, "transportCallMode");
                    route.TransportProcessStat = GetJsonInt(terminal, "transportProcessStat");
                    route.TransportTerminalCnt = GetJsonInt(terminal, "transportTerminalCnt");
                    JsonElement observed = GetJsonObject(terminal, "observed");
                    AddObservedIntVariants(route.TransportTerminalTypeVariants, GetJsonIntArray(observed, "transportTerminalType"), route.TransportTerminalType);
                    AddObservedIntVariants(route.TransportTerminalNoVariants, GetJsonIntArray(observed, "transportTerminalNo"), route.TransportTerminalNo);
                    AddObservedIntVariants(route.TransportJumpNoVariants, GetJsonIntArray(observed, "transportJumpNo"), route.TransportJumpNo);
                    AddObservedIntVariants(route.TransportEventStatVariants, GetJsonIntArray(observed, "transportEventStat"), route.TransportEventStat);
                    AddObservedIntVariants(route.TransportSeqVariants, GetJsonIntArray(observed, "transportSeq"), route.TransportSeq);
                    AddObservedIntVariants(route.TransportCallModeVariants, GetJsonIntArray(observed, "transportCallMode"), route.TransportCallMode);
                    AddObservedIntVariants(route.TransportProcessStatVariants, GetJsonIntArray(observed, "transportProcessStat"), route.TransportProcessStat);
                    AddObservedIntVariants(route.TransportTerminalCntVariants, GetJsonIntArray(observed, "transportTerminalCnt"), route.TransportTerminalCnt);

                    parsed.Route = route;
                    entry = parsed;
                    return !string.IsNullOrEmpty(route.RouteId) && !string.IsNullOrEmpty(route.Kind);
                }
                catch
                {
                    entry = null;
                    return false;
                }
            }

            private static string BuildWarpCatalogRouteFavoriteBrowseInline(WarpCatalogRouteFavoriteEntry entry, int index, int count)
            {
                string body = FormatWarpCatalogRouteInline(entry.Route);
                if (count <= 0)
                    return $"[0/0] {body}";
                return $"[{(index + 1).ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)}] {body}";
            }


            public static void WriteSelectedWarpCatalogRouteFavoriteTerminalSeamProbe(StreamWriter w)
            {
                if (w == null)
                    return;

                w.WriteLine("selected_route_favorite_probe:");
                if (!TryGetSelectedWarpCatalogRouteFavoriteEntry(out WarpCatalogRouteFavoriteEntry? entry, out int index, out int count) || entry == null)
                {
                    w.WriteLine("  selected=<none>");
                    return;
                }

                WarpCatalogRoute route = entry.Route;
                w.WriteLine($"  selected={BuildWarpCatalogRouteFavoriteBrowseInline(entry, index, count)}");
                w.WriteLine($"  kind={San(route.Kind)}");
                w.WriteLine($"  saved_at={San(entry.SavedAt)}");
                w.WriteLine($"  saved_browse={San(entry.BrowseMode)} [{entry.SelectedIndex1.ToString(CultureInfo.InvariantCulture)}/{entry.SelectedCount.ToString(CultureInfo.InvariantCulture)}]");
                w.WriteLine($"  src=F{route.SrcF} A{route.SrcA} S{route.SrcS}");
                w.WriteLine($"  dst=F{route.DstF} A{route.DstA} S{route.DstS}");
                w.WriteLine($"  executor_current_door_favorites_compatible={entry.CurrentDoorFavoritesCompatible}");
                if (!string.IsNullOrEmpty(entry.CurrentDoorFavoriteLine))
                    w.WriteLine($"  executor_current_door_favorite_line={San(entry.CurrentDoorFavoriteLine)}");

                if (!string.Equals(route.Kind, "terminal", StringComparison.Ordinal))
                {
                    w.WriteLine("  terminal_candidate=false");
                    return;
                }

                int[] observedTransportTerminalType = GetOrderedObservedTransportTerminalTypeVariants(route);
                int[] observedTransportTerminalNo = GetOrderedObservedTransportTerminalNoVariants(route);
                int[] observedTransportJumpNo = GetOrderedObservedTransportJumpNoVariants(route);
                int[] observedTransportEventStat = GetOrderedObservedTransportEventStatVariants(route);
                int[] observedTransportSeq = GetOrderedObservedTransportSeqVariants(route);
                int[] observedTransportCallMode = GetOrderedObservedTransportCallModeVariants(route);
                int[] observedTransportProcessStat = GetOrderedObservedTransportProcessStatVariants(route);
                int[] observedTransportTerminalCnt = GetOrderedObservedTransportTerminalCntVariants(route);

                w.WriteLine("  terminal_candidate=true");
                w.WriteLine($"  candidate_transport_latest=type={route.TransportTerminalType.ToString(CultureInfo.InvariantCulture)} no={route.TransportTerminalNo.ToString(CultureInfo.InvariantCulture)} jump={route.TransportJumpNo.ToString(CultureInfo.InvariantCulture)} evt={FormatTrmEvtValue(route.TransportEventStat)} seq={FormatTrmSeqValue(route.TransportSeq)} call={route.TransportCallMode.ToString(CultureInfo.InvariantCulture)} proc={route.TransportProcessStat.ToString(CultureInfo.InvariantCulture)} cnt={route.TransportTerminalCnt.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  candidate_transport_observed=type={FormatObservedIntArray(observedTransportTerminalType)} no={FormatObservedIntArray(observedTransportTerminalNo)} jump={FormatObservedIntArray(observedTransportJumpNo)} evt={FormatObservedIntArrayWithLabels(observedTransportEventStat, true)} seq={FormatObservedIntArrayWithLabels(observedTransportSeq, false)} call={FormatObservedIntArray(observedTransportCallMode)} proc={FormatObservedIntArray(observedTransportProcessStat)} cnt={FormatObservedIntArray(observedTransportTerminalCnt)}");

                bool readAnything = TryReadCurrentTerminalState(
                    out bool terminalActive,
                    out int liveCheckTerminal,
                    out int liveCallMode,
                    out int liveProcessStat,
                    out int liveEventStat,
                    out int liveJumpNo,
                    out int liveTerminalType,
                    out int liveTerminalNo,
                    out int liveTerminalCnt);

                w.WriteLine($"  live_probe_read_anything={readAnything}");
                w.WriteLine($"  live_terminal_active={terminalActive}");
                w.WriteLine($"  live_check_terminal={liveCheckTerminal.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_terminal_type={liveTerminalType.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_terminal_no={liveTerminalNo.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_jump_no={liveJumpNo.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_event_stat={FormatTrmEvtValue(liveEventStat)}");
                w.WriteLine($"  live_call_mode={liveCallMode.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_process_stat={liveProcessStat.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_terminal_cnt={liveTerminalCnt.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"  live_trm_seq=<unavailable from current safe probe surface>");

                bool identityMatch =
                    ContainsObservedInt(observedTransportTerminalType, liveTerminalType) &&
                    ContainsObservedInt(observedTransportTerminalNo, liveTerminalNo) &&
                    ContainsObservedInt(observedTransportJumpNo, liveJumpNo) &&
                    ContainsObservedInt(observedTransportEventStat, liveEventStat);
                bool stateMatch =
                    ContainsObservedInt(observedTransportCallMode, liveCallMode) &&
                    ContainsObservedInt(observedTransportProcessStat, liveProcessStat) &&
                    ContainsObservedInt(observedTransportTerminalCnt, liveTerminalCnt);

                w.WriteLine($"  compare_identity_match={identityMatch}");
                w.WriteLine($"  compare_state_match={stateMatch}");
                w.WriteLine($"  compare_call_mode_match={ContainsObservedInt(observedTransportCallMode, liveCallMode)}");
                w.WriteLine($"  compare_process_stat_match={ContainsObservedInt(observedTransportProcessStat, liveProcessStat)}");
                w.WriteLine($"  compare_terminal_cnt_match={ContainsObservedInt(observedTransportTerminalCnt, liveTerminalCnt)}");
                w.WriteLine("  compare_trm_seq_match=<unavailable from current safe probe surface>");
            }

            private static string FormatObservedIntArrayWithLabels(int[] values, bool eventLabels)
            {
                if (values == null || values.Length == 0)
                    return "<none>";

                var parts = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                    parts[i] = eventLabels ? FormatTrmEvtValue(values[i]) : FormatTrmSeqValue(values[i]);
                return string.Join(" | ", parts);
            }

            private static string BuildWarpCatalogRouteFavoriteSelectedText(WarpCatalogRouteFavoriteEntry entry, int index, int count)
            {
                WarpCatalogRoute route = entry.Route;
                string[] sourcePoints = GetOrderedSourcePointVariants(route);
                string[] sourceCams = GetOrderedSourceCamVariants(route);
                int[] observedTransportTerminalType = GetOrderedObservedTransportTerminalTypeVariants(route);
                int[] observedTransportTerminalNo = GetOrderedObservedTransportTerminalNoVariants(route);
                int[] observedTransportJumpNo = GetOrderedObservedTransportJumpNoVariants(route);
                int[] observedTransportEventStat = GetOrderedObservedTransportEventStatVariants(route);
                int[] observedTransportSeq = GetOrderedObservedTransportSeqVariants(route);
                int[] observedTransportCallMode = GetOrderedObservedTransportCallModeVariants(route);
                int[] observedTransportProcessStat = GetOrderedObservedTransportProcessStatVariants(route);
                int[] observedTransportTerminalCnt = GetOrderedObservedTransportTerminalCntVariants(route);

                var sb = new StringBuilder(1024);
                sb.AppendLine($"RegistryIndex: {index + 1}/{count}");
                sb.AppendLine($"SchemaVersion: {entry.SchemaVersion}");
                sb.AppendLine($"SavedAt: {San(entry.SavedAt)}");
                sb.AppendLine($"SavedBrowse: {San(entry.BrowseMode)} [{entry.SelectedIndex1.ToString(CultureInfo.InvariantCulture)}/{entry.SelectedCount.ToString(CultureInfo.InvariantCulture)}]");
                sb.AppendLine($"RegistrySource: {San(Path.Combine(DumpsDir, WarpCatalogRouteFavoritesFileName))}");
                sb.AppendLine($"CatalogSource: {San(entry.Source)}");
                sb.AppendLine($"Route: {FormatWarpCatalogRouteInline(route)}");
                sb.AppendLine($"SrcContext: F{route.SrcF} A{route.SrcA} S{route.SrcS}");
                sb.AppendLine($"DstContext: F{route.DstF} A{route.DstA} S{route.DstS}");
                sb.AppendLine($"SourcePoints: {(sourcePoints.Length == 0 ? "<none>" : string.Join(" | ", sourcePoints))}");
                sb.AppendLine($"SourceCams: {(sourceCams.Length == 0 ? "<none>" : string.Join(" | ", sourceCams))}");
                if (string.Equals(route.Kind, "door", StringComparison.Ordinal))
                {
                    sb.AppendLine($"DoorIdx: {route.DoorIdx.ToString(CultureInfo.InvariantCulture)}");
                }
                else if (string.Equals(route.Kind, "terminal", StringComparison.Ordinal))
                {
                    sb.AppendLine($"TransportLatest: type={route.TransportTerminalType.ToString(CultureInfo.InvariantCulture)} no={route.TransportTerminalNo.ToString(CultureInfo.InvariantCulture)} jump={route.TransportJumpNo.ToString(CultureInfo.InvariantCulture)} evt={route.TransportEventStat.ToString(CultureInfo.InvariantCulture)} seq={route.TransportSeq.ToString(CultureInfo.InvariantCulture)} call={route.TransportCallMode.ToString(CultureInfo.InvariantCulture)} proc={route.TransportProcessStat.ToString(CultureInfo.InvariantCulture)} cnt={route.TransportTerminalCnt.ToString(CultureInfo.InvariantCulture)}");
                    sb.AppendLine($"TransportObserved: type={FormatObservedIntArray(observedTransportTerminalType)} no={FormatObservedIntArray(observedTransportTerminalNo)} jump={FormatObservedIntArray(observedTransportJumpNo)} evt={FormatObservedIntArray(observedTransportEventStat)} seq={FormatObservedIntArray(observedTransportSeq)} call={FormatObservedIntArray(observedTransportCallMode)} proc={FormatObservedIntArray(observedTransportProcessStat)} cnt={FormatObservedIntArray(observedTransportTerminalCnt)}");
                }
                sb.AppendLine($"ExecutorCurrentDoorFavoritesCompatible: {entry.CurrentDoorFavoritesCompatible}");
                sb.AppendLine($"ExecutorCurrentDoorFavoriteLine: {(string.IsNullOrEmpty(entry.CurrentDoorFavoriteLine) ? "<none>" : San(entry.CurrentDoorFavoriteLine))}");
                sb.AppendLine($"CatalogSeenCount: {route.Count.ToString(CultureInfo.InvariantCulture)}");
                sb.AppendLine($"CatalogLastTimestamp: {San(route.LastTimestamp)}");
                sb.AppendLine();
                sb.AppendLine("RawJson:");
                sb.AppendLine(PrettyPrintJson(entry.RawJsonLine));
                return sb.ToString();
            }

            private static string PrettyPrintJson(string jsonLine)
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonLine);
                    using var stream = new MemoryStream();
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        doc.RootElement.WriteTo(writer);
                    }
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
                catch
                {
                    return jsonLine ?? string.Empty;
                }
            }

            private static void AddObservedStringVariants(HashSet<string> set, string[] values, string fallback)
            {
                if (set == null)
                    return;

                for (int i = 0; i < values.Length; i++)
                {
                    string value = values[i] ?? string.Empty;
                    if (value.Length > 0)
                        set.Add(value);
                }

                if (!string.IsNullOrEmpty(fallback))
                    set.Add(fallback);
            }

            private static void AddObservedIntVariants(HashSet<int> set, int[] values, int fallback)
            {
                if (set == null)
                    return;

                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] >= 0)
                        set.Add(values[i]);
                }

                if (fallback >= 0)
                    set.Add(fallback);
            }

            private static bool GetJsonBool(JsonElement parent, string name)
            {
                try
                {
                    if (!parent.TryGetProperty(name, out JsonElement elem))
                        return false;

                    switch (elem.ValueKind)
                    {
                        case JsonValueKind.True:
                            return true;
                        case JsonValueKind.False:
                            return false;
                        case JsonValueKind.String:
                            return bool.TryParse(elem.GetString(), out bool parsed) && parsed;
                        case JsonValueKind.Number:
                            return elem.TryGetInt32(out int n) && n != 0;
                        default:
                            return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            private static string[] GetJsonStringArray(JsonElement parent, string name)
            {
                try
                {
                    if (!parent.TryGetProperty(name, out JsonElement elem) || elem.ValueKind != JsonValueKind.Array)
                        return Array.Empty<string>();

                    List<string> values = new List<string>();
                    foreach (JsonElement item in elem.EnumerateArray())
                    {
                        string value = item.ValueKind == JsonValueKind.String ? (item.GetString() ?? string.Empty) : (item.ToString() ?? string.Empty);
                        if (!string.IsNullOrEmpty(value))
                            values.Add(value);
                    }
                    return values.ToArray();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            private static int[] GetJsonIntArray(JsonElement parent, string name)
            {
                try
                {
                    if (!parent.TryGetProperty(name, out JsonElement elem) || elem.ValueKind != JsonValueKind.Array)
                        return Array.Empty<int>();

                    List<int> values = new List<int>();
                    foreach (JsonElement item in elem.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int n))
                        {
                            values.Add(n);
                            continue;
                        }

                        if (item.ValueKind == JsonValueKind.String)
                        {
                            string s = item.GetString() ?? string.Empty;
                            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                                values.Add(parsed);
                        }
                    }
                    return values.ToArray();
                }
                catch
                {
                    return Array.Empty<int>();
                }
            }



            private static bool ContainsObservedInt(int[] values, int probe)
            {
                if (probe < 0 || values == null || values.Length == 0)
                    return false;

                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] == probe)
                        return true;
                }

                return false;
            }

            private static string FormatObservedIntArray(int[] values)
            {
                if (values == null || values.Length == 0)
                    return "<none>";

                return string.Join(" | ", values.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray());
            }
        }
    }
}
