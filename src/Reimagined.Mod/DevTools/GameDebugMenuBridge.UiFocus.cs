#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // A light-weight UI snapshot intended to be taken frequently during A/B/C/D runs.
            // This is *not* a replacement for F2/F6; it is a small, stable set of anchor checks
            // that lets us quickly see what vanilla menus toggle.

            private struct UiFocusItem
            {
                public string Path;
                public bool Exists;
                public bool ActiveSelf;
                public bool ActiveInHierarchy;
                public int ChildCount;
                public string Extra;
            }

            private static Dictionary<string, UiFocusItem>? s_lastUiFocus;
            private static string? s_lastUiFocusScene;

            // Called by DumpInputSnapshot.
            private static void AppendUiFocusSnapshotLines(Action<string> logLine)
            {
                var snap = CaptureUiFocusSnapshot(out string sceneName);

                logLine($"[Reimagined] uiFocus: scene='{sceneName}' items={snap.Count}");

                // Diff vs previous snapshot (if any)
                if (s_lastUiFocus != null)
                {
                    int changed = 0;
                    foreach (var kv in snap)
                    {
                        if (!s_lastUiFocus.TryGetValue(kv.Key, out var prev))
                        {
                            changed++;
                            continue;
                        }
                        if (prev.Exists != kv.Value.Exists || prev.ActiveSelf != kv.Value.ActiveSelf || prev.ActiveInHierarchy != kv.Value.ActiveInHierarchy)
                            changed++;
                    }
                    foreach (var kv in s_lastUiFocus)
                    {
                        if (!snap.ContainsKey(kv.Key))
                            changed++;
                    }

                    if (changed > 0)
                    {
                        logLine($"[Reimagined] uiFocusDiff: changed={changed} (prevScene='{s_lastUiFocusScene ?? "?"}')");
                        foreach (var kv in snap)
                        {
                            s_lastUiFocus.TryGetValue(kv.Key, out var prev);
                            bool existedBefore = s_lastUiFocus.ContainsKey(kv.Key);
                            if (!existedBefore)
                            {
                                logLine($"[Reimagined] uiFocusDiff: + {FormatUiFocusItem(kv.Value)}");
                                continue;
                            }
                            if (prev.Exists != kv.Value.Exists || prev.ActiveSelf != kv.Value.ActiveSelf || prev.ActiveInHierarchy != kv.Value.ActiveInHierarchy)
                                logLine($"[Reimagined] uiFocusDiff: * {FormatUiFocusItem(kv.Value)} (prev: {FormatUiFocusItem(prev)})");
                        }
                        foreach (var kv in s_lastUiFocus)
                        {
                            if (!snap.ContainsKey(kv.Key))
                                logLine($"[Reimagined] uiFocusDiff: - {FormatUiFocusItem(kv.Value)}");
                        }
                    }
                    else
                    {
                        logLine($"[Reimagined] uiFocusDiff: (no changes vs prevScene='{s_lastUiFocusScene ?? "?"}')");
                    }
                }
                else
                {
                    logLine("[Reimagined] uiFocusDiff: (baseline)");
                }

                // Emit items (stable order).
                foreach (var key in UiFocusKeyOrder())
                {
                    if (snap.TryGetValue(key, out var it))
                        logLine($"[Reimagined] uiFocus: {FormatUiFocusItem(it)}");
                }
                // If we ever add more keys than order list, still print them.
                foreach (var kv in snap)
                {
                    if (!UiFocusKeyOrderSet.Contains(kv.Key))
                        logLine($"[Reimagined] uiFocus: {FormatUiFocusItem(kv.Value)}");
                }

                s_lastUiFocus = snap;
                s_lastUiFocusScene = sceneName;
            }

            private static string FormatUiFocusItem(UiFocusItem it)
            {
                string exist = it.Exists ? "Y" : "N";
                string aS = it.Exists ? (it.ActiveSelf ? "1" : "0") : "-";
                string aH = it.Exists ? (it.ActiveInHierarchy ? "1" : "0") : "-";
                string cc = it.Exists ? it.ChildCount.ToString() : "-";
                string extra = string.IsNullOrEmpty(it.Extra) ? "" : $" {it.Extra}";
                return $"{it.Path} exist={exist} aS={aS} aH={aH} child={cc}{extra}";
            }

            private static Dictionary<string, UiFocusItem> CaptureUiFocusSnapshot(out string sceneName)
            {
                sceneName = "?";
                var dict = new Dictionary<string, UiFocusItem>(32);

                try
                {
                    var scene = SceneManager.GetActiveScene();
                    sceneName = scene.name;
                }
                catch { /* ignore */ }

                // Root probes (names only; stable).
                var roots = SafeGetSceneRoots();
                AddRootProbe(dict, roots, "Canvas_UI");
                AddRootProbe(dict, roots, "EventSystem");
                AddRootProbe(dict, roots, "Canvas");
                AddRootProbe(dict, roots, "Main Camera");

                var canvasUi = FindRootByName(roots, "Canvas_UI");
                if (canvasUi != null)
                {
                    // Command menu anchors.
                    AddPathProbe(dict, canvasUi, "Canvas_UI/fieldUI");
                    AddPathProbe(dict, canvasUi, "Canvas_UI/campUIBase");
                    AddPathProbe(dict, canvasUi, "Canvas_UI/campUIBase/campUI");
                    AddPathProbe(dict, canvasUi, "Canvas_UI/campUIBase/campUI/campMenu");

                    // "Interact" prompt / button guide: names vary, so we include buttonguide root + a best-effort descendant.
                    var bg = AddPathProbe(dict, canvasUi, "Canvas_UI/buttonguide01");
                    if (bg != null)
                    {
                        // Common sub-anchors under buttonguide.
                        AddPathProbe(dict, canvasUi, "Canvas_UI/buttonguide01/guide");
                        AddPathProbe(dict, canvasUi, "Canvas_UI/buttonguide01/talkUI");

                        var sw = FindDescendantNameContains(bg.transform, "Switch", maxNodes: 256);
                        if (sw != null)
                            AddExactObjectProbe(dict, "Canvas_UI/buttonguide01/<Switch*>", sw.gameObject);

                        // Many interact prompts are expressed via a bmenuset* object (often cloned).
                        var bm = FindDescendantNameStartsWith(bg.transform, "bmenuset", maxNodes: 256);
                        if (bm != null)
                            AddExactObjectProbe(dict, "Canvas_UI/buttonguide01/<bmenuset*>", bm.gameObject);
                    }

                    // Facility / terminal-ish UI anchors (best effort; existence is informative).
                    AddPathProbe(dict, canvasUi, "Canvas_UI/terminalUI");
                    AddPathProbe(dict, canvasUi, "Canvas_UI/saveUI");
                    AddPathProbe(dict, canvasUi, "Canvas_UI/cathedralUI");
                    AddPathProbe(dict, canvasUi, "Canvas_UI/shopUI");

                    // Some UIs are spawned as clones.
                    AddFirstDescendantPrefixProbe(dict, canvasUi.transform, "Canvas_UI/<saveUI*>", "saveUI", maxNodes: 512);
                    AddFirstDescendantPrefixProbe(dict, canvasUi.transform, "Canvas_UI/<terminal*>", "terminal", maxNodes: 512);
                    AddFirstDescendantPrefixProbe(dict, canvasUi.transform, "Canvas_UI/<cathedral*>", "cathedral", maxNodes: 512);
                }

                // Debug menu: scene-object presence is already used elsewhere, but we also record a crude signal.
                bool goVisible = IsDebugMenuVisibleBySceneGameObject(out string goDiag);
                dict["dbg/goVisible"] = new UiFocusItem
                {
                    Path = "dbg/goVisible",
                    Exists = true,
                    ActiveSelf = goVisible,
                    ActiveInHierarchy = goVisible,
                    ChildCount = 0,
                    Extra = $"diag='{goDiag}'"
                };

                return dict;
            }

            private static readonly HashSet<string> UiFocusKeyOrderSet = new HashSet<string>(UiFocusKeyOrder());

            private static IEnumerable<string> UiFocusKeyOrder()
            {
                yield return "root/Canvas_UI";
                yield return "root/EventSystem";
                yield return "root/Canvas";
                yield return "root/Main Camera";

                yield return "Canvas_UI/fieldUI";
                yield return "Canvas_UI/campUIBase";
                yield return "Canvas_UI/campUIBase/campUI";
                yield return "Canvas_UI/campUIBase/campUI/campMenu";
                yield return "Canvas_UI/buttonguide01";
                yield return "Canvas_UI/buttonguide01/guide";
                yield return "Canvas_UI/buttonguide01/talkUI";
                yield return "Canvas_UI/buttonguide01/<Switch*>";
                yield return "Canvas_UI/buttonguide01/<bmenuset*>";

                yield return "Canvas_UI/terminalUI";
                yield return "Canvas_UI/saveUI";
                yield return "Canvas_UI/cathedralUI";
                yield return "Canvas_UI/shopUI";
                yield return "Canvas_UI/<saveUI*>";
                yield return "Canvas_UI/<terminal*>";
                yield return "Canvas_UI/<cathedral*>";

                yield return "dbg/goVisible";
            }

            private static List<GameObject> SafeGetSceneRoots()
            {
                var roots = new List<GameObject>(64);
                try
                {
                    var scene = SceneManager.GetActiveScene();
	                    // Some Unity reference surfaces only expose the List<> overload.
	                    // Populate 'roots' via the overload to avoid relying on the GameObject[] return variant.
	                    roots.Clear();
	                    scene.GetRootGameObjects(roots);
                }
                catch
                {
                    // fallback: try common root search
                    try
                    {
                        var go = GameObject.Find("Canvas_UI");
                        if (go != null) roots.Add(go);
                    }
                    catch { /* ignore */ }
                }
                return roots;
            }

            private static GameObject? FindRootByName(List<GameObject> roots, string name)
            {
                foreach (var r in roots)
                {
                    if (r != null && r.name == name)
                        return r;
                }
                return null;
            }

            private static void AddRootProbe(Dictionary<string, UiFocusItem> dict, List<GameObject> roots, string name)
            {
                var go = FindRootByName(roots, name);
                AddExactObjectProbe(dict, $"root/{name}", go);
            }

            private static GameObject? AddPathProbe(Dictionary<string, UiFocusItem> dict, GameObject canvasUiRoot, string fullPath)
            {
                // fullPath is expected to begin with "Canvas_UI/...".
                string[] parts = fullPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return null;
                if (parts[0] != "Canvas_UI")
                    return null;

                Transform cur = canvasUiRoot.transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = FindDirectChildByName(cur, parts[i]);
                    if (next == null)
                    {
                        AddExactObjectProbe(dict, fullPath, null);
                        return null;
                    }
                    cur = next;
                }

                AddExactObjectProbe(dict, fullPath, cur.gameObject);
                return cur.gameObject;
            }

            private static void AddFirstDescendantPrefixProbe(Dictionary<string, UiFocusItem> dict, Transform root, string probeKey, string namePrefix, int maxNodes)
            {
                var found = FindDescendantNameStartsWith(root, namePrefix, maxNodes);
                AddExactObjectProbe(dict, probeKey, found != null ? found.gameObject : null);
            }

            private static Transform? FindDirectChildByName(Transform parent, string name)
            {
                try
                {
                    int c = parent.childCount;
                    for (int i = 0; i < c; i++)
                    {
                        var ch = parent.GetChild(i);
                        if (ch != null && ch.name == name)
                            return ch;
                    }
                }
                catch { /* ignore */ }
                return null;
            }

            private static Transform? FindDescendantNameContains(Transform root, string contains, int maxNodes)
            {
                try
                {
                    var q = new Queue<Transform>();
                    q.Enqueue(root);
                    int visited = 0;
                    while (q.Count > 0 && visited < maxNodes)
                    {
	                        // Queue<T> can hold null references; guard to avoid nullable flow warnings.
	                        Transform? tt = q.Dequeue();
	                        if (tt == null)
	                            continue;
	                        visited++;
	                        if (tt.name != null && tt.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
	                            return tt;
                        int c = 0;
                        try { c = tt.childCount; } catch { c = 0; }
                        for (int i = 0; i < c; i++)
                        {
                            Transform? ch = null;
                            try { ch = tt.GetChild(i); } catch { ch = null; }
                            if (ch != null)
                                q.Enqueue(ch);
                        }
                    }
                }
                catch { /* ignore */ }
                return null;
            }

            private static Transform? FindDescendantNameStartsWith(Transform root, string prefix, int maxNodes)
            {
                try
                {
                    var q = new Queue<Transform>();
                    q.Enqueue(root);
                    int visited = 0;
                    while (q.Count > 0 && visited < maxNodes)
                    {
	                        // Queue<T> can hold null references; guard to avoid nullable flow warnings.
	                        Transform? tt = q.Dequeue();
	                        if (tt == null)
	                            continue;
	                        visited++;
	                        if (tt.name != null && tt.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
	                            return tt;
                        int c = 0;
                        try { c = tt.childCount; } catch { c = 0; }
                        for (int i = 0; i < c; i++)
                        {
                            Transform? ch = null;
                            try { ch = tt.GetChild(i); } catch { ch = null; }
                            if (ch != null)
                                q.Enqueue(ch);
                        }
                    }
                }
                catch { /* ignore */ }
                return null;
            }

            private static void AddExactObjectProbe(Dictionary<string, UiFocusItem> dict, string key, GameObject? go)
            {
                if (go == null)
                {
                    dict[key] = new UiFocusItem { Path = key, Exists = false, ActiveSelf = false, ActiveInHierarchy = false, ChildCount = 0, Extra = "" };
                    return;
                }

                bool aS = false, aH = false;
                int cc = 0;
                try { aS = go.activeSelf; } catch { aS = false; }
                try { aH = go.activeInHierarchy; } catch { aH = false; }
                try { cc = go.transform != null ? go.transform.childCount : 0; } catch { cc = 0; }

                dict[key] = new UiFocusItem
                {
                    Path = key,
                    Exists = true,
                    ActiveSelf = aS,
                    ActiveInHierarchy = aH,
                    ChildCount = cc,
                    Extra = $"name='{go.name}'"
                };
            }
        }
    }
}
