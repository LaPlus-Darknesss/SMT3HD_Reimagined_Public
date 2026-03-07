#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            private const string WarpFavoritesFileName = "warp_favorites.txt";

            private struct WarpFavorite
            {
                // Door table index *within the current loaded field/area*.
                public int DoorIndex;

                // Optional capture context (helps avoid confusing 'same index, different area' collisions).
                // -1 means unknown / legacy entry.
                public int LoadField;
                public int LoadArea;
                public int LoadScript;

                // Optional door event name (often union02 from the door table).
                public string Evename;

                // User label (free-form).
                public string Label;

                public bool HasContext => LoadField >= 0 && LoadArea >= 0 && LoadScript >= 0;
            }

            private static readonly object s_warpFavLock = new object();
            private static List<WarpFavorite>? s_warpFavs;
            private static int s_warpFavSel = 0;
            private static DateTime s_warpFavLastLoadUtc;
            private static string s_warpFavLastLoadSummary = "";
            private static string s_warpLastActionSummary = "";

            // UX: when enabled, warp-favorites prev/next will skip entries captured from a different field/area/script.
            // This keeps the list "sane" in long sessions where the same doorIndex means different things per area.
            private static bool s_warpFavFilterByContext = true;


            // Safety: require a second press within a short window before warping.
            private static int s_warpArmDoorIndex = -1;
            private static float s_warpArmUntilRealtime = 0.0f;

            // Cache fldEveHit type so we can capture the currently-colliding door index (OldHitDoor).
            private static Type? s_warpFav_fldEveHitType = null;


            public static bool TryGetWarpFavoriteSummary(out string summary)
            {
                lock (s_warpFavLock)
                {
                    if (s_warpFavs == null || s_warpFavs.Count <= 0)
                    {
                        summary = "none (edit dumps/warp_favorites.txt, then Ctrl+Alt+Shift+F7 to reload)";
                        return true;
                    }

                    int sel = Math.Clamp(s_warpFavSel, 0, s_warpFavs.Count - 1);
                    var fav = s_warpFavs[sel];

                    summary = $"sel={sel + 1}/{s_warpFavs.Count} doorIndex={fav.DoorIndex} label=\"{San(fav.Label)}\"";
                    if (!string.IsNullOrEmpty(s_warpLastActionSummary))
                        summary += $" | last: {San(s_warpLastActionSummary)}";

                    return true;
                }
            }

            public static bool TryGetWarpFavoritesSourceSummary(out string summary)
            {
                lock (s_warpFavLock)
                {
                    string path = Path.Combine(DumpsDir, WarpFavoritesFileName);
                    summary = $"file=\"{path}\" lastLoadUtc={s_warpFavLastLoadUtc:O} | {s_warpFavLastLoadSummary}";
                    return true;
                }
            }

            public static bool TryGetWarpArmSummary(out string summary)
            {
                float now = Time.realtimeSinceStartup;
                if (s_warpArmDoorIndex >= 0 && now <= s_warpArmUntilRealtime)
                    summary = $"ARMED doorIndex={s_warpArmDoorIndex} expiresIn={(s_warpArmUntilRealtime - now):0.0}s";
                else
                    summary = "not armed";
                return true;
            }


            public static void ToggleWarpFavoritesFilterByContext()
{
    s_warpFavFilterByContext = !s_warpFavFilterByContext;

    if (!s_warpFavFilterByContext)
    {
        s_warpLastActionSummary = "filterByContext=OFF";
        MelonLogger.Msg("[WarpFavorites] filterByContext=OFF");
        return;
    }

    // When turning ON, report how many favorites match the current context
    // so the user understands why cycling might appear "stuck".
    int curF = -1, curA = -1, curS = -1;
    string _pr = "", _cn = "";
    TryGetCurrentFieldContext(out curF, out curA, out curS, out _pr, out _cn);

    int matchCount = 0;
    lock (s_warpFavLock)
    {
        if (s_warpFavs != null)
        {
            for (int i = 0; i < s_warpFavs.Count; i++)
            {
                var f = s_warpFavs[i];
                if (!f.HasContext) continue;
                if (f.LoadField == curF && f.LoadArea == curA && f.LoadScript == curS)
                    matchCount++;
            }
        }
    }

    s_warpLastActionSummary = $"filterByContext=ON (matches={matchCount} for F={curF} A={curA} S={curS})";
    MelonLogger.Msg($"[WarpFavorites] filterByContext=ON (matches={matchCount} for F={curF} A={curA} S={curS})");
}

            public static void ReloadWarpFavorites()
            {
                string path = Path.Combine(DumpsDir, WarpFavoritesFileName);

                List<WarpFavorite> favs = new List<WarpFavorite>();
                int badLines = 0;

                try
                {
                    if (!File.Exists(path))
                    {
                        // Create a stub to guide users.
                        Directory.CreateDirectory(DumpsDir);
                        File.WriteAllText(path,
@"# warp_favorites.txt
# One favorite per line.
#
# Accepted formats:
#   <doorIndex>
#   <doorIndex> <label...>
#   <doorIndex> | <label...>
#   F=<loadField> A=<loadArea> S=<loadScript> D=<doorIndex> E=<evename> | <label...>
#
# - <doorIndex> accepts decimal or hex (0x123).
# - Labels may be quoted to preserve spacing.
#
# Examples:
#   12
#   12 Shinjuku Medical Center Entrance
#   12 | ""Shinjuku Medical Center Entrance""
#   0x2A | Yoyogi Park Gate
#
# Notes:
# - Use Ctrl+Alt+Shift+F9 first to dump the warp table and pick door indices safely.
# - Reload with Ctrl+Alt+Shift+F7.
# - Add current hit door to favorites with Ctrl+Alt+Shift+F6 (stand at door boundary).
",
                            System.Text.Encoding.UTF8);

                        lock (s_warpFavLock)
                        {
                            s_warpFavs = favs;
                            s_warpFavSel = 0;
                            s_warpFavLastLoadUtc = DateTime.UtcNow;
                            s_warpFavLastLoadSummary = "created stub file (empty favorites)";
                        }

                        MelonLogger.Warning($"[Reimagined] Created stub warp favorites file at: {path}");
                        return;
                    }

                    string[] lines = File.ReadAllLines(path);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string raw = lines[i] ?? "";
                        string line = raw.Trim();

                        if (line.Length == 0)
                            continue;
                        if (line.StartsWith("#", StringComparison.Ordinal))
                            continue;

                        // Strip inline comment.
                        int hash = line.IndexOf('#');
                        if (hash >= 0)
                            line = line.Substring(0, hash).Trim();

                        if (line.Length == 0)
                            continue;

                        if (!TryParseWarpFavoriteLine(line, out int idx, out int lf, out int la, out int ls, out string evename, out string label))
                        {
                            badLines++;
                            continue;
                        }

                        favs.Add(new WarpFavorite { DoorIndex = idx, LoadField = lf, LoadArea = la, LoadScript = ls, Evename = evename ?? "", Label = label ?? "" });
                    }
                }
                catch (Exception ex)
                {
                    badLines++;
                    MelonLogger.Warning($"[Reimagined] ReloadWarpFavorites failed: {ex.Message}");
                }

                lock (s_warpFavLock)
                {
                    s_warpFavs = favs;
                    s_warpFavSel = Math.Clamp(s_warpFavSel, 0, Math.Max(0, favs.Count - 1));
                    s_warpFavLastLoadUtc = DateTime.UtcNow;
                    s_warpFavLastLoadSummary = $"entries={favs.Count} badLines={badLines}";
                }

                MelonLogger.Msg($"[Reimagined] Warp favorites loaded: entries={favs.Count} badLines={badLines} (file: {path})");
            }

            private static bool TryParseWarpFavoriteLine(string line, out int doorIndex, out int loadField, out int loadArea, out int loadScript, out string evename, out string label)
            {
                doorIndex = -1;
                loadField = -1;
                loadArea = -1;
                loadScript = -1;
                evename = "";
                label = "";

                line = (line ?? "").Trim();
                if (line.Length == 0)
                    return false;

                // Strip inline comment.
                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash).Trim();
                if (line.Length == 0)
                    return false;

                // Prefer explicit separators first.
                string idxPart = line;
                string labelPart = "";

                int pipe = line.IndexOf('|');
                if (pipe >= 0)
                {
                    idxPart = line.Substring(0, pipe).Trim();
                    labelPart = line.Substring(pipe + 1).Trim();
                }
                else
                {
                    // Allow CSV-ish: "12, Label".
                    int comma = line.IndexOf(',');
                    if (comma >= 0)
                    {
                        idxPart = line.Substring(0, comma).Trim();
                        labelPart = line.Substring(comma + 1).Trim();
                    }
                    else
                    {
                        // Fallback: first token is doorIndex, remainder is label.
                        int sp = IndexOfWhitespace(line);
                        if (sp >= 0)
                        {
                            idxPart = line.Substring(0, sp).Trim();
                            labelPart = line.Substring(sp + 1).Trim();
                        }
                    }
                }

                // New (v2) format support:
                //   F=<loadField> A=<loadArea> S=<loadScript> D=<doorIndex> E=<evename> | <label>
                // Keys are case-insensitive; unknown keys are ignored.
                // If any key=value token is present, we parse those first.
                bool sawKv = idxPart.IndexOf('=') >= 0;
                if (sawKv)
                {
                    string[] toks = idxPart.Split(new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < toks.Length; i++)
                    {
                        string tok = toks[i];
                        int eq = tok.IndexOf('=');
                        if (eq <= 0)
                            continue;

                        string k = tok.Substring(0, eq).Trim();
                        string v = Unquote(tok.Substring(eq + 1).Trim());
                        if (k.Length == 0)
                            continue;

                        k = k.Trim().ToUpperInvariant();
                        if (k == "D" || k == "DOOR" || k == "DOORINDEX")
                        {
                            if (!TryParseDoorIndex(v, out doorIndex))
                                return false;
                        }
                        else if (k == "F" || k == "FIELD" || k == "LOADFIELD")
                        {
                            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out loadField);
                        }
                        else if (k == "A" || k == "AREA" || k == "LOADAREA")
                        {
                            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out loadArea);
                        }
                        else if (k == "S" || k == "SCRIPT" || k == "LOADSCRIPT")
                        {
                            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out loadScript);
                        }
                        else if (k == "E" || k == "EVENAME" || k == "UNION02")
                        {
                            evename = v ?? "";
                        }
                    }

                    // doorIndex is required.
                    if (doorIndex < 0 && idxPart.IndexOf("D=", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        if (!TryParseDoorIndex(idxPart, out doorIndex))
                            return false;
                    }

                    label = Unquote(labelPart);
                    return true;
                }

                // Legacy format.
                if (!TryParseDoorIndex(idxPart, out doorIndex))
                    return false;

                label = Unquote(labelPart);
                return true;
            }

            private static int IndexOfWhitespace(string s)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    if (char.IsWhiteSpace(s[i]))
                        return i;
                }
                return -1;
            }

            private static string Unquote(string s)
            {
                s = (s ?? "").Trim();
                if (s.Length < 2)
                    return s;

                char q = s[0];
                if ((q == '"' || q == '\'') && s[s.Length - 1] == q)
                    return s.Substring(1, s.Length - 2);

                return s;
            }

	            // Best-effort: fetch the currently loaded field context from fldSceneParam.
	            // Door indices are only meaningful within a given field/area/script, so we capture these
	            // alongside favorites whenever possible.
	            private static void TryGetCurrentFieldContext(out int loadField, out int loadArea, out int loadScript, out string pointResName, out string camName)
	            {
	                loadField = -1;
	                loadArea = -1;
	                loadScript = -1;
	                pointResName = "";
	                camName = "";

	                try
	                {
	                    var sp = TryGetFldSceneParam(out _);
	                    if (sp == null)
	                        return;

	                    loadField = GetInt(sp, "loadField", -1);
	                    loadArea = GetInt(sp, "loadArea", -1);
	                    loadScript = GetInt(sp, "loadScript", -1);
	                    pointResName = GetMaybeFixedCharString(sp, "pointResName", 96);
	                    camName = GetMaybeFixedCharString(sp, "camName", 96);
	                }
	                catch
	                {
	                    // best-effort; keep defaults
	                }
	            }

	            // Append a line to a file, but guard against files that do not end with a newline.
	            // This prevents accidental concatenation/corruption when users repeatedly append favorites.
	            private static void AppendLineEnsureNewline(string path, string line)
	            {
	                try
	                {
	                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

	                    bool needsLeadingNewline = false;
	                    if (File.Exists(path))
	                    {
	                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
	                        {
	                            if (fs.Length > 0)
	                            {
	                                fs.Seek(-1, SeekOrigin.End);
	                                int b = fs.ReadByte();
	                                // If the file does not end with '\n', treat it as missing a newline.
	                                // This also covers files ending with '\r' only.
	                                if (b != (int)'\n')
	                                    needsLeadingNewline = true;
	                            }
	                        }
	                    }
						using (var fsw = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
						using (var sw = new StreamWriter(fsw, System.Text.Encoding.UTF8))
						{
							if (needsLeadingNewline)
								sw.Write(Environment.NewLine);
							sw.WriteLine(line ?? "");
						}
					}
					catch (Exception ex)
					{
						MelonLogger.Warning($"[WarpFavorites] AppendLineEnsureNewline failed: {ex.GetType().Name}: {ex.Message}");
						// Last resort: attempt a normal append.
						try
						{
							File.AppendAllText(path, (line ?? "") + Environment.NewLine, System.Text.Encoding.UTF8);
						}
						catch
						{
							// ignore
						}
					}
				}



// === Warp Favorites file helpers (v2) ===========================================
private static string SanFavValue(string? s)
{
    if (string.IsNullOrEmpty(s))
        return "";

    // Keep it single-line and delimiter-safe.
    s = San(s);
    s = s.Replace('|', '/');
    return s.Trim();
}

private static string FormatWarpFavoriteLine(int loadField, int loadArea, int loadScript, int doorIndex, string evename, string label)
{
    evename = SanFavValue(evename);
    label = SanFavValue(label);

    bool hasCtx = (loadField >= 0 && loadArea >= 0 && loadScript >= 0);

    var sb = new System.Text.StringBuilder(96);
    if (hasCtx)
    {
        sb.Append("F=").Append(loadField).Append(' ');
        sb.Append("A=").Append(loadArea).Append(' ');
        sb.Append("S=").Append(loadScript).Append(' ');
    }

    sb.Append("D=").Append(doorIndex);

    if (!string.IsNullOrEmpty(evename))
        sb.Append(' ').Append("E=").Append(evename);

    sb.Append(" | ").Append(label);
    return sb.ToString();
}

private static bool IsUserLabel(string label)
{
    if (string.IsNullOrEmpty(label))
        return false;

    // Our auto labels start with "hitDoor".
    return !label.TrimStart().StartsWith("hitDoor", StringComparison.OrdinalIgnoreCase);
}

private static bool TryUpsertWarpFavoriteInFile(
    string path,
    int hitDoor,
    int curLoadField,
    int curLoadArea,
    int curLoadScript,
    string evenameCap,
    string autoLabel,
    out bool updatedExisting,
    out string actionSummary)
{
    updatedExisting = false;
    actionSummary = "";

    bool wantCtx = (curLoadField >= 0 && curLoadArea >= 0 && curLoadScript >= 0);

    List<string> lines = new List<string>();
    if (File.Exists(path))
    {
        lines.AddRange(File.ReadAllLines(path));
    }
    else
    {
        lines.Add("# warp_favorites.txt");
        lines.Add("# Format:");
        lines.Add("#   Legacy: <doorIndex> \"Label\"");
        lines.Add("#   v2: F=<field> A=<area> S=<script> D=<doorIndex> E=<evename> | <Label>");
        lines.Add("");
    }

    int ctxMatchLine = -1;
    int legacyMatchLine = -1;
    string existingLabel = "";
    string existingEvename = "";

    for (int i = 0; i < lines.Count; i++)
    {
        string raw = (lines[i] ?? "");
        string t = raw.Trim();
        if (t.Length == 0 || t.StartsWith("#"))
            continue;

        if (!TryParseWarpFavoriteLine(t, out int di, out int lf, out int la, out int ls, out string ev, out string lbl))
            continue;

        if (di != hitDoor)
            continue;

        bool hasCtx = (lf >= 0 && la >= 0 && ls >= 0);

        if (wantCtx && hasCtx && lf == curLoadField && la == curLoadArea && ls == curLoadScript)
        {
            ctxMatchLine = i;
            existingLabel = lbl ?? "";
            existingEvename = ev ?? "";
            break; // best match
        }

        if (!hasCtx && legacyMatchLine < 0)
        {
            legacyMatchLine = i;
            existingLabel = lbl ?? "";
            existingEvename = ev ?? "";
        }
    }

    string mergedLabel = autoLabel;
    if (IsUserLabel(existingLabel))
        mergedLabel = existingLabel;

    string mergedEvename = evenameCap;
    if (string.IsNullOrEmpty(mergedEvename))
        mergedEvename = existingEvename;

    string newLine = FormatWarpFavoriteLine(curLoadField, curLoadArea, curLoadScript, hitDoor, mergedEvename, mergedLabel);

    if (ctxMatchLine >= 0)
    {
        // Update existing context-specific entry.
        lines[ctxMatchLine] = newLine;
        updatedExisting = true;
        actionSummary = $"capture OK (updated): doorIndex={hitDoor} ctx=F={curLoadField} A={curLoadArea} S={curLoadScript} label=\"{San(mergedLabel)}\"";
    }
    else if (!wantCtx && legacyMatchLine >= 0)
    {
        // No context known: update the first legacy match in-place.
        lines[legacyMatchLine] = FormatWarpFavoriteLine(-1, -1, -1, hitDoor, mergedEvename, mergedLabel);
        updatedExisting = true;
        actionSummary = $"capture OK (updated legacy): doorIndex={hitDoor} label=\"{San(mergedLabel)}\"";
    }
    else
    {
        // Append new line (even if a legacy entry already exists; context disambiguates).
        lines.Add(newLine);
        updatedExisting = false;

        if (wantCtx)
            actionSummary = $"capture OK (appended): doorIndex={hitDoor} ctx=F={curLoadField} A={curLoadArea} S={curLoadScript} label=\"{San(mergedLabel)}\"";
        else
            actionSummary = $"capture OK (appended legacy): doorIndex={hitDoor} label=\"{San(mergedLabel)}\"";
    }

    // Write back (rewrite file so it's always newline-terminated).
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    return true;
}

public static void NormalizeWarpFavoritesFile()
{
    try
    {
        string path = Path.Combine(DumpsDir, WarpFavoritesFileName);
        if (!File.Exists(path))
        {
            MelonLogger.Msg("[WarpFavorites] normalize: no file found");
            return;
        }

        string[] rawLines = File.ReadAllLines(path);

        var outLines = new List<string>(rawLines.Length + 8);

        // Preserve initial comment block / header.
        int i = 0;
        for (; i < rawLines.Length; i++)
        {
            string t = (rawLines[i] ?? "").Trim();
            if (t.Length == 0 || t.StartsWith("#"))
            {
                outLines.Add(rawLines[i] ?? "");
                continue;
            }
            break;
        }

        int ok = 0, bad = 0, split = 0;
        for (; i < rawLines.Length; i++)
        {
            string raw = rawLines[i] ?? "";
            string t = raw.Trim();
            if (t.Length == 0 || t.StartsWith("#"))
            {
                outLines.Add(raw);
                continue;
            }

            if (TryParseWarpFavoriteLine(t, out int di, out int lf, out int la, out int ls, out string ev, out string lbl))
            {
                outLines.Add(FormatWarpFavoriteLine(lf, la, ls, di, ev, lbl));
                ok++;
                continue;
            }

            // Try to salvage a common corruption case: missing newline between two entries.
            bool salvaged = false;
            if (TrySplitConcatenatedLine(t, out string left, out string right))
            {
                bool leftOk = TryParseWarpFavoriteLine(left.Trim(), out int diL, out int lfL, out int laL, out int lsL, out string evL, out string lblL);
                bool rightOk = TryParseWarpFavoriteLine(right.Trim(), out int diR, out int lfR, out int laR, out int lsR, out string evR, out string lblR);
                if (leftOk && rightOk)
                {
                    outLines.Add(FormatWarpFavoriteLine(lfL, laL, lsL, diL, evL, lblL));
                    outLines.Add(FormatWarpFavoriteLine(lfR, laR, lsR, diR, evR, lblR));
                    ok += 2;
                    split++;
                    salvaged = true;
                }
            }

            if (!salvaged)
            {
                outLines.Add("# BAD: " + raw);
                bad++;
            }
        }

        File.WriteAllLines(path, outLines, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        ReloadWarpFavorites();
        s_warpLastActionSummary = $"normalize OK: okLines={ok} splitLines={split} badLines={bad}";
        MelonLogger.Msg($"[WarpFavorites] normalize OK: okLines={ok} splitLines={split} badLines={bad}");
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[WarpFavorites] normalize exception: {ex.GetType().Name}: {ex.Message}");
    }
}

private static bool TrySplitConcatenatedLine(string line, out string left, out string right)
{
    left = "";
    right = "";
    if (string.IsNullOrEmpty(line))
        return false;

    // Heuristic: missing newline produces ...\"<digits>... where the next entry starts immediately.
    // Example: 54 "Label"4 | hitDoor ...
    for (int i = 1; i < line.Length - 1; i++)
    {
        if (line[i - 1] != '"')
            continue;

        if (!char.IsDigit(line[i]))
            continue;

        left = line.Substring(0, i).TrimEnd();
        right = line.Substring(i).TrimStart();
        if (left.Length > 0 && right.Length > 0)
            return true;
    }

    return false;
}

// Small helpers local to WarpFavoritesConfig. We keep these here (instead of elsewhere)
// because this file is the only current consumer.
private static Type? TryResolveType(string typeName)
{
    if (string.IsNullOrWhiteSpace(typeName))
        return null;

    // If caller already passed a qualified name, try it verbatim first.
    var t = FindTypeInLoadedAssemblies(typeName);
    if (t != null)
        return t;

    // Otherwise, try common namespaces used by Il2CppInterop-generated wrappers.
    if (typeName.IndexOf('.') < 0)
    {
        t = FindTypeInLoadedAssemblies("Il2Cpp." + typeName);
        if (t != null)
            return t;

        t = FindTypeInLoadedAssemblies("field." + typeName);
        if (t != null)
            return t;
    }

    return null;
}

private static int ReadStaticIntMemberSafe(Type t, string memberName, int fallback)
{
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    try
    {
        // Prefer property (many IL2CPP statics are exposed as properties).
        var p = t.GetProperty(memberName, flags);
        if (p != null && p.CanRead)
        {
            object? v = p.GetValue(null, null);
            if (v != null)
                return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }

        var f = t.GetField(memberName, flags);
        if (f != null)
        {
            object? v = f.GetValue(null);
            if (v != null)
                return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }
    }
    catch
    {
        // ignore
    }

    return fallback;
}
// === End Warp Favorites file helpers ================================================

public static void AppendCurrentHitDoorToWarpFavorites()
{
    try
    {
        // Resolve fldEveHit once.
        if (s_warpFav_fldEveHitType == null)
            s_warpFav_fldEveHitType = TryResolveType("fldEveHit");

        if (s_warpFav_fldEveHitType == null)
        {
            s_warpLastActionSummary = "capture failed: couldn't resolve fldEveHit";
            MelonLogger.Warning("[WarpFavorites] capture failed: couldn't resolve fldEveHit");
            return;
        }

        int hitDoor = ReadStaticIntMemberSafe(s_warpFav_fldEveHitType, "OldHitDoor", -1);
        if (hitDoor < 0)
        {
            s_warpLastActionSummary = "capture failed: OldHitDoor < 0 (stand near a door trigger)";
            MelonLogger.Msg("[WarpFavorites] capture failed: OldHitDoor < 0 (stand near a door trigger)");
            return;
        }

        int curLoadField = -1;
        int curLoadArea = -1;
        int curLoadScript = -1;
        string curProg = "";
        string curCell = "";
        TryGetCurrentFieldContext(out curLoadField, out curLoadArea, out curLoadScript, out curProg, out curCell);

        // Best-effort label + evename capture from the current door table.
        string evenameCap = "";
        string autoLabel = $"hitDoor {hitDoor}";
        int doorCount = -1;
        if (TryGetDoorBuff(out object? doorBuff, out doorCount) && doorBuff != null && hitDoor >= 0 && hitDoor < doorCount)
        {
            if (TryGetArrayElement(doorBuff, hitDoor, out object? doorObj) && doorObj != null)
            {
                evenameCap = GetMaybeFixedCharString(doorObj, "union02", 128);
                int wt = GetInt(doorObj, "warptype", 0);
                int exNo = GetDoorIntMemberSafe(doorObj, "union05", defaultValue: -1);

                if (!string.IsNullOrEmpty(evenameCap))
                    autoLabel = $"hitDoor evename={evenameCap} wt={wt} ex={exNo}";
                else
                    autoLabel = $"hitDoor wt={wt} ex={exNo}";
            }
        }

        string path = Path.Combine(DumpsDir, WarpFavoritesFileName);
        Directory.CreateDirectory(DumpsDir);

        if (!TryUpsertWarpFavoriteInFile(path, hitDoor, curLoadField, curLoadArea, curLoadScript, evenameCap, autoLabel, out bool updated, out string action))
        {
            s_warpLastActionSummary = "capture failed: couldn't write favorites file";
            MelonLogger.Warning("[WarpFavorites] capture failed: couldn't write favorites file");
            return;
        }

        ReloadWarpFavorites();

        // Auto-select the captured/upserted favorite.
        lock (s_warpFavLock)
        {
            if (s_warpFavs != null && s_warpFavs.Count > 0)
            {
                int best = -1;
                int fallback = -1;
                for (int i = 0; i < s_warpFavs.Count; i++)
                {
                    var f = s_warpFavs[i];
                    if (f.DoorIndex != hitDoor)
                        continue;

                    if (fallback < 0)
                        fallback = i;

                    if (f.HasContext && f.LoadField == curLoadField && f.LoadArea == curLoadArea && f.LoadScript == curLoadScript)
                    {
                        best = i;
                        break;
                    }
                }

                if (best >= 0)
                    s_warpFavSel = best;
                else if (fallback >= 0)
                    s_warpFavSel = fallback;
            }
        }

        s_warpLastActionSummary = action;
        MelonLogger.Msg("[WarpFavorites] " + action);
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[WarpFavorites] capture exception: {ex.GetType().Name}: {ex.Message}");
    }
}

            public static void WarpFavoritePrev()
{
    lock (s_warpFavLock)
    {
        if (s_warpFavs == null || s_warpFavs.Count <= 0)
            return;

        int start = Math.Clamp(s_warpFavSel, 0, s_warpFavs.Count - 1);

        // Optional context filter: keep selection within the current field/area/script.
        int curF = -1, curA = -1, curS = -1;
        string _pr = "", _cn = "";
        TryGetCurrentFieldContext(out curF, out curA, out curS, out _pr, out _cn);

        if (!s_warpFavFilterByContext)
        {
            s_warpFavSel = start - 1;
            if (s_warpFavSel < 0)
                s_warpFavSel = s_warpFavs.Count - 1;

            s_warpLastActionSummary = $"select prev -> {DescribeSelectedWarpFavorite_NoLock()}";
            MelonLogger.Msg($"[WarpFavorites] select prev -> {DescribeSelectedWarpFavorite_NoLock()}");
            return;
        }

        int found = -1;
        int idx = start;
        for (int step = 0; step < s_warpFavs.Count; step++)
        {
            idx--;
            if (idx < 0)
                idx = s_warpFavs.Count - 1;

            var cand = s_warpFavs[idx];
            if (!cand.HasContext)
                continue;

            if (cand.LoadField == curF && cand.LoadArea == curA && cand.LoadScript == curS)
            {
                found = idx;
                break;
            }
        }

        int matchCount = 0;
for (int i = 0; i < s_warpFavs.Count; i++)
{
    var f = s_warpFavs[i];
    if (!f.HasContext) continue;
    if (f.LoadField == curF && f.LoadArea == curA && f.LoadScript == curS)
        matchCount++;
}

if (found >= 0)
{
    int prev = s_warpFavSel;
    s_warpFavSel = found;

    string desc = DescribeSelectedWarpFavorite_NoLock();
    if (matchCount <= 1 || prev == found)
    {
        s_warpLastActionSummary = $"select prev (filtered): {desc} (only {matchCount} match)";
        MelonLogger.Msg($"[WarpFavorites] select prev (filtered): staying on {desc} (matches={matchCount})");
    }
    else
    {
        s_warpLastActionSummary = $"select prev (filtered) -> {desc}";
        MelonLogger.Msg($"[WarpFavorites] select prev (filtered) -> {desc} (matches={matchCount})");
    }
}
else
{
    s_warpLastActionSummary = "select prev (filtered): no favorites match current field context (capture with Ctrl+Alt+Shift+F6 or toggle filter off)";
    MelonLogger.Msg("[WarpFavorites] select prev (filtered): no matches (capture with Ctrl+Alt+Shift+F6 or toggle filter off)");
}
    }
}

            public static void WarpFavoriteNext()
{
    lock (s_warpFavLock)
    {
        if (s_warpFavs == null || s_warpFavs.Count <= 0)
            return;

        int start = Math.Clamp(s_warpFavSel, 0, s_warpFavs.Count - 1);

        // Optional context filter: keep selection within the current field/area/script.
        int curF = -1, curA = -1, curS = -1;
        string _pr = "", _cn = "";
        TryGetCurrentFieldContext(out curF, out curA, out curS, out _pr, out _cn);

        if (!s_warpFavFilterByContext)
        {
            s_warpFavSel = start + 1;
            if (s_warpFavSel >= s_warpFavs.Count)
                s_warpFavSel = 0;

            s_warpLastActionSummary = $"select next -> {DescribeSelectedWarpFavorite_NoLock()}";
            MelonLogger.Msg($"[WarpFavorites] select next -> {DescribeSelectedWarpFavorite_NoLock()}");
            return;
        }

        int found = -1;
        int idx = start;
        for (int step = 0; step < s_warpFavs.Count; step++)
        {
            idx++;
            if (idx >= s_warpFavs.Count)
                idx = 0;

            var cand = s_warpFavs[idx];
            if (!cand.HasContext)
                continue;

            if (cand.LoadField == curF && cand.LoadArea == curA && cand.LoadScript == curS)
            {
                found = idx;
                break;
            }
        }

        int matchCount = 0;
for (int i = 0; i < s_warpFavs.Count; i++)
{
    var f = s_warpFavs[i];
    if (!f.HasContext) continue;
    if (f.LoadField == curF && f.LoadArea == curA && f.LoadScript == curS)
        matchCount++;
}

if (found >= 0)
{
    int prev = s_warpFavSel;
    s_warpFavSel = found;

    string desc = DescribeSelectedWarpFavorite_NoLock();
    if (matchCount <= 1 || prev == found)
    {
        s_warpLastActionSummary = $"select next (filtered): {desc} (only {matchCount} match)";
        MelonLogger.Msg($"[WarpFavorites] select next (filtered): staying on {desc} (matches={matchCount})");
    }
    else
    {
        s_warpLastActionSummary = $"select next (filtered) -> {desc}";
        MelonLogger.Msg($"[WarpFavorites] select next (filtered) -> {desc} (matches={matchCount})");
    }
}
else
{
    s_warpLastActionSummary = "select next (filtered): no favorites match current field context (capture with Ctrl+Alt+Shift+F6 or toggle filter off)";
    MelonLogger.Msg("[WarpFavorites] select next (filtered): no matches (capture with Ctrl+Alt+Shift+F6 or toggle filter off)");
}
    }
}

            public static void WarpToFavoriteConfirm()
{
    int doorIndex;
    string label;
    bool hasCtx;
    int favF, favA, favS;

    lock (s_warpFavLock)
    {
        if (s_warpFavs == null || s_warpFavs.Count <= 0)
        {
            s_warpLastActionSummary = "no favorites";
            return;
        }

        int sel = Math.Clamp(s_warpFavSel, 0, s_warpFavs.Count - 1);
        var fav = s_warpFavs[sel];
        doorIndex = fav.DoorIndex;
        label = fav.Label ?? "";
        hasCtx = fav.HasContext;
        favF = fav.LoadField;
        favA = fav.LoadArea;
        favS = fav.LoadScript;
    }

    // Safety: door indices are scoped to a field/area/script. If the selected favorite includes context
    // and we're in a different context, don't attempt to warp (it would hit the "same index" in the *current* area).
    if (hasCtx)
    {
        int curF = -1, curA = -1, curS = -1;
        string _pr = "", _cn = "";
        TryGetCurrentFieldContext(out curF, out curA, out curS, out _pr, out _cn);

        if (curF != favF || curA != favA || curS != favS)
        {
            s_warpLastActionSummary = $"warp blocked: favorite context F={favF} A={favA} S={favS} != current F={curF} A={curA} S={curS} (cross-area warp not implemented yet)";
            MelonLogger.Msg("[WarpFavorites] warp blocked: favorite context differs from current (cross-area warp not implemented yet)");
            return;
        }
    }

    float now = Time.realtimeSinceStartup;
    if (s_warpArmDoorIndex == doorIndex && now <= s_warpArmUntilRealtime)
    {
        // Confirmed: attempt warp.
        s_warpArmDoorIndex = -1;
        s_warpArmUntilRealtime = 0.0f;

        TryWarpToDoorIndex(doorIndex, label);
        return;
    }

    // Arm.
    s_warpArmDoorIndex = doorIndex;
    s_warpArmUntilRealtime = now + 2.5f;
    s_warpLastActionSummary = $"ARM (press again to warp) -> {DescribeSelectedWarpFavorite_NoLock()}";
}

            private static void TryWarpToDoorIndex(int doorIndex, string label)
            {
                // Hard guardrails:
                // 1) Only warp if the door buffer is present and index is in range.
                // 2) Try to avoid warping from non-field contexts by requiring fldSceneParam access.
                int doorCount = -1;

                try
                {
                    StartAutoCapture($"DevtoolsWarp doorIdx={doorIndex} label={label}", 480);
                    ForceSnapshot("DevtoolsWarp pre");
                    if (!TryGetDoorBuff(out var doorBuff, out doorCount) || doorBuff == null)
                    {
                        s_warpLastActionSummary = "warp failed: door buffer not ready (not in field?)";
                        return;
                    }

                    if (doorIndex < 0 || doorIndex >= doorCount)
                    {
                        s_warpLastActionSummary = $"warp failed: index out of range ({doorIndex} / {doorCount})";
                        return;
                    }
                }
                catch
                {
                    s_warpLastActionSummary = "warp failed: exception while checking door buffer";
                    return;
                }

                // Best-effort: if fldSceneParam access throws / is null, assume non-field and bail.
                try
                {
                    if (TryGetFldSceneParam(out _) == null)
                    {
                        s_warpLastActionSummary = "warp failed: fldSceneParam null (likely not in field)";
                        return;
                    }
                }
                catch
                {
                    s_warpLastActionSummary = "warp failed: fldSceneParam not accessible (likely not in field)";
                    return;
                }

                try
                {
                    var fldWapType = FindGameType("Il2Cpp.fldWap", "fldWap");
                    if (fldWapType == null)
                    {
                        s_warpLastActionSummary = "warp failed: type fldWap not found";
                        return;
                    }

                    var sp = TryGetFldSceneParam(out var spErr);
                    if (sp == null)
                    {
                        s_warpLastActionSummary = $"warp failed: fldSceneParam null ({San(spErr)})";
                        return;
                    }

                    // Current context (fallbacks).
                    int curCamMode = GetInt(sp, "camMode", 0);
                    int curBgm = GetInt(sp, "bgm", 0);

                    if (!TryGetDoorBuff(out var doorBuff, out _) || doorBuff == null || !TryGetArrayElement(doorBuff, doorIndex, out var doorObj) || doorObj == null)
                    {
                        s_warpLastActionSummary = $"warp failed: door idx={doorIndex} not found";
                        return;
                    }

                    MelonLogger.Msg($"[WarpFavorites] door[{doorIndex}] {DescribeDoorEntryForLogs(doorObj)}");
                    Mark($"door[{doorIndex}] {DescribeDoorEntryForLogs(doorObj)}");

                    // Extract likely destination fields.
                    int u08 = GetInt(doorObj, "union08", 0);
                    int u09 = GetInt(doorObj, "union09", 0);
                    int u10 = GetInt(doorObj, "union10", 0);
                    string pos = GetMaybeFixedCharString(doorObj, "warp_pos", 128);
                    string cam = GetMaybeFixedCharString(doorObj, "warp_camname", 128);
                    int camMode = GetInt(doorObj, "warp_cammode", curCamMode);
                    int bgm = GetInt(doorObj, "warp_bgm", curBgm);
                    int afterFlag = GetInt(doorObj, "after_flag", 0);
                    string afterScr = GetMaybeFixedCharString(doorObj, "after_scr", 128);

                    int mt = GetInt(doorObj, "movetype", 0);
                    int wt = GetInt(doorObj, "warptype", 0);
                    int wt2 = GetInt(doorObj, "warptype2", 0);
                    int attr = GetInt(doorObj, "warp_attr", 0);

                    // Preferred probe path (based on IL2CPP stubs):
                    //   fldWap.MakeWarpIndex(doormovetbl_s)
                    //   fldWap.fldDoor_ChkWarp()
                    //   fldWap.fldDoor_Warp(ref string evename)
                    // Our earlier fldDoor_WarpEx(int) probe appears to have 0 callsites and does not move us.

                    var miMake = FindMakeWarpIndexFromDoor(fldWapType);
                    var miChk = FindStaticMethod0(fldWapType, "fldDoor_ChkWarp");
                    var miWarp = FindFldDoorWarp(fldWapType);

                    if (miMake != null && miWarp != null)
                    {
                        MelonLogger.Warning($"[Reimagined] WARP EXECUTE (MAKE+WARP) -> doorIndex={doorIndex} label=\"{label}\" (doorCount={doorCount})");

                        // Snapshot pre-state (these are fldWap statics; very helpful for debugging whether MakeWarpIndex actually armed a warp)
int warpIdxBefore = TryReadStaticInt(fldWapType, "gFldWarpIdx", -1);
int afterNextBefore = TryReadStaticInt(fldWapType, "gFldWap_after_nextidx", -1);

int afterFlagBefore = TryReadStaticInt(fldWapType, "gFldWap_after_flag", 0);
string afterScrBefore = TryReadStaticCharArrayString(fldWapType, "gFldWap_after_scr", 128);

		// PREP: attempt to start door-work for this entry (best-effort).
		// Some warp routines appear to no-op unless a door-work is active.
		// NOTE: union08 is already read above as an int (u08). Treat it as the best-effort evehit_resid.
		int evehitResid = u08;
	if (evehitResid != 0)
	{
	    var miStart = FindStaticMethod1(fldWapType, "fldDoor_Start");
	    if (miStart != null)
	    {
	        try
	        {
	            miStart.Invoke(null, new object?[] { evehitResid });
	            MelonLogger.Warning($"[WARP PREP: fldDoor_Start] evehit_resid={evehitResid} => OK");
	        }
	        catch (Exception ex)
	        {
	            MelonLogger.Warning($"[WARP PREP: fldDoor_Start] evehit_resid={evehitResid} threw: {ex.GetType().Name}: {ex.Message}");
	        }
	    }
	    else
	    {
	        MelonLogger.Warning($"[WARP PREP: fldDoor_Start] method missing (evehit_resid={evehitResid})");
	    }
	}
	else
	{
	    MelonLogger.Warning("[WARP PREP: fldDoor_Start] skipped (union08=0)");
	}

	// PREP: discover current evename (if any)
	string currentEvename = string.Empty;
	try
	{
	    var miFind3 = FindStaticMethod0(fldWapType, "fldDoor_Find3");
	    if (miFind3 != null)
	        currentEvename = (miFind3.Invoke(null, Array.Empty<object>()) as string) ?? string.Empty;
	}
	catch { /* ignore */ }
	if (!string.IsNullOrEmpty(currentEvename))
	    MelonLogger.Warning($"[WARP PREP: evename] fldDoor_Find3 => \"{currentEvename}\"");


miMake.Invoke(null, new object?[] { doorObj });

                    ForceSnapshot("DevtoolsWarp after MakeWarpIndex");

// Optional: some doors set BGM via a separate helper.
int setBgmRv = int.MinValue;
var miSetBgm = FindSetBgmFromDoor(fldWapType);
if (miSetBgm != null)
{
    try { setBgmRv = Convert.ToInt32(miSetBgm.Invoke(null, new object?[] { doorObj })); }
    catch (Exception ex) { MelonLogger.Warning($"[WARP EXECUTE] fldWap_SetBgm threw: {ex.GetType().Name}: {ex.Message}"); }
}

int warpIdxAfterMake = TryReadStaticInt(fldWapType, "gFldWarpIdx", -1);
int afterNextAfterMake = TryReadStaticInt(fldWapType, "gFldWap_after_nextidx", -1);
int afterFlagAfterMake = TryReadStaticInt(fldWapType, "gFldWap_after_flag", 0);
string afterScrAfterMake = TryReadStaticCharArrayString(fldWapType, "gFldWap_after_scr", 128);

int chk = -1;
if (miChk != null)
{
    try { chk = Convert.ToInt32(miChk.Invoke(null, Array.Empty<object>())); }
    catch (Exception ex) { MelonLogger.Warning($"[WARP EXECUTE] fldDoor_ChkWarp threw: {ex.GetType().Name}: {ex.Message}"); }
}

// NOTE: fldDoor_Warp takes `ref string evename`, but in our call traces it behaves like an **IN** parameter.
// In vanilla door transitions, it is called with a populated label like "01d_01"/"01d_08".
// We therefore prefer the door-table's union02 string (when present), otherwise fall back to the favorites label / currentEvename.
string evenameFromDoor = GetMaybeFixedCharString(doorObj, "union02", 128);
string evenameIn =
    !string.IsNullOrEmpty(evenameFromDoor) ? evenameFromDoor :
    (!string.IsNullOrEmpty(label) ? label : currentEvename);

if (evenameIn == null)
    evenameIn = string.Empty;

MelonLogger.Warning($"[WARP EXECUTE] fldDoor_Warp(evenameIn=\"{evenameIn}\") (union02=\"{evenameFromDoor}\", label=\"{label}\", currentEvename=\"{currentEvename}\")");

object?[] warpArgs = new object?[] { evenameIn };
if (miWarp != null)
{
    try
    {
        miWarp.Invoke(null, warpArgs);
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[WARP EXECUTE] fldDoor_Warp threw: {ex.GetType().Name}: {ex.Message}");
    }
}
else
{
    MelonLogger.Warning("fldDoor_Warp method not found");
}
string evenameOut = warpArgs[0] as string ?? string.Empty;

int warpIdxAfterWarp = TryReadStaticInt(fldWapType, "gFldWarpIdx", -1);
int afterNextAfterWarp = TryReadStaticInt(fldWapType, "gFldWap_after_nextidx", -1);
int afterFlagAfterWarp = TryReadStaticInt(fldWapType, "gFldWap_after_flag", 0);
string afterScrAfterWarp = TryReadStaticCharArrayString(fldWapType, "gFldWap_after_scr", 128);

// If nothing seems to have been armed, attempt WarpEx as a fallback (this is commonly used by debug pathways).
bool looksArmed =
    (chk != 0) ||
    (warpIdxAfterMake != warpIdxBefore) ||
    (afterNextAfterMake != afterNextBefore) ||
    (afterFlagAfterMake != afterFlagBefore) ||
    (!string.Equals(afterScrAfterMake, afterScrBefore, StringComparison.Ordinal)) ||
    (warpIdxAfterWarp != warpIdxBefore) ||
    (afterNextAfterWarp != afterNextBefore) ||
    (afterFlagAfterWarp != afterFlagBefore) ||
    (!string.Equals(afterScrAfterWarp, afterScrBefore, StringComparison.Ordinal));

const bool ENABLE_WARP_EX_FALLBACK = false;
bool didWarpEx = false;
var miWarpEx = ENABLE_WARP_EX_FALLBACK ? FindWarpEx(fldWapType) : null;
if (ENABLE_WARP_EX_FALLBACK && !looksArmed && miWarpEx != null)
{
    try
    {
	    // IMPORTANT: fldDoor_WarpEx takes an *exit number* (ex_no), not a door-table index.
	    // In doormovetbl_s this is stored in union05 (short).
	    int exNo = GetDoorIntMemberSafe(doorObj, "union05", defaultValue: -1);
	    int exNoArg = exNo >= 0 ? exNo : doorIndex;

	    MelonLogger.Warning($"[WARP EXECUTE] Make+Warp did not arm (chk={chk}, gFldWarpIdx {warpIdxBefore}->{warpIdxAfterMake}, after_nextidx {afterNextBefore}->{afterNextAfterMake}). Trying fldDoor_WarpEx(ex_no={exNoArg})... (doorIndex={doorIndex}, union05={exNo})");
	    ForceSnapshot($"DevtoolsWarp before WarpEx ex_no={exNoArg}");
	    miWarpEx.Invoke(null, new object?[] { exNoArg });
	    ForceSnapshot($"DevtoolsWarp after WarpEx ex_no={exNoArg}");
        didWarpEx = true;
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[WARP EXECUTE] fldDoor_WarpEx threw: {ex.GetType().Name}: {ex.Message}");
    }
}

string summaryCore =
    $"idx={doorIndex} chk={chk} gWarpIdx={warpIdxBefore}->{warpIdxAfterMake}->{warpIdxAfterWarp} " +
    $"afterNext={afterNextBefore}->{afterNextAfterMake}->{afterNextAfterWarp} " +
    $"afterFlag={afterFlagBefore}->{afterFlagAfterMake}->{afterFlagAfterWarp} " +
    $"afterScr=\"{afterScrAfterWarp}\" setBgmRv={(setBgmRv == int.MinValue ? "n/a" : setBgmRv.ToString())} " +
    $"evenameOut=\"{evenameOut}\"";

s_warpLastActionSummary = didWarpEx ? $"warp_make+warp_ex: {summaryCore}" : $"warp_make+warp: {summaryCore}";
MelonLogger.Msg($"[WARP EXECUTE] {(didWarpEx ? "MAKE+WARP_EX" : "MAKE+WARP")} {summaryCore}");

return;}

                    // Fallback: keep WarpEx probe for now.
                    var miEx = FindStaticMethod1(fldWapType, "fldDoor_WarpEx");
                    if (miEx == null)
                    {
                        s_warpLastActionSummary = $"warp failed: no warp entrypoint idx={doorIndex} (miMake={(miMake != null)} miWarp={(miWarp != null)})";
                        return;
                    }

                    MelonLogger.Warning($"[Reimagined] WARP EXECUTE (FALLBACK EX) -> doorIndex={doorIndex} label=\"{label}\" (doorCount={doorCount})");
                    miEx.Invoke(null, new object?[] { doorIndex });
                    s_warpLastActionSummary = $"warp_probe_ex: idx={doorIndex}";
                }
                catch (Exception ex)
                {
                    s_warpLastActionSummary = $"warp exception: {ex.GetType().Name}: {ex.Message}";
                }
            }

            private static MethodInfo? FindStaticMethod0(Type t, string name)
            {
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (var m in t.GetMethods(BF))
                {
                    if (!string.Equals(m.Name, name, StringComparison.Ordinal))
                        continue;
                    if (m.GetParameters().Length != 0)
                        continue;
                    return m;
                }
                return null;
            }

            private static MethodInfo? FindMakeWarpIndexFromDoor(Type fldWapType)
            {
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (var m in fldWapType.GetMethods(BF))
                {
                    if (!string.Equals(m.Name, "MakeWarpIndex", StringComparison.Ordinal))
                        continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1)
                        continue;
                    var p0 = ps[0].ParameterType;
                    var n = p0.FullName ?? p0.Name;
                    if (n.IndexOf("doormovetbl_s", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    return m;
                }
                return null;
            }

            private static MethodInfo? FindFldDoorWarp(Type fldWapType)
            {
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (var m in fldWapType.GetMethods(BF))
                {
                    if (!string.Equals(m.Name, "fldDoor_Warp", StringComparison.Ordinal))
                        continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1)
                        continue;
                    if (!ps[0].ParameterType.IsByRef)
                        continue;
                    if (ps[0].ParameterType.GetElementType() != typeof(string))
                        continue;
                    return m;
                }
                return null;
            }

            private static MethodInfo? FindStaticMethod1(Type t, string name)
            {
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (var m in t.GetMethods(BF))
                {
                    if (!string.Equals(m.Name, name, StringComparison.Ordinal))
                        continue;

                    var ps = m.GetParameters();
                    if (ps.Length != 1)
                        continue;

                    // We only need "convertible to int".
                    return m;
                }
                return null;
            }

            
            private static MethodInfo? FindSetBgmFromDoor(Type fldWapType)
            {
                // fldWap_SetBgm(doormovetbl_s pWap) : int
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (var mi in fldWapType.GetMethods(flags))
                {
                    if (!string.Equals(mi.Name, "fldWap_SetBgm", StringComparison.Ordinal))
                        continue;

                    var ps = mi.GetParameters();
                    if (ps.Length != 1)
                        continue;

                    // We deliberately match on the parameter type name rather than a compile-time type.
                    if (ps[0].ParameterType.Name.IndexOf("doormovetbl_s", StringComparison.OrdinalIgnoreCase) >= 0)
                        return mi;
                }

                return null;
            }

            private static MethodInfo? FindWarpEx(Type fldWapType)
            {
                // fldDoor_WarpEx(int ex_no)
                var mi = FindStaticMethod1(fldWapType, "fldDoor_WarpEx");
                if (mi == null)
                    return null;

                var ps = mi.GetParameters();
                if (ps.Length != 1)
                    return null;

                // Accept int / System.Int32-like
                if (ps[0].ParameterType != typeof(int))
                    return null;

                return mi;
            }

            private static int TryReadStaticInt(Type t, string memberName, int fallback)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                try
                {
                    var p = t.GetProperty(memberName, flags);
                    if (p != null && p.CanRead)
                    {
                        object? v = p.GetValue(null, null);
                        if (v != null)
                            return Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    }

                    var f = t.GetField(memberName, flags);
                    if (f != null)
                    {
                        object? v = f.GetValue(null);
                        if (v != null)
                            return Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    // ignore - caller uses fallback
                }

                return fallback;
            }

            private static string TryReadStaticCharArrayString(Type t, string memberName, int maxChars)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                object? v = null;
                try
                {
                    var p = t.GetProperty(memberName, flags);
                    if (p != null && p.CanRead)
                        v = p.GetValue(null, null);
                    else
                    {
                        var f = t.GetField(memberName, flags);
                        if (f != null)
                            v = f.GetValue(null);
                    }
                }
                catch
                {
                    return string.Empty;
                }

                return TryReadCharArrayObjectString(v, maxChars);
            }

            
            private static bool TryCoerceChar(object? chObj, out char ch)
            {
                ch = '\0';
                if (chObj == null)
                    return false;

                if (chObj is char c)
                {
                    ch = c;
                    return true;
                }

                if (chObj is string s && s.Length > 0)
                {
                    ch = s[0];
                    return true;
                }

                try
                {
                    ch = Convert.ToChar(chObj, CultureInfo.InvariantCulture);
                    return true;
                }
                catch { /* ignore */ }

                try
                {
                    Type t = chObj.GetType();
                    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    // Il2CppSystem.Char often wraps a ushort in a field like m_value / value.
                    foreach (string name in new[] { "m_value", "value", "_value", "Value" })
                    {
                        var fi = t.GetField(name, BF);
                        if (fi != null)
                        {
                            object? v = fi.GetValue(chObj);
                            if (v != null)
                            {
                                ushort u = Convert.ToUInt16(v, CultureInfo.InvariantCulture);
                                ch = (char)u;
                                return true;
                            }
                        }

                        var pi = t.GetProperty(name, BF);
                        if (pi != null && pi.CanRead)
                        {
                            object? v = pi.GetValue(chObj, null);
                            if (v != null)
                            {
                                ushort u = Convert.ToUInt16(v, CultureInfo.InvariantCulture);
                                ch = (char)u;
                                return true;
                            }
                        }
                    }

                    string ts = chObj.ToString() ?? "";
                    if (ts.Length == 1)
                    {
                        ch = ts[0];
                        return true;
                    }
                }
                catch { /* ignore */ }

                return false;
            }

private static string TryReadCharArrayObjectString(object? arrObj, int maxChars)
            {
                if (arrObj == null)
                    return string.Empty;

                if (arrObj is string s)
                    return s;

                if (arrObj is char[] ca)
                {
                    int n = 0;
                    while (n < ca.Length && n < maxChars && ca[n] != '\0')
                        n++;
                    return new string(ca, 0, n);
                }

                try
                {
                    Type at = arrObj.GetType();
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                    int len = -1;
                    var pLen = at.GetProperty("Length", flags);
                    if (pLen != null && pLen.CanRead)
                        len = Convert.ToInt32(pLen.GetValue(arrObj, null), CultureInfo.InvariantCulture);
                    else
                    {
                        var miLen = at.GetMethod("get_Length", flags);
                        if (miLen != null)
                            len = Convert.ToInt32(miLen.Invoke(arrObj, Array.Empty<object>()), CultureInfo.InvariantCulture);
                    }

                    if (len <= 0)
                        return string.Empty;

                    int cap = Math.Min(Math.Min(len, maxChars), 4096);

                    MethodInfo? miGet = at.GetMethod("get_Item", flags, null, new[] { typeof(int) }, null)
                        ?? at.GetMethod("Get", flags, null, new[] { typeof(int) }, null);

                    PropertyInfo? pItem = at.GetProperty("Item", flags, null, typeof(char), new[] { typeof(int) }, null);

                    var chars = new List<char>(cap);

                    for (int i = 0; i < cap; i++)
                    {
                        object? chObj = null;

                        if (pItem != null)
                            chObj = pItem.GetValue(arrObj, new object?[] { i });
                        else if (miGet != null)
                            chObj = miGet.Invoke(arrObj, new object?[] { i });

                        if (chObj == null)
                            break;

                        if (!TryCoerceChar(chObj, out char ch))
                            break;
                        if (ch == '\0')
                            break;

                        chars.Add(ch);
                    }

                    return chars.Count > 0 ? new string(chars.ToArray()) : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            private static int GetDoorIntMemberSafe(object doorObj, string memberName, int defaultValue)
            {
                try
                {
                    Type t = doorObj.GetType();
                    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    FieldInfo? fi = t.GetField(memberName, BF);
                    if (fi != null)
                    {
                        object? v = fi.GetValue(doorObj);
                        if (v == null)
                            return defaultValue;
                        return Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    }

                    PropertyInfo? pi = t.GetProperty(memberName, BF);
                    if (pi != null && pi.CanRead)
                    {
                        object? v = pi.GetValue(doorObj, null);
                        if (v == null)
                            return defaultValue;
                        return Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    // best-effort
                }
                return defaultValue;
            }

private static string DescribeSelectedWarpFavorite_NoLock()
            {
                if (s_warpFavs == null || s_warpFavs.Count <= 0)
                    return "none";

                int sel = Math.Clamp(s_warpFavSel, 0, s_warpFavs.Count - 1);
                var fav = s_warpFavs[sel];

                string ctx = fav.HasContext ? $"F={fav.LoadField} A={fav.LoadArea} S={fav.LoadScript}" : "legacy";
                string ev = !string.IsNullOrEmpty(fav.Evename) ? $" evename=\"{San(fav.Evename)}\"" : "";
                return $"sel={sel + 1}/{s_warpFavs.Count} doorIndex={fav.DoorIndex} ctx={ctx}{ev} label=\"{San(fav.Label)}\"";
            }

            // NOTE: This helper intentionally has a distinct name from the skill favorites
            // parser's TryParseInt() to avoid partial-class duplicate member collisions.
            private static bool TryParseDoorIndex(string s, out int value)
            {
                value = 0;
                s = (s ?? "").Trim();
                if (s.Length == 0)
                    return false;

                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }
    }
}
