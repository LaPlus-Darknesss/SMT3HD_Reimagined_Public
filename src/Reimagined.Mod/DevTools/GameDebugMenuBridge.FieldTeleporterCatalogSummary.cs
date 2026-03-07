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

            public static bool TryGetWarpCatalogHudLines(int loadField, int loadArea, int loadScript, out string[] lines)
            {
                var outLines = new List<string>(capacity: 6);
                lines = Array.Empty<string>();

                try
                {
                    if (!TryLoadWarpCatalogStats(out var exactRoutes, out var contextRoutes, out int rawCount, out string path))
                        return false;

                    int outgoing = 0;
                    int incoming = 0;
                    for (int i = 0; i < contextRoutes.Count; i++)
                    {
                        var r = contextRoutes[i];
                        if (r.SrcF == loadField && r.SrcA == loadArea && r.SrcS == loadScript)
                            outgoing++;
                        if (r.DstF == loadField && r.DstA == loadArea && r.DstS == loadScript)
                            incoming++;
                    }

                    outLines.Add($"warpCatalog: file=\"{San(path)}\" rows={rawCount} exact={exactRoutes.Count} context={contextRoutes.Count} outgoingHere={outgoing} incomingHere={incoming}");

                    int shown = 0;
                    for (int i = 0; i < contextRoutes.Count && shown < 3; i++)
                    {
                        var r = contextRoutes[i];
                        if (r.SrcF != loadField || r.SrcA != loadArea || r.SrcS != loadScript)
                            continue;

                        shown++;
                        outLines.Add($"warpCatalogOut[{shown}]: {FormatWarpCatalogRouteInline(r)}");
                    }

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
                for (int i = 0; i < contextRoutes.Count; i++)
                {
                    var r = contextRoutes[i];
                    if (r.SrcF == curF && r.SrcA == curA && r.SrcS == curS)
                        outgoingHere.Add(r);
                }

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
                var sb = new StringBuilder(192);
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

                if (r.SourcePointVariants.Count == 1)
                {
                    foreach (string point in r.SourcePointVariants)
                    {
                        if (!string.IsNullOrEmpty(point))
                            sb.Append(" srcPoint=\"").Append(San(point)).Append("\"");
                        break;
                    }
                }
                else if (r.SourcePointVariants.Count > 1)
                {
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

                sb.Append(" seen=").Append(r.Count.ToString(CultureInfo.InvariantCulture));
                return sb.ToString();
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
