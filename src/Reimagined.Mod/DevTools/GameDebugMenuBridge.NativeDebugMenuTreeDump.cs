#nullable enable
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Game-native debug menu (cmpTest) tree/state dump
            // =========================================================

            internal static void HotkeyDumpNativeDebugMenuTree()
            {
                string path = MakeDumpPath("native_debug_menu_tree");
                MelonLogger.Msg($"[Reimagined] Native debug menu tree dump -> {path}");

                try
                {
                    using var w = new StreamWriter(path, append: false, Encoding.UTF8);

                    w.WriteLine("=== Native Debug Menu Tree Dump (cmpTest) ===");
                    w.WriteLine("dumpSchema=NativeDebugMenuTree v2");
                    w.WriteLine($"timeLocal={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    w.WriteLine($"timeUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}  ticksUtc={DateTime.UtcNow.Ticks}");
                    w.WriteLine();

                    Type? cmpTest = FindTypeInLoadedAssemblies("Il2Cpp.cmpTest");
                    if (cmpTest == null)
                    {
                        w.WriteLine("ERROR: Il2Cpp.cmpTest type not found in loaded assemblies.");
                        w.WriteLine("hint=If this persists, verify tools/references has the correct Assembly-CSharp mapping and that the game has finished loading.");
                        return;
                    }

                    w.WriteLine($"cmpTest.type={cmpTest.FullName}");
                    w.WriteLine();

                    // Best-effort: ensure cmpTest has been initialized so root lists exist.
                    WriteCmpTestInitStatusNoSideEffects(cmpTest, w);
                    w.WriteLine();

                    DumpCmpTestStatics(cmpTest, w);
                    w.WriteLine();
                    // Additional signal scans: look for id-like and selection-like statics beyond the hard-coded list.
                    DumpCmpTestSignalScan(cmpTest, w);
                    w.WriteLine();

                    // Dump cursorInfo internals so we can map 'which entry is highlighted' for each section.
                    DumpCmpTestCursorInfoDetails(cmpTest, w);
                    w.WriteLine();

                    // Procedural list resolution (ITEM/SKILL pages): try to map current highlight to datItemName/datSkillName.
                    DumpNativeDebugMenuProceduralResolution(cmpTest, w);
                    w.WriteLine();

                    object? rootList = null;
                    if (!TryReadStaticMember(cmpTest, "cmpDbgRootList", out rootList) || rootList == null)
                    {
                        // Some builds use gRootList as the canonical root.
                        TryReadStaticMember(cmpTest, "gRootList", out rootList);
                    }

                    if (rootList == null)
                    {
                        w.WriteLine("ERROR: cmpDbgRootList/gRootList is null.");
                        w.WriteLine("hint=Try pressing F1 once to open/initialize the debug menu, then dump again.");
                        return;
                    }

                    int rootLen = GetIl2CppRefArrayLength(rootList);
                    w.WriteLine($"rootList.type={rootList.GetType().FullName}");
                    w.WriteLine($"rootList.len={rootLen}");
                    w.WriteLine();

                    w.WriteLine("--- Root Entries ---");
                    w.WriteLine("idx | word | Para | hasFunc | nextSize");
                    w.WriteLine("---------------------------------------");

                    for (int i = 0; i < rootLen; i++)
                    {
                        object? it = GetIl2CppRefArrayItem(rootList, i);
                        string word = Safe(it != null ? (GetStringProp(it, "word") ?? "") : "");
                        int para = GetIntProp(it, "Para");
                        bool hasFunc = it != null && GetProp(it, "func") != null;
                        int nextSize = GetIntProp(it, "nextSize");

                        w.WriteLine($"{i,3} | {word} | {para} | {(hasFunc ? "yes" : "no"),6} | {nextSize}");
                    }

                    w.WriteLine();
                    w.WriteLine("--- Expanded Submenus (1 level) ---");

                    for (int i = 0; i < rootLen; i++)
                    {
                        object? it = GetIl2CppRefArrayItem(rootList, i);
                        string word = Safe(it != null ? (GetStringProp(it, "word") ?? "") : "");
                        int nextSize = GetIntProp(it, "nextSize");
                        object? nextList = it != null ? GetProp(it, "nextList") : null;

                        if (nextList == null || nextSize <= 0)
                            continue;

                        // Heuristic: only expand roots likely to be useful. This keeps dumps readable.
                        if (!ShouldExpandRoot(word))
                            continue;

                        int subLen = GetIl2CppRefArrayLength(nextList);
                        w.WriteLine();
                        w.WriteLine($"[root {i}] {word}  nextSize={nextSize}  nextList.len={subLen}");
                        w.WriteLine("  idx | word | Para | hasFunc | nextSize");
                        w.WriteLine("  ---------------------------------------");

                        int max = Math.Min(subLen, 80); // keep sane; we can widen later if needed
                        for (int j = 0; j < max; j++)
                        {
                            object? sub = GetIl2CppRefArrayItem(nextList, j);
                            string w2 = Safe(sub != null ? (GetStringProp(sub, "word") ?? "") : "");
                            int p2 = GetIntProp(sub, "Para");
                            bool f2 = sub != null && GetProp(sub, "func") != null;
                            int n2 = GetIntProp(sub, "nextSize");

                            w.WriteLine($"  {j,3} | {w2} | {p2} | {(f2 ? "yes" : "no"),6} | {n2}");
                        }

                        if (subLen > max)
                            w.WriteLine($"  ... truncated ({subLen - max} more)");
                    }

                    w.WriteLine();
                    w.WriteLine();
                    DumpCmpTestDynamicLists(cmpTest, w);
                    DumpCmpTestStaticCensusDiff(cmpTest, w);

                    w.WriteLine("--- Notes / How to use this ---");
                    w.WriteLine("1) Open the debug menu with F1 at least once (so cmpTest initializes).");
                    w.WriteLine("2) Dump again while the debug menu is OPEN to see cursor fields (gCursorInfo/gCursorSection) change meaningfully.");
                    w.WriteLine("3) Look for root/sub entries that contain 'Devil', 'Skill', 'Item', etc; those are prime candidates for re-use.");
                    w.WriteLine("4) Once we identify a target submenu, the next pass can dump *that* submenu deeper, and/or dump gTargetModel.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Reimagined] Native debug menu tree dump failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            private static bool ShouldExpandRoot(string word)
            {
                if (string.IsNullOrEmpty(word))
                    return false;

                // Keep broad on purpose; these strings are typically short English/Japanese tokens.
                string w = word.ToLowerInvariant();
                return w.Contains("devil")
                    || w.Contains("skill")
                    || w.Contains("item")
                    || w.Contains("stock")
                    || w.Contains("param")
                    || w.Contains("status")
                    || w.Contains("money")
                    || w.Contains("moon")
                    || w.Contains("recover")
                    || w.Contains("unit")
                    || w.Contains("position")
                    || w.Contains("reimagined");
            }

            private static void WriteCmpTestInitStatusNoSideEffects(Type cmpTest, TextWriter w)
{
    // IMPORTANT: This dump intentionally avoids invoking cmpTest init methods (cmpDbgInit/cmpDbgProcessStart/etc),
    // because those calls can reset the native debug menu state/cursor and can interfere with navigation.
    // If root lists are null, open the native debug menu with F1 once and re-dump.
    try
    {
        if (TryReadSByteStatic(cmpTest, "gInitFlag", out sbyte init))
            w.WriteLine($"cmpTest.initFlag={init} (dump_did_not_invoke_init)");
        else
            w.WriteLine("cmpTest.initFlag=(unavailable) (dump_did_not_invoke_init)");

        if (TryReadSByteStatic(cmpTest, "gActive2", out sbyte act2))
            w.WriteLine($"cmpTest.active2={act2}");
        if (TryReadSByteStatic(cmpTest, "gActive1", out sbyte act1))
            w.WriteLine($"cmpTest.active1={act1}");

        w.WriteLine("cmpTest.initAction=skipped_to_avoid_side_effects");
        w.WriteLine("hint=If rootList is null, press F1 to open/initialize the debug menu, keep it open, then dump again.");
    }
    catch (Exception ex)
    {
        w.WriteLine($"cmpTest.initStatus=exception {ex.GetType().Name}: {Safe(ex.Message)}");
    }
}
            private static int GetIntProp(object? obj, string propName, int fallback = 0)
            {
                if (obj == null)
                    return fallback;

                object? v;
                try
                {
                    v = GetProp(obj, propName);
                }
                catch
                {
                    return fallback;
                }

                if (v == null)
                    return fallback;

                try
                {
                    if (v is int i) return i;
                    if (v is short s) return s;
                    if (v is sbyte sb) return sb;
                    if (v is byte b) return b;
                    if (v is long l) return unchecked((int)l);
                    if (v is uint ui) return unchecked((int)ui);
                    if (v is ulong ul) return unchecked((int)ul);
                    if (v is Enum e) return Convert.ToInt32(e);
                    return Convert.ToInt32(v);
                }
                catch
                {
                    return fallback;
                }
            }

            private static void DumpCmpTestStatics(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- cmpTest Static State (best-effort) ---");

                DumpStaticSByte(cmpTest, w, "gInitFlag");
                DumpStaticSByte(cmpTest, w, "gActive1");
                DumpStaticSByte(cmpTest, w, "gActive2");
                DumpStaticSByte(cmpTest, w, "gTerminateFlag");

                DumpStaticInt(cmpTest, w, "gListNums");
                DumpCmpDbgPIDDetails(cmpTest, w);
                DumpStaticInt(cmpTest, w, "status_id");
                DumpStaticInt(cmpTest, w, "Skill_id");

                DumpStaticObjBrief(cmpTest, w, "gCursorSection");
                DumpStaticObjBrief(cmpTest, w, "gCursorInfo");
                DumpStaticObjBrief(cmpTest, w, "gTargetModel");
                DumpStaticObjBrief(cmpTest, w, "gWork");

                // Also show the "status list" which is commonly used by some pages.
                DumpStaticObjBrief(cmpTest, w, "cmpDbgStatusList");
                DumpStaticObjBrief(cmpTest, w, "cmpDbgListParam");
            }

            private static void DumpStaticSByte(Type t, TextWriter w, string name)
            {
                if (TryReadSByteStatic(t, name, out sbyte v))
                    w.WriteLine($"{name}={v}");
                else
                    w.WriteLine($"{name}=(unavailable)");
            }

            private static void DumpStaticInt(Type t, TextWriter w, string name)
            {
                if (TryReadIntStatic(t, name, out int v))
                    w.WriteLine($"{name}={v}");
                else
                    w.WriteLine($"{name}=(unavailable)");
            }

            private static void DumpCmpDbgPIDDetails(Type cmpTest, TextWriter w)
            {
                if (!TryReadStaticMember(cmpTest, "cmpDbgPID", out object? pidObj) || pidObj == null)
                {
                    w.WriteLine("cmpDbgPID=null");
                    return;
                }

                w.WriteLine($"cmpDbgPID.type={pidObj.GetType().FullName}");

                try
                {
                    if (TryGetInstanceMemberValue(pidObj, "id", out object? idObj) && idObj != null)
                        w.WriteLine($"cmpDbgPID.id={Safe(idObj.ToString() ?? "(null)")}");
                    else
                        w.WriteLine("cmpDbgPID.id=(unavailable)");

                    if (TryGetInstanceMemberValue(pidObj, "namehash", out object? nhObj) && nhObj != null)
                        w.WriteLine($"cmpDbgPID.namehash={Safe(nhObj.ToString() ?? "(null)")}");
                    else
                        w.WriteLine("cmpDbgPID.namehash=(unavailable)");

                    if (TryGetInstanceMemberValue(pidObj, "name", out object? nameObj) && nameObj != null)
                    {
                        string s = DecodeByteArrayBestEffort(nameObj, 64);
                        if (string.IsNullOrEmpty(s))
                            w.WriteLine("cmpDbgPID.name=<blank>");
                        else
                            w.WriteLine($"cmpDbgPID.name=\"{Safe(s)}\"");
                    }
                    else
                    {
                        w.WriteLine("cmpDbgPID.name=(unavailable)");
                    }
                }
                catch
                {
                    // keep tree dump resilient even if pid wrapper differs
                }
            }


            private static void DumpStaticObjBrief(Type t, TextWriter w, string name)
            {
                if (TryReadStaticMember(t, name, out object? v) && v != null)
                {
                    w.WriteLine($"{name}.type={v.GetType().FullName}");
                    w.WriteLine($"{name}.brief={Safe(ToBriefString(v))}");
                }
                else
                {
                    w.WriteLine($"{name}=(null/unavailable)");
                }
            }


            private static void DumpCmpTestSignalScan(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- cmpTest Signal Scan (auto-discovered statics) ---");
                w.WriteLine("note=This section scans static fields/properties for id-like and selection-like values so we can find Item_id/etc even if names differ between builds.");
                w.WriteLine();

                DumpStaticNumericByNameFilter(cmpTest, w,
                    header: "ID-like statics (*id*, *_id, *Id*)",
                    nameFilter: n =>
                    {
                        // Keep conservative: avoid 'guid' false positives.
                        string nl = n.ToLowerInvariant();
                        if (nl.Contains("guid"))
                            return false;
                        return nl.EndsWith("_id") || nl.Contains("id");
                    },
                    max: 60);

                w.WriteLine();

                DumpStaticNumericByNameFilter(cmpTest, w,
                    header: "Selection/cursor-like statics (*sel*, *cursor*, *idx*, *now*, *pos*)",
                    nameFilter: n =>
                    {
                        string nl = n.ToLowerInvariant();
                        return nl.Contains("sel") || nl.Contains("cursor") || nl.Contains("idx") || nl.Contains("now") || nl.Contains("pos");
                    },
                    max: 60);
            }

            
            private static void DumpCmpTestCursorInfoDetails(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- gCursorInfo Summary + Deep Dump (best-effort) ---");

                if (!TryReadStaticMember(cmpTest, "gCursorInfo", out object? arr) || arr == null)
                {
                    w.WriteLine("gCursorInfo=(null/unavailable)");
                    w.WriteLine("hint=Open the native debug menu (F1) and keep it open while dumping.");
                    return;
                }

                int len;
                try
                {
                    len = GetIl2CppRefArrayLength(arr);
                }
                catch (Exception ex)
                {
                    w.WriteLine($"gCursorInfo.type={arr.GetType().FullName}");
                    w.WriteLine($"gCursorInfo.len=ERROR {ex.GetType().Name}: {Safe(ex.Message)}");
                    return;
                }

                w.WriteLine($"gCursorInfo.type={arr.GetType().FullName}");
                w.WriteLine($"gCursorInfo.len={len}");

                // Current active cursor section (which gCursorInfo entry is controlling highlight right now).
                int activeSection = -1;
                if (TryReadSByteStatic(cmpTest, "gCursorSection", out sbyte sec))
                    activeSection = sec;

                w.WriteLine($"gCursorSection.brief={activeSection}");

                // We want to resolve "which row is highlighted" for each cursor section.
                // cmpTest uses a nested CursorPos struct; in this build, CursorPos.Index is often 0 and CursorPos.Shift moves.
                // Empirically, (Shift + Index) behaves like the "effective selected row" within the list.
                int GetCursorInt(object? cursorPos, string name, int fallback = 0)
                    => GetIntProp(cursorPos, name, fallback);

                // Gather which list lengths appear right now so we only scan statics that can actually help.
                var neededLens = new HashSet<int>();
                var cursorPosObjs = new object?[len];
                var cursorPosListNums = new int[len];
                var cursorPosSel = new int[len];

                for (int i = 0; i < len; i++)
                {
                    object? ci = null;
                    try { ci = GetIl2CppRefArrayItem(arr, i); }
                    catch { ci = null; }

                    object? pos = null;
                    if (ci != null)
                    {
                        try { pos = GetProp(ci, "CursorPos"); } catch { pos = null; }
                    }

                    cursorPosObjs[i] = pos;

                    int listNums = GetCursorInt(pos, "ListNums", 0);
                    int idx = GetCursorInt(pos, "Index", 0);
                    int shift = GetCursorInt(pos, "Shift", 0);

                    int sel = shift + idx;

                    cursorPosListNums[i] = listNums;
                    cursorPosSel[i] = sel;

                    if (listNums > 0)
                        neededLens.Add(listNums);
                }

                // Build a best-effort map: (list length) -> (static name, list object)
                // so we can resolve listIdx -> word/Para for whatever page is active (skills, items, params, etc).
                var listByLen = BuildDbgListCandidatesByLength(cmpTest, neededLens);

                w.WriteLine();
                w.WriteLine("cursorSummary.cols=i active listNums shiftMax index shift sel drawShift effFlag resolvedWord (listName)");
                for (int i = 0; i < len; i++)
                {
                    object? pos = cursorPosObjs[i];
                    int listNums = cursorPosListNums[i];
                    int sel = cursorPosSel[i];

                    int shiftMax = GetCursorInt(pos, "ShiftMax", 0);
                    int idx = GetCursorInt(pos, "Index", 0);
                    int shift = GetCursorInt(pos, "Shift", 0);
                    int drawShift = GetCursorInt(pos, "DrawShift", 0);
                    int effFlag = GetCursorInt(pos, "EffFlag", 0);

                    string activeMark = (i == activeSection) ? "*" : "";

                    string resolved = "";
                    if (listNums > 0 && listByLen.TryGetValue(listNums, out var cand) && cand.listObj != null)
                    {
                        try
                        {
                            int clamp = sel;
                            if (clamp < 0) clamp = 0;
                            if (clamp >= listNums) clamp = listNums - 1;

                            object? it = GetIl2CppRefArrayItem(cand.listObj, clamp);
                            string word = (it != null) ? (GetStringProp(it, "word") ?? "") : "";
                            int para = (it != null) ? GetIntProp(it, "Para") : 0;

                            resolved = $"{Safe(word)} [Para={para}] ({cand.name})";
                        }
                        catch
                        {
                            resolved = $"(resolve failed) ({cand.name})";
                        }
                    }

                    w.WriteLine($"  i={i}{activeMark} listNums={listNums} shiftMax={shiftMax} index={idx} shift={shift} sel={sel} drawShift={drawShift} effFlag={effFlag}  resolved={resolved}");
                }

                w.WriteLine();
                w.WriteLine("--- gCursorInfo Deep Dump (raw numeric members) ---");
                for (int i = 0; i < len; i++)
                {
                    object? ci = null;
                    try { ci = GetIl2CppRefArrayItem(arr, i); }
                    catch (Exception ex)
                    {
                        w.WriteLine($"[{i}] ERROR reading cursorInfo element: {ex.GetType().Name}: {Safe(ex.Message)}");
                        continue;
                    }

                    if (ci == null)
                    {
                        w.WriteLine($"[{i}] (null)");
                        continue;
                    }

                    w.WriteLine($"[{i}] type={ci.GetType().FullName}");

                    // Dump numeric members on the cursorInfo object itself, plus one level of nested objects.
                    // Keep the active section more verbose; others get a smaller cap to reduce noise.
                    int cap = (i == activeSection) ? 60 : 30;
                    DumpObjectNumericMembers(ci, w, indent: "  ", depth: 0, maxDepth: 1, maxLines: cap);

                    // Also dump CursorPos fields explicitly (these are the most actionable for selection mapping).
                    object? pos = cursorPosObjs[i];
                    if (pos != null)
                    {
                        int listNums = cursorPosListNums[i];
                        int sel = cursorPosSel[i];

                        int inv = GetCursorInt(pos, "InvisibleListNums", 0);
                        int shiftMax = GetCursorInt(pos, "ShiftMax", 0);
                        int idx = GetCursorInt(pos, "Index", 0);
                        int shift = GetCursorInt(pos, "Shift", 0);
                        int drawShift = GetCursorInt(pos, "DrawShift", 0);
                        int timer = GetCursorInt(pos, "Timer", 0);
                        int atimer = GetCursorInt(pos, "ActiveTimer", 0);
                        int effFlag = GetCursorInt(pos, "EffFlag", 0);

                        w.WriteLine($"  CursorPos.summary: ListNums={listNums} InvisibleListNums={inv} Index={idx} Shift={shift} DrawShift={drawShift} ShiftMax={shiftMax} sel(Shift+Index)={sel} Timer={timer} ActiveTimer={atimer} EffFlag={effFlag}");
                    }

                    w.WriteLine();
                }

                w.WriteLine("hint=The cursorSummary.sel field is our current best guess for the highlighted row index (0-based) within that cursor's list.");
                w.WriteLine("hint=When listNums is large (e.g., skills/items), ShiftMax may be the visible window size; sel can still track highlight if Index remains 0.");
            }

            private struct DbgListCandidate
            {
                public int score;
                public string name;
                public object? listObj;
            }

            private static Dictionary<int, DbgListCandidate> BuildDbgListCandidatesByLength(Type cmpTest, HashSet<int> neededLens)
            {
                var dict = new Dictionary<int, DbgListCandidate>();

                // Always include the canonical ones if present.
                TryAddDbgListCandidate(dict, cmpTest, "gRootList", neededLens);
                TryAddDbgListCandidate(dict, cmpTest, "cmpDbgRootList", neededLens);
                TryAddDbgListCandidate(dict, cmpTest, "cmpDbgListParam", neededLens);
                TryAddDbgListCandidate(dict, cmpTest, "cmpDbgStatusList", neededLens);
                TryAddDbgListCandidate(dict, cmpTest, "cmpDbgNowList", neededLens);
                TryAddDbgListCandidate(dict, cmpTest, "gNowList", neededLens);
                TryAddDbgListCandidate(dict, cmpTest, "gSubList", neededLens);

                // Auto-discover any other Il2CppReferenceArray<cmpDbgList_s> statics that match lengths we saw in gCursorInfo.
                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                foreach (var p in cmpTest.GetProperties(FLAGS))
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    TryAddDbgListCandidateFromMember(dict, neededLens, cmpTest, p.Name);
                }

                foreach (var f in cmpTest.GetFields(FLAGS))
                {
                    TryAddDbgListCandidateFromMember(dict, neededLens, cmpTest, f.Name);
                }

                return dict;
            }

            private static void TryAddDbgListCandidate(Dictionary<int, DbgListCandidate> dict, Type cmpTest, string staticName, HashSet<int> neededLens)
            {
                if (!TryReadStaticMember(cmpTest, staticName, out object? v) || v == null)
                    return;

                TryAddDbgListCandidateValue(dict, neededLens, staticName, v);
            }

            private static void TryAddDbgListCandidateFromMember(Dictionary<int, DbgListCandidate> dict, HashSet<int> neededLens, Type cmpTest, string memberName)
            {
                if (!TryReadStaticMember(cmpTest, memberName, out object? v) || v == null)
                    return;

                TryAddDbgListCandidateValue(dict, neededLens, memberName, v);
            }

            private static void TryAddDbgListCandidateValue(Dictionary<int, DbgListCandidate> dict, HashSet<int> neededLens, string name, object v)
            {
                // We only care about Il2CppReferenceArray<> that contains cmpDbgList_s entries.
                string tn = v.GetType().FullName ?? "";
                if (!tn.Contains("Il2CppReferenceArray"))
                    return;

                Type? elemType = null;
                try
                {
                    if (v.GetType().IsGenericType)
                        elemType = v.GetType().GetGenericArguments()[0];
                }
                catch
                {
                    elemType = null;
                }

                string elemName = elemType?.FullName ?? "";
                if (elemName.Length == 0)
                {
                    // Fallback: parse from FullName if it is embedded.
                    elemName = tn;
                }

                if (!elemName.Contains("cmpDbgList_s"))
                    return;

                int len;
                try { len = GetIl2CppRefArrayLength(v); }
                catch { return; }

                if (len <= 0)
                    return;

                if (neededLens.Count > 0 && !neededLens.Contains(len))
                    return;

                int score = ScoreDbgListCandidateName(name);

                if (dict.TryGetValue(len, out var prior))
                {
                    if (score <= prior.score)
                        return;
                }

                dict[len] = new DbgListCandidate { score = score, name = name, listObj = v };
            }

            private static int ScoreDbgListCandidateName(string name)
            {
                string n = name.ToLowerInvariant();
                int s = 0;

                if (n.Contains("now")) s += 100;
                if (n.Contains("cur")) s += 90;
                if (n.Contains("sel")) s += 80;
                if (n.Contains("skill")) s += 70;
                if (n.Contains("item")) s += 60;
                if (n.Contains("devil")) s += 50;
                if (n.Contains("param")) s += 40;
                if (n.Contains("status")) s += 30;
                if (n.Contains("list")) s += 10;

                // Prefer canonical "cmpDbg" naming slightly.
                if (n.Contains("cmpdbg")) s += 5;

                return s;
            }



            private static List<string>? TryGetDbgListWords(Type cmpTest, string staticName)
            {
                if (!TryReadStaticMember(cmpTest, staticName, out object? arr) || arr == null)
                    return null;

                int len;
                try { len = GetIl2CppRefArrayLength(arr); }
                catch { return null; }

                var words = new List<string>(len);

                for (int i = 0; i < len; i++)
                {
                    object? entry = null;
                    try { entry = GetIl2CppRefArrayItem(arr, i); } catch { /* ignore */ }

                    if (entry == null)
                    {
                        words.Add("(null)");
                        continue;
                    }

                    // cmpDbgList_s has: word, Para, Func, nextSize
                    string w = GetStringProp(entry, "word") ?? "(no-word)";
                    words.Add(w);
                }

                return words;
            }

            private static bool TryGetIntMember(object obj, string memberName, out int value)
            {
                value = 0;

                if (obj == null)
                    return false;

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                try
                {
                    var p = obj.GetType().GetProperty(memberName, FLAGS);
                    if (p != null && p.GetIndexParameters().Length == 0)
                    {
                        object? v = p.GetValue(obj, null);
                        if (v is int i) { value = i; return true; }
                        if (v is short s) { value = s; return true; }
                        if (v is sbyte sb) { value = sb; return true; }
                        if (v is byte b) { value = b; return true; }
                    }
                }
                catch { /* ignore */ }

                try
                {
                    var f = obj.GetType().GetField(memberName, FLAGS);
                    if (f != null)
                    {
                        object? v = f.GetValue(obj);
                        if (v is int i) { value = i; return true; }
                        if (v is short s) { value = s; return true; }
                        if (v is sbyte sb) { value = sb; return true; }
                        if (v is byte b) { value = b; return true; }
                    }
                }
                catch { /* ignore */ }

                return false;
            }

            private static void DumpStaticNumericByNameFilter(Type t, TextWriter w, string header, Func<string, bool> nameFilter, int max)
            {
                w.WriteLine(header);
                w.WriteLine("name | kind | type | value");
                w.WriteLine("------------------------------------------------------------");

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                int count = 0;

                // Properties first (often used for Il2Cpp field wrappers).
                foreach (var p in t.GetProperties(FLAGS))
                {
                    if (count >= max)
                        break;

                    if (p.GetIndexParameters().Length != 0)
                        continue;

                    string name = p.Name;
                    if (!nameFilter(name))
                        continue;

                    Type pt = p.PropertyType;
                    if (!IsScalarNumericOrBool(pt))
                        continue;

                    object? v = null;
                    try { v = p.GetValue(null, null); } catch { /* ignore */ }

                    if (TryFormatScalar(v, out string s))
                    {
                        w.WriteLine($"{name} | prop | {pt.Name} | {s}");
                        count++;
                    }
                }

                foreach (var f in t.GetFields(FLAGS))
                {
                    if (count >= max)
                        break;

                    string name = f.Name;
                    if (!nameFilter(name))
                        continue;

                    Type ft = f.FieldType;
                    if (!IsScalarNumericOrBool(ft))
                        continue;

                    object? v = null;
                    try { v = f.GetValue(null); } catch { /* ignore */ }

                    if (TryFormatScalar(v, out string s))
                    {
                        w.WriteLine($"{name} | field | {ft.Name} | {s}");
                        count++;
                    }
                }

                if (count == 0)
                    w.WriteLine("(none)");
                else if (count >= max)
                    w.WriteLine($"... truncated (showing first {max})");
            }

            private static void DumpObjectNumericMembers(object obj, TextWriter w, string indent, int depth, int maxDepth, int maxLines)
            {
                if (obj == null)
                    return;

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                int lines = 0;

                // First: scalar numeric/bool/string members on this object.
                foreach (var p in obj.GetType().GetProperties(FLAGS))
                {
                    if (lines >= maxLines)
                        break;

                    if (p.GetIndexParameters().Length != 0)
                        continue;

                    string name = p.Name;

                    object? v = null;
                    try { v = p.GetValue(obj, null); } catch { continue; }

                    if (TryFormatScalar(v, out string s))
                    {
                        w.WriteLine($"{indent}{name}={s}");
                        lines++;
                        continue;
                    }

                    // One-level deep: if this property looks like it might contain cursor state, dump its numeric members too.
                    if (depth < maxDepth && v != null)
                    {
                        string nl = name.ToLowerInvariant();
                        if (nl.Contains("cursor") || nl.Contains("curs") || nl.Contains("now") || nl.Contains("sel"))
                        {
                            w.WriteLine($"{indent}{name}.type={v.GetType().FullName}");
                            lines++;
                            DumpObjectNumericMembers(v, w, indent + "  ", depth + 1, maxDepth, Math.Max(0, maxLines - lines));
                        }
                    }
                }

                foreach (var f in obj.GetType().GetFields(FLAGS))
                {
                    if (lines >= maxLines)
                        break;

                    string name = f.Name;

                    object? v = null;
                    try { v = f.GetValue(obj); } catch { continue; }

                    if (TryFormatScalar(v, out string s))
                    {
                        w.WriteLine($"{indent}{name}={s}");
                        lines++;
                        continue;
                    }

                    if (depth < maxDepth && v != null)
                    {
                        string nl = name.ToLowerInvariant();
                        if (nl.Contains("cursor") || nl.Contains("curs") || nl.Contains("now") || nl.Contains("sel"))
                        {
                            w.WriteLine($"{indent}{name}.type={v.GetType().FullName}");
                            lines++;
                            DumpObjectNumericMembers(v, w, indent + "  ", depth + 1, maxDepth, Math.Max(0, maxLines - lines));
                        }
                    }
                }

                if (lines == 0)
                    w.WriteLine($"{indent}(no scalar numeric members found)");
                else if (lines >= maxLines)
                    w.WriteLine($"{indent}... truncated");
            }

            private static bool IsScalarNumericOrBool(Type t)
            {
                if (t.IsEnum)
                    return true;

                return t == typeof(bool)
                    || t == typeof(byte)
                    || t == typeof(sbyte)
                    || t == typeof(short)
                    || t == typeof(ushort)
                    || t == typeof(int)
                    || t == typeof(uint)
                    || t == typeof(long)
                    || t == typeof(ulong);
            }

            private static bool TryFormatScalar(object? v, out string s)
            {
                s = "";

                if (v == null)
                {
                    s = "null";
                    return true;
                }

                try
                {
                    if (v is string str)
                    {
                        if (str.Length > 120)
                            str = str.Substring(0, 120) + "...";
                        s = "\"" + Safe(str) + "\"";
                        return true;
                    }

                    if (v is bool b)
                    {
                        s = b ? "true" : "false";
                        return true;
                    }

                    if (v is Enum e)
                    {
                        s = e + " (" + Convert.ToInt64(e) + ")";
                        return true;
                    }

                    if (v is byte or sbyte or short or ushort or int or uint or long or ulong)
                    {
                        s = Convert.ToInt64(v).ToString();
                        return true;
                    }

                    // Some Il2Cpp wrappers return numeric values boxed as System.Object.
                    var vt = v.GetType();
                    if (IsScalarNumericOrBool(vt))
                    {
                        s = Safe(v.ToString() ?? "");
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryReadStaticMember(Type t, string name, out object? value)
            {
                value = null;

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                try
                {
                    var p = t.GetProperty(name, FLAGS);
                    if (p != null && p.GetIndexParameters().Length == 0)
                    {
                        value = p.GetValue(null, null);
                        return true;
                    }

                    var f = t.GetField(name, FLAGS);
                    if (f != null)
                    {
                        value = f.GetValue(null);
                        return true;
                    }

                    return false;
                }
                catch
                {
                    value = null;
                    return false;
                }
            }

            private static string ToBriefString(object v)
            {
                try
                {
                    string s = v.ToString() ?? "";
                    if (s.Length > 240)
                        return s.Substring(0, 240) + "...";
                    return s;
                }
                catch
                {
                    return v.GetType().Name;
                }
            }
        }
    }
}