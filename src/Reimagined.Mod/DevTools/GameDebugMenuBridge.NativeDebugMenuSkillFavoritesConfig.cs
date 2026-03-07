#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass_B12: File-backed skill favorites
            // =========================================================
            // Goal:
            // - Make the favorites list editable without recompiling.
            // - Keep it fail-safe: if the file is missing/malformed, fall back to defaults.
            // - Provide a reload hotkey so iteration is fast.

            private static readonly string SkillFavoritesPath = Path.Combine(DumpsDir, "devtools_skill_favorites.txt");

            private static bool s_skillFavoritesLoaded;
            private static int[] s_skillFavoritesActive = Array.Empty<int>();
            private static string s_skillFavoritesSource = "<uninitialized>";
            private static string s_skillFavoritesLoadNote = string.Empty;

            private static void EnsureSkillFavoritesLoaded()
            {
                if (s_skillFavoritesLoaded)
                    return;

                // First load is best-effort; never throws.
                TryLoadSkillFavoritesFromDisk(force: false, out _, out _);
            }

            internal static bool TryReloadSkillFavorites(out string summary, out string note)
            {
                return TryLoadSkillFavoritesFromDisk(force: true, out summary, out note);
            }

            internal static bool TryGetSkillFavoritesSourceSummary(out string summary)
            {
                summary = string.Empty;
                try
                {
                    EnsureSkillFavoritesLoaded();
                    int n = s_skillFavoritesActive?.Length ?? 0;
                    summary = $"{s_skillFavoritesSource} (n={n})";
                    if (!string.IsNullOrEmpty(s_skillFavoritesLoadNote))
                        summary += $" · {s_skillFavoritesLoadNote}";
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryLoadSkillFavoritesFromDisk(bool force, out string summary, out string note)
            {
                summary = string.Empty;
                note = string.Empty;

                try
                {
                    Directory.CreateDirectory(DumpsDir);

                    // If missing, create a template once so the user has a discoverable place to edit.
                    if (!File.Exists(SkillFavoritesPath))
                    {
                        TryWriteFavoritesTemplate();
                        s_skillFavoritesSource = "defaults (template created)";
                        s_skillFavoritesLoadNote = $"created {Path.GetFileName(SkillFavoritesPath)}";
                        s_skillFavoritesActive = (int[])s_skillFavoritesDefaults.Clone();
                        s_skillFavoritesLoaded = true;
                        ClampFavoriteIndex();

                        summary = $"skill favorites: using defaults · wrote template {Path.GetFileName(SkillFavoritesPath)}";
                        return true;
                    }

                    // Parse file.
                    var ids = new List<int>(32);
                    var seen = new HashSet<int>();

                    string[] lines = File.ReadAllLines(SkillFavoritesPath);
                    for (int li = 0; li < lines.Length; li++)
                    {
                        string line = lines[li] ?? string.Empty;
                        line = StripComments(line).Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        if (TryParseFirstIntToken(line, out int sid))
                        {
                            if (sid < 0)
                                continue;

                            if (seen.Add(sid))
                                ids.Add(sid);
                        }
                    }

                    if (ids.Count <= 0)
                    {
                        // Empty/malformed file; fall back.
                        s_skillFavoritesSource = "defaults (file empty/invalid)";
                        s_skillFavoritesLoadNote = $"{Path.GetFileName(SkillFavoritesPath)} had no parseable ids";
                        s_skillFavoritesActive = (int[])s_skillFavoritesDefaults.Clone();
                        s_skillFavoritesLoaded = true;
                        ClampFavoriteIndex();

                        summary = "skill favorites: using defaults (file had no ids)";
                        note = s_skillFavoritesLoadNote;
                        return true;
                    }

                    // Cap size to avoid accidental massive lists.
                    if (ids.Count > 128)
                    {
                        ids.RemoveRange(128, ids.Count - 128);
                        note = "favorites list capped to 128 entries";
                    }

                    s_skillFavoritesActive = ids.ToArray();
                    s_skillFavoritesSource = $"file:{Path.GetFileName(SkillFavoritesPath)}";
                    s_skillFavoritesLoadNote = note;
                    s_skillFavoritesLoaded = true;
                    ClampFavoriteIndex();

                    summary = $"skill favorites: loaded {s_skillFavoritesActive.Length} from {Path.GetFileName(SkillFavoritesPath)}";
                    return true;
                }
                catch (Exception ex)
                {
                    // Fail-safe fallback.
                    s_skillFavoritesActive = (int[])s_skillFavoritesDefaults.Clone();
                    s_skillFavoritesSource = "defaults (load exception)";
                    s_skillFavoritesLoadNote = ex.GetType().Name;
                    s_skillFavoritesLoaded = true;
                    ClampFavoriteIndex();

                    summary = "skill favorites: using defaults (load exception)";
                    note = ex.GetType().Name;
                    return false;
                }
            }

            private static void ClampFavoriteIndex()
            {
                try
                {
                    int n = s_skillFavoritesActive?.Length ?? 0;
                    if (n <= 0)
                    {
                        s_skillFavoriteIndex = 0;
                        return;
                    }

                    if (s_skillFavoriteIndex < 0) s_skillFavoriteIndex = 0;
                    if (s_skillFavoriteIndex >= n) s_skillFavoriteIndex = n - 1;
                }
                catch
                {
                    s_skillFavoriteIndex = 0;
                }
            }

            private static string StripComments(string line)
            {
                if (string.IsNullOrEmpty(line))
                    return string.Empty;

                int cut = -1;

                int hash = line.IndexOf('#');
                if (hash >= 0) cut = hash;

                int semi = line.IndexOf(';');
                if (semi >= 0) cut = (cut < 0) ? semi : Math.Min(cut, semi);

                int sl = line.IndexOf("//", StringComparison.Ordinal);
                if (sl >= 0) cut = (cut < 0) ? sl : Math.Min(cut, sl);

                if (cut >= 0)
                    return line.Substring(0, cut);

                return line;
            }

            private static bool TryParseFirstIntToken(string line, out int value)
            {
                value = -1;

                try
                {
                    // Allow formats like:
                    //  26
                    //  0x1A
                    //  id=26
                    //  26, 28, 7
                    // We parse the first token that looks like an int.

                    string s = (line ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(s))
                        return false;

                    s = s.Replace(',', ' ').Replace('=', ' ').Replace('\t', ' ');
                    string[] parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string p = parts[i].Trim();
                        if (string.IsNullOrEmpty(p))
                            continue;

                        if (TryParseInt(p, out int v))
                        {
                            value = v;
                            return true;
                        }
                    }

                    return false;
                }
                catch
                {
                    value = -1;
                    return false;
                }
            }

            private static bool TryParseInt(string token, out int value)
            {
                value = -1;
                token = (token ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(token))
                    return false;

                // Strip a leading "id" prefix if present.
                if (token.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                {
                    // id26 or id:26 are both handled by the outer splitter in most cases,
                    // but this catches tight formats.
                    token = token.Substring(2).TrimStart(':');
                }

                if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    return int.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
                }

                return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            private static void TryWriteFavoritesTemplate()
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("# SMT3HD_Reimagined DevTools — Skill Favorites");
                    sb.AppendLine("#");
                    sb.AppendLine("# One skill id per line. Supported formats:");
                    sb.AppendLine("#   26");
                    sb.AppendLine("#   0x1A");
                    sb.AppendLine("#   id=26");
                    sb.AppendLine("# Comments: #  ;  //");
                    sb.AppendLine("#");
                    sb.AppendLine("# After editing this file, press Ctrl+Alt+Shift+R in-game to reload.");
                    sb.AppendLine();

                    for (int i = 0; i < s_skillFavoritesDefaults.Length; i++)
                    {
                        sb.AppendLine(s_skillFavoritesDefaults[i].ToString(CultureInfo.InvariantCulture));
                    }

                    File.WriteAllText(SkillFavoritesPath, sb.ToString());
                }
                catch
                {
                    // best-effort; ignore
                }
            }
        }
    }
}
