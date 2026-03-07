#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            private static Dictionary<string, string>? s_lastUiTextSnapshot;
            private static string? s_lastUiTextScene;

            internal static void HotkeyDumpUiTextMap()
            {
                try
                {
                    string path = MakeDumpPath("ui_text_map");
                    using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    var scene = SceneManager.GetActiveScene();
                    var roots = SafeGetSceneRoots();
                    var canvasUi = FindRootByName(roots, "Canvas_UI");

                    // Prefer Canvas_UI if present (most of our UIs live under it). Fall back to all roots.
                    var scanRoots = new List<GameObject>();
                    if (canvasUi != null)
                        scanRoots.Add(canvasUi);
                    else
                        scanRoots.AddRange(roots);

                    const int maxNodes = 14000;
                    const int maxEntries = 6500;
                    const bool includeInactive = false;

                    var snap = new Dictionary<string, string>(StringComparer.Ordinal);
                    var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

                    int nodes = 0;
                    int entries = 0;

                    w.WriteLine("SMT3HD_Reimagined UI Text Map (paths -> visible strings)");
                    w.WriteLine($"ts={DateTime.Now:O}");
                    w.WriteLine($"scene={scene.name} ({scene.buildIndex})");
                    w.WriteLine($"root=\"{(canvasUi != null ? "Canvas_UI" : "<all scene roots>")}\"");
                    w.WriteLine($"includeInactive={includeInactive}");
                    w.WriteLine($"maxNodes={maxNodes} maxEntries={maxEntries}");
                    w.WriteLine();

                    foreach (var r in scanRoots.Where(r => r != null).OrderBy(r => r.name))
                    {
                        ScanUiTextRecursive(r.transform, snap, keyCounts, ref nodes, ref entries, maxNodes, maxEntries, includeInactive, w);
                        if (nodes >= maxNodes || entries >= maxEntries)
                            break;
                    }

                    w.WriteLine();
                    w.WriteLine($"nodes_scanned={nodes}");
                    w.WriteLine($"text_entries={entries}");

                    // Diff vs last snapshot in the same scene.
                    if (s_lastUiTextSnapshot == null || !string.Equals(s_lastUiTextScene, scene.name, StringComparison.Ordinal))
                    {
                        w.WriteLine();
                        w.WriteLine("No previous snapshot for this scene (baseline captured).");
                    }
                    else
                    {
                        var prev = s_lastUiTextSnapshot;
                        var keys = new HashSet<string>(prev.Keys, StringComparer.Ordinal);
                        keys.UnionWith(snap.Keys);

                        int changed = 0;
                        int added = 0;
                        int removed = 0;

                        var changedLines = new List<string>(256);

                        foreach (var k in keys)
                        {
                            bool aHave = prev.TryGetValue(k, out var a);
                            bool bHave = snap.TryGetValue(k, out var b);

                            if (aHave && !bHave) { removed++; continue; }
                            if (!aHave && bHave) { added++; continue; }
                            if (!string.Equals(a, b, StringComparison.Ordinal))
                            {
                                changed++;
                                if (changedLines.Count < 200)
                                    changedLines.Add($"- {k}: \"{a}\" -> \"{b}\"");
                            }
                        }

                        w.WriteLine();
                        w.WriteLine("Delta since last Ctrl+Alt+F12 in this scene:");
                        w.WriteLine($"added={added} removed={removed} changed={changed}");
                        if (changedLines.Count > 0)
                        {
                            w.WriteLine();
                            w.WriteLine("Top changed entries:");
                            foreach (var l in changedLines)
                                w.WriteLine(l);
                        }
                    }

                    // Update baseline.
                    s_lastUiTextSnapshot = snap;
                    s_lastUiTextScene = scene.name;

                    MelonLoader.MelonLogger.Msg($"[Reimagined] UI text map written: {path}");
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error($"[Reimagined] HotkeyDumpUiTextMap exception: {ex}");
                }
            }

            private static void ScanUiTextRecursive(
                Transform root,
                Dictionary<string, string> snap,
                Dictionary<string, int> keyCounts,
                ref int nodes,
                ref int entries,
                int maxNodes,
                int maxEntries,
                bool includeInactive,
                TextWriter w)
            {
                if (root == null)
                    return;

                if (nodes >= maxNodes || entries >= maxEntries)
                    return;

                var go = root.gameObject;
                nodes++;

                if (go != null)
                {
                    bool activeHier = false;
                    bool activeSelf = false;
                    try { activeHier = go.activeInHierarchy; activeSelf = go.activeSelf; }
                    catch { /* ignore */ }

                    if (includeInactive || activeHier)
                    {
                        if (TryGetComponentsOnNodeBestEffort(go, out var comps))
                        {
                            for (int i = 0; i < comps.Count; i++)
                            {
                                if (entries >= maxEntries)
                                    break;

                                var c = comps[i];
                                if (c == null)
                                    continue;

                                try
                                {
                                    if (c is Text ut)
                                    {
                                        EmitTextEntry(GetHierarchyPath(root), "Text", ut.text ?? "", activeSelf, activeHier, snap, keyCounts, ref entries, w);
                                        continue;
                                    }

                                    var ct = c.GetType();
                                    var full = ct.FullName ?? ct.Name;
                                    if (full.StartsWith("TMPro.", StringComparison.Ordinal) || full.IndexOf("TMP_", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        // Avoid referencing TMPro types directly; use reflection to read the 'text' property.
                                        var p = ct.GetProperty("text");
                                        if (p != null && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                                        {
                                            var tv = p.GetValue(c, null) as string;
                                            EmitTextEntry(GetHierarchyPath(root), "TMP", tv ?? "", activeSelf, activeHier, snap, keyCounts, ref entries, w);
                                        }
                                    }
                                }
                                catch
                                {
                                    // ignore per-component
                                }
                            }
                        }
                    }
                }

                // Recurse.
                int childCount = 0;
                try { childCount = root.childCount; }
                catch { childCount = 0; }

                for (int i = 0; i < childCount; i++)
                {
                    if (nodes >= maxNodes || entries >= maxEntries)
                        break;
                    Transform? ch = null;
                    try { ch = root.GetChild(i); }
                    catch { ch = null; }
                    if (ch == null)
                        continue;
                    ScanUiTextRecursive(ch, snap, keyCounts, ref nodes, ref entries, maxNodes, maxEntries, includeInactive, w);
                }
            }

            private static void EmitTextEntry(
                string path,
                string kind,
                string raw,
                bool activeSelf,
                bool activeHier,
                Dictionary<string, string> snap,
                Dictionary<string, int> keyCounts,
                ref int entries,
                TextWriter w)
            {
                // Reduce noise: keep empty strings out of the map unless they look like a localization key.
                raw = raw ?? "";
                string trimmed = raw.Trim();
                if (trimmed.Length == 0)
                    return;

                string display = Trim(Safe(trimmed), 240);

                string loc = "";
                if (LooksLikeLocKey(trimmed) && TryLocalizeKey(trimmed, out var localized) && !string.IsNullOrEmpty(localized))
                    loc = localized;

                string keyBase = $"{path}|{kind}";
                keyCounts.TryGetValue(keyBase, out int n);
                n++;
                keyCounts[keyBase] = n;
                string key = n == 1 ? keyBase : $"{keyBase}#{n}";

                string value = string.IsNullOrEmpty(loc) ? display : $"{display} -> {loc}";
                snap[key] = value;

                w.WriteLine($"- {key} activeSelf={activeSelf} activeHier={activeHier} text=\"{value}\"");
                entries++;
            }

            private static bool LooksLikeLocKey(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return false;
                if (s.Length < 6 || s.Length > 32)
                    return false;
                // No spaces, must contain an underscore, and mostly uppercase/digits.
                if (s.IndexOf(' ') >= 0)
                    return false;
                if (s.IndexOf('_') < 0)
                    return false;
                int ok = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                        ok++;
                }
                return ok >= (int)(s.Length * 0.85f);
            }
        }
    }
}
