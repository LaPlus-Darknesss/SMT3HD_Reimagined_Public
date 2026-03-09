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

            private readonly struct TerminalUiValueLabelRow
            {
                public TerminalUiValueLabelRow(int ordinal0, int value, string label, bool highlighted, string key)
                {
                    Ordinal0 = ordinal0;
                    Value = value;
                    Label = label ?? string.Empty;
                    Highlighted = highlighted;
                    Key = key ?? string.Empty;
                }

                public int Ordinal0 { get; }
                public int Value { get; }
                public string Label { get; }
                public bool Highlighted { get; }
                public string Key { get; }
            }

            private static bool TryReadCurrentTerminalUiValueLabelMap(out string? title, out List<TerminalUiValueLabelRow>? rows)
            {
                title = null;
                rows = null;

                if (!TryReadCurrentTerminalStaticWorkSnapshot(out TerminalStaticWorkSnapshot? staticWork) || staticWork == null)
                    return false;

                int[] terminalListValues = staticWork.TerminalListValues ?? Array.Empty<int>();
                int activeCount = Math.Min(Math.Max(0, staticWork.TerminalCnt), terminalListValues.Length);
                if (activeCount <= 0)
                    return false;

                Type? terminalDraw = TryFindLoadedType("Il2Cpp.fclTerminalDraw") ?? TryFindLoadedType("fclTerminalDraw");
                if (terminalDraw == null)
                    return false;

                object? textObjs = TryGetStaticMemberValue(terminalDraw, "TextObjs");
                var entries = TryEnumerateDictionaryEntries(textObjs, 128);
                if (entries.Count == 0)
                    return false;

                var ordered = new SortedDictionary<int, TerminalUiValueLabelRow>();
                for (int i = 0; i < entries.Count; i++)
                {
                    var row = entries[i];
                    string key = row.key ?? string.Empty;
                    string rawText = TryGetTextPropertyStrict(row.value) ?? TryGetTmpText(row.value) ?? string.Empty;
                    string label = NormalizeTerminalUiLabel(rawText);
                    if (string.IsNullOrWhiteSpace(label))
                        continue;

                    if (string.Equals(key, "tmnltitle", StringComparison.OrdinalIgnoreCase))
                    {
                        title = label;
                        continue;
                    }

                    if (!TryParseTerminalWindowOrdinal0(key, out int ordinal0))
                        continue;
                    if (ordinal0 < 0 || ordinal0 >= activeCount)
                        continue;

                    bool highlighted = rawText.IndexOf("TMC01", StringComparison.OrdinalIgnoreCase) >= 0;
                    ordered[ordinal0] = new TerminalUiValueLabelRow(ordinal0, terminalListValues[ordinal0], label, highlighted, key);
                }

                if (ordered.Count == 0)
                    return false;

                rows = ordered.Values.ToList();
                return true;
            }

            private static bool TryParseTerminalWindowOrdinal0(string? key, out int ordinal0)
            {
                ordinal0 = -1;
                if (string.IsNullOrWhiteSpace(key))
                    return false;

                const string prefix = "t_termwnd";
                string k = key!.Trim();
                if (!k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;

                string digits = k.Substring(prefix.Length);
                if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ordinal1))
                    return false;

                ordinal0 = ordinal1 - 1;
                return ordinal0 >= 0;
            }

            private static string NormalizeTerminalUiLabel(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return string.Empty;

                string s = raw!;
                var sb = new StringBuilder(s.Length);
                bool inTag = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '<')
                    {
                        inTag = true;
                        continue;
                    }
                    if (c == '>')
                    {
                        inTag = false;
                        continue;
                    }
                    if (!inTag)
                        sb.Append(c);
                }

                string stripped = sb.ToString().Replace("\r", " ").Replace("\n", " ").Trim();
                while (stripped.Contains("  ", StringComparison.Ordinal))
                    stripped = stripped.Replace("  ", " ", StringComparison.Ordinal);
                return stripped;
            }

            private static string FormatTerminalUiValueLabelRows(List<TerminalUiValueLabelRow> rows)
            {
                if (rows == null || rows.Count == 0)
                    return "<none>";

                var parts = new List<string>(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    TerminalUiValueLabelRow row = rows[i];
                    parts.Add($"{row.Value.ToString(CultureInfo.InvariantCulture)}->{San(row.Label)}{(row.Highlighted ? " [selected]" : string.Empty)}");
                }
                return string.Join(" | ", parts);
            }


            public static void WriteCurrentTerminalCoverageExport(StreamWriter w)
            {
                if (w == null)
                    return;

                w.WriteLine("[terminal coverage export]");

                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out _, out _);
                string sourceContext = (curF >= 0 && curA >= 0 && curS >= 0)
                    ? $"F{curF} A{curA} S{curS}"
                    : "<unavailable>";
                w.WriteLine($"source_context={sourceContext}");

                if (TryGetSelectedWarpCatalogRouteFavoriteEntry(out WarpCatalogRouteFavoriteEntry? selectedEntry, out int selectedIndex, out int selectedCount) && selectedEntry != null)
                    w.WriteLine($"selected_route_favorite={BuildWarpCatalogRouteFavoriteBrowseInline(selectedEntry, selectedIndex, selectedCount)}");
                else
                    w.WriteLine("selected_route_favorite=<none>");

                if (!TryReadCurrentTerminalStaticWorkSnapshot(out TerminalStaticWorkSnapshot? staticWork) || staticWork == null)
                {
                    w.WriteLine("terminal_static_work=<unavailable>");
                    return;
                }

                w.WriteLine($"phase_hint={ClassifyTerminalPhase(staticWork.SeqCurrent)}");
                w.WriteLine($"seq_current={FormatTrmSeqValue(staticWork.SeqCurrent)}");
                w.WriteLine($"seq_last={FormatTrmSeqValue(staticWork.SeqLast)}");
                w.WriteLine($"terminal_identity=type={staticWork.TerminalType.ToString(CultureInfo.InvariantCulture)} no={staticWork.TerminalNo.ToString(CultureInfo.InvariantCulture)} cnt={staticWork.TerminalCnt.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"terminal_list_active={FormatTerminalListActive(staticWork.TerminalListValues, staticWork.TerminalCnt)}");

                bool hasSelected = TryGetCurrentTerminalSelectedListValue(staticWork, out int selectedOrdinal0, out int selectedValue);
                if (hasSelected)
                    w.WriteLine($"selected_terminal_list=ordinal0={selectedOrdinal0.ToString(CultureInfo.InvariantCulture)} ordinal1={(selectedOrdinal0 + 1).ToString(CultureInfo.InvariantCulture)}/{Math.Max(0, staticWork.TerminalCnt).ToString(CultureInfo.InvariantCulture)} value={selectedValue.ToString(CultureInfo.InvariantCulture)}");
                else
                    w.WriteLine("selected_terminal_list=<unavailable>");

                bool uiLabelPhaseEligible = staticWork.SeqCurrent == 1 || staticWork.SeqCurrent == 5;
                string? uiTitle = null;
                List<TerminalUiValueLabelRow>? uiLabelRows = null;
                bool gotUiLabels = uiLabelPhaseEligible
                    && TryReadCurrentTerminalUiValueLabelMap(out uiTitle, out uiLabelRows)
                    && uiLabelRows != null
                    && uiLabelRows.Count > 0;
                if (gotUiLabels)
                {
                    w.WriteLine($"terminal_ui_title={San(uiTitle)}");
                    w.WriteLine($"terminal_ui_value_label_map={FormatTerminalUiValueLabelRows(uiLabelRows!)}");
                    if (hasSelected && selectedOrdinal0 >= 0 && selectedOrdinal0 < uiLabelRows!.Count)
                    {
                        TerminalUiValueLabelRow selectedUiRow = uiLabelRows[selectedOrdinal0];
                        w.WriteLine($"selected_terminal_ui_label={San(selectedUiRow.Label)}");
                        w.WriteLine($"selected_terminal_ui_highlight={(selectedUiRow.Highlighted ? "true" : "false")}");
                    }
                    else
                    {
                        w.WriteLine("selected_terminal_ui_label=<unavailable>");
                        w.WriteLine("selected_terminal_ui_highlight=<unavailable>");
                    }
                }
                else
                {
                    w.WriteLine(uiLabelPhaseEligible ? "terminal_ui_value_label_map=<unavailable>" : "terminal_ui_value_label_map=<not transport/confirm phase>");
                }

                if (!TryLoadWarpCatalogStats(out _, out List<WarpCatalogRoute> contextRoutes, out _, out _))
                {
                    w.WriteLine("warp_catalog_context_routes=<unavailable>");
                    return;
                }

                List<WarpCatalogRoute> allTerminalRoutes = contextRoutes
                    .Where(r => string.Equals(r.Kind, "terminal", StringComparison.Ordinal))
                    .ToList();
                List<WarpCatalogRoute> outgoingTerminalRoutes = allTerminalRoutes
                    .Where(r => r.SrcF == curF
                             && r.SrcA == curA
                             && r.SrcS == curS)
                    .OrderBy(r => r.TransportJumpNo)
                    .ThenBy(r => r.DstF)
                    .ThenBy(r => r.DstA)
                    .ThenBy(r => r.DstS)
                    .ThenBy(r => r.DstPointRes, StringComparer.Ordinal)
                    .ToList();
                List<WarpCatalogRoute> incomingTerminalRoutes = allTerminalRoutes
                    .Where(r => r.DstF == curF
                             && r.DstA == curA
                             && r.DstS == curS)
                    .OrderBy(r => r.SrcF)
                    .ThenBy(r => r.SrcA)
                    .ThenBy(r => r.SrcS)
                    .ThenBy(r => r.RouteId, StringComparer.Ordinal)
                    .ToList();

                w.WriteLine($"catalog_terminal_routes_outgoing={outgoingTerminalRoutes.Count.ToString(CultureInfo.InvariantCulture)}");
                w.WriteLine($"catalog_terminal_routes_incoming={incomingTerminalRoutes.Count.ToString(CultureInfo.InvariantCulture)}");

                int activeCount = 0;
                int mappedCount = 0;
                int unmappedCount = 0;
                int ambiguousCount = 0;
                int[] terminalListValues = staticWork.TerminalListValues ?? Array.Empty<int>();
                int count = Math.Min(Math.Max(0, staticWork.TerminalCnt), terminalListValues.Length);
                activeCount = count;

                var lines = new List<string>(count * 3 + 1);
                for (int i = 0; i < count; i++)
                {
                    int value = terminalListValues[i];
                    bool isSelected = hasSelected && i == selectedOrdinal0;
                    List<WarpCatalogRoute> matches = outgoingTerminalRoutes
                        .Where(r => ContainsObservedInt(GetOrderedObservedTransportJumpNoVariants(r), value))
                        .ToList();

                    string status;
                    if (matches.Count == 0)
                    {
                        unmappedCount++;
                        status = "unmapped";
                    }
                    else if (matches.Count == 1)
                    {
                        mappedCount++;
                        status = "mapped";
                    }
                    else
                    {
                        ambiguousCount++;
                        status = "ambiguous";
                    }

                    lines.Add($"entry[{i.ToString(CultureInfo.InvariantCulture)}]=value={value.ToString(CultureInfo.InvariantCulture)} selected={(isSelected ? "true" : "false")} status={status} match_count={matches.Count.ToString(CultureInfo.InvariantCulture)}");
                    if (gotUiLabels && uiLabelRows != null && i >= 0 && i < uiLabelRows.Count)
                    {
                        TerminalUiValueLabelRow uiRow = uiLabelRows[i];
                        lines.Add($"entry[{i.ToString(CultureInfo.InvariantCulture)}].ui_label={San(uiRow.Label)}");
                        lines.Add($"entry[{i.ToString(CultureInfo.InvariantCulture)}].ui_highlight={(uiRow.Highlighted ? "true" : "false")}");
                    }
                    for (int m = 0; m < matches.Count; m++)
                        lines.Add($"entry[{i.ToString(CultureInfo.InvariantCulture)}].match[{m.ToString(CultureInfo.InvariantCulture)}]={BuildCatalogTerminalRouteInferenceInline(matches[m])}");

                    if (matches.Count > 0)
                    {
                        List<WarpCatalogRoute> reverseMatches = allTerminalRoutes
                            .Where(r => matches.Any(m =>
                                r.SrcF == m.DstF &&
                                r.SrcA == m.DstA &&
                                r.SrcS == m.DstS &&
                                r.DstF == curF &&
                                r.DstA == curA &&
                                r.DstS == curS &&
                                GetOrderedObservedTransportJumpNoVariants(r).Intersect(GetOrderedObservedTransportJumpNoVariants(m)).Any()))
                            .OrderBy(r => r.SrcF)
                            .ThenBy(r => r.SrcA)
                            .ThenBy(r => r.SrcS)
                            .ThenBy(r => r.RouteId, StringComparer.Ordinal)
                            .ToList();
                        lines.Add($"entry[{i.ToString(CultureInfo.InvariantCulture)}].reverse_match_count={reverseMatches.Count.ToString(CultureInfo.InvariantCulture)}");
                        for (int r = 0; r < reverseMatches.Count; r++)
                            lines.Add($"entry[{i.ToString(CultureInfo.InvariantCulture)}].reverse_match[{r.ToString(CultureInfo.InvariantCulture)}]={BuildCatalogTerminalReverseInferenceInline(reverseMatches[r], curF, curA, curS)}");
                    }
                }

                w.WriteLine($"catalog_terminal_list_coverage=active={activeCount.ToString(CultureInfo.InvariantCulture)} mapped={mappedCount.ToString(CultureInfo.InvariantCulture)} unmapped={unmappedCount.ToString(CultureInfo.InvariantCulture)} ambiguous={ambiguousCount.ToString(CultureInfo.InvariantCulture)}");
                for (int i = 0; i < lines.Count; i++)
                    w.WriteLine(lines[i]);
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

                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out _, out _);
                bool gotStaticWork = TryReadCurrentTerminalStaticWorkSnapshot(out TerminalStaticWorkSnapshot? staticWork) && staticWork != null;
                int selectedListOrdinal0 = -1;
                int selectedListValue = -1;
                string catalogSourceContext = "<unavailable>";
                int catalogSourceTerminalRouteCount = -1;
                string catalogTerminalListMap = "<unavailable>";
                int catalogActiveTerminalListCount = -1;
                int catalogMappedTerminalListCount = -1;
                int catalogUnmappedTerminalListCount = -1;
                int catalogAmbiguousTerminalListCount = -1;
                string catalogMappedTerminalListValues = "<unavailable>";
                string catalogUnmappedTerminalListValues = "<unavailable>";
                string catalogAmbiguousTerminalListValues = "<unavailable>";
                List<WarpCatalogRoute>? selectedCatalogMatches = null;
                List<WarpCatalogRoute>? selectedCatalogReverseMatches = null;
                if (gotStaticWork)
                {
                    w.WriteLine($"  live_static_phase_hint={ClassifyTerminalPhase(staticWork!.SeqCurrent)}");
                    w.WriteLine($"  live_static_seq_current={FormatTrmSeqValue(staticWork.SeqCurrent)}");
                    w.WriteLine($"  live_static_seq_last={FormatTrmSeqValue(staticWork.SeqLast)}");
                    if (TryGetCurrentTerminalSelectedListValue(staticWork, out selectedListOrdinal0, out selectedListValue))
                    {
                        int ordinal1 = selectedListOrdinal0 + 1;
                        string countText = staticWork.TerminalCnt > 0
                            ? staticWork.TerminalCnt.ToString(CultureInfo.InvariantCulture)
                            : "?";
                        w.WriteLine($"  live_selected_terminal_list=ordinal0={selectedListOrdinal0.ToString(CultureInfo.InvariantCulture)} ordinal1={ordinal1.ToString(CultureInfo.InvariantCulture)}/{countText} value={selectedListValue.ToString(CultureInfo.InvariantCulture)} getterJumpMatch={(selectedListValue == liveJumpNo ? "true" : "false")}");
                    }
                    else
                    {
                        w.WriteLine("  live_selected_terminal_list=<unavailable>");
                    }

                    _ = TryBuildCurrentTerminalCatalogInference(
                        staticWork,
                        selectedListValue,
                        out catalogSourceContext,
                        out catalogSourceTerminalRouteCount,
                        out catalogTerminalListMap,
                        out catalogActiveTerminalListCount,
                        out catalogMappedTerminalListCount,
                        out catalogUnmappedTerminalListCount,
                        out catalogAmbiguousTerminalListCount,
                        out catalogMappedTerminalListValues,
                        out catalogUnmappedTerminalListValues,
                        out catalogAmbiguousTerminalListValues,
                        out selectedCatalogMatches,
                        out selectedCatalogReverseMatches);
                    w.WriteLine($"  live_catalog_source_context={catalogSourceContext}");
                    w.WriteLine($"  live_catalog_source_terminal_route_count={(catalogSourceTerminalRouteCount >= 0 ? catalogSourceTerminalRouteCount.ToString(CultureInfo.InvariantCulture) : "<unavailable>")}");
                    w.WriteLine($"  live_catalog_terminal_list_map={catalogTerminalListMap}");
                    w.WriteLine($"  live_catalog_terminal_list_coverage={(catalogActiveTerminalListCount >= 0 ? $"active={catalogActiveTerminalListCount.ToString(CultureInfo.InvariantCulture)} mapped={catalogMappedTerminalListCount.ToString(CultureInfo.InvariantCulture)} unmapped={catalogUnmappedTerminalListCount.ToString(CultureInfo.InvariantCulture)} ambiguous={catalogAmbiguousTerminalListCount.ToString(CultureInfo.InvariantCulture)}" : "<unavailable>")}");
                    w.WriteLine($"  live_catalog_terminal_list_mapped_values={catalogMappedTerminalListValues}");
                    w.WriteLine($"  live_catalog_terminal_list_unmapped_values={catalogUnmappedTerminalListValues}");
                    w.WriteLine($"  live_catalog_terminal_list_ambiguous_values={catalogAmbiguousTerminalListValues}");
                    if (selectedCatalogMatches != null)
                    {
                        w.WriteLine($"  live_selected_catalog_match_count={selectedCatalogMatches.Count.ToString(CultureInfo.InvariantCulture)}");
                        for (int i = 0; i < selectedCatalogMatches.Count; i++)
                            w.WriteLine($"  live_selected_catalog_match[{i.ToString(CultureInfo.InvariantCulture)}]={BuildCatalogTerminalRouteInferenceInline(selectedCatalogMatches[i])}");
                    }
                    else
                    {
                        w.WriteLine("  live_selected_catalog_match_count=<unavailable>");
                    }

                    if (selectedCatalogReverseMatches != null)
                    {
                        w.WriteLine($"  live_selected_catalog_reverse_match_count={selectedCatalogReverseMatches.Count.ToString(CultureInfo.InvariantCulture)}");
                        for (int i = 0; i < selectedCatalogReverseMatches.Count; i++)
                            w.WriteLine($"  live_selected_catalog_reverse_match[{i.ToString(CultureInfo.InvariantCulture)}]={BuildCatalogTerminalReverseInferenceInline(selectedCatalogReverseMatches[i], curF, curA, curS)}");
                    }
                    else
                    {
                        w.WriteLine("  live_selected_catalog_reverse_match_count=<unavailable>");
                    }
                }
                else
                {
                    w.WriteLine("  live_static_phase_hint=<unavailable>");
                    w.WriteLine("  live_static_seq_current=<unavailable>");
                    w.WriteLine("  live_static_seq_last=<unavailable>");
                    w.WriteLine("  live_selected_terminal_list=<unavailable>");
                    w.WriteLine("  live_catalog_source_context=<unavailable>");
                    w.WriteLine("  live_catalog_source_terminal_route_count=<unavailable>");
                    w.WriteLine("  live_catalog_terminal_list_map=<unavailable>");
                    w.WriteLine("  live_catalog_terminal_list_coverage=<unavailable>");
                    w.WriteLine("  live_catalog_terminal_list_mapped_values=<unavailable>");
                    w.WriteLine("  live_catalog_terminal_list_unmapped_values=<unavailable>");
                    w.WriteLine("  live_catalog_terminal_list_ambiguous_values=<unavailable>");
                    w.WriteLine("  live_selected_catalog_match_count=<unavailable>");
                    w.WriteLine("  live_selected_catalog_reverse_match_count=<unavailable>");
                }

                bool identityMatch =
                    ContainsObservedInt(observedTransportTerminalType, liveTerminalType) &&
                    ContainsObservedInt(observedTransportTerminalNo, liveTerminalNo) &&
                    ContainsObservedInt(observedTransportJumpNo, liveJumpNo) &&
                    ContainsObservedInt(observedTransportEventStat, liveEventStat);
                bool stateMatch =
                    ContainsObservedInt(observedTransportCallMode, liveCallMode) &&
                    ContainsObservedInt(observedTransportProcessStat, liveProcessStat) &&
                    ContainsObservedInt(observedTransportTerminalCnt, liveTerminalCnt);
                bool selectedJumpMatch = selectedListValue >= 0 && ContainsObservedInt(observedTransportJumpNo, selectedListValue);
                bool selectedCatalogRouteIdMatch = selectedCatalogMatches != null && selectedCatalogMatches.Any(m => string.Equals(m.RouteId, route.RouteId, StringComparison.Ordinal));
                bool selectedCatalogDstMatch = selectedCatalogMatches != null && selectedCatalogMatches.Any(m => m.DstF == route.DstF && m.DstA == route.DstA && m.DstS == route.DstS && string.Equals(m.DstPointRes ?? string.Empty, route.DstPointRes ?? string.Empty, StringComparison.Ordinal));
                bool selectedCatalogTerminalNoMatch = selectedCatalogMatches != null && selectedCatalogMatches.Any(m => ContainsObservedInt(GetOrderedObservedTransportTerminalNoVariants(m), route.TransportTerminalNo));
                bool reverseCatalogLiveTerminalNoMatch = selectedCatalogReverseMatches != null && selectedCatalogReverseMatches.Any(m => ContainsObservedInt(GetOrderedObservedTransportTerminalNoVariants(m), liveTerminalNo));

                w.WriteLine($"  compare_identity_match={identityMatch}");
                w.WriteLine($"  compare_state_match={stateMatch}");
                w.WriteLine($"  compare_call_mode_match={ContainsObservedInt(observedTransportCallMode, liveCallMode)}");
                w.WriteLine($"  compare_process_stat_match={ContainsObservedInt(observedTransportProcessStat, liveProcessStat)}");
                w.WriteLine($"  compare_terminal_cnt_match={ContainsObservedInt(observedTransportTerminalCnt, liveTerminalCnt)}");
                w.WriteLine($"  compare_selected_jump_match={selectedJumpMatch}");
                w.WriteLine($"  compare_selected_catalog_route_id_match={(selectedCatalogMatches != null ? (selectedCatalogRouteIdMatch ? "true" : "false") : "<unavailable>")}");
                w.WriteLine($"  compare_selected_catalog_dst_match={(selectedCatalogMatches != null ? (selectedCatalogDstMatch ? "true" : "false") : "<unavailable>")}");
                w.WriteLine($"  compare_selected_catalog_terminal_no_match={(selectedCatalogMatches != null ? (selectedCatalogTerminalNoMatch ? "true" : "false") : "<unavailable>")}");
                w.WriteLine($"  compare_live_source_terminal_no_match={(selectedCatalogReverseMatches != null ? (reverseCatalogLiveTerminalNoMatch ? "true" : "false") : "<unavailable>")}");
                w.WriteLine("  compare_trm_seq_match=<unavailable from current safe probe surface>");
            }

            private static bool TryBuildCurrentTerminalCatalogInference(TerminalStaticWorkSnapshot staticWork, int selectedListValue, out string sourceContext, out int sourceTerminalRouteCount, out string terminalListMap, out int activeTerminalListCount, out int mappedTerminalListCount, out int unmappedTerminalListCount, out int ambiguousTerminalListCount, out string mappedTerminalListValues, out string unmappedTerminalListValues, out string ambiguousTerminalListValues, out List<WarpCatalogRoute>? selectedMatches, out List<WarpCatalogRoute>? selectedCatalogReverseMatches)
            {
                sourceContext = "<unavailable>";
                sourceTerminalRouteCount = -1;
                terminalListMap = "<unavailable>";
                activeTerminalListCount = -1;
                mappedTerminalListCount = -1;
                unmappedTerminalListCount = -1;
                ambiguousTerminalListCount = -1;
                mappedTerminalListValues = "<unavailable>";
                unmappedTerminalListValues = "<unavailable>";
                ambiguousTerminalListValues = "<unavailable>";
                selectedMatches = null;
                selectedCatalogReverseMatches = null;

                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out _, out _);
                if (curF < 0 || curA < 0 || curS < 0)
                    return false;

                sourceContext = $"F{curF} A{curA} S{curS}";
                if (!TryLoadWarpCatalogStats(out _, out List<WarpCatalogRoute> contextRoutes, out _, out _))
                    return false;

                List<WarpCatalogRoute> allTerminalRoutes = contextRoutes
                    .Where(r => string.Equals(r.Kind, "terminal", StringComparison.Ordinal))
                    .ToList();

                List<WarpCatalogRoute> terminalRoutes = allTerminalRoutes
                    .Where(r => r.SrcF == curF
                             && r.SrcA == curA
                             && r.SrcS == curS)
                    .OrderBy(r => r.TransportJumpNo)
                    .ThenBy(r => r.DstF)
                    .ThenBy(r => r.DstA)
                    .ThenBy(r => r.DstS)
                    .ThenBy(r => r.DstPointRes, StringComparer.Ordinal)
                    .ToList();

                sourceTerminalRouteCount = terminalRoutes.Count;
                if (staticWork.TerminalListValues == null || staticWork.TerminalListValues.Length == 0 || staticWork.TerminalCnt <= 0)
                {
                    terminalListMap = "<none>";
                    activeTerminalListCount = 0;
                    mappedTerminalListCount = 0;
                    unmappedTerminalListCount = 0;
                    ambiguousTerminalListCount = 0;
                    mappedTerminalListValues = "<none>";
                    unmappedTerminalListValues = "<none>";
                    ambiguousTerminalListValues = "<none>";
                    selectedMatches = new List<WarpCatalogRoute>();
                    selectedCatalogReverseMatches = new List<WarpCatalogRoute>();
                    return true;
                }

                int count = Math.Min(staticWork.TerminalCnt, staticWork.TerminalListValues.Length);
                activeTerminalListCount = count;
                mappedTerminalListCount = 0;
                unmappedTerminalListCount = 0;
                ambiguousTerminalListCount = 0;
                var parts = new List<string>(count);
                var mappedValues = new List<int>(count);
                var unmappedValues = new List<int>(count);
                var ambiguousValues = new List<int>(count);
                for (int i = 0; i < count; i++)
                {
                    int value = staticWork.TerminalListValues[i];
                    List<WarpCatalogRoute> matches = terminalRoutes.Where(r => ContainsObservedInt(GetOrderedObservedTransportJumpNoVariants(r), value)).ToList();
                    if (value == selectedListValue)
                        selectedMatches = matches;

                    if (matches.Count == 0)
                    {
                        unmappedTerminalListCount++;
                        unmappedValues.Add(value);
                        parts.Add($"{i}:{value}-><unmapped>");
                    }
                    else if (matches.Count == 1)
                    {
                        mappedTerminalListCount++;
                        mappedValues.Add(value);
                        parts.Add($"{i}:{value}->{BuildCatalogTerminalRouteInferenceInline(matches[0])}");
                    }
                    else
                    {
                        ambiguousTerminalListCount++;
                        ambiguousValues.Add(value);
                        parts.Add($"{i}:{value}-><ambiguous x{matches.Count.ToString(CultureInfo.InvariantCulture)}>");
                    }
                }

                terminalListMap = parts.Count == 0 ? "<none>" : string.Join(" | ", parts);
                mappedTerminalListValues = FormatObservedIntArray(mappedValues.ToArray());
                unmappedTerminalListValues = FormatObservedIntArray(unmappedValues.ToArray());
                ambiguousTerminalListValues = FormatObservedIntArray(ambiguousValues.ToArray());
                if (selectedMatches == null)
                    selectedMatches = new List<WarpCatalogRoute>();

                if (selectedMatches.Count > 0)
                {
                    List<WarpCatalogRoute> selectedMatchesLocal = selectedMatches;
                    selectedCatalogReverseMatches = allTerminalRoutes
                        .Where(r => selectedMatchesLocal.Any(m =>
                            r.SrcF == m.DstF &&
                            r.SrcA == m.DstA &&
                            r.SrcS == m.DstS &&
                            r.DstF == curF &&
                            r.DstA == curA &&
                            r.DstS == curS &&
                            GetOrderedObservedTransportJumpNoVariants(r).Intersect(GetOrderedObservedTransportJumpNoVariants(m)).Any()))
                        .OrderBy(r => r.SrcF)
                        .ThenBy(r => r.SrcA)
                        .ThenBy(r => r.SrcS)
                        .ThenBy(r => r.DstF)
                        .ThenBy(r => r.DstA)
                        .ThenBy(r => r.DstS)
                        .ThenBy(r => r.RouteId, StringComparer.Ordinal)
                        .ToList();
                }
                else
                {
                    selectedCatalogReverseMatches = new List<WarpCatalogRoute>();
                }

                return true;
            }

            private static string BuildCatalogTerminalRouteInferenceInline(WarpCatalogRoute route)
            {
                int[] observedJump = GetOrderedObservedTransportJumpNoVariants(route);
                int[] observedTerminalNo = GetOrderedObservedTransportTerminalNoVariants(route);
                var sb = new StringBuilder(160);
                if (!string.IsNullOrEmpty(route.RouteId))
                    sb.Append('[').Append(route.RouteId).Append("] ");
                sb.Append("F").Append(route.DstF.ToString(CultureInfo.InvariantCulture));
                sb.Append(" A").Append(route.DstA.ToString(CultureInfo.InvariantCulture));
                sb.Append(" S").Append(route.DstS.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(route.DstPointRes))
                    sb.Append(" point=\"").Append(San(route.DstPointRes)).Append("\"");
                sb.Append(" jump=").Append(FormatObservedIntArray(observedJump));
                sb.Append(" dstTermNo=").Append(FormatObservedIntArray(observedTerminalNo));
                return sb.ToString();
            }

            private static string BuildCatalogTerminalReverseInferenceInline(WarpCatalogRoute route, int currentF, int currentA, int currentS)
            {
                int[] observedJump = GetOrderedObservedTransportJumpNoVariants(route);
                int[] observedTerminalNo = GetOrderedObservedTransportTerminalNoVariants(route);
                var sb = new StringBuilder(192);
                if (!string.IsNullOrEmpty(route.RouteId))
                    sb.Append('[').Append(route.RouteId).Append("] ");
                sb.Append("from F").Append(route.SrcF.ToString(CultureInfo.InvariantCulture));
                sb.Append(" A").Append(route.SrcA.ToString(CultureInfo.InvariantCulture));
                sb.Append(" S").Append(route.SrcS.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(route.SrcPointRes))
                    sb.Append(" point=\"").Append(San(route.SrcPointRes)).Append("\"");
                sb.Append(" -> current F").Append(currentF.ToString(CultureInfo.InvariantCulture));
                sb.Append(" A").Append(currentA.ToString(CultureInfo.InvariantCulture));
                sb.Append(" S").Append(currentS.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(route.DstPointRes))
                    sb.Append(" point=\"").Append(San(route.DstPointRes)).Append("\"");
                sb.Append(" jump=").Append(FormatObservedIntArray(observedJump));
                sb.Append(" srcTermNo=").Append(FormatObservedIntArray(observedTerminalNo));
                return sb.ToString();
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
