#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass A39: Camp/Command-menu reflection surface dumper
            // =========================================================
            // Motivation:
            // We already can *observe* the vanilla command-menu (camp) state machine via seqTrace.
            // The remaining missing piece is reliably identifying the real Il2Cpp member names for:
            //   - the menu list (length, item entries)
            //   - the active cursor object
            //   - submenu routing objects / next lists
            // This dumper is intentionally "read-only" and only uses reflection.

            internal static void HotkeyDumpCampReflectionSurface()
            {
                string path = MakeDumpPath("camp_reflection_surface");
                MelonLogger.Msg($"[Reimagined] Camp reflection surface dump -> {path}");

                try
                {
                    using var w = new StreamWriter(path, append: false, Encoding.UTF8);
                    w.WriteLine("=== Camp / Command Menu Reflection Surface ===");
                    w.WriteLine("dumpSchema=CampReflSurface v10 (cmpCalc⇔StockInfo cursor mapping + canonical selection summary)");
                    w.WriteLine($"timeLocal={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    w.WriteLine($"timeUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}  ticksUtc={DateTime.UtcNow.Ticks}");
                    w.WriteLine();
                    // --- Devtools diagnostics (IL2CPP-safe component enumeration) ---
                    try
                    {
                        EnsureGetComponentsCache();
                        w.WriteLine($"dev.getComponentsOverloads(sysType={s_getComponentsSysType.Length}, il2cppType={s_getComponentsIl2CppType.Length}, il2cppArg={(s_il2cppComponentTypeArg != null)})");
                    }
                    catch { /* ignore */ }
                    w.WriteLine();


                    var cmpInit = FindTypeInLoadedAssemblies("Il2Cpp.cmpInit");
                    if (cmpInit == null)
                    {
                        w.WriteLine("ERROR: type Il2Cpp.cmpInit not found in loaded assemblies.");
                        w.WriteLine("(This usually means we're not in a scene where the camp/menu system is loaded.)");
                        return;
                    }

                    // --- Quick headline state (same signals we use in campProbe) ---
                    w.WriteLine("--- Headline state ---");
                    if (TryReadSByteStatic(cmpInit, "gProcessStat", out sbyte ps)) w.WriteLine($"cmpInit.gProcessStat={ps}");
                    else w.WriteLine("cmpInit.gProcessStat=?");

                    if (TryReadSByteStatic(cmpInit, "gExitState", out sbyte ex)) w.WriteLine($"cmpInit.gExitState={ex}");
                    else w.WriteLine("cmpInit.gExitState=?");

                    if (TryReadByteArrayHexStatic(cmpInit, "gCommandFlag", 32, out int cLen, out string cHex, out int cHash))
                        w.WriteLine($"cmpInit.gCommandFlag(len={cLen},hash={cHash})={cHex}");
                    else
                        w.WriteLine("cmpInit.gCommandFlag=?");

                    var seqType = FindTypeInLoadedAssemblies("Il2Cpp.dds3SequenceList");
                    if (seqType != null && TryCallIntStaticMethod(seqType, "CheckCamp", out int campGate))
                        w.WriteLine($"dds3SequenceList.CheckCamp()={campGate}  (empirically: 0=open, 1=closed)");
                    else
                        w.WriteLine("dds3SequenceList.CheckCamp()=?");

                    w.WriteLine();


                    // --- cmpInit menu string tables (best-effort) ---
                    // Highest ROI for mapping SeqInfo.Current indices -> actual labels the vanilla command menu uses.
                    // Dumped here so we can diff across contexts and correlate with seqTrace + snapshots.
                    w.WriteLine("--- cmpInit menu tables (best-effort) ---");
	                    TryDumpIl2CppArrayStatic(w, cmpInit, "gCmpRootMenuStr", 32);
	                    TryDumpLocalizedKeyArrayStatic(w, cmpInit, "gCmpRootMenuStr", 32);
	                    TryDumpIl2CppArrayStatic(w, cmpInit, "gCmpRootMenuSub", 64);
	                    TryDumpLocalizedKeyArrayStatic(w, cmpInit, "gCmpRootMenuSub", 64);
	                    TryDumpIl2CppArrayStatic(w, cmpInit, "gCmpItemRootStr", 16);
	                    TryDumpLocalizedKeyArrayStatic(w, cmpInit, "gCmpItemRootStr", 16);
	                    TryDumpIl2CppArrayStatic(w, cmpInit, "gCmpPartyRootStr", 32);
	                    TryDumpLocalizedKeyArrayStatic(w, cmpInit, "gCmpPartyRootStr", 32);
	                    TryDumpIl2CppArrayStatic(w, cmpInit, "gCmpHeartsRootStr", 32);
	                    TryDumpLocalizedKeyArrayStatic(w, cmpInit, "gCmpHeartsRootStr", 32);
                    w.WriteLine();
                    // --- cmpInit static surface (filtered) ---
                    w.WriteLine("--- cmpInit static members (filtered) ---");
                    DumpStaticMembersFiltered(
                        w,
                        cmpInit,
                        memberNameFilter: NameMatchesAnyToken,
                        tokens: new[] { "camp", "Camp", "menu", "Menu", "Index", "Flag", "g", "UI", "_camp", "CMP_" },
                        maxLines: 160);
                    w.WriteLine();

                    // --- CMP_GBWK and its SeqInfo surface ---
                    object? gbwkObj = null;
                    try
                    {
                        var m = FindStaticMember(cmpInit, "CMP_GBWK");
                        if (m != null)
                            gbwkObj = GetStaticMemberValue(m);
                    }
                    catch
                    {
                        gbwkObj = null;
                    }

                    if (gbwkObj != null)
                    {
                        w.WriteLine("--- cmpInit.CMP_GBWK (filtered members) ---");
                        DumpObjectMembersFiltered(
                            w,
                            label: "CMP_GBWK",
                            obj: gbwkObj,
                            tokens: new[] { "Seq", "seq", "Flag", "flag", "Camp", "camp", "Menu", "menu", "Cur", "cur", "Next", "Last", "Mes", "Time" },
                            maxLines: 220);

                        // Try to expand gbwk.SeqInfo specifically.
                        try
                        {
                            var seqInfoProp = gbwkObj.GetType().GetProperty("SeqInfo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var seqInfoObj = seqInfoProp?.GetValue(gbwkObj, null);
                            if (seqInfoObj != null)
                            {
                                w.WriteLine();
                                w.WriteLine("--- CMP_GBWK.SeqInfo (ALL members, capped) ---");
                                DumpObjectMembersAll(w, "SeqInfo", seqInfoObj, maxLines: 240);
                            }
                        }
                        catch
                        {
                            // ignore
                        }

	                        // Extra deep-reads that let us map selection+scroll in a generic way.
	                        w.WriteLine();
	                        w.WriteLine("--- CMP_GBWK cursor arrays (RootCI / WorkCI) ---");
	                        TryDumpCmpCursorInfoArrays(w, gbwkObj, maxEntriesPerList: 24);
	                        w.WriteLine();
	                        w.WriteLine("--- CMP_GBWK item list (ItemIdx -> name, best-effort) ---");
	                        TryDumpCmpItemIdx(w, gbwkObj, maxItems: 32);
	                        w.WriteLine();
	                        w.WriteLine("--- CMP_GBWK stock info (StockInfo/LocalStock, best-effort) ---");
	                        TryDumpCmpStockInfo(w, gbwkObj, maxPreviewEntries: 48);
	                        w.WriteLine();
	                        w.WriteLine("--- cmpCalc selection (read-only, best-effort) ---");
	                        var calcSel = TryGetCmpCalcSelectionBestEffort();
	                        TryDumpCmpCalcSelection(w, calcSel);
	                        w.WriteLine();
	                        w.WriteLine("--- Derived stock/party selection via CMP_GBWK.StockInfo (best-effort) ---");
	                        TryDumpDerivedStockSelectionFromCmp(w, gbwkObj, calcSel);
	                        w.WriteLine();
                        w.WriteLine("--- DDS3_GBWK unitwork/stocklist snapshot (best-effort) ---");
                        TryDumpDds3GbwkUnitSnapshot(w, maxUnits: 20, maxStock: 24);
                        w.WriteLine();
                    }
                    else
                    {
                        w.WriteLine("--- cmpInit.CMP_GBWK ---");
                        w.WriteLine("(unavailable)");
                        w.WriteLine();
                    }

                    // --- _campUIScr surface ---
                    object? campUiObj = null;
                    try
                    {
                        var m = FindStaticMember(cmpInit, "_campUIScr");
                        if (m != null)
                            campUiObj = GetStaticMemberValue(m);
                    }
                    catch
                    {
                        campUiObj = null;
                    }

                    if (campUiObj != null)
                    {
                        w.WriteLine("--- cmpInit._campUIScr (filtered members) ---");
                        w.WriteLine($"type={campUiObj.GetType().FullName}");
                        w.WriteLine($"unity={DescribeUnityObject(campUiObj)}");

                        DumpObjectMembersFiltered(
                            w,
                            label: "_campUIScr",
                            obj: campUiObj,
                            tokens: new[]
                            {
                                "menu", "Menu", "list", "List", "obj", "Obj", "cur", "Cur",
                                "cursor", "Cursor", "sel", "Sel", "index", "Index", "arrow", "Arrow",
                                "word", "Word", "para", "Para", "next", "Next", "size", "Size",
                                "help", "Help", "camp", "Camp", "root", "Root", "func", "Func"
                            },
                            maxLines: 300);

                        w.WriteLine();
                        w.WriteLine("--- cmpInit._campUIScr (ALL members, capped) ---");
                        DumpObjectMembersAll(w, "_campUIScr", campUiObj, maxLines: 260);
	                        w.WriteLine();
	                        w.WriteLine("--- cmpInit._campUIScr (decoded UI, best-effort) ---");
	                        TryDumpCampUiDecoded(w, cmpInit, campUiObj, gbwkObj);
                        w.WriteLine();
                    }
                    else
                    {
                        w.WriteLine("--- cmpInit._campUIScr ---");
                        w.WriteLine("(unavailable)");
                        w.WriteLine();
                    }

                    w.WriteLine("=== Notes / How to use ===");
                    w.WriteLine("- Run this dump while the vanilla command menu (press F) is OPEN.");
                    w.WriteLine("- Then run it again in overworld with no menus, and diff the files.");
                    w.WriteLine("- Look for members that change (menu list, cursor index, current entry, next list pointers)." );
                    w.WriteLine("- Once we identify the real member names, we can upgrade campProbe to log menuLen and submenu routing reliably.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Reimagined] Camp reflection surface dump failed: {ex}");
                }
            }

            // -------- helpers --------

	            // Cached reflection handles (dump-only; safe to be null if types/methods are not present)
	            private static Type? s_localizeType;
	            private static MethodInfo? s_localizeGetLocalizeText;
	            private static Type? s_datItemNameType;
	            private static MethodInfo? s_datItemNameGet;

            private static bool NameMatchesAnyToken(string name, string[] tokens)
            {
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }

            // Best-effort dumper for Il2CppInterop array wrappers and/or managed arrays exposed via static members.
            // The exact wrapper types can vary depending on IL2CPP bindings, so we keep this extremely defensive.
            private static void TryDumpIl2CppArrayStatic(TextWriter w, Type t, string memberName, int maxItems)
            {
                object? arrObj = null;
                try
                {
                    var m = FindStaticMember(t, memberName);
                    if (m != null)
                        arrObj = GetStaticMemberValue(m);
                }
                catch
                {
                    arrObj = null;
                }

                if (arrObj == null)
                {
                    w.WriteLine($"{memberName}=(unavailable)");
                    return;
                }

                if (!TryGetLengthOrCount(arrObj, out int len, out _))
                {
                    w.WriteLine($"{memberName}={FormatValue(arrObj)}");
                    return;
                }

                int show = Math.Min(len, Math.Max(0, maxItems));
                w.WriteLine($"{memberName}: type={arrObj.GetType().FullName} len={len} (showing {show})");
                for (int i = 0; i < show; i++)
                {
                    object? item = null;
                    try { item = TryGetIndexValue(arrObj, i); } catch { item = null; }
                    w.WriteLine($"  [{i}] {FormatArrayEntry(item)}");
                }
            }

	            // For string-key arrays like CAMP_L0035, dump a best-effort resolved version via Localize.GetLocalizeText.
	            private static void TryDumpLocalizedKeyArrayStatic(TextWriter w, Type t, string memberName, int maxItems)
	            {
	                object? arrObj = null;
	                try
	                {
	                    var m = FindStaticMember(t, memberName);
	                    if (m != null)
	                        arrObj = GetStaticMemberValue(m);
	                }
	                catch
	                {
	                    arrObj = null;
	                }

	                if (arrObj == null)
	                    return;
	
	                if (!TryGetLengthOrCount(arrObj, out int len, out _))
	                    return;
	
	                int show = Math.Min(len, Math.Max(0, maxItems));
	                if (show <= 0)
	                    return;
	
	                w.WriteLine($"{memberName} (localized): len={len} (showing {show})");
	                for (int i = 0; i < show; i++)
	                {
	                    string key = "";
	                    try
	                    {
	                        var item = TryGetIndexValue(arrObj, i);
	                        key = UnwrapToString(item);
	                    }
	                    catch
	                    {
	                        key = "";
	                    }

	                    if (string.IsNullOrEmpty(key))
	                    {
	                        w.WriteLine($"  [{i}] (null/empty)");
	                        continue;
	                    }

	                    if (TryLocalizeKey(key, out string loc))
	                        w.WriteLine($"  [{i}] {key} -> {loc}");
	                    else
	                        w.WriteLine($"  [{i}] {key} -> (unresolved)");
	                }
	            }

	            // Helper: read a string-key at [index] from a static array field (e.g. gCmpRootMenuStr)
	            // and attempt to resolve it via Localize.GetLocalizeText.
	            private static bool TryGetStaticStringKeyAt(Type t, string memberName, int index, out string key, out string loc)
	            {
	                key = "";
	                loc = "";
	                try
	                {
	                    if (index < 0)
	                        return false;

	                    object? arrObj = null;
	                    try
	                    {
	                        var m = FindStaticMember(t, memberName);
	                        if (m != null)
	                            arrObj = GetStaticMemberValue(m);
	                    }
	                    catch { arrObj = null; }

	                    if (arrObj == null)
	                        return false;

	                    if (!TryGetLengthOrCount(arrObj, out int len, out _))
	                        return false;

	                    if (index >= len)
	                        return false;

	                    object? item = null;
	                    try { item = TryGetIndexValue(arrObj, index); } catch { item = null; }
	                    if (item == null)
	                        return false;

	                    key = UnwrapToString(item);
	                    if (string.IsNullOrEmpty(key))
	                        return false;

	                    if (TryLocalizeKey(key, out string resolved))
	                        loc = resolved;
	
	                    return true;
	                }
	                catch
	                {
	                    key = "";
	                    loc = "";
	                    return false;
	                }
	            }

	            private static string UnwrapToString(object? v)
	            {
	                if (v == null)
	                    return "";
	
	                try
	                {
	                    if (v is string s)
	                        return Safe(s);

	                    var vt = v.GetType();
	                    // Common IL2CPP wrappers: .String property.
	                    var pStr = vt.GetProperty("String", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    if (pStr != null && pStr.PropertyType == typeof(string) && pStr.GetIndexParameters().Length == 0)
	                    {
	                        var sv = pStr.GetValue(v, null) as string;
	                        if (sv != null)
	                            return Safe(sv);
	                    }
	
	                    // Fallback: ToString.
	                    return Safe(v.ToString() ?? "");
	                }
	                catch
	                {
	                    return "";
	                }
	            }

	            private static bool TryLocalizeKey(string key, out string localized)
	            {
	                localized = "";
	                try
	                {
	                    if (s_localizeGetLocalizeText == null)
	                    {
	                        s_localizeType ??= FindTypeInLoadedAssemblies("Il2Cpp.Localize") ?? FindTypeInLoadedAssemblies("Localize");
	                        if (s_localizeType != null)
	                        {
	                            s_localizeGetLocalizeText = s_localizeType.GetMethod(
	                                "GetLocalizeText",
	                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                null,
	                                new[] { typeof(string) },
	                                null);
	                        }
	                    }

	                    if (s_localizeGetLocalizeText == null)
	                        return false;

	                    var r = s_localizeGetLocalizeText.Invoke(null, new object[] { key });
	                    if (r is string s && !string.IsNullOrEmpty(s))
	                    {
	                        localized = Trim(Safe(s), 240);
	                        return true;
	                    }
	                }
	                catch
	                {
	                    // ignore
	                }
	
	                return false;
	            }

	            private static bool TryGetItemName(int itemId, out string name)
	            {
	                name = "";
	                try
	                {
	                    if (s_datItemNameGet == null)
	                    {
	                        s_datItemNameType ??= FindTypeInLoadedAssemblies("Il2Cpp.datItemName") ?? FindTypeInLoadedAssemblies("datItemName");
	                        if (s_datItemNameType != null)
	                        {
	                            s_datItemNameGet = s_datItemNameType.GetMethod(
	                                "Get",
	                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                null,
	                                new[] { typeof(int) },
	                                null);
	                        }
	                    }

	                    if (s_datItemNameGet == null)
	                        return false;

	                    var r = s_datItemNameGet.Invoke(null, new object[] { itemId });
					string s = UnwrapToString(r);
					if (s == null)
						return false;

					// Empty-string names are valid in SMT3HD for dummy/reserved entries.
					name = Trim(s, 240);
					return true;
				}
	                catch
	                {
	                    // ignore
	                }

	                return false;
	            }

	            private static int TryReadInt32(object obj, string memberName, int fallback)
	            {
	                try
	                {
	                    if (TryGetInstanceMemberValue(obj, memberName, out object? v) && v != null)
	                        return Convert.ToInt32(v);
	                }
	                catch
	                {
	                    // ignore
	                }
	                return fallback;
	            }

	            private static void TryDumpCmpCursorInfoArrays(TextWriter w, object gbwkObj, int maxEntriesPerList)
	            {
	                TryDumpCmpCursorInfoArray(w, gbwkObj, "RootCI", maxEntriesPerList);
	                TryDumpCmpCursorInfoArray(w, gbwkObj, "WorkCI", maxEntriesPerList);
	            }

	            private static void TryDumpCmpCursorInfoArray(TextWriter w, object gbwkObj, string memberName, int maxEntriesPerList)
	            {
	                if (!TryGetInstanceMemberValue(gbwkObj, memberName, out object? ciArr) || ciArr == null)
	                {
	                    w.WriteLine($"{memberName}: (unavailable)");
	                    return;
	                }

	                if (!TryGetLengthOrCount(ciArr, out int len, out _))
	                {
	                    w.WriteLine($"{memberName}: {FormatValue(ciArr)}");
	                    return;
	                }

	                int show = Math.Min(len, Math.Max(0, maxEntriesPerList));
	                w.WriteLine($"{memberName}: len={len} (showing {show})");
	
	                for (int i = 0; i < show; i++)
	                {
	                    object? ci = null;
	                    try { ci = TryGetIndexValue(ciArr, i); } catch { ci = null; }
	                    if (ci == null)
	                    {
	                        w.WriteLine($"  [{i}] null");
	                        continue;
	                    }

	                    // CursorPos
	                    int idx = -1, listNums = -1, shift = -1, shiftMax = -1;
	                    string? keysPreview = null;
	
	                    try
	                    {
	                        if (TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) && cp != null)
	                        {
	                            idx = TryReadInt32(cp, "Index", -1);
	                            listNums = TryReadInt32(cp, "ListNums", -1);
	                            shift = TryReadInt32(cp, "Shift", -1);
	                            shiftMax = TryReadInt32(cp, "ShiftMax", -1);

	                            if (TryGetInstanceMemberValue(cp, "pStr", out object? pStr) && pStr != null && TryGetLengthOrCount(pStr, out int slen, out _))
	                            {
	                                int sshow = Math.Min(slen, Math.Max(0, listNums > 0 ? listNums : 8));
	                                var sb = new StringBuilder();
	                                sb.Append('[').Append(sshow).Append(" keys]");
	                                for (int k = 0; k < sshow; k++)
	                                {
	                                    string key = UnwrapToString(TryGetIndexValue(pStr, k));
	                                    if (string.IsNullOrEmpty(key))
	                                        continue;
	                                    string disp = key;
	                                    if (TryLocalizeKey(key, out string loc))
	                                        disp = $"{key}->{loc}";
	                                    sb.Append(" ").Append(disp);
	                                }
	                                keysPreview = Trim(sb.ToString(), 800);
	                            }
	                        }
	                    }
	                    catch
	                    {
	                        // ignore
	                    }

	                    int sel = (idx >= 0 && shift >= 0) ? (idx + shift) : -1;
                    w.WriteLine($"  [{i}] CursorPos: Index={idx} ListNums={listNums} Shift={shift}/{shiftMax} Sel={sel}");
	                    if (!string.IsNullOrEmpty(keysPreview))
	                        w.WriteLine($"       {keysPreview}");
	                }
	            }

	            private static void TryDumpCmpItemIdx(TextWriter w, object gbwkObj, int maxItems)
	            {
	                int itemCnt = TryReadInt32(gbwkObj, "ItemCnt", -1);
	                if (itemCnt < 0)
	                    itemCnt = TryReadInt32(gbwkObj, "ItemCnt", 0);

	                if (!TryGetInstanceMemberValue(gbwkObj, "ItemIdx", out object? itemIdxArr) || itemIdxArr == null)
	                {
	                    w.WriteLine($"ItemCnt={itemCnt}, ItemIdx=(unavailable)");
	                    return;
	                }

	                if (!TryGetLengthOrCount(itemIdxArr, out int len, out _))
	                {
	                    w.WriteLine($"ItemCnt={itemCnt}, ItemIdx={FormatValue(itemIdxArr)}");
	                    return;
	                }

	                int show = Math.Min(Math.Min(len, maxItems), itemCnt > 0 ? itemCnt : maxItems);
	                w.WriteLine($"ItemCnt={itemCnt} ItemIdx.len={len} (showing {show})");
	
	                for (int i = 0; i < show; i++)
	                {
	                    int id = 0;
	                    try
	                    {
	                        var v = TryGetIndexValue(itemIdxArr, i);
	                        id = Convert.ToInt32(v);
	                    }
	                    catch
	                    {
	                        id = 0;
	                    }

	                    string name = "";
	                    if (id != 0 && TryGetItemName(id, out string nm))
	                        name = nm;
	
	                    if (!string.IsNullOrEmpty(name))
	                        w.WriteLine($"  [{i}] {id} -> {name}");
	                    else
	                        w.WriteLine($"  [{i}] {id}");
	                }
	            }
	            
	            private static void TryDumpCmpStockInfo(TextWriter w, object gbwkObj, int maxPreviewEntries)
	            {
	                try
	                {
	                    if (!TryGetInstanceMemberValue(gbwkObj, "StockInfo", out object? stockInfo) || stockInfo == null)
	                    {
	                        w.WriteLine("StockInfo: (unavailable)");
	                        return;
	                    }

	                    w.WriteLine($"StockInfo.type={stockInfo.GetType().FullName}");

	                    int drawMode = TryReadInt32(stockInfo, "DrawMode", int.MinValue);
	                    if (drawMode != int.MinValue) w.WriteLine($"StockInfo.DrawMode={drawMode}");

	                    // Selection selectors (these are sbyte in the game code; we print as ints).
	                    int listSel = TryReadInt32(stockInfo, "ListSel", int.MinValue);
	                    int listCurSel = TryReadInt32(stockInfo, "ListCurSel", int.MinValue);
	                    int stockSel = TryReadInt32(stockInfo, "StockSel", int.MinValue);
	                    int stockCurSel = TryReadInt32(stockInfo, "StockCurSel", int.MinValue);
	                    int cursorSel = TryReadInt32(stockInfo, "CursorSel", int.MinValue);
	                    int cursorCurSel = TryReadInt32(stockInfo, "CursorCurSel", int.MinValue);

	                    if (listSel != int.MinValue || listCurSel != int.MinValue || stockSel != int.MinValue || stockCurSel != int.MinValue || cursorSel != int.MinValue || cursorCurSel != int.MinValue)
	                        w.WriteLine($"StockInfo.selectors: ListSel={listSel} ListCurSel={listCurSel}  StockSel={stockSel} StockCurSel={stockCurSel}  CursorSel={cursorSel} CursorCurSel={cursorCurSel}");

	                    // IMPORTANT (v8): cmpStockInfo_t.SelPos is NOT a cmpCursorPos_t.
	                    // It is an Il2CppReferenceArray<cmpCursorInfo_t>, each entry holding a CursorPos (cmpCursorPos_t).
	                    if (TryGetInstanceMemberValue(stockInfo, "SelPos", out object? selPosArr) && selPosArr != null && TryGetLengthOrCount(selPosArr, out int selLen, out string selKind))
	                    {
	                        w.WriteLine($"StockInfo.SelPos.{selKind}={selLen}");
	                        int show = Math.Min(selLen, 12);

	                        // Choose "active" cursor list index best-effort.
	                        int activeCursor = -1;
	                        if (cursorCurSel >= 0 && cursorCurSel < selLen) activeCursor = cursorCurSel;
	                        else if (cursorSel >= 0 && cursorSel < selLen) activeCursor = cursorSel;
	                        else if (selLen > 0) activeCursor = 0;

	                        for (int i = 0; i < show; i++)
	                        {
	                            object? ci = null;
	                            try { ci = TryGetIndexValue(selPosArr, i); } catch { ci = null; }
	                            if (ci == null)
	                            {
	                                w.WriteLine($"  [{i}] (null)");
	                                continue;
	                            }

	                            int stepY = TryReadInt32(ci, "StepY", int.MinValue);
	                            int stopFlag = TryReadInt32(ci, "StopFlag", int.MinValue);

	                            int idx = -1, shift = -1, listNums = -1, shiftMax = -1, sel = -1;
	                            if (TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) && cp != null)
	                            {
	                                idx = TryReadInt32(cp, "Index", -1);
	                                shift = TryReadInt32(cp, "Shift", -1);
	                                listNums = TryReadInt32(cp, "ListNums", -1);
	                                shiftMax = TryReadInt32(cp, "ShiftMax", -1);
	                                sel = TryReadInt32(cp, "Sel", -1);
	                            }

	                            int overall = (idx >= 0 && shift >= 0) ? (idx + shift) : -1;
	                            string tag = (i == activeCursor) ? " <ACTIVE>" : "";
	                            w.WriteLine($"  [{i}] Index={idx} Shift={shift}/{shiftMax} Overall={overall} Sel={sel} ListNums={listNums} StepY={stepY} StopFlag={stopFlag}{tag}");
	                        }
	                    }
	                    else
	                    {
	                        w.WriteLine("StockInfo.SelPos: (unavailable)");
	                    }

	                    // IMPORTANT (v8): cmpStockInfo_t.LocalStock is an Il2CppReferenceArray<cmpLocalStock_t>.
	                    if (TryGetInstanceMemberValue(stockInfo, "LocalStock", out object? localStockArr) && localStockArr != null && TryGetLengthOrCount(localStockArr, out int lsLen, out string lsKind))
	                    {
	                        w.WriteLine($"StockInfo.LocalStock.{lsKind}={lsLen}");
	                        int show = Math.Min(lsLen, 4);
	                        for (int i = 0; i < show; i++)
	                        {
	                            object? ls = null;
	                            try { ls = TryGetIndexValue(localStockArr, i); } catch { ls = null; }
	                            if (ls == null)
	                            {
	                                w.WriteLine($"  LocalStock[{i}] (null)");
	                                continue;
	                            }

	                            int flag = TryReadInt32(ls, "Flag", int.MinValue);
	                            int partyCnt = TryReadInt32(ls, "PartyCnt", -1);
	                            int stockCnt = TryReadInt32(ls, "StockCnt", -1);
	                            int badStatus = TryReadInt32(ls, "BadStatus", int.MinValue);
	                            int badTimer = TryReadInt32(ls, "BadTimer", int.MinValue);

	                            w.WriteLine($"  LocalStock[{i}].type={ls.GetType().FullName}");
	                            if (flag != int.MinValue) w.WriteLine($"    Flag=0x{flag:X8}  PartyCnt={partyCnt}  StockCnt={stockCnt}");
	                            else w.WriteLine($"    PartyCnt={partyCnt}  StockCnt={stockCnt}");
	                            if (badStatus != int.MinValue || badTimer != int.MinValue)
	                                w.WriteLine($"    BadStatus={badStatus}  BadTimer={badTimer}");

	                            // Keep previews short; the arrays can be long.
	                            TryDumpIntArrayMemberPreview(w, ls, "ListIdx", Math.Min(maxPreviewEntries, 24));
	                            TryDumpIntArrayMemberPreview(w, ls, "StockIdx", Math.Min(maxPreviewEntries, 24));
	                        }
	                    }
	                    else
	                    {
	                        w.WriteLine("StockInfo.LocalStock: (unavailable)");
	                    }

	                    // CMP_GBWK.DrawList is often the "currently displayed" list as a single cmpLocalStock_t.
	                    if (TryGetInstanceMemberValue(gbwkObj, "DrawList", out object? drawList) && drawList != null)
	                    {
	                        int flag = TryReadInt32(drawList, "Flag", int.MinValue);
	                        int partyCnt = TryReadInt32(drawList, "PartyCnt", -1);
	                        int stockCnt = TryReadInt32(drawList, "StockCnt", -1);

	                        w.WriteLine($"DrawList.type={drawList.GetType().FullName}  PartyCnt={partyCnt} StockCnt={stockCnt}{(flag != int.MinValue ? $" Flag=0x{flag:X8}" : "")}");
	                        TryDumpIntArrayMemberPreview(w, drawList, "ListIdx", Math.Min(maxPreviewEntries, 24));
	                        TryDumpIntArrayMemberPreview(w, drawList, "StockIdx", Math.Min(maxPreviewEntries, 24));
	                    }
	                }
	                catch
	                {
	                    w.WriteLine("StockInfo: (error)");
	                }
	            }

private static void TryDumpIntArrayMemberPreview(TextWriter w, object owner, string memberName, int maxPreviewEntries)
	            {
	                try
	                {
	                    if (!TryGetInstanceMemberValue(owner, memberName, out object? arr) || arr == null)
	                        return;

	                    if (!TryGetLengthOrCount(arr, out int len, out _))
	                        return;

	                    int show = Math.Min(Math.Max(0, maxPreviewEntries), len);
	                    w.WriteLine($"  {memberName}: len={len} showing {show}");

	                    if (show == 0)
	                        return;

	                    var sb = new StringBuilder();
	                    for (int i = 0; i < show; i++)
	                    {
	                        object? v = null;
	                        try { v = TryGetIndexValue(arr, i); } catch { v = null; }

	                        int iv = TryCoerceInt32(v, int.MinValue);
	                        if (iv == int.MinValue)
	                            continue;

	                        if (sb.Length > 0) sb.Append(", ");
	                        sb.Append($"[{i}]={iv}");
	                    }

	                    if (sb.Length > 0)
	                        w.WriteLine($"    {sb}");
	                }
	                catch
	                {
	                    // ignore
	                }
	            }

	            private static int TryCoerceInt32(object? v, int fallback)
	            {
	                try
	                {
	                    if (v == null) return fallback;
	                    if (v is int i) return i;
	                    if (v is short s) return s;
	                    if (v is byte b) return b;
	                    if (v is sbyte sb) return sb;
	                    if (v is ushort us) return us;
	                    if (v is uint ui) return unchecked((int)ui);
	                    if (v is long l) return unchecked((int)l);
	                    if (v is ulong ul) return unchecked((int)ul);

	                    // Il2Cpp boxed scalars often still Convert cleanly.
	                    return Convert.ToInt32(v);
	                }
	                catch
	                {
	                    return fallback;
	                }
	            }

	            private static bool TryGetIntArrayAt(object owner, string memberName, int index, out int value)
	            {
	                value = int.MinValue;
	                try
	                {
	                    if (index < 0)
	                        return false;

	                    if (!TryGetInstanceMemberValue(owner, memberName, out object? arr) || arr == null)
	                        return false;

	                    if (!TryGetLengthOrCount(arr, out int len, out _))
	                        return false;

	                    if (index >= len)
	                        return false;

	                    object? v = null;
	                    try { v = TryGetIndexValue(arr, index); } catch { v = null; }
	                    int iv = TryCoerceInt32(v, int.MinValue);
	                    if (iv == int.MinValue)
	                        return false;

	                    value = iv;
	                    return true;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            
	            	            // ---------------------------------------------------------------------
	            // cmpCalc selection (read-only, best-effort)
	            // ---------------------------------------------------------------------
	            // Many camp flows (especially items / swap / multi-step menus) keep multiple StockInfo cursors
	            // alive at once. The UI cursor position helps, but cmpCalc exposes the engine's current
	            // "selected source" and "selected destination" unitworks directly.
	            //
	            // IMPORTANT: We intentionally avoid compile-time references to Il2Cpp game namespaces/types here
	            // (e.g., Il2Cpp.*, newdata_H.*, datUnitWork_t) because wrapper namespace layouts can vary between
	            // environments. Everything is bound via reflection and treated as object.
	            private struct CmpCalcSelection
	            {
	                public bool Ok;
	                public int ItemId;
	                public int DstIndex;
	                public object? SrcObj;
	                public object? DstObj;
	                public string? Error;
	            }

	            private static Type? s_cmpCalcType;
	            private static MethodInfo? s_cmpGetSelectItemID;
	            private static MethodInfo? s_cmpGetSelectDstIndex;
	            private static MethodInfo? s_cmpGetSelectSrcStock;
	            private static MethodInfo? s_cmpGetSelectDstStock;

	            private static bool EnsureCmpCalcBindings(out string? error)
	            {
	                error = null;

	                try
	                {
	                    if (s_cmpCalcType == null)
	                    {
	                        // Depending on the wrapper generator and loader, the type may be exposed as "cmpCalc" or "Il2Cpp.cmpCalc".
	                        s_cmpCalcType = FindTypeInLoadedAssemblies("Il2Cpp.cmpCalc") ?? FindTypeInLoadedAssemblies("cmpCalc");
	                    }

	                    if (s_cmpCalcType == null)
	                    {
	                        error = "type cmpCalc not found";
	                        return false;
	                    }

	                    if (s_cmpGetSelectItemID == null)
	                        s_cmpGetSelectItemID = s_cmpCalcType.GetMethod("cmpGetSelectItemID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

	                    if (s_cmpGetSelectDstIndex == null)
	                        s_cmpGetSelectDstIndex = s_cmpCalcType.GetMethod("cmpGetSelectDstIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

	                    if (s_cmpGetSelectSrcStock == null)
	                        s_cmpGetSelectSrcStock = s_cmpCalcType.GetMethod("cmpGetSelectSrcStock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

	                    if (s_cmpGetSelectDstStock == null)
	                        s_cmpGetSelectDstStock = s_cmpCalcType.GetMethod("cmpGetSelectDstStock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

	                    // Missing methods are okay: we can still emit partial output and keep going.
	                    return true;
	                }
	                catch (Exception ex)
	                {
	                    error = ex.GetType().Name + ": " + ex.Message;
	                    return false;
	                }
	            }

	            private static bool TryInvokeStaticInt32(MethodInfo? mi, out int value)
	            {
	                value = int.MinValue;
	                if (mi == null)
	                    return false;

	                try
	                {
	                    object? r = mi.Invoke(null, null);
	                    value = TryCoerceInt32(r, int.MinValue);
	                    return value != int.MinValue;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            private static object? TryInvokeStaticObj(MethodInfo? mi)
	            {
	                if (mi == null)
	                    return null;

	                try
	                {
	                    return mi.Invoke(null, null);
	                }
	                catch
	                {
	                    return null;
	                }
	            }

	            private static CmpCalcSelection TryGetCmpCalcSelectionBestEffort()
	            {
	                var sel = new CmpCalcSelection
	                {
	                    Ok = false,
	                    ItemId = int.MinValue,
	                    DstIndex = int.MinValue,
	                    SrcObj = null,
	                    DstObj = null,
	                    Error = null,
	                };

	                if (!EnsureCmpCalcBindings(out string? err))
	                {
	                    sel.Error = err;
	                    return sel;
	                }

	                // Bind + invoke each method independently to avoid hard-failing on any single callsite.
	                if (!TryInvokeStaticInt32(s_cmpGetSelectItemID, out sel.ItemId))
	                    sel.ItemId = int.MinValue;

	                if (!TryInvokeStaticInt32(s_cmpGetSelectDstIndex, out sel.DstIndex))
	                    sel.DstIndex = int.MinValue;

	                sel.SrcObj = TryInvokeStaticObj(s_cmpGetSelectSrcStock);
	                sel.DstObj = TryInvokeStaticObj(s_cmpGetSelectDstStock);

	                sel.Ok = (sel.ItemId != int.MinValue) ||
	                         (sel.DstIndex != int.MinValue) ||
	                         (sel.SrcObj != null) ||
	                         (sel.DstObj != null);

	                if (!sel.Ok && string.IsNullOrEmpty(sel.Error))
	                    sel.Error = "no data (methods missing or returned invalid values)";

	                return sel;
	            }

	            private static void TryDumpCmpCalcSelection(TextWriter w, CmpCalcSelection sel)
	            {
	                if (!sel.Ok)
	                {
	                    w.WriteLine($"cmpCalc: (unavailable)  err={Safe(sel.Error)}");
	                    return;
	                }

	                string itemName = "";
	                if (sel.ItemId >= 0 && TryGetItemName(sel.ItemId, out string nm))
	                    itemName = nm;

	                w.WriteLine($"cmpCalc.itemId={sel.ItemId}  itemName=\"{Safe(itemName)}\"");
	                w.WriteLine($"cmpCalc.dstIndex={sel.DstIndex}");
	                DumpCmpCalcUnit(w, "srcStock", sel.SrcObj);
	                DumpCmpCalcUnit(w, "dstStock", sel.DstObj);
	            }

	            private static int TryReadAnyInt32(object owner, int fallback, params string[] memberNames)
	            {
	                foreach (var m in memberNames)
	                {
	                    int v = TryReadInt32(owner, m, int.MinValue);
	                    if (v != int.MinValue)
	                        return v;
	                }
	                return fallback;
	            }

	            private static void DumpCmpCalcUnit(TextWriter w, string label, object? u)
	            {
	                if (u == null)
	                {
	                    w.WriteLine($"cmpCalc.{label}=(null)");
	                    return;
	                }

	                IntPtr p = IntPtr.Zero;
	                try { p = TryGetIl2CppPointer(u); } catch { p = IntPtr.Zero; }

	                int id = TryReadAnyInt32(u, 0, "id", "Id");
	                int lvl = TryReadAnyInt32(u, 0, "level", "Level");
	                int hp = TryReadAnyInt32(u, 0, "hp", "HP");
	                int maxhp = TryReadAnyInt32(u, 0, "maxhp", "MaxHp", "MaxHP");
	                int mp = TryReadAnyInt32(u, 0, "mp", "MP");
	                int maxmp = TryReadAnyInt32(u, 0, "maxmp", "MaxMp", "MaxMP");
	                int uniqueid = TryReadAnyInt32(u, 0, "uniqueid", "UniqueId");

	                string namecodeStr = "";
	                uint namecodeSig = 0;
	                TryReadCharCode8(u, "namecode", out namecodeStr, out namecodeSig);

	                string fullnamecodeStr = "";
	                uint fullnamecodeSig = 0;
	                TryReadCharCode8(u, "fullnamecode", out fullnamecodeStr, out fullnamecodeSig);

	                string nm = "";
	                TryGetDevilNameBestEffort(id, out nm);

	                w.WriteLine(
	                    $"cmpCalc.{label}: type={u.GetType().FullName}  ptr=0x{p.ToInt64():X}  id={id}  lvl={lvl}  hp={hp}/{maxhp}  mp={mp}/{maxmp}  uniqueid={uniqueid}  namecodeSig=0x{namecodeSig:X8}  namecode=\"{Safe(namecodeStr)}\"  fullnameSig=0x{fullnamecodeSig:X8}  fullname=\"{Safe(fullnamecodeStr)}\"  name=\"{Safe(nm)}\"");
	            }

private static void TryDumpDerivedStockSelectionFromCmp(TextWriter w, object? gbwkObj, CmpCalcSelection calcSel)
{
    try
    {
        if (gbwkObj == null)
        {
            w.WriteLine("derived.stockInfo: (CMP_GBWK unavailable)");
            return;
        }

        if (!TryGetInstanceMemberValue(gbwkObj, "StockInfo", out object? stockInfo) || stockInfo == null)
        {
            w.WriteLine("derived.stockInfo: (unavailable)");
            return;
        }

        int drawMode = TryReadInt32(stockInfo, "DrawMode", int.MinValue);

        if (!TryGetInstanceMemberValue(stockInfo, "SelPos", out object? selPosArr) ||
            selPosArr == null ||
            !TryGetLengthOrCount(selPosArr, out int selLen, out string selKind) ||
            selLen <= 0)
        {
            w.WriteLine("derived.stockInfo.selPos: (unavailable)");
            return;
        }

        // LocalStock is usually parallel to SelPos (party vs stock cursors, etc.).
        object? localStockArr = null;
        int localStockLen = -1;
        if (TryGetInstanceMemberValue(stockInfo, "LocalStock", out object? lsArr) && lsArr != null && TryGetLengthOrCount(lsArr, out int lsLen, out _))
        {
            localStockArr = lsArr;
            localStockLen = lsLen;
        }

        // Some camp flows also expose a single DrawList on CMP_GBWK; keep as a last-resort fallback only.
        object? drawListFallback = null;
        if (TryGetInstanceMemberValue(gbwkObj, "DrawList", out object? dl) && dl != null)
            drawListFallback = dl;

        // If available, also fetch the game global-work object so we can resolve StockIdx/ListIdx -> unitwork -> devil id/name.
        object? dds3Gbwk = null;
        TryGetDds3GlobalWorkObject(out dds3Gbwk);

        // cmpCalc selection pointers (used only for tagging/matching; never used to drive gameplay)
        long srcPtr = 0;
        long dstPtr = 0;
        try { if (calcSel.SrcObj != null) srcPtr = TryGetIl2CppPointer(calcSel.SrcObj).ToInt64(); } catch { }
        try { if (calcSel.DstObj != null) dstPtr = TryGetIl2CppPointer(calcSel.DstObj).ToInt64(); } catch { }

        // Pass 1: locate which StockInfo cursor corresponds to cmpCalc.srcStock / cmpCalc.dstStock.
        int srcCursor = -1;
        int dstCursor = -1;
        UnitResolveInfo srcInfo = default;
        UnitResolveInfo dstInfo = default;
        bool haveSrcInfo = false;
        bool haveDstInfo = false;

        if (dds3Gbwk != null && (srcPtr != 0 || dstPtr != 0))
        {
            for (int cursor = 0; cursor < selLen; cursor++)
            {
                object? ci = null;
                try { ci = TryGetIndexValue(selPosArr, cursor); } catch { ci = null; }

                if (ci == null || !TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) || cp == null)
                    continue;

                int idx = TryReadInt32(cp, "Index", -1);
                int shift = TryReadInt32(cp, "Shift", -1);
                int overall = (idx >= 0 && shift >= 0) ? (idx + shift) : -1;
                if (overall < 0)
                    continue;

                // Choose LocalStock[cursor] when possible.
                object? localStock = null;
                if (localStockArr != null && cursor >= 0 && cursor < localStockLen)
                {
                    try { localStock = TryGetIndexValue(localStockArr, cursor); } catch { localStock = null; }
                }
                if (localStock == null)
                    localStock = drawListFallback;
                if (localStock == null)
                    continue;

                int listIdxVal = int.MinValue;
                int stockIdxVal = int.MinValue;
                if (TryGetIntArrayAt(localStock, "ListIdx", overall, out int li)) listIdxVal = li;
                if (TryGetIntArrayAt(localStock, "StockIdx", overall, out int si)) stockIdxVal = si;

                if (TryResolveUnitFromCandidateIndex(dds3Gbwk, stockIdxVal, out UnitResolveInfo infoA) ||
                    TryResolveUnitFromCandidateIndex(dds3Gbwk, listIdxVal, out infoA))
                {
                    if (infoA.Ptr != 0)
                    {
                        if (srcPtr != 0 && infoA.Ptr == srcPtr && srcCursor < 0)
                        {
                            srcCursor = cursor;
                            srcInfo = infoA;
                            haveSrcInfo = true;
                        }

                        if (dstPtr != 0 && infoA.Ptr == dstPtr && dstCursor < 0)
                        {
                            dstCursor = cursor;
                            dstInfo = infoA;
                            haveDstInfo = true;
                        }
                    }
                }
            }
        }

        // Pass 2: determine which cursor should be considered "active" for the UI selection we care about.
        // In item-target selection (drawMode==5), the "dst" cursor is usually the one the player is moving.
        // In summon/return flows (drawMode==1), the "src" cursor is usually the one the player is moving.
        int cursorSel = TryReadInt32(stockInfo, "CursorSel", int.MinValue);
        int cursorCurSel = TryReadInt32(stockInfo, "CursorCurSel", int.MinValue);
        int activeCursor = -1;

        if (drawMode == 5 && dstCursor >= 0 && dstCursor < selLen)
            activeCursor = dstCursor;
        else if (srcCursor >= 0 && srcCursor < selLen)
            activeCursor = srcCursor;
        else if (cursorCurSel >= 0 && cursorCurSel < selLen)
            activeCursor = cursorCurSel;
        else if (cursorSel >= 0 && cursorSel < selLen)
            activeCursor = cursorSel;
        else if (selLen > 0)
            activeCursor = 0;

        w.WriteLine(
            $"derived.stockInfo.meta: drawMode={drawMode}  SelPos.Length={selLen}  LocalStock.Length={(localStockLen >= 0 ? localStockLen : -1)}  activeCursor={activeCursor}  CursorSel={cursorSel}  CursorCurSel={cursorCurSel}  " +
            $"cmpSrcPtr=0x{srcPtr:X}  cmpDstPtr=0x{dstPtr:X}  srcCursor={srcCursor}  dstCursor={dstCursor}  (SelPos.{selKind})");

        // Pass 3: emit full per-cursor detail (this is the part you diff between dumps).
        for (int cursor = 0; cursor < selLen; cursor++)
        {
            object? ci = null;
            try { ci = TryGetIndexValue(selPosArr, cursor); } catch { ci = null; }

            if (ci == null || !TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) || cp == null)
            {
                w.WriteLine($"derived.stockInfo[{cursor}].cursor: (unavailable)");
                continue;
            }

            int idx = TryReadInt32(cp, "Index", -1);
            int shift = TryReadInt32(cp, "Shift", -1);
            int listNums = TryReadInt32(cp, "ListNums", -1);
            int shiftMax = TryReadInt32(cp, "ShiftMax", -1);
            int sel = TryReadInt32(cp, "Sel", -1);
            int stepY = TryReadInt32(cp, "StepY", -1);
            int stopFlag = TryReadInt32(cp, "StopFlag", -1);

            int overall = (idx >= 0 && shift >= 0) ? (idx + shift) : -1;

            string tag = (cursor == activeCursor) ? " <ACTIVE>" : "";
            w.WriteLine($"derived.stockInfo[{cursor}].cursor: Index={idx} Shift={shift}/{shiftMax} Overall={overall} Sel={sel} ListNums={listNums} StepY={stepY} StopFlag={stopFlag}{tag}");

            // Choose LocalStock[cursor] when possible.
            object? localStock = null;
            if (localStockArr != null && cursor >= 0 && cursor < localStockLen)
            {
                try { localStock = TryGetIndexValue(localStockArr, cursor); } catch { localStock = null; }
            }
            if (localStock == null)
                localStock = drawListFallback;

            if (localStock == null)
            {
                w.WriteLine($"derived.stockInfo[{cursor}].local: (unavailable)");
                continue;
            }

            int partyCnt = TryReadInt32(localStock, "PartyCnt", -1);
            int stockCnt = TryReadInt32(localStock, "StockCnt", -1);
            int flag = TryReadInt32(localStock, "Flag", int.MinValue);

            w.WriteLine($"derived.stockInfo[{cursor}].local: type={localStock.GetType().FullName}  Flag=0x{flag:X8}  PartyCnt={partyCnt} StockCnt={stockCnt}");

            if (overall < 0)
            {
                w.WriteLine($"derived.stockInfo[{cursor}].slot: (overall<0)");
                continue;
            }

            int listIdxVal = int.MinValue;
            int stockIdxVal = int.MinValue;

            if (TryGetIntArrayAt(localStock, "ListIdx", overall, out int li)) listIdxVal = li;
            if (TryGetIntArrayAt(localStock, "StockIdx", overall, out int si)) stockIdxVal = si;

            w.WriteLine($"derived.stockInfo[{cursor}].slot: overall={overall}  listIdx={listIdxVal}  stockIdx={stockIdxVal}");

            if (partyCnt > 0)
            {
                bool isParty = overall < partyCnt;
                int local = isParty ? overall : (overall - partyCnt);
                w.WriteLine($"derived.stockInfo[{cursor}].partition: focus={(isParty ? "party" : "stock")} localIndex={local}");
            }

            // Attempt to resolve these indices into an actual unitwork entry.
            // Empirically, StockIdx is often closest to a unitwork index; if that fails, try ListIdx.
            if (dds3Gbwk != null)
            {
                if (TryResolveUnitFromCandidateIndex(dds3Gbwk, stockIdxVal, out UnitResolveInfo infoA) ||
                    TryResolveUnitFromCandidateIndex(dds3Gbwk, listIdxVal, out infoA))
                {
                    bool isSrc = (srcPtr != 0 && infoA.Ptr == srcPtr);
                    bool isDst = (dstPtr != 0 && infoA.Ptr == dstPtr);

                    string cmpTag = "";
                    if (isSrc) cmpTag += " <CMP_SRC>";
                    if (isDst) cmpTag += " <CMP_DST>";

                    w.WriteLine($"derived.stockInfo[{cursor}].unit: unitworkIndex={infoA.UnitworkIndex}  ptr=0x{infoA.Ptr:X}  id={infoA.UnitId}  lvl={infoA.Level}  hp={infoA.HP}/{infoA.MaxHP}  mp={infoA.MP}/{infoA.MaxMP}  uniqueid={infoA.UniqueId}  namecode={infoA.NameCode}  fullnamecode={infoA.FullNameCode}{cmpTag}");
                    if (!string.IsNullOrEmpty(infoA.NameTag))
                        w.WriteLine($"derived.stockInfo[{cursor}].unitName=\"{Safe(infoA.NameTag)}\"");

                    if (isSrc || isDst)
                        w.WriteLine($"derived.stockInfo[{cursor}].role={(isSrc ? "SRC" : "")}{(isSrc && isDst ? "+" : "")}{(isDst ? "DST" : "")}");
                }
            }
        }

        // Canonical summary for diffing and for future in-code "selection snapshot" APIs.
        string itemName = "";
        if (calcSel.ItemId >= 0)
            TryGetItemName(calcSel.ItemId, out itemName);

        string srcName = haveSrcInfo ? (srcInfo.NameTag ?? "") : "";
        string dstName = haveDstInfo ? (dstInfo.NameTag ?? "") : "";

        bool dstIndexMatches = false;
        if (haveDstInfo && calcSel.DstIndex != int.MinValue)
            dstIndexMatches = (calcSel.DstIndex == dstInfo.UnitworkIndex);

        w.WriteLine();
        w.WriteLine("--- derived.selection.summary (cmpCalc + StockInfo) ---");

        var snap = new CampSelectionSnapshot(
            drawMode: drawMode,
            cmpItemMeaningful: (drawMode == 5),
            itemId: calcSel.ItemId,
            itemName: itemName,
            cmpDstIndex: calcSel.DstIndex,
            cmpDstIndexMatchesDstUnitworkIndex: dstIndexMatches,
            src: new CampSelectionUnit(
                cursor: srcCursor,
                unitworkIndex: haveSrcInfo ? srcInfo.UnitworkIndex : -1,
                ptr: srcPtr,
                name: srcName
            ),
            dst: new CampSelectionUnit(
                cursor: dstCursor,
                unitworkIndex: haveDstInfo ? dstInfo.UnitworkIndex : -1,
                ptr: dstPtr,
                name: dstName
	            ),
	            cursorEntries: null
        );
        snap.WriteSummaryLines(w);

    }
    catch
    {
        // ignore
    }
}

static Type? s_datDevilNameType;
	            static MethodInfo? s_datDevilNameGet;
	            static MethodInfo? s_datDevilNameGetTag;
	            static MethodInfo? s_datDevilNameGetEncyc;

	            static Type? s_datSkillNameType;
	            static MethodInfo? s_datSkillNameGet;

	            private static bool TryGetDevilNameBestEffort(int devilId, out string name)
	            {
	                name = "";
	                try
	                {
	                    if (devilId < 0)
	                        return false;

	                    // The protagonist (Demi-fiend) typically uses id=0 in unitwork; it is not present in datDevilName.
	                    if (devilId == 0)
	                    {
	                        name = "Demi-fiend";
	                        return true;
	                    }

	                    if (s_datDevilNameType == null)
	                    {
	                        s_datDevilNameType = FindTypeInLoadedAssemblies("Il2Cpp.datDevilName") ?? FindTypeInLoadedAssemblies("datDevilName");
	                        if (s_datDevilNameType != null)
	                        {
	                            // In the shipped SMT3HD IL2CPP wrappers, datDevilName.Get(int) returns the display string.
	                            // GetTag(int) returns the internal "<DEVIL_L####>" token.
	                            s_datDevilNameGet = s_datDevilNameType.GetMethod(
	                                "Get",
	                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                null,
	                                new[] { typeof(int) },
	                                null);

	                            s_datDevilNameGetEncyc = s_datDevilNameType.GetMethod(
	                                "GetEncyc",
	                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                null,
	                                new[] { typeof(int) },
	                                null);

	                            s_datDevilNameGetTag = s_datDevilNameType.GetMethod(
	                                "GetTag",
	                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                null,
	                                new[] { typeof(int) },
	                                null);
	                        }
	                    }

	                    object? r = null;

	                    // 1) Prefer the display string.
	                    if (s_datDevilNameGet != null)
	                    {
	                        r = s_datDevilNameGet.Invoke(null, new object[] { devilId });
	                        string s = UnwrapToString(r);
						if (s != null)
						{
							// Empty-string names are valid in SMT3HD for dummy/reserved entries.
							name = Trim(s, 240);
							return true;
						}

	                        // Some wrappers return an object; try reading common members.
	                        if (r != null)
	                        {
	                            if (TryGetInstanceMemberValue(r, "txt_loc", out object? tl))
	                            {
	                                string s2 = UnwrapToString(tl);
								if (s2 != null)
								{
									// Empty-string names are valid in SMT3HD for dummy/reserved entries.
									name = Trim(s2, 240);
									return true;
								}
	                            }

	                            if (TryGetInstanceMemberValue(r, "txt", out object? t2))
	                            {
	                                string s3 = UnwrapToString(t2);
	                                if (!string.IsNullOrEmpty(s3))
	                                {
	                                    name = Trim(s3, 240);
	                                    return true;
	                                }
	                            }
	                        }
	                    }

	                    // 2) Encyclopedia string (sometimes differs from display string).
	                    if (s_datDevilNameGetEncyc != null)
	                    {
	                        r = s_datDevilNameGetEncyc.Invoke(null, new object[] { devilId });
	                        string s = UnwrapToString(r);
						if (s != null)
						{
							// Empty-string names are valid in SMT3HD for dummy/reserved entries.
							name = Trim(s, 240);
							return true;
						}
	                    }

	                    // 3) Internal tag token as a last resort.
	                    if (s_datDevilNameGetTag != null)
	                    {
	                        r = s_datDevilNameGetTag.Invoke(null, new object[] { devilId });
	                        string s = UnwrapToString(r);
						if (s != null)
						{
							// Empty-string names are valid in SMT3HD for dummy/reserved entries.
							name = Trim(s, 240);
							return true;
						}
	                    }
	                }
	                catch
	                {
	                }

	                return false;
	            }

	            static bool TryGetSkillNameBestEffort(int skillId, int devilId, out string name)
	            {
	                name = "";
	                try
	                {
	                    if (skillId < 0)
	                        return false;

	                    if (s_datSkillNameType == null)
	                    {
	                        s_datSkillNameType = FindTypeInLoadedAssemblies("Il2Cpp.datSkillName") ?? FindTypeInLoadedAssemblies("datSkillName");
	                        if (s_datSkillNameType != null)
	                        {
	                            // datSkillName.Get(int id, int Devilid = 0) → localized display string
	                            s_datSkillNameGet = s_datSkillNameType.GetMethod(
	                                "Get",
	                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                null,
	                                new[] { typeof(int), typeof(int) },
	                                null);

	                            // Some wrapper variants expose Get(int) only.
	                            if (s_datSkillNameGet == null)
	                            {
	                                s_datSkillNameGet = s_datSkillNameType.GetMethod(
	                                    "Get",
	                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                                    null,
	                                    new[] { typeof(int) },
	                                    null);
	                            }
	                        }
	                    }

	                    if (s_datSkillNameGet == null)
	                        return false;

	                    object? r;
	                    if (s_datSkillNameGet.GetParameters().Length >= 2)
	                        r = s_datSkillNameGet.Invoke(null, new object[] { skillId, devilId });
	                    else
	                        r = s_datSkillNameGet.Invoke(null, new object[] { skillId });

	                    string s = UnwrapToString(r);
					if (s == null)
						return false;

					// Empty-string names are valid in SMT3HD for dummy/reserved entries.
					name = Trim(s, 240);
					return true;
	                }
	                catch
	                {
	                    return false;
	                }
	            }





            private static object? TryGetIndexValue(object arrObj, int index)
            {
                // System.Array
                if (arrObj is Array a)
                    return a.GetValue(index);

                var t = arrObj.GetType();

                // Indexer property "Item[int]" (or any int indexer)
                try
                {
                    var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    for (int i = 0; i < props.Length; i++)
                    {
                        var p = props[i];
                        if (p == null || !p.CanRead)
                            continue;
                        var idx = p.GetIndexParameters();
                        if (idx.Length == 1 && idx[0].ParameterType == typeof(int))
                            return p.GetValue(arrObj, new object[] { index });
                    }
                }
                catch
                {
                    // ignore
                }

                // Common methods
                try
                {
                    var mGet = t.GetMethod("Get", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (mGet != null)
                        return mGet.Invoke(arrObj, new object[] { index });
                }
                catch { }

                try
                {
                    var mGetItem = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (mGetItem != null)
                        return mGetItem.Invoke(arrObj, new object[] { index });
                }
                catch { }

                return null;
            }

            private static string DecodeByteArrayBestEffort(object arrObj, int maxBytes)
            {
                try
                {
                    if (!TryGetLengthOrCount(arrObj, out int len, out _))
                        return "";

                    int n = Math.Min(len, maxBytes);
                    if (n <= 0)
                        return "";

                    var bytes = new List<byte>(n);
                    for (int i = 0; i < n; i++)
                    {
                        object? v = TryGetIndexValue(arrObj, i);
                        if (v == null)
                            break;

                        byte b;
                        try { b = Convert.ToByte(v); }
                        catch { break; }

                        if (b == 0)
                            break;

                        bytes.Add(b);
                    }

                    if (bytes.Count == 0)
                        return "";

                    return Encoding.UTF8.GetString(bytes.ToArray());
                }
                catch
                {
                    return "";
                }
            }


            private static string FormatArrayEntry(object? v)
            {
                if (v == null)
                    return "null";

                try
                {
                    if (v is string s)
                        return $"\"{Trim(Safe(s), 240)}\"";

                    var vt = v.GetType();

                    // Some IL2CPP string wrappers expose a 'String' property.
                    try
                    {
                        var pStr = vt.GetProperty("String", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (pStr != null && pStr.PropertyType == typeof(string) && pStr.GetIndexParameters().Length == 0)
                        {
                            var sv = pStr.GetValue(v, null) as string;
                            if (sv != null)
                                return $"\"{Trim(Safe(sv), 240)}\"";
                        }
                    }
                    catch { }

                    return FormatValue(v);
                }
                catch
                {
                    return v.GetType().Name;
                }
            }

            private static void DumpStaticMembersFiltered(
                TextWriter w,
                Type t,
                Func<string, string[], bool> memberNameFilter,
                string[] tokens,
                int maxLines)
            {
                int lines = 0;
                try
                {
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                    // Fields
                    foreach (var f in t.GetFields(flags).OrderBy(f => f.Name, StringComparer.Ordinal))
                    {
                        if (!memberNameFilter(f.Name, tokens))
                            continue;
                        if (lines++ >= maxLines)
                            return;
                        object? v = null;
                        try { v = f.GetValue(null); } catch { v = null; }
                        w.WriteLine($"field {f.FieldType.Name} {f.Name} = {FormatValue(v)}");
                    }

                    // Properties
                    foreach (var p in t.GetProperties(flags).OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!p.CanRead)
                            continue;
                        if (p.GetIndexParameters().Length != 0)
                            continue;
                        if (!memberNameFilter(p.Name, tokens))
                            continue;
                        if (lines++ >= maxLines)
                            return;
                        object? v = null;
                        try { v = p.GetValue(null, null); } catch { v = null; }
                        w.WriteLine($"prop  {p.PropertyType.Name} {p.Name} = {FormatValue(v)}");
                    }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"(static member dump error: {ex.GetType().Name}: {ex.Message})");
                }
            }

            private static void DumpObjectMembersAll(TextWriter w, string label, object obj, int maxLines)
            {
                try
                {
                    var t = obj.GetType();
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    int lines = 0;

                    w.WriteLine($"[{label}] type={t.FullName}");

                    foreach (var p in t.GetProperties(flags).OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!p.CanRead)
                            continue;
                        if (p.GetIndexParameters().Length != 0)
                            continue;
                        if (lines++ >= maxLines)
                            break;
                        object? v = null;
                        try { v = p.GetValue(obj, null); } catch { v = null; }
                        w.WriteLine($"prop  {p.PropertyType.Name} {p.Name} = {FormatValue(v)}");
                    }

                    foreach (var f in t.GetFields(flags).OrderBy(f => f.Name, StringComparer.Ordinal))
                    {
                        if (lines++ >= maxLines)
                            break;
                        object? v = null;
                        try { v = f.GetValue(obj); } catch { v = null; }
                        w.WriteLine($"field {f.FieldType.Name} {f.Name} = {FormatValue(v)}");
                    }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"({label} dump error: {ex.GetType().Name}: {ex.Message})");
                }
            }

            private static void DumpObjectMembersFiltered(TextWriter w, string label, object obj, string[] tokens, int maxLines)
            {
                try
                {
                    var t = obj.GetType();
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    int lines = 0;

                    w.WriteLine($"[{label}] type={t.FullName}");

                    foreach (var p in t.GetProperties(flags).OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!p.CanRead)
                            continue;
                        if (p.GetIndexParameters().Length != 0)
                            continue;
                        if (!NameMatchesAnyToken(p.Name, tokens))
                            continue;
                        if (lines++ >= maxLines)
                            break;
                        object? v = null;
                        try { v = p.GetValue(obj, null); } catch { v = null; }
                        w.WriteLine($"prop  {p.PropertyType.Name} {p.Name} = {FormatValue(v)}");
                    }

                    foreach (var f in t.GetFields(flags).OrderBy(f => f.Name, StringComparer.Ordinal))
                    {
                        if (!NameMatchesAnyToken(f.Name, tokens))
                            continue;
                        if (lines++ >= maxLines)
                            break;
                        object? v = null;
                        try { v = f.GetValue(obj); } catch { v = null; }
                        w.WriteLine($"field {f.FieldType.Name} {f.Name} = {FormatValue(v)}");
                    }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"({label} filtered dump error: {ex.GetType().Name}: {ex.Message})");
                }
            }

            private static string DescribeUnityObject(object obj)
            {
                try
                {
                    if (obj is GameObject go)
                        return $"GameObject name=\"{Safe(go.name)}\" activeHier={go.activeInHierarchy} path=\"{Safe(GetGameObjectPath(go))}\"";
                    if (obj is Component c)
                    {
                        var go2 = c.gameObject;
                        return $"Component {c.GetType().Name} go=\"{Safe(go2 != null ? go2.name : "<null>")}\" activeHier={(go2 != null && go2.activeInHierarchy)} path=\"{Safe(go2 != null ? GetGameObjectPath(go2) : "") }\"";
                    }
                    if (obj is UnityEngine.Object uo)
                        return $"UnityObject {uo.GetType().Name} name=\"{Safe(uo.name)}\"";
                    return "(not a UnityEngine.Object)";
                }
                catch
                {
                    return "(unity describe failed)";
                }
            }

            private static string GetGameObjectPath(GameObject go)
            {
                try
                {
                    var t = go.transform;
                    if (t == null)
                        return go.name ?? "";
                    var parts = new List<string>(16);
                    while (t != null)
                    {
                        parts.Add(t.name ?? "");
                        t = t.parent;
                    }
                    parts.Reverse();
                    return string.Join("/", parts);
                }
                catch
                {
                    return go.name ?? "";
                }
            }

            private static string FormatValue(object? v)
            {
                if (v == null)
                    return "null";

                try
                {
                    // Common primitives
                    if (v is string s)
                        return $"\"{Trim(s, 180)}\"";
                    if (v is bool b)
                        return b ? "true" : "false";
                    if (v is sbyte or byte or short or ushort or int or uint or long or ulong or float or double)
                        return v.ToString() ?? v.GetType().Name;

                    // Unity types
                    if (v is GameObject go)
                        return $"GameObject(\"{Safe(go.name)}\", activeHier={go.activeInHierarchy}, path=\"{Safe(GetGameObjectPath(go))}\")";
                    if (v is Component c)
                    {
                        var go2 = c.gameObject;
                        return $"Component({c.GetType().Name}, go=\"{Safe(go2 != null ? go2.name : "<null>")}\", activeHier={(go2 != null && go2.activeInHierarchy)})";
                    }
                    if (v is UnityEngine.Object uo)
                        return $"UnityObject({uo.GetType().Name}, \"{Safe(uo.name)}\")";

                    // Enum
                    var vt = v.GetType();
                    if (vt.IsEnum)
                        return $"{vt.Name}.{v} ({Convert.ToInt32(v)})";

                    // Arrays/lists (length only)
                    if (TryGetLengthOrCount(v, out int len, out string kind))
                        return $"{vt.Name}({kind}={len})";

                    // Fallback
                    string txt = v.ToString() ?? vt.Name;
                    txt = Trim(txt, 180);
                    return $"{vt.Name}({txt})";
                }
                catch
                {
                    return v.GetType().Name;
                }
            }

            private static bool TryGetLengthOrCount(object v, out int len, out string kind)
            {
                len = 0;
                kind = "";

                try
                {
                    var t = v.GetType();

                    // System.Array
                    if (v is Array a)
                    {
                        len = a.Length;
                        kind = "Length";
                        return true;
                    }

                    // Common "Length" property
                    var pLen = t.GetProperty("Length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pLen != null && pLen.PropertyType == typeof(int) && pLen.GetIndexParameters().Length == 0)
                    {
                        object? o = pLen.GetValue(v, null);
                        if (o != null)
                        {
                            len = Convert.ToInt32(o);
                            kind = "Length";
                            return true;
                        }
                    }

                    // Common "Count" property
                    var pCount = t.GetProperty("Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pCount != null && pCount.PropertyType == typeof(int) && pCount.GetIndexParameters().Length == 0)
                    {
                        object? o = pCount.GetValue(v, null);
                        if (o != null)
                        {
                            len = Convert.ToInt32(o);
                            kind = "Count";
                            return true;
                        }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

	            // Decode the *live* UI state of campUI in a way that's directly useful for
	            // correlating to what the player sees (menu entries, cursor position, etc.).
	            // This intentionally avoids any compile-time dependency on TMPro by using reflection.
	            private static void TryDumpCampUiDecoded(TextWriter w, Type cmpInitType, object campUiObj, object? gbwkObj)
	            {
	                try
	                {
	                    // --- Title/Help text ---
	                    if (TryGetInstanceMemberValue(campUiObj, "titleText", out object? titleA) && TryGetTextProperty(titleA, out string? titleAText))
	                        w.WriteLine($"titleText.text=\"{Safe(titleAText)}\"");
	                    if (TryGetInstanceMemberValue(campUiObj, "titleText_e", out object? titleE) && TryGetTextProperty(titleE, out string? titleEText))
	                        w.WriteLine($"titleText_e.text=\"{Safe(titleEText)}\"");
	                    if (TryGetInstanceMemberValue(campUiObj, "helpText", out object? help) && TryGetTextProperty(help, out string? helpText))
	                        w.WriteLine($"helpText.text=\"{Safe(helpText)}\"");

	                    // --- Cursor object ---
	                    GameObject? menuCurGo = null;
	                    if (TryGetInstanceMemberValue(campUiObj, "menuCur", out object? menuCurObj))
	                        menuCurGo = CoerceGameObject(menuCurObj);

	                    if (menuCurGo != null)
	                    {
	                        w.WriteLine($"menuCur={DescribeGameObject(menuCurGo)}");
	                        w.WriteLine($"menuCur.path={GetHierarchyPath(menuCurGo.transform)}");
	                        w.WriteLine($"menuCur.pos={FormatTransformPos(menuCurGo.transform)}");
	                    }
	                    else
	                    {
	                        w.WriteLine("menuCur=(unavailable)");
	                    }

	                    // --- Root menu entries (menuObj) ---
	                    object? menuArr = null;
	                    if (TryGetInstanceMemberValue(campUiObj, "menuObj", out object? mo))
	                        menuArr = mo;

	                    if (menuArr == null)
	                    {
	                        w.WriteLine("menuObj=(unavailable)");
	                        return;
	                    }

	                    if (!TryGetLengthOrCount(menuArr, out int menuLen, out string menuLenKind))
	                    {
	                        w.WriteLine("menuObj=(present but length unavailable)");
	                        return;
	                    }

	                    w.WriteLine($"menuObj.{menuLenKind}={menuLen}");

	                    var entryGos = new List<GameObject>(Mathf.Clamp(menuLen, 0, 64));
	                    var entryPos = new List<Vector3>(Mathf.Clamp(menuLen, 0, 64));
	                    var entryText = new List<string>(Mathf.Clamp(menuLen, 0, 64));

	                    int dumpN = Mathf.Min(menuLen, 16); // cap to keep dumps readable
	                    for (int i = 0; i < dumpN; i++)
	                    {
	                        if (!TryGetIndexedValue(menuArr, i, out object? itemObj))
	                        {
	                            w.WriteLine($"  menuObj[{i}] = (unreadable)");
	                            continue;
	                        }
	
	                        var go = CoerceGameObject(itemObj);
	                        if (go == null)
	                        {
	                            w.WriteLine($"  menuObj[{i}] = (null)");
	                            continue;
	                        }
	
	                        string txt = TryFindAnyTextOn(go, out string? found) ? found! : "";
	                        string sprite = TryFindAnySpriteOn(go, out string? sprFound) ? sprFound! : "";

	                        // For root menu, we can *always* map indices to the cmpInit string table.
	                        // This is the most reliable label source (even if the visible UI is sprite-based).
	                        string key = "";
	                        string loc = "";
	                        if (TryGetStaticStringKeyAt(cmpInitType, "gCmpRootMenuStr", i, out string k, out string l))
	                        {
	                            key = k;
	                            loc = l;
	                        }

	                        w.WriteLine($"  menuObj[{i}] = {DescribeGameObject(go)}  text=\"{Safe(txt)}\"  sprite=\"{Safe(sprite)}\"  labelKey=\"{Safe(key)}\"  labelLoc=\"{Safe(loc)}\"  pos={FormatTransformPos(go.transform)}");

	                        entryGos.Add(go);
	                        entryPos.Add(go.transform.position);
	                        entryText.Add(txt);
	                    }
	                    // Infer selection:
	                    // - If the cursor GameObject is active, use its world-space position.
	                    // - Otherwise (common on some camp screens), fall back to CMP_GBWK.RootCI[0]
	                    //   which tracks the current root-menu selection reliably.
	                    if (entryGos.Count > 0)
	                    {
	                        int bestIdx = -1;
	                        int ciIdx = -1;
	                        bool hasCi = false;
	                        if (gbwkObj != null && TryReadCmpCursorSel(gbwkObj, "RootCI", 0, out int ciSel, out _))
	                        {
	                            ciIdx = ciSel;
	                            hasCi = true;
	                        }

	                        if (menuCurGo != null && menuCurGo.activeInHierarchy)
	                        {
	                            Vector3 cur = menuCurGo.transform.position;
	                            float bestD = float.MaxValue;
	                            for (int i = 0; i < entryPos.Count; i++)
	                            {
	                                float d = (entryPos[i] - cur).sqrMagnitude;
	                                if (d < bestD)
	                                {
	                                    bestD = d;
	                                    bestIdx = i;
	                                }
	                            }

	                            if (bestIdx >= 0)
	                            {
	                                // Prefer CI for labels (cursor position can be on decorative objects).
	                                int labelIdx = (hasCi && ciIdx >= 0) ? ciIdx : bestIdx;
	                                TryGetStaticStringKeyAt(cmpInitType, "gCmpRootMenuStr", labelIdx, out string key, out string loc);
	                                w.WriteLine($"derived.selectedIndex={bestIdx}  derived.selectedText=\"{Safe(entryText[bestIdx])}\"  derived.selectedGo={entryGos[bestIdx].name}  derived.rootSel={labelIdx}  derived.rootKey=\"{Safe(key)}\"  derived.rootLoc=\"{Safe(loc)}\"  (via menuCur{(hasCi ? "+RootCI" : "")})");

	                                // Extra: nearest label/sprite/string candidates around the cursor.
	                                TryDumpNearbyLabelCandidates(w, "derived.menu", entryGos[bestIdx], menuCurGo.transform.position, maxCandidates: 12, maxNodes: 900, ancestorDepth: 4);
	                            }
	                        }
	                        else if (hasCi)
	                        {
	                            if (ciIdx >= 0 && ciIdx < entryGos.Count)
	                            {
	                                bestIdx = ciIdx;
	                                TryGetStaticStringKeyAt(cmpInitType, "gCmpRootMenuStr", bestIdx, out string key, out string loc);
	                                w.WriteLine($"derived.selectedIndex={bestIdx}  derived.selectedText=\"{Safe(entryText[bestIdx])}\"  derived.selectedGo={entryGos[bestIdx].name}  derived.rootSel={bestIdx}  derived.rootKey=\"{Safe(key)}\"  derived.rootLoc=\"{Safe(loc)}\"  (via RootCI[0])");

	                                // Cursor object is often inactive in this mode; use the selected GO position as target.
	                                TryDumpNearbyLabelCandidates(w, "derived.menu", entryGos[bestIdx], entryPos[bestIdx], maxCandidates: 12, maxNodes: 900, ancestorDepth: 4);
	                            }
	                            else
	                            {
	                                w.WriteLine($"derived.selectedIndex=(unmapped)  (RootCI[0].Sel={ciIdx}, entryCount={entryGos.Count})");
	                            }
	                        }
	                    }

	                    // Also expose CI selections for other submenus (handy for mapping without relying on cursors).
	                    if (gbwkObj != null)
	                    {
	                        if (TryReadCmpCursorSel(gbwkObj, "RootCI", 1, out int ciItem, out _))
	                        {
	                            TryGetStaticStringKeyAt(cmpInitType, "gCmpItemRootStr", ciItem, out string key, out string loc);
	                            w.WriteLine($"derived.ci.itemSubSel={ciItem}  derived.ci.itemKey=\"{Safe(key)}\"  derived.ci.itemLoc=\"{Safe(loc)}\"");
	                        }
	                        if (TryReadCmpCursorSel(gbwkObj, "RootCI", 2, out int ciParty, out _))
	                        {
	                            TryGetStaticStringKeyAt(cmpInitType, "gCmpPartyRootStr", ciParty, out string key, out string loc);
	                            w.WriteLine($"derived.ci.partySubSel={ciParty}  derived.ci.partyKey=\"{Safe(key)}\"  derived.ci.partyLoc=\"{Safe(loc)}\"");
	                        }
	                        if (TryReadCmpCursorSel(gbwkObj, "RootCI", 3, out int ciHearts, out _))
	                        {
	                            TryGetStaticStringKeyAt(cmpInitType, "gCmpHeartsRootStr", ciHearts, out string key, out string loc);
	                            w.WriteLine($"derived.ci.heartsSubSel={ciHearts}  derived.ci.heartsKey=\"{Safe(key)}\"  derived.ci.heartsLoc=\"{Safe(loc)}\"");
	                        }
	                        if (TryReadCmpCursorSel(gbwkObj, "RootCI", 4, out int ciSystem, out _))
	                            w.WriteLine($"derived.ci.systemSubSel={ciSystem}");
	                    }

	                    // --- Item list entries (only when item list object is active) ---
	                    try
	                    {
	                        if (TryGetInstanceMemberValue(campUiObj, "itemListObj", out object? ilo))
	                        {
	                            var iloGo = CoerceGameObject(ilo);
	                            if (iloGo != null)
	                            {
	                                w.WriteLine($"itemListObj={DescribeGameObject(iloGo)}");
	                                if (iloGo.activeInHierarchy)
	                                {
	                                    if (TryGetInstanceMemberValue(campUiObj, "itemObj", out object? itemArr) && itemArr != null && TryGetLengthOrCount(itemArr, out int itemLen, out string itemKind))
	                                    {
	                                        w.WriteLine($"itemObj.{itemKind}={itemLen}");
	                                        int itemDumpN = Mathf.Min(itemLen, 10);
	                                        for (int i = 0; i < itemDumpN; i++)
	                                        {
	                                            if (!TryGetIndexedValue(itemArr, i, out object? itObj))
	                                            {
	                                                w.WriteLine($"  itemObj[{i}] = (unreadable)");
	                                                continue;
	                                            }
	                                            var itGo = CoerceGameObject(itObj);
	                                            if (itGo == null)
	                                            {
	                                                w.WriteLine($"  itemObj[{i}] = (null)");
	                                                continue;
	                                            }
	                                            string itTxt = TryFindAnyTextOn(itGo, out string? itFound) ? itFound! : "";
	                                            string itSpr = TryFindAnySpriteOn(itGo, out string? itSprFound) ? itSprFound! : "";
	                                            w.WriteLine($"  itemObj[{i}] = {DescribeGameObject(itGo)}  text=\"{Safe(itTxt)}\"  sprite=\"{Safe(itSpr)}\"  pos={FormatTransformPos(itGo.transform)}");
	                                        }
	                                    }
	                                    	// Derived item selection summary (links cursor -> ItemIdx -> item id)
	                                    	w.WriteLine("--- Derived item selection (best-effort) ---");
	                                    	TryDumpDerivedItemSelectionFromCmp(w, gbwkObj, itemArr);
	                                    	w.WriteLine();
	                                }
	                            }
	                        }
	                    }
	                    catch
	                    {
	                        // ignore
	                    }

				    // --- Party / Stock UI entries (useful for Party root + Summon screen) ---
				    try
				    {
	                        // partyObj: "Summon / Return to stock / Part with"
	                        object? partyArr = null;
	                        if (TryGetInstanceMemberValue(campUiObj, "partyObj", out object? po))
	                            partyArr = po;

	                        var partyEntryGos = new List<GameObject>(16);
	                        var partyEntryPos = new List<Vector3>(16);
	                        var partyEntryText = new List<string>(16);

	                        if (partyArr != null && TryGetLengthOrCount(partyArr, out int partyLen, out string partyKind))
	                        {
	                            w.WriteLine($"partyObj.{partyKind}={partyLen}");
	                            int dumpPartyN = Mathf.Min(partyLen, 8);
	                            for (int i = 0; i < dumpPartyN; i++)
	                            {
	                                if (!TryGetIndexedValue(partyArr, i, out object? pObj))
	                                {
	                                    w.WriteLine($"  partyObj[{i}] = (unreadable)");
	                                    continue;
	                                }
	                                var pGo = CoerceGameObject(pObj);
	                                if (pGo == null)
	                                {
	                                    w.WriteLine($"  partyObj[{i}] = (null)");
	                                    continue;
	                                }
	                                string pTxt = TryFindAnyTextOn(pGo, out string? pFound) ? pFound! : "";
	                                string pSpr = TryFindAnySpriteOn(pGo, out string? pSprFound) ? pSprFound! : "";
	                                w.WriteLine($"  partyObj[{i}] = {DescribeGameObject(pGo)}  text=\"{Safe(pTxt)}\"  sprite=\"{Safe(pSpr)}\"  pos={FormatTransformPos(pGo.transform)}");
	                                partyEntryGos.Add(pGo);
	                                partyEntryPos.Add(pGo.transform.position);
	                                partyEntryText.Add(pTxt);
	                            }
	                        }

	                        // partyCur: cursor(s) used by party menus (often inactive outside the Party root)
	                        GameObject? activePartyCur = null;
	                        int activePartyCurIdx = -1;
	                        if (TryGetInstanceMemberValue(campUiObj, "partyCur", out object? pc) && pc != null && TryGetLengthOrCount(pc, out int pcLen, out string pcKind))
	                        {
	                            w.WriteLine($"partyCur.{pcKind}={pcLen}");
	                            int dumpPcN = Mathf.Min(pcLen, 16);
	                            for (int i = 0; i < dumpPcN; i++)
	                            {
	                                if (!TryGetIndexedValue(pc, i, out object? cObj))
	                                {
	                                    w.WriteLine($"  partyCur[{i}] = (unreadable)");
	                                    continue;
	                                }
	                                var cGo = CoerceGameObject(cObj);
	                                if (cGo == null)
	                                {
	                                    w.WriteLine($"  partyCur[{i}] = (null)");
	                                    continue;
	                                }

	                                bool act = cGo.activeInHierarchy;
	                                w.WriteLine($"  partyCur[{i}] = {DescribeGameObject(cGo)}  pos={FormatTransformPos(cGo.transform)}  path={GetHierarchyPath(cGo.transform)}");
	                                if (act && activePartyCur == null)
	                                {
	                                    activePartyCur = cGo;
	                                    activePartyCurIdx = i;
	                                }
	                            }
	                        }

	                        if (activePartyCur != null && partyEntryGos.Count > 0)
	                        {
	                            Vector3 cur = activePartyCur.transform.position;
	                            int bestIdx = -1;
	                            float bestD = float.MaxValue;
	                            for (int i = 0; i < partyEntryPos.Count; i++)
	                            {
	                                float d = (partyEntryPos[i] - cur).sqrMagnitude;
	                                if (d < bestD)
	                                {
	                                    bestD = d;
	                                    bestIdx = i;
	                                }
	                            }

	                            if (bestIdx >= 0)
	                            {
	                                w.WriteLine($"derived.party.selectedIndex={bestIdx}  derived.party.selectedText=\"{Safe(partyEntryText[bestIdx])}\"  derived.party.cursorIdx={activePartyCurIdx}");
	                                TryDumpNearbyLabelCandidates(w, "derived.party", partyEntryGos[bestIdx], activePartyCur.transform.position, maxCandidates: 12, maxNodes: 900, ancestorDepth: 4);
	                                TryDumpTextNodes(w, "derived.party", partyEntryGos[bestIdx], maxNodes: 12);
	                                TryDumpRowIdentityProbe(w, "derived.party", partyEntryGos[bestIdx], maxComponents: 128, maxHits: 28);
	                            }
}

	                        // stockObj: demon stock list cells for Summon screen (grid/list depending on context)
	                        object? stockArr = null;
	                        if (TryGetInstanceMemberValue(campUiObj, "stockObj", out object? so))
	                            stockArr = so;

	                        var stockEntryGos = new List<GameObject>(32);
	                        var stockEntryPos = new List<Vector3>(32);
	                        var stockEntryText = new List<string>(32);

	                        if (stockArr != null && TryGetLengthOrCount(stockArr, out int stockLen, out string stockKind))
	                        {
	                            w.WriteLine($"stockObj.{stockKind}={stockLen}");
	                            int dumpStockN = Mathf.Min(stockLen, 12);
	                            for (int i = 0; i < dumpStockN; i++)
	                            {
	                                if (!TryGetIndexedValue(stockArr, i, out object? sObj))
	                                {
	                                    w.WriteLine($"  stockObj[{i}] = (unreadable)");
	                                    continue;
	                                }
	                                var sGo = CoerceGameObject(sObj);
	                                if (sGo == null)
	                                {
	                                    w.WriteLine($"  stockObj[{i}] = (null)");
	                                    continue;
	                                }
	                                string sTxt = TryFindAnyTextOn(sGo, out string? sFound) ? sFound! : "";
	                                string sSpr = TryFindAnySpriteOn(sGo, out string? sSprFound) ? sSprFound! : "";
	                                w.WriteLine($"  stockObj[{i}] = {DescribeGameObject(sGo)}  text=\"{Safe(sTxt)}\"  sprite=\"{Safe(sSpr)}\"  pos={FormatTransformPos(sGo.transform)}");
	                                stockEntryGos.Add(sGo);
	                                stockEntryPos.Add(sGo.transform.position);
	                                stockEntryText.Add(sTxt);
	                            }
	                        }

	                        // stockCur: cursor(s) used by Summon stock list (menuCur is often inactive here)
	                        GameObject? activeStockCur = null;
	                        int activeStockCurIdx = -1;
	                        if (TryGetInstanceMemberValue(campUiObj, "stockCur", out object? sc) && sc != null && TryGetLengthOrCount(sc, out int scLen, out string scKind))
	                        {
	                            w.WriteLine($"stockCur.{scKind}={scLen}");
	                            int dumpScN = Mathf.Min(scLen, 16);
	                            for (int i = 0; i < dumpScN; i++)
	                            {
	                                if (!TryGetIndexedValue(sc, i, out object? cObj))
	                                {
	                                    w.WriteLine($"  stockCur[{i}] = (unreadable)");
	                                    continue;
	                                }
	                                var cGo = CoerceGameObject(cObj);
	                                if (cGo == null)
	                                {
	                                    w.WriteLine($"  stockCur[{i}] = (null)");
	                                    continue;
	                                }

	                                bool act = cGo.activeInHierarchy;
	                                w.WriteLine($"  stockCur[{i}] = {DescribeGameObject(cGo)}  pos={FormatTransformPos(cGo.transform)}  path={GetHierarchyPath(cGo.transform)}");
	                                if (act && activeStockCur == null)
	                                {
	                                    activeStockCur = cGo;
	                                    activeStockCurIdx = i;
	                                }
	                            }
	                        }

	                        if (activeStockCur != null)
	                        {
	                            Vector3 cur = activeStockCur.transform.position;

	                            // NOTE: On several camp sub-screens (notably Party -> "Return to stock"),
	                            // the active cursor in stockCur can move over BOTH the party list (top) and the stock list (bottom).
	                            // Derive both indices by nearest world-space cell, then choose a focus by distance.

	                            int partyBestIdx = -1;
	                            float partyBestD2 = float.MaxValue;
	                            if (partyEntryGos.Count > 0)
	                            {
	                                for (int i = 0; i < partyEntryPos.Count; i++)
	                                {
	                                    float d2 = (partyEntryPos[i] - cur).sqrMagnitude;
	                                    if (d2 < partyBestD2)
	                                    {
	                                        partyBestD2 = d2;
	                                        partyBestIdx = i;
	                                    }
	                                }
	                            }

	                            int stockBestIdx = -1;
	                            float stockBestD2 = float.MaxValue;
	                            if (stockEntryGos.Count > 0)
	                            {
	                                for (int i = 0; i < stockEntryPos.Count; i++)
	                                {
	                                    float d2 = (stockEntryPos[i] - cur).sqrMagnitude;
	                                    if (d2 < stockBestD2)
	                                    {
	                                        stockBestD2 = d2;
	                                        stockBestIdx = i;
	                                    }
	                                }
	                            }

	                            if (partyBestIdx >= 0)
	                                w.WriteLine($"derived.party.selectedIndex={partyBestIdx}  derived.party.selectedText=\"{Safe(partyEntryText[partyBestIdx])}\"  derived.party.cursorIdx={activeStockCurIdx}  derived.party.d2={partyBestD2:0.###}");
	                            if (stockBestIdx >= 0)
	                                w.WriteLine($"derived.stock.selectedIndex={stockBestIdx}  derived.stock.selectedText=\"{Safe(stockEntryText[stockBestIdx])}\"  derived.stock.cursorIdx={activeStockCurIdx}  derived.stock.d2={stockBestD2:0.###}");

	                            string focus = "none";
	                            if (partyBestIdx >= 0 && stockBestIdx >= 0)
	                                focus = (partyBestD2 <= stockBestD2) ? "party" : "stock";
	                            else if (partyBestIdx >= 0)
	                                focus = "party";
	                            else if (stockBestIdx >= 0)
	                                focus = "stock";

	                            w.WriteLine($"derived.focus={focus}  cursorIdx={activeStockCurIdx}  cursorGo={activeStockCur.name}  cursorPos={FormatTransformPos(activeStockCur.transform)}");

	                            // Dump nearby text nodes for the focused row (more robust than "under row" when labels live in sibling GOs).
	                            if (focus == "party" && partyBestIdx >= 0)
	                            {
	                                TryDumpNearbyLabelCandidates(w, "derived.party", partyEntryGos[partyBestIdx], activeStockCur.transform.position, maxCandidates: 12, maxNodes: 900, ancestorDepth: 4);
	                                TryDumpTextNodes(w, "derived.party", partyEntryGos[partyBestIdx], maxNodes: 12);
	                            }
	                            else if (focus == "stock" && stockBestIdx >= 0)
	                            {
	                                TryDumpNearbyLabelCandidates(w, "derived.stock", stockEntryGos[stockBestIdx], activeStockCur.transform.position, maxCandidates: 12, maxNodes: 900, ancestorDepth: 4);
	                                TryDumpTextNodes(w, "derived.stock", stockEntryGos[stockBestIdx], maxNodes: 12);
	                                TryDumpRowIdentityProbe(w, "derived.stock", stockEntryGos[stockBestIdx], maxComponents: 128, maxHits: 28);
	                            }
	                        }
	                    }

				    catch
				    {
				        // ignore
				    }
				}
				catch (Exception ex)
				{
	                    w.WriteLine($"(decoded UI failed: {ex.GetType().Name}: {ex.Message})");
	                }
	            }


	            private static bool TryReadCmpCursorSel(object gbwkObj, string memberName, int cursorIndex, out int sel, out int listNums)
	            {
	                sel = -1;
	                listNums = -1;
	                try
	                {
	                    if (!TryGetInstanceMemberValue(gbwkObj, memberName, out object? ciArr) || ciArr == null)
	                        return false;

	                    if (!TryGetLengthOrCount(ciArr, out int len, out _))
	                        return false;

	                    if (cursorIndex < 0 || cursorIndex >= len)
	                        return false;

	                    object? ci = null;
	                    try { ci = TryGetIndexValue(ciArr, cursorIndex); } catch { ci = null; }
	                    if (ci == null)
	                        return false;

	                    if (!TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) || cp == null)
	                        return false;

	                    int idx = TryReadInt32(cp, "Index", -1);
	                    listNums = TryReadInt32(cp, "ListNums", -1);
	                    int shift = TryReadInt32(cp, "Shift", -1);

	                    if (idx < 0 || shift < 0)
	                        return false;

	                    sel = idx + shift;
	                    return true;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            

	            private static bool TryReadCmpCursorPos(object gbwkObj, string memberName, int cursorIndex, out int idx, out int shift, out int listNums, out int shiftMax)
	            {
	                idx = -1;
	                shift = -1;
	                listNums = -1;
	                shiftMax = -1;
	                try
	                {
	                    if (!TryGetInstanceMemberValue(gbwkObj, memberName, out object? ciArr) || ciArr == null)
	                        return false;

	                    if (!TryGetLengthOrCount(ciArr, out int len, out _))
	                        return false;

	                    if (cursorIndex < 0 || cursorIndex >= len)
	                        return false;

	                    object? ci = null;
	                    try { ci = TryGetIndexValue(ciArr, cursorIndex); } catch { ci = null; }
	                    if (ci == null)
	                        return false;

	                    // CursorPos.Index is the visible-row index; Shift is the scroll offset.
	                    if (TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) && cp != null)
	                    {
	                        idx = TryReadInt32(cp, "Index", -1);
	                    }

	                    // Prefer reading list/scroll info from CursorPos (SMT3HD uses these on the CursorPos struct).
	                    if (TryGetInstanceMemberValue(ci, "CursorPos", out object? cp2) && cp2 != null)
	                    {
	                        int ln2 = TryReadInt32(cp2, "ListNums", -1);
	                        int sh2 = TryReadInt32(cp2, "Shift", -1);
	                        int sm2 = TryReadInt32(cp2, "ShiftMax", -1);
	                        if (ln2 >= 0) listNums = ln2;
	                        if (sh2 >= 0) shift = sh2;
	                        if (sm2 >= 0) shiftMax = sm2;
	                    }

	                    // Fallbacks for other builds where these live on the CI object.
	                    if (listNums < 0) listNums = TryReadInt32(ci, "ListNums", -1);
	                    if (shift < 0) shift = TryReadInt32(ci, "Shift", -1);
	                    if (shiftMax < 0) shiftMax = TryReadInt32(ci, "ShiftMax", -1);

	                    return (idx >= 0 && shift >= 0);
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            private static void TryDumpDerivedItemSelectionFromCmp(TextWriter w, object? gbwkObj, object? itemObjArr)
	            {
	                try
	                {
	                    if (gbwkObj == null)
	                        return;

	                    // WorkCI[0] appears to be the item-list cursor in the camp command menu flow.
	                    if (!TryReadCmpCursorPos(gbwkObj, "WorkCI", 0, out int idx, out int shift, out int listNums, out int shiftMax))
	                    {
	                        w.WriteLine("derived.item: (WorkCI[0] unavailable)");
	                        return;
	                    }

	                    int overall = idx + shift;

	                    // Map overall index -> item id via ItemIdx array.
	                    int itemId = -1;
	                    string itemName = "";
	                    if (TryGetInstanceMemberValue(gbwkObj, "ItemIdx", out object? itemIdxArr) && itemIdxArr != null)
	                    {
	                        if (TryGetIndexedValue(itemIdxArr, overall, out object? v) && v != null)
	                        {
	                            try { itemId = Convert.ToInt32(v); } catch { itemId = -1; }
	                            if (itemId >= 0)
	                                TryGetItemName(itemId, out itemName);
	                        }
	                    }

	                    w.WriteLine($"derived.item.cursorIndex={idx}  derived.item.shift={shift}{(shiftMax >= 0 ? $"/{shiftMax}" : "")}  derived.item.overallIndex={overall}{(listNums >= 0 ? $" (listNums={listNums})" : "")}");
	                    if (itemId >= 0)
	                        w.WriteLine($"derived.item.itemId={itemId}  derived.item.itemName=\"{Safe(itemName)}\"");
	                    else
	                        w.WriteLine("derived.item.itemId=(unresolved)");

	                    // If we have the visible item row objects, try to show the currently highlighted row.
	                    if (itemObjArr != null && idx >= 0 && TryGetIndexedValue(itemObjArr, idx, out object? rowObj))
	                    {
	                        var rowGo = CoerceGameObject(rowObj);
	                        if (rowGo != null)
	                        {
	                            string rowTxt = TryFindAnyTextOn(rowGo, out string? t) ? t! : "";
	                            string rowSpr = TryFindAnySpriteOn(rowGo, out string? s) ? s! : "";
	                            string rowMenuTxt = TryFindCampMenuListTextOn(rowGo, out string? mtxt) ? mtxt! : "";
	                            w.WriteLine($"derived.item.rowGo={DescribeGameObject(rowGo)}  text=\"{Safe(rowTxt)}\"  campMenuText=\"{Safe(rowMenuTxt)}\"  sprite=\"{Safe(rowSpr)}\"  pos={FormatTransformPos(rowGo.transform)}");
	                            TryDumpNearbyLabelCandidates(w, "derived.item", rowGo, rowGo.transform.position, maxCandidates: 10, maxNodes: 800, ancestorDepth: 3);
	                            TryDumpTextNodes(w, "derived.item", rowGo, maxNodes: 10);
	                            TryDumpRowIdentityProbe(w, "derived.item", rowGo, maxComponents: 96, maxHits: 24);
	                        }
	                    }
	                }
	                catch
	                {
	                    // ignore
	                }
	            }

	            private 
	            // =========================================================
	            // Il2Cpp class-name helpers (best-effort)
	            // =========================================================
	            static Type? s_il2cppApiType;
	            static MethodInfo? s_il2cpp_object_get_class;
	            static MethodInfo? s_il2cpp_class_get_name;
	            static MethodInfo? s_il2cpp_class_get_namespace;

	            private static bool EnsureIl2CppApiBound()
	            {
	                try
	                {
	                    if (s_il2cppApiType != null)
	                        return true;

	                    s_il2cppApiType =
	                        FindTypeInLoadedAssemblies("Il2CppInterop.Runtime.IL2CPP") ??
	                        FindTypeInLoadedAssemblies("Il2CppInterop.Runtime.IL2CPP+IL2CPP") ??
	                        FindTypeInLoadedAssemblies("Il2CppInterop.Runtime.IL2CPP.IL2CPP");

	                    if (s_il2cppApiType == null)
	                        return false;

	                    s_il2cpp_object_get_class = s_il2cppApiType.GetMethod(
	                        "il2cpp_object_get_class",
	                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                        null,
	                        new[] { typeof(IntPtr) },
	                        null);

	                    s_il2cpp_class_get_name = s_il2cppApiType.GetMethod(
	                        "il2cpp_class_get_name",
	                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                        null,
	                        new[] { typeof(IntPtr) },
	                        null);

	                    s_il2cpp_class_get_namespace = s_il2cppApiType.GetMethod(
	                        "il2cpp_class_get_namespace",
	                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
	                        null,
	                        new[] { typeof(IntPtr) },
	                        null);

	                    return (s_il2cpp_class_get_name != null);
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            private static bool TryGetIl2CppClassPtrFromObject(object obj, out IntPtr klass)
	            {
	                klass = IntPtr.Zero;
	                try
	                {
	                    if (obj == null)
	                        return false;

	                    // Many generated Il2Cpp wrappers expose ObjectClass directly.
	                    if (TryGetInstanceMemberValue(obj, "ObjectClass", out object? oc) && oc != null)
	                    {
	                        if (oc is IntPtr ip && ip != IntPtr.Zero)
	                        {
	                            klass = ip;
	                            return true;
	                        }

	                        // Sometimes boxed as long/ulong.
	                        if (oc is long l && l != 0)
	                        {
	                            klass = new IntPtr(l);
	                            return true;
	                        }
	                        if (oc is ulong ul && ul != 0)
	                        {
	                            klass = new IntPtr(unchecked((long)ul));
	                            return true;
	                        }
	                    }

	                    // Fallback: take Pointer and ask IL2CPP for the class.
	                    if (!EnsureIl2CppApiBound() || s_il2cpp_object_get_class == null)
	                        return false;

	                    if (TryGetInstanceMemberValue(obj, "Pointer", out object? p) && p != null)
	                    {
	                        IntPtr ptr = IntPtr.Zero;
	                        if (p is IntPtr ip2) ptr = ip2;
	                        else if (p is long l2) ptr = new IntPtr(l2);
	                        else if (p is ulong ul2) ptr = new IntPtr(unchecked((long)ul2));

	                        if (ptr != IntPtr.Zero)
	                        {
	                            object? r = s_il2cpp_object_get_class.Invoke(null, new object[] { ptr });
	                            if (r is IntPtr ip3 && ip3 != IntPtr.Zero)
	                            {
	                                klass = ip3;
	                                return true;
	                            }
	                        }
	                    }
	                }
	                catch
	                {
	                }

	                return false;
	            }

	            private static bool TryResolveIl2CppClassName(IntPtr klass, out string ns, out string name)
	            {
	                ns = "";
	                name = "";
	                try
	                {
	                    if (klass == IntPtr.Zero)
	                        return false;

	                    if (!EnsureIl2CppApiBound() || s_il2cpp_class_get_name == null)
	                        return false;

	                    // name
	                    object? rn = s_il2cpp_class_get_name.Invoke(null, new object[] { klass });
	                    if (rn is IntPtr pName && pName != IntPtr.Zero)
	                        name = Marshal.PtrToStringAnsi(pName) ?? "";
	                    else if (rn is string sName)
	                        name = sName ?? "";

	                    if (s_il2cpp_class_get_namespace != null)
	                    {
	                        object? rns = s_il2cpp_class_get_namespace.Invoke(null, new object[] { klass });
	                        if (rns is IntPtr pNs && pNs != IntPtr.Zero)
	                            ns = Marshal.PtrToStringAnsi(pNs) ?? "";
	                        else if (rns is string sNs)
	                            ns = sNs ?? "";
	                    }

	                    return !string.IsNullOrEmpty(name);
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            private static string GetIl2CppClassKey(object obj)
	            {
	                try
	                {
	                    if (TryGetIl2CppClassPtrFromObject(obj, out IntPtr klass) && klass != IntPtr.Zero)
	                    {
	                        if (TryResolveIl2CppClassName(klass, out string ns, out string name))
	                        {
	                            string full = string.IsNullOrEmpty(ns) ? name : (ns + "." + name);
	                            return $"{full}@0x{klass.ToInt64():X}";
	                        }

	                        return $"0x{klass.ToInt64():X}";
	                    }
	                }
	                catch
	                {
	                }

	                var t = obj.GetType();
	                return (t.FullName ?? t.Name);
	            }

	            private static bool TryGetComponentsDirectBestEffort(GameObject go, out List<Component> comps)
	            {
	                comps = new List<Component>(16);
	                try
	                {
	                    if (go == null)
	                        return false;

	                    // Prefer the non-generic overload: GetComponents(Type).
	                    MethodInfo? mi = go.GetType().GetMethod("GetComponents", new[] { typeof(Type) });
	                    if (mi == null)
	                        mi = typeof(GameObject).GetMethod("GetComponents", new[] { typeof(Type) });

	                    if (mi == null)
	                        return false;

	                    object? arr = mi.Invoke(go, new object[] { typeof(Component) });
	                    if (arr == null)
	                        return false;

	                    if (arr is Component[] ca)
	                    {
	                        comps.AddRange(ca.Where(c => c != null));
	                        return true;
	                    }

	                    if (TryGetLengthOrCount(arr, out int len, out _))
	                    {
	                        int take = Math.Min(len, 256);
	                        for (int i = 0; i < take; i++)
	                        {
	                            object? v = null;
	                            try { v = TryGetIndexValue(arr, i); } catch { v = null; }
	                            if (v is Component c && c != null)
	                                comps.Add(c);
	                        }

	                        return comps.Count > 0;
	                    }
	                }
	                catch
	                {
	                }

	                return false;
	            }
static void TryDumpRowIdentityProbe(TextWriter w, string label, GameObject go, int maxComponents, int maxHits)
	            {
	                try
	                {
	                    if (go == null || maxComponents <= 0 || maxHits <= 0)
	                        return;

	                    if (!TryGetComponentsInChildrenBestEffort(go, includeInactive: true, out var comps))
	                    {
	                        w.WriteLine($"  {label}.rowProbe[error] (component enumeration failed under {go.name})");
	                        return;
	                    }

	                    // Type histogram (helps identify custom UI binder components).
	                    var typeCounts = new Dictionary<string, int>();
	                    int take = Math.Min(maxComponents, comps.Count);
	                    for (int i = 0; i < take; i++)
	                    {
	                        var c = comps[i];
	                        if (c == null) continue;
	                        string tn = GetIl2CppClassKey(c);
	                        if (typeCounts.TryGetValue(tn, out int cur)) typeCounts[tn] = cur + 1;
	                        else typeCounts[tn] = 1;
	                    }

	                    // Print top 12 types by count (stable ordering).
	                    var tops = new List<KeyValuePair<string, int>>(typeCounts);
	                    tops.Sort((a, b) =>
	                    {
	                        int dc = b.Value.CompareTo(a.Value);
	                        if (dc != 0) return dc;
	                        return string.CompareOrdinal(a.Key, b.Key);
	                    });

	                    int showTypes = Math.Min(12, tops.Count);
	                    w.WriteLine($"  {label}.rowProbe.types (showing {showTypes}/{tops.Count}, scanned {take}/{comps.Count} components)");
	                    for (int i = 0; i < showTypes; i++)
	                    {
	                        var kv = tops[i];
	                        w.WriteLine($"    [{i}] {kv.Value}x {kv.Key}");
	                    }

	                    
	                    // Ancestor probe: sometimes the binder lives one or two parents up (row container owns the data).
	                    w.WriteLine($"  {label}.rowProbe.ancestors (direct components, depth<=3)");
	                    try
	                    {
	                        Transform? t = go.transform;
	                        for (int depth = 0; depth <= 3 && t != null; depth++)
	                        {
	                            var g = t.gameObject;
	                            if (g == null) break;

	                            if (TryGetComponentsDirectBestEffort(g, out var ac) && ac.Count > 0)
	                            {
	                                // Build a small unique set of class keys (skip Transform noise).
	                                var seen = new List<string>();
	                                int takeA = Math.Min(32, ac.Count);
	                                for (int j = 0; j < takeA; j++)
	                                {
	                                    var c = ac[j];
	                                    if (c == null) continue;
	                                    string k = GetIl2CppClassKey(c);
	                                    if (k.IndexOf("Transform", StringComparison.OrdinalIgnoreCase) >= 0)
	                                        continue;
	                                    if (!seen.Contains(k))
	                                        seen.Add(k);
	                                    if (seen.Count >= 8)
	                                        break;
	                                }

	                                if (seen.Count > 0)
	                                {
	                                    w.WriteLine($"    depth={depth} go={g.name} classes={string.Join(", ", seen)}");
	                                }
	                                else
	                                {
	                                    w.WriteLine($"    depth={depth} go={g.name} classes=(Transform-only)");
	                                }
	                            }
	                            else
	                            {
	                                w.WriteLine($"    depth={depth} go={g.name} classes=(none)");
	                            }

	                            t = t.parent;
	                        }
	                    }
	                    catch
	                    {
	                        w.WriteLine($"    (ancestor probe error)");
	                    }

// Member probe (strings + scalar ids) for non-Transform components.
	                    int wrote = 0;
	                    w.WriteLine($"  {label}.rowProbe.members (showing up to {maxHits})");
	                    for (int i = 0; i < take && wrote < maxHits; i++)
	                    {
	                        var c = comps[i];
	                        if (c == null) continue;

	                        var ct = c.GetType();
	                        string full = ct.FullName ?? ct.Name;
	                        string typeKey = GetIl2CppClassKey(c);

	                        // Skip Transform-y noise.
	                        if (full.IndexOf("UnityEngine.Transform", StringComparison.OrdinalIgnoreCase) >= 0
	                            || full.IndexOf("RectTransform", StringComparison.OrdinalIgnoreCase) >= 0)
	                            continue;

	                        // Collect a few string members.
	                        var pairs = new List<(string member, string value)>(4);
	                        CollectStringMemberCandidates(c, pairs, maxPairs: 4);

	                        // Collect a few scalar/id members.
	                        CollectScalarMemberCandidates(c, pairs, maxPairs: 4);

	                        for (int p = 0; p < pairs.Count && wrote < maxHits; p++)
	                        {
	                            w.WriteLine($"    [{wrote}] typeKey={typeKey} managedType={full} member={pairs[p].member} value=\"{Safe(pairs[p].value)}\"");
	                            wrote++;
	                        }
	                    }

	                    if (wrote == 0)
	                        w.WriteLine($"    (no useful members found; likely name/id is not stored as simple fields on UI components)");
	                }
	                catch
	                {
	                    // ignore
	                }
	            }

	            private static void CollectScalarMemberCandidates(object comp, List<(string member, string value)> outPairs, int maxPairs)
	            {
	                if (comp == null || outPairs == null || maxPairs <= 0)
	                    return;

	                try
	                {
	                    var t = comp.GetType();

	                    bool NameOk(string n)
	                    {
	                        if (string.IsNullOrEmpty(n)) return false;
	                        string ln = n.ToLowerInvariant();

	                        // Likely ids/indices
	                        if (ln == "id" || ln.EndsWith("id") || ln.Contains("_id")) return true;
	                        if (ln.Contains("index") || ln.EndsWith("idx")) return true;
	                        if (ln.Contains("no") || ln.EndsWith("num")) return true;
	                        if (ln.Contains("devil") || ln.Contains("demon") || ln.Contains("nakama") || ln.Contains("chara") || ln.Contains("unit")) return true;
	                        if (ln.Contains("party") || ln.Contains("stock")) return true;

	                        return false;
	                    }

	                    bool TypeOk(Type mt)
	                    {
	                        if (mt == typeof(int) || mt == typeof(short) || mt == typeof(byte) || mt == typeof(long)) return true;
	                        if (mt.IsEnum) return true;
	                        return false;
	                    }

	                    // Properties
	                    var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    for (int i = 0; i < props.Length && outPairs.Count < maxPairs; i++)
	                    {
	                        var p = props[i];
	                        if (p == null || p.GetIndexParameters().Length != 0) continue;
	                        if (!NameOk(p.Name)) continue;
	                        if (!TypeOk(p.PropertyType)) continue;

	                        object? v = null;
	                        try { v = p.GetValue(comp, null); } catch { v = null; }
	                        if (v == null) continue;
	                        outPairs.Add(($"scalar:{p.Name}", v.ToString() ?? ""));
	                    }

	                    // Fields
	                    var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    for (int i = 0; i < fields.Length && outPairs.Count < maxPairs; i++)
	                    {
	                        var f = fields[i];
	                        if (f == null) continue;
	                        if (!NameOk(f.Name)) continue;
	                        if (!TypeOk(f.FieldType)) continue;

	                        object? v = null;
	                        try { v = f.GetValue(comp); } catch { v = null; }
	                        if (v == null) continue;
	                        outPairs.Add(($"scalar:{f.Name}", v.ToString() ?? ""));
	                    }
	                }
	                catch
	                {
	                    // ignore
	                }
	            }

private static bool TryGetInstanceMemberValue(object obj, string memberName, out object? value)
	            {
	                value = null;
	                try
	                {
	                    var t = obj.GetType();
	                    var p = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    if (p != null && p.GetIndexParameters().Length == 0)
	                    {
	                        value = p.GetValue(obj, null);
	                        return true;
	                    }
	                    var f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    if (f != null)
	                    {
	                        value = f.GetValue(obj);
	                        return true;
	                    }
	                    return false;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            private static GameObject? CoerceGameObject(object? o)
	            {
	                try
	                {
	                    if (o == null)
	                        return null;
	                    if (o is GameObject go)
	                        return go;
	                    if (o is Component c)
	                        return c.gameObject;
	                    var p = o.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    if (p != null && p.GetIndexParameters().Length == 0)
	                    {
	                        var v = p.GetValue(o, null);
	                        if (v is GameObject go2)
	                            return go2;
	                    }
	                    return null;
	                }
	                catch
	                {
	                    return null;
	                }
	            }

	            private static bool TryGetTextProperty(object? textObj, out string? text)
{
    text = null;
    try
    {
        if (textObj == null)
            return false;

        var t = textObj.GetType();

        // IL2CPP wrappers sometimes expose text as Il2CppSystem.String (not System.String).
        // Be permissive: accept any return type and convert via ToString().
        var p = t.GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.GetIndexParameters().Length == 0)
        {
            object? v = null;
            try { v = p.GetValue(textObj, null); } catch { v = null; }
            if (v != null)
            {
                // If this is already a managed string, keep it.
                if (v is string s)
                {
                    text = s;
                    return true;
                }

                // Il2CppSystem.String and many UI wrappers stringify correctly.
                text = v.ToString();
                return !string.IsNullOrEmpty(text);
            }
        }

        var m = t.GetMethod("get_text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (m != null && m.GetParameters().Length == 0)
        {
            object? v = null;
            try { v = m.Invoke(textObj, null); } catch { v = null; }
            if (v != null)
            {
                if (v is string s)
                {
                    text = s;
                    return true;
                }
                text = v.ToString();
                return !string.IsNullOrEmpty(text);
            }
        }

        // Fallback: common backing fields on TMPro
        var f = t.GetField("m_text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null)
        {
            object? v = null;
            try { v = f.GetValue(textObj); } catch { v = null; }
            if (v != null)
            {
                if (v is string s)
                {
                    text = s;
                    return true;
                }
                text = v.ToString();
                return !string.IsNullOrEmpty(text);
            }
        }

        return false;
    }
    catch
    {
        return false;
    }
}

// Best-effort sprite extraction (for sprite-driven UI labels).
private static bool TryGetSpriteInfo(object? compObj, out string? spriteInfo)
{
    spriteInfo = null;
    try
    {
        if (compObj == null)
            return false;

        // Fast-path for known Unity components.
        try
        {
            if (compObj is Image img)
            {
                var sp = img.sprite;
                if (sp != null)
                {
                    string tex = "";
                    try { if (sp.texture != null) tex = sp.texture.name; } catch { tex = ""; }
                    spriteInfo = string.IsNullOrEmpty(tex) ? sp.name : (sp.name + "@" + tex);
                    return true;
                }
            }
        }
        catch { /* ignore */ }

        try
        {
            if (compObj is SpriteRenderer sr)
            {
                var sp = sr.sprite;
                if (sp != null)
                {
                    string tex = "";
                    try { if (sp.texture != null) tex = sp.texture.name; } catch { tex = ""; }
                    spriteInfo = string.IsNullOrEmpty(tex) ? sp.name : (sp.name + "@" + tex);
                    return true;
                }
            }
        }
        catch { /* ignore */ }

        // Reflection fallback: look for sprite-like property/field.
        var t = compObj.GetType();

        // property "sprite"
        var p = t.GetProperty("sprite", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.GetIndexParameters().Length == 0)
        {
            object? v = null;
            try { v = p.GetValue(compObj, null); } catch { v = null; }
            if (v is Sprite sp)
            {
                string tex = "";
                try { if (sp.texture != null) tex = sp.texture.name; } catch { tex = ""; }
                spriteInfo = string.IsNullOrEmpty(tex) ? sp.name : (sp.name + "@" + tex);
                return true;
            }
        }

        var f = t.GetField("sprite", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null)
        {
            object? v = null;
            try { v = f.GetValue(compObj); } catch { v = null; }
            if (v is Sprite sp)
            {
                string tex = "";
                try { if (sp.texture != null) tex = sp.texture.name; } catch { tex = ""; }
                spriteInfo = string.IsNullOrEmpty(tex) ? sp.name : (sp.name + "@" + tex);
                return true;
            }
        }

        return false;
    }
    catch
    {
        spriteInfo = null;
        return false;
    }
}

// Probe string-like members on a component when it doesn't expose a "text" property.
// This helps discover custom UI systems (e.g., components that store localization keys).
private static void CollectStringMemberCandidates(object compObj, List<(string member, string value)> outPairs, int maxPairs)
{
    if (compObj == null || outPairs == null || maxPairs <= 0)
        return;

    try
    {
        var t = compObj.GetType();

        bool NameOk(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string ln = n.ToLowerInvariant();

            // Always reject noisy/common Unity names.
            if (ln == "name" || ln == "m_name" || ln == "tag") return false;
            if (ln.Contains("hideflags")) return false;

            // Reject Transform-ish false positives (localPosition/localEulerAngles/localScale),
            // but keep things like "localize" / "localization".
            if (ln.StartsWith("local") && !ln.StartsWith("localize") && !ln.StartsWith("localisation") && !ln.StartsWith("localization"))
                return false;

            if (ln.Contains("position") || ln.Contains("euler") || ln.Contains("rotation") || ln.Contains("scale"))
                return false;

            // Heuristic: only keep things that plausibly encode text/labels/localization keys.
            return ln.Contains("text")
                   || ln.Contains("label")
                   || ln.Contains("caption")
                   || ln.Contains("title")
                   || ln.Contains("mes") || ln.Contains("msg") || ln.Contains("message")
                   || ln.Contains("str") || ln.Contains("string")
                   || ln.Contains("key")
                   || (ln.Contains("loc") && !ln.Contains("local")); // "loc" is ambiguous; guard against "local*".
        }

        // Properties
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < props.Length && outPairs.Count < maxPairs; i++)
        {
            var p = props[i];
            if (p == null) continue;
            if (p.GetIndexParameters().Length != 0) continue;
            if (!NameOk(p.Name)) continue;

            object? v = null;
            try { v = p.GetValue(compObj, null); } catch { v = null; }
            if (v == null) continue;

            string s = UnwrapToString(v);
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (s.Length > 256) s = s.Substring(0, 256) + "…";
            outPairs.Add((p.Name, s));
        }

        // Fields
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < fields.Length && outPairs.Count < maxPairs; i++)
        {
            var f = fields[i];
            if (f == null) continue;
            if (!NameOk(f.Name)) continue;
            object? v = null;
            try { v = f.GetValue(compObj); } catch { v = null; }
            if (v == null) continue;
            string s = UnwrapToString(v);
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (s.Length > 256) s = s.Substring(0, 256) + "…";
            outPairs.Add((f.Name, s));
        }
    }
    catch
    {
        // ignore
    }
}

private static bool TryGetIndexedValue(object arrObj, int index, out object? value)
	            {
	                value = null;
	                try
	                {
	                    if (arrObj is Array a)
	                    {
	                        if (index < 0 || index >= a.Length)
	                            return false;
	                        value = a.GetValue(index);
	                        return true;
	                    }
	
	                    var t = arrObj.GetType();
	                    var pItem = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	                    if (pItem != null)
	                    {
	                        var idxParams = pItem.GetIndexParameters();
	                        if (idxParams.Length == 1 && idxParams[0].ParameterType == typeof(int))
	                        {
	                            value = pItem.GetValue(arrObj, new object[] { index });
	                            return true;
	                        }
	                    }
	                    var mGet = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
	                    if (mGet != null)
	                    {
	                        value = mGet.Invoke(arrObj, new object[] { index });
	                        return true;
	                    }
	                    return false;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            private static bool TryFindAnyTextOn(GameObject go, out string? text)
	            {
	                text = null;
	                try
	                {
	                    // IMPORTANT: SMT3HD IL2CPP surface often strips/changes the generic overload.
	                    // Do not call GetComponentsInChildren<T>() directly; use reflection to support both:
	                    //   - Component[] GetComponentsInChildren(bool includeInactive)
	                    //   - Component[] GetComponentsInChildren(Type t, bool includeInactive)
	                    // and any Il2CppInterop wrapper arrays.
	                    if (!TryGetComponentsInChildrenBestEffort(go, includeInactive: true, out var comps))
	                        return false;

	                    string? best = null;
	                    int bestScore = int.MinValue;

	                    for (int i = 0; i < comps.Count; i++)
	                    {
	                        var c = comps[i];
	                        if (c == null)
	                            continue;

	                        var ct = c.GetType();
	                        string full = ct.FullName ?? ct.Name;
	                        string typeKey = GetIl2CppClassKey(c);

	                        bool looksText = full.IndexOf("TMPro", StringComparison.OrdinalIgnoreCase) >= 0
	                                         || full.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0;
	                        if (!looksText)
	                            continue;

	                        if (!TryGetTextProperty(c, out string? raw))
	                            continue;

	                        string trimmed = (raw ?? "").Trim();
	                        bool nonEmpty = trimmed.Length > 0;

	                        // Prefer non-empty, prefer TMPro, and (when possible) prefer nodes whose path looks like "name".
	                        int score = 0;
	                        if (nonEmpty)
	                            score += 1000;
	                        if (full.IndexOf("TMPro", StringComparison.OrdinalIgnoreCase) >= 0)
	                            score += 200;

	                        string path = "";
	                        try
	                        {
	                            if (c is Component uc)
	                                path = GetHierarchyPath(uc.transform);
	                        }
	                        catch { path = ""; }

	                        if (!string.IsNullOrEmpty(path))
	                        {
	                            if (path.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
	                                score += 120;
	                            if (path.IndexOf("lv", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0)
	                                score += 30;
	                        }

	                        score += Math.Min(trimmed.Length, 120);

	                        // Keep at least *something* if all candidates are empty.
	                        if (best == null || score > bestScore)
	                        {
	                            best = trimmed;
	                            bestScore = score;
	                        }
	                    }

	                    if (best != null)
	                    {
	                        text = best;
	                        return true;
	                    }

	                    return false;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            
	            // Many camp list entries carry their visible label via a campMenu component's listText field.
	            // This tends to be more reliable than scanning for arbitrary TMPro nodes (some lists are sprite-driven).
	            private static bool TryFindCampMenuListTextOn(GameObject go, out string? text)
	            {
	                text = null;
	                try
	                {
	                    if (!TryGetComponentsInChildrenBestEffort(go, includeInactive: true, out var comps))
	                        return false;

	                    for (int i = 0; i < comps.Count; i++)
	                    {
	                        var c = comps[i];
	                        if (c == null)
	                            continue;

	                        var ct = c.GetType();
	                        string full = ct.FullName ?? ct.Name;
	                        if (full.IndexOf("campMenu", StringComparison.OrdinalIgnoreCase) < 0)
	                            continue;

	                        // campMenu.listText is typically a TMPro.TMP_Text.
	                        if (!TryGetInstanceMemberValue(c, "listText", out object? lt) || lt == null)
	                            continue;

	                        if (TryGetTextProperty(lt, out string? raw))
	                        {
	                            string trimmed = (raw ?? "").Trim();
	                            if (trimmed.Length > 0)
	                            {
	                                text = trimmed;
	                                return true;
	                            }
	                        }

	                        // Fallback if listText is wrapped or stored strangely.
	                        string s = UnwrapToString(lt).Trim();
	                        if (s.Length > 0)
	                        {
	                            text = s;
	                            return true;
	                        }
	                    }

	                    return false;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

// Many SMT3HD camp/menu lists appear to be sprite-driven (localized sprite atlases),
	            // so we capture sprite names as first-class "what the player sees" labels.
	            private static bool TryFindAnySpriteOn(GameObject go, out string? spriteInfo)
	            {
	                spriteInfo = null;
	                try
	                {
	                    if (!TryGetComponentsInChildrenBestEffort(go, includeInactive: true, out var comps))
	                        return false;

	                    string? best = null;
	                    int bestScore = int.MinValue;

	                    for (int i = 0; i < comps.Count; i++)
	                    {
	                        var c = comps[i];
	                        if (c == null)
	                            continue;

	                        if (!TryGetSpriteInfo(c, out string? info))
	                            continue;

	                        string s = (info ?? "").Trim();
	                        if (string.IsNullOrWhiteSpace(s))
	                            continue;

	                        // Prefer labels that look like menu text sprites.
	                        int score = 0;
	                        if (s.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0)
	                            score += 200;
	                        if (s.IndexOf("cmd", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("camp", StringComparison.OrdinalIgnoreCase) >= 0)
	                            score += 80;
	                        score += Math.Min(s.Length, 120);

	                        // Prefer nearer-to-root components (often the intended label sprite)
	                        try
	                        {
	                            if (c is Component uc)
	                            {
	                                string path = GetHierarchyPath(uc.transform);
	                                if (path.IndexOf("txt", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0)
	                                    score += 120;
	                            }
	                        }
	                        catch { /* ignore */ }

	                        if (best == null || score > bestScore)
	                        {
	                            best = s;
	                            bestScore = score;
	                        }
	                    }

	                    if (best != null)
	                    {
	                        spriteInfo = best;
	                        return true;
	                    }

	                    return false;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

	            // Dump a small number of non-empty text nodes under a UI cell. This is extremely useful
	            // for mapping "what the player sees" without hard-binding to TMPro types.
	            private static void TryDumpTextNodes(TextWriter w, string label, GameObject go, int maxNodes)
	            {
	                try
	                {
	                    if (maxNodes <= 0)
	                        return;

	                    if (!TryGetComponentsInChildrenBestEffort(go, includeInactive: true, out var comps))
                        {
                            w.WriteLine($"  {label}.textNode[error] (component enumeration failed under {go.name})");
                            return;
                        }

	                    int wrote = 0;
	                    for (int i = 0; i < comps.Count && wrote < maxNodes; i++)
	                    {
	                        var c = comps[i];
	                        if (c == null)
	                            continue;

	                        var ct = c.GetType();
	                        string full = ct.FullName ?? ct.Name;
	                        string typeKey = GetIl2CppClassKey(c);

	                        bool looksText = full.IndexOf("TMPro", StringComparison.OrdinalIgnoreCase) >= 0
	                                         || full.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0;
	                        if (!looksText)
	                            continue;

	                        if (!TryGetTextProperty(c, out string? raw))
	                            continue;

	                        string s = (raw ?? "").Trim();
	                        if (string.IsNullOrWhiteSpace(s))
	                            continue;

	                        string path = "";
	                        try
	                        {
	                            if (c is Component uc)
	                                path = GetHierarchyPath(uc.transform);
	                        }
	                        catch { path = ""; }

	                        w.WriteLine($"  {label}.textNode[{wrote}] type={full} path=\"{Safe(path)}\" text=\"{Safe(s)}\"");
	                        wrote++;
	                    }

	                    if (wrote == 0)
	                        w.WriteLine($"  {label}.textNode[none] (no non-empty text nodes found under {go.name})");
	                }
	                catch
	                {
	                    // ignore
	                }
	            }
	            // More robust than "text under row": many SMT3HD list cells store visible labels in sibling/nearby objects.
	            // This dumps a small set of non-empty text nodes that are spatially closest to the cursor/target position,
	            // searching under the selected row and a few ancestor roots.
	            private struct NearLabelNode
	            {
	                public string Kind;
	                public string TypeName;
	                public string Path;
	                public string Value;
	                public float D2;
	            }

	            // Collect and print the closest "label" candidates (Text, Sprite name, custom string members)
	            // near a cursor/selected row. This is the primary tool for mapping UI lists that are not TMPro-driven.
	            private static void TryDumpNearbyLabelCandidates(
	                TextWriter w,
	                string label,
	                GameObject pivot,
	                Vector3 targetPos,
	                int maxCandidates,
	                int maxNodes,
	                int ancestorDepth)
	            {
	                try
	                {
	                    if (pivot == null || maxCandidates <= 0)
	                        return;

	                    Transform root = pivot.transform;
	                    for (int i = 0; i < ancestorDepth; i++)
	                    {
	                        if (root.parent == null)
	                            break;
	                        root = root.parent;
	                    }

	                    var nodes = new List<NearLabelNode>(64);
	                    CollectNearbyLabelCandidatesUnder(root, targetPos, nodes, maxCandidates: Math.Max(32, maxCandidates * 6), maxNodes: maxNodes);
	                    if (nodes.Count == 0)
	                    {
	                        w.WriteLine($"  {label}.nearLabel[none] (root={GetHierarchyPath(root)})");
	                        return;
	                    }

	                    // Sort by kind priority first (text/sprite before string-members), then by distance.
					nodes.Sort((a, b) =>
					{
						int pa = (a.Kind == "text") ? 0 : (a.Kind == "sprite") ? 1 : 2;
						int pb = (b.Kind == "text") ? 0 : (b.Kind == "sprite") ? 1 : 2;
						if (pa != pb)
							return pa.CompareTo(pb);
						return a.D2.CompareTo(b.D2);
					});

					// If we found any actual UI labels (text/sprite), prefer those and only sprinkle in
					// string-members when we otherwise have too few candidates.
					bool hasVisual = false;
					for (int i = 0; i < nodes.Count; i++)
					{
						if (nodes[i].Kind == "text" || nodes[i].Kind == "sprite")
						{
							hasVisual = true;
							break;
						}
					}

					List<NearLabelNode> view = nodes;
					if (hasVisual)
					{
						view = new List<NearLabelNode>(nodes.Count);
						for (int i = 0; i < nodes.Count; i++)
						{
							var n = nodes[i];
							if (n.Kind == "text" || n.Kind == "sprite")
								view.Add(n);
						}
						if (view.Count < maxCandidates)
						{
							for (int i = 0; i < nodes.Count && view.Count < maxCandidates; i++)
							{
								var n = nodes[i];
								if (n.Kind != "text" && n.Kind != "sprite")
									view.Add(n);
							}
						}
					}

					int show = Math.Min(maxCandidates, view.Count);
					w.WriteLine($"  {label}.nearLabel (root={GetHierarchyPath(root)}) targetPos={FormatVec3(targetPos)} showing {show}/{view.Count} (allCandidates={nodes.Count})");
					for (int i = 0; i < show; i++)
					{
						var n = view[i];
	                        w.WriteLine($"    [{i}] kind={n.Kind} d2={n.D2:0.00} type={n.TypeName} path={n.Path} value=\"{Safe(n.Value)}\"");
	                    }
	                }
	                catch
	                {
	                    // ignore
	                }
	            }

	            private static void CollectNearbyLabelCandidatesUnder(
	                Transform root,
	                Vector3 targetPos,
	                List<NearLabelNode> outNodes,
	                int maxCandidates,
	                int maxNodes)
	            {
	                if (root == null || outNodes == null || maxCandidates <= 0 || maxNodes <= 0)
	                    return;

	                int visited = 0;
	                var q = new Queue<Transform>();
	                q.Enqueue(root);

	                while (q.Count > 0 && visited < maxNodes && outNodes.Count < maxCandidates)
	                {
	                    Transform t = q.Dequeue();
	                    visited++;

	                    try
	                    {
	                        GameObject? go = null;
	                        try { go = t.gameObject; } catch { go = null; }
	                        if (go != null)
	                        {
	                            if (TryGetComponentsOnNodeBestEffort(go, out var comps))
	                            {
	                                for (int ci = 0; ci < comps.Count && outNodes.Count < maxCandidates; ci++)
	                                {
	                                    var c = comps[ci];
	                                    if (c == null)
	                                        continue;

	                                    string full = "";
	                                    try
	                                    {
	                                        var ct = c.GetType();
	                                        full = ct.FullName ?? ct.Name;
	                                    }
	                                    catch { full = "(unknown)"; }

	                                    Vector3 p = t.position;
	                                    try { if (c is Component uc) p = uc.transform.position; } catch { /* keep t.position */ }

	                                    float dx = p.x - targetPos.x;
	                                    float dy = p.y - targetPos.y;
	                                    float dz = p.z - targetPos.z;
	                                    float d2 = dx * dx + dy * dy + dz * dz;

	                                    string path = "";
	                                    try { if (c is Component uc) path = GetHierarchyPath(uc.transform); else path = GetHierarchyPath(t); }
	                                    catch { path = ""; }

	                                    // 1) Text (TMPro / UI.Text / custom "text" property)
	                                    if (TryGetTextProperty(c, out string? rawText))
	                                    {
	                                        string s = (rawText ?? "").Trim();
	                                        if (!string.IsNullOrWhiteSpace(s))
	                                        {
	                                            outNodes.Add(new NearLabelNode { Kind = "text", TypeName = full, Path = path, Value = s, D2 = d2 });
	                                            if (outNodes.Count >= maxCandidates) break;
	                                        }
	                                    }

	                                    // 2) Sprite names (for sprite-based UI)
	                                    if (TryGetSpriteInfo(c, out string? spr))
	                                    {
	                                        string s = (spr ?? "").Trim();
	                                        if (!string.IsNullOrWhiteSpace(s))
	                                        {
	                                            outNodes.Add(new NearLabelNode { Kind = "sprite", TypeName = full, Path = path, Value = s, D2 = d2 });
	                                            if (outNodes.Count >= maxCandidates) break;
	                                        }
	                                    }

	                                    // 3) String-like members with label-ish names (textKey/locKey/etc)
	                                    var pairs = new List<(string member, string value)>(4);
	                                    CollectStringMemberCandidates(c, pairs, maxPairs: 2);
	                                    for (int pi = 0; pi < pairs.Count && outNodes.Count < maxCandidates; pi++)
	                                    {
	                                        var pr = pairs[pi];
	                                        string v = (pr.value ?? "").Trim();
	                                        if (string.IsNullOrWhiteSpace(v))
	                                            continue;
	                                        outNodes.Add(new NearLabelNode { Kind = "str:" + pr.member, TypeName = full, Path = path, Value = v, D2 = d2 });
	                                    }
	                                }
	                            }
	                        }
	                    }
	                    catch
	                    {
	                        // ignore
	                    }

	                    // Enqueue children
	                    int cc = 0;
	                    try { cc = t.childCount; } catch { cc = 0; }
	                    for (int i = 0; i < cc && visited < maxNodes; i++)
	                    {
	                        Transform? ch = null;
	                        try { ch = t.GetChild(i); } catch { ch = null; }
	                        if (ch != null)
	                            q.Enqueue(ch);
	                    }
	                }
	            }

	            private struct NearTextNode
	            {
	                public string TypeName;
	                public string Path;
	                public string Text;
	                public float D2;
	            }

	            private static void TryDumpNearbyTextNodes(TextWriter w, string label, GameObject rowGo, Vector3 targetPos, int maxTextNodes, int maxNodes, int ancestorDepth)
	            {
	                try
	                {
	                    if (w == null)
	                        return;

	                    if (rowGo == null)
	                    {
	                        w.WriteLine($"  {label}.nearText[error] (rowGo is null)");
	                        return;
	                    }

	                    // Probe marker so we can confirm this code-path is running in real dumps.
	                    try
	                    {
	                        w.WriteLine($"  {label}.nearText[probe] row=\"{Safe(rowGo.name)}\" targetPos=({targetPos.x:0.###},{targetPos.y:0.###},{targetPos.z:0.###})");
	                    }
	                    catch { /* ignore probe write failures */ }

	                    if (maxTextNodes <= 0)
	                        return;
	                    if (maxNodes <= 0)
	                        maxNodes = 256;
	                    if (ancestorDepth < 0)
	                        ancestorDepth = 0;

	                    var nodes = new List<NearTextNode>(128);

	                    // Walk up a few ancestors: row -> parent -> grandparent...
	                    Transform? baseT = null;
	                    try { baseT = rowGo.transform; } catch { baseT = null; }

	                    for (int depth = 0; depth <= ancestorDepth; depth++)
	                    {
	                        if (baseT == null)
	                            break;

	                        Transform? root = baseT;
	                        for (int i = 0; i < depth && root != null; i++)
	                        {
	                            try { root = root.parent; } catch { root = null; }
	                        }
	                        if (root == null)
	                            continue;

	                        nodes.Clear();
	                        CollectNearbyTextNodesUnder(root, targetPos, nodes, maxNodes: maxNodes, maxTextCandidates: 512);

	                        if (nodes.Count <= 0)
	                            continue;

	                        nodes.Sort((a, b) => a.D2.CompareTo(b.D2));

	                        int wrote = 0;
	                        int lim = Math.Min(maxTextNodes, nodes.Count);
	                        for (int i = 0; i < lim; i++)
	                        {
	                            var n = nodes[i];
	                            w.WriteLine($"  {label}.nearText[{wrote}] d2={n.D2:0.###} path=\"{Safe(n.Path)}\" type={n.TypeName} text=\"{Safe(n.Text)}\"");
	                            wrote++;
	                        }

	                        return;
	                    }

	                    w.WriteLine($"  {label}.nearText[none] (no non-empty text nodes found near target under {rowGo.name} ancestors)");
	                }
	                catch (Exception ex)
	                {
	                    try
	                    {
	                        w.WriteLine($"  {label}.nearText[error] ({ex.GetType().Name}: {ex.Message})");
	                    }
	                    catch { /* ignore */ }
	                }
	            }

	            private static void CollectNearbyTextNodesUnder(Transform root, Vector3 targetPos, List<NearTextNode> outNodes, int maxNodes, int maxTextCandidates)
	            {
	                try
	                {
	                    if (root == null || outNodes == null)
	                        return;

	                    var q = new Queue<Transform>();
	                    q.Enqueue(root);

	                    int visited = 0;
	                    while (q.Count > 0 && visited++ < maxNodes && outNodes.Count < maxTextCandidates)
	                    {
	                        Transform t = q.Dequeue();
	                        if (t == null)
	                            continue;

	                        GameObject go = null!;
	                        try { go = t.gameObject; } catch { go = null!; }
	                        if (go == null)
	                            continue;

	                        // Grab components on this node and look for text-ish types by name.
	                        if (TryGetComponentsOnNodeBestEffort(go, out var nodeComps))
	                        {
	                            foreach (var c in nodeComps)
	                            {
	                                if (c == null)
	                                    continue;

	                                var ct = c.GetType();
	                                string full = ct.FullName ?? ct.Name;
	                        string typeKey = GetIl2CppClassKey(c);

	                                bool looksText = full.IndexOf("TMPro", StringComparison.OrdinalIgnoreCase) >= 0
	                                                 || full.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0;
	                                if (!looksText)
	                                    continue;

	                                if (!TryGetTextProperty(c, out string? raw))
	                                    continue;

	                                string s = (raw ?? "").Trim();
	                                if (string.IsNullOrWhiteSpace(s))
	                                    continue;

	                                Vector3 p = t.position;
	                                try
	                                {
	                                    if (c is Component uc)
	                                        p = uc.transform.position;
	                                }
	                                catch { /* keep t.position */ }

	                                float dx = p.x - targetPos.x;
	                                float dy = p.y - targetPos.y;
	                                float dz = p.z - targetPos.z;
	                                float d2 = dx * dx + dy * dy + dz * dz;

	                                string path = "";
	                                try
	                                {
	                                    if (c is Component uc)
	                                        path = GetHierarchyPath(uc.transform);
	                                    else
	                                        path = GetHierarchyPath(t);
	                                }
	                                catch { path = ""; }

	                                outNodes.Add(new NearTextNode
	                                {
	                                    TypeName = full,
	                                    Path = path,
	                                    Text = s,
	                                    D2 = d2
	                                });

	                                if (outNodes.Count >= maxTextCandidates)
	                                    break;
	                            }
	                        }

	                        // Enqueue children
	                        int cc = 0;
	                        try { cc = t.childCount; } catch { cc = 0; }
	                        for (int i = 0; i < cc && visited < maxNodes; i++)
	                        {
	                            Transform? ch = null;
	                            try { ch = t.GetChild(i); } catch { ch = null; }
	                            if (ch != null)
	                                q.Enqueue(ch);
	                        }
	                    }
	                }
	                catch
	                {
	                    // ignore
	                }
	            }


	            
// =========================================================
// GetComponents() best-effort helpers (IL2CPP-safe)
// =========================================================
// The generic GameObject.GetComponents<T>() instantiation is sometimes stripped on IL2CPP builds,
// which causes MissingMethodException at runtime (even though it compiles).
// For devtools, we instead reflect for 1-param overloads that accept either:
//   - System.Type
//   - Il2CppSystem.Type
// and then coerce the returned collection into a list of objects.

private static bool s_getComponentsCacheInit;
private static MethodInfo[] s_getComponentsSysType = Array.Empty<MethodInfo>();
private static MethodInfo[] s_getComponentsIl2CppType = Array.Empty<MethodInfo>();
private static object? s_il2cppComponentTypeArg;

private static void EnsureGetComponentsCache()
{
    if (s_getComponentsCacheInit)
        return;

    s_getComponentsCacheInit = true;
    try
    {
        var tGo = typeof(GameObject);

        var cands = tGo.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "GetComponents")
            .ToArray();

        var sys = new List<MethodInfo>();
        var il2 = new List<MethodInfo>();

        foreach (var m in cands)
        {
            ParameterInfo[] ps;
            try { ps = m.GetParameters(); }
            catch { continue; }

            if (ps.Length != 1)
                continue;

            var p0 = ps[0].ParameterType;

            if (p0 == typeof(Type))
                sys.Add(m);
            else if (string.Equals(p0.FullName, "Il2CppSystem.Type", StringComparison.Ordinal))
                il2.Add(m);
        }

        s_getComponentsSysType = sys.ToArray();
        s_getComponentsIl2CppType = il2.ToArray();

        if (s_getComponentsIl2CppType.Length > 0)
            s_il2cppComponentTypeArg = TryMakeIl2CppSystemType(typeof(Component));
    }
    catch
    {
        // If we can't populate caches, callers will just report "no components".
        s_getComponentsSysType = Array.Empty<MethodInfo>();
        s_getComponentsIl2CppType = Array.Empty<MethodInfo>();
        s_il2cppComponentTypeArg = null;
    }
}

private static bool TryGetComponentsOnNodeBestEffort(GameObject go, out List<object?> comps)
{
    comps = new List<object?>(16);

    try
    {
        if (go == null)
            return false;

        EnsureGetComponentsCache();

        // Prefer System.Type overloads first (simplest).
        for (int i = 0; i < s_getComponentsSysType.Length; i++)
        {
            object? res = null;
            try { res = s_getComponentsSysType[i].Invoke(go, new object?[] { typeof(Component) }); }
            catch { res = null; }

            AppendObjectsFromResult(res, comps, max: 256);
            if (comps.Count > 0)
                return true;
        }

        // Then try Il2CppSystem.Type overloads.
        if (s_il2cppComponentTypeArg != null)
        {
            for (int i = 0; i < s_getComponentsIl2CppType.Length; i++)
            {
                object? res = null;
                try { res = s_getComponentsIl2CppType[i].Invoke(go, new object?[] { s_il2cppComponentTypeArg }); }
                catch { res = null; }

                AppendObjectsFromResult(res, comps, max: 256);
                if (comps.Count > 0)
                    return true;
            }
        }
    }
    catch
    {
        // ignore
    }

    return comps.Count > 0;
}

private static void AppendObjectsFromResult(object? res, List<object?> outList, int max)
{
    if (res == null || outList.Count >= max)
        return;

    // Managed arrays
    if (res is Array arr)
    {
        int n = arr.Length;
        for (int i = 0; i < n && outList.Count < max; i++)
            outList.Add(arr.GetValue(i));
        return;
    }

    // Some Il2CppInterop / Unity wrappers expose Length/Count + get_Item(int). Prefer this over IEnumerable:
    // on some IL2CPP surfaces, enumerators can throw when invoked via reflection.
    if (TryAppendLengthGetItem(res, outList, max))
        return;

    // IEnumerable fallback (best-effort)
    if (res is System.Collections.IEnumerable ie)
    {
        try
        {
            foreach (var x in ie)
            {
                outList.Add(x);
                if (outList.Count >= max) break;
            }
            return;
        }
        catch
        {
            // ignore and continue to last resort
        }
    }

    // Last resort: treat as single object
    outList.Add(res);
}

private static bool TryAppendLengthGetItem(object res, List<object?> outList, int max)
{
    try
    {
        var t = res.GetType();

        // Common wrappers: Length or Count
        var pLen = t.GetProperty("Length") ?? t.GetProperty("Count");
        var mGet = t.GetMethod("get_Item", new[] { typeof(int) });

        if (pLen == null || mGet == null)
            return false;

        int len = 0;
        try { len = (int)pLen.GetValue(res)!; } catch { len = 0; }
        if (len <= 0)
            return false;

        for (int i = 0; i < len && outList.Count < max; i++)
        {
            object? v = null;
            try { v = mGet.Invoke(res, new object[] { i }); } catch { v = null; }
            outList.Add(v);
        }
        return true;
    }
    catch
    {
        return false;
    }
}

private static object? TryMakeIl2CppSystemType(Type sysType)
{
    // Copy of the robust helper used elsewhere in ReimaginedMod.cs, trimmed down for devtools.
    try
    {
        var il2cppType = FindLoadedTypeByFullName("Il2CppInterop.Runtime.Il2CppType")
                         ?? FindLoadedTypeBySimpleName("Il2CppType");

        if (il2cppType != null)
        {
            // From(Type)
            var from = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    try
                    {
                        if (m.Name != "From") return false;
                        var ps = m.GetParameters();
                        return ps.Length == 1 && ps[0].ParameterType == typeof(Type);
                    }
                    catch { return false; }
                });

            if (from != null)
            {
                try
                {
                    var v = from.Invoke(null, new object?[] { sysType });
                    if (v != null) return v;
                }
                catch { /* ignore */ }
            }

            // Of<T>()
            var of = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    try
                    {
                        if (m.Name != "Of") return false;
                        if (!m.IsGenericMethodDefinition) return false;
                        if (m.GetGenericArguments().Length != 1) return false;
                        return m.GetParameters().Length == 0;
                    }
                    catch { return false; }
                });

            if (of != null)
            {
                try
                {
                    var mg = of.MakeGenericMethod(sysType);
                    var v = mg.Invoke(null, null);
                    if (v != null) return v;
                }
                catch { /* ignore */ }
            }
        }

        // Fallback: Il2CppSystem.Type op_Implicit(Type)
        var il2cppSysType = FindLoadedTypeByFullName("Il2CppSystem.Type");
        if (il2cppSysType != null)
        {
            var op = il2cppSysType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    try
                    {
                        if (m.Name != "op_Implicit") return false;
                        var ps = m.GetParameters();
                        return ps.Length == 1 && ps[0].ParameterType == typeof(Type);
                    }
                    catch { return false; }
                });

            if (op != null)
            {
                try
                {
                    var v = op.Invoke(null, new object?[] { sysType });
                    if (v != null) return v;
                }
                catch { /* ignore */ }
            }
        }
    }
    catch
    {
        // ignore
    }

    return null;
}

private static Type? FindLoadedTypeByFullName(string fullName)
{
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        try
        {
            var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (t != null) return t;
        }
        catch { /* ignore */ }
    }
    return null;
}

private static Type? FindLoadedTypeBySimpleName(string simpleName)
{
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        Type[] types;
        try { types = asm.GetTypes(); }
        catch { continue; }

        for (int i = 0; i < types.Length; i++)
        {
            Type t = types[i];
            string n;
            try { n = t.Name; } catch { continue; }
            if (string.Equals(n, simpleName, StringComparison.Ordinal))
                return t;
        }
    }
    return null;
}

private static bool TryGetComponentsInChildrenBestEffort(GameObject go, bool includeInactive, out List<object?> comps)
{
    comps = new List<object?>(128);
    try
    {
        if (go == null)
            return false;

        var tGo = typeof(GameObject);

        // Prefer: GetComponentsInChildren(Type,bool) when available
        MethodInfo? m = null;
        try
        {
            m = tGo.GetMethod(
                "GetComponentsInChildren",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Type), typeof(bool) },
                modifiers: null);
        }
        catch { m = null; }

        object? result = null;

        if (m != null)
        {
            try { result = m.Invoke(go, new object[] { typeof(Component), includeInactive }); }
            catch { result = null; }
        }
        else
        {
            // Fallback: GetComponentsInChildren(bool)
            try
            {
                m = tGo.GetMethod(
                    "GetComponentsInChildren",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(bool) },
                    modifiers: null);
            }
            catch { m = null; }

            if (m != null)
            {
                try { result = m.Invoke(go, new object[] { includeInactive }); }
                catch { result = null; }
            }
        }

        // Robustly unpack result into comps. Even if reflection returns something but unpacking fails,
        // we still fall back to a manual Transform walk below.
        if (result != null)
        {
            try
            {
                AppendObjectsFromResult(result, comps, max: 8192);
                if (comps.Count > 0)
                    return true;
                comps.Clear();
            }
            catch
            {
                comps.Clear();
            }
        }

        // Manual Transform walk + GetComponents(Type) on each node. This is slower but far more reliable
        // across IL2CPP surfaces where reflection invocation / enumeration can be flaky.
        try
        {
            if (TryCollectComponentsByTransformWalk(go, includeInactive, comps))
                return comps.Count > 0;
        }
        catch { /* ignore */ }

        return false;
    }
    catch
    {
        return false;
    }
}

private static bool TryCollectComponentsByTransformWalk(GameObject root, bool includeInactive, List<object?> comps)
	            {
	                try
	                {
	                    if (root == null)
	                        return false;

	                    // Collect components by walking the Transform tree and calling GameObject.GetComponents(Type) directly.
	                    // Reflection-invocation of Unity methods can fail on some IL2CPP surfaces, so keep this path purely API-based.
	                    var q = new Queue<Transform>();
	                    q.Enqueue(root.transform);

	                    int visited = 0;
	                    int maxNodes = 512;      // safety: UI trees can be deep
	                    int maxComps = 8192;     // safety: avoid runaway
	                    while (q.Count > 0 && visited++ < maxNodes && comps.Count < maxComps)
	                    {
	                        Transform t = q.Dequeue();
	                        if (t == null)
	                            continue;

	                        GameObject go = t.gameObject;
	                        if (go == null)
	                            continue;

	                        bool act = true;
	                        try { act = go.activeInHierarchy; } catch { act = true; }
	                        if (!includeInactive && !act)
	                        {
	                            // If this node is inactive, the entire subtree is effectively inactive in hierarchy.
	                            continue;
	                        }

	                        // Add components on this GameObject (best-effort without relying on generic GetComponents<T> instantiations).
	                        try
	                        {
	                            if (TryGetComponentsOnNodeBestEffort(go, out var nodeComps))
	                            {
	                                for (int i = 0; i < nodeComps.Count && comps.Count < maxComps; i++)
	                                    comps.Add(nodeComps[i]);
	                            }
	                        }
	                        catch { /* ignore per-node */ }

	                        // Enqueue children
	                        int cc = 0;
	                        try { cc = t.childCount; } catch { cc = 0; }
	                        for (int i = 0; i < cc && visited < maxNodes; i++)
	                        {
	                            Transform? ch = null;
	                            try { ch = t.GetChild(i); } catch { ch = null; }
	                            if (ch != null)
	                                q.Enqueue(ch);
	                        }
	                    }

	                    return comps.Count > 0;
	                }
	                catch
	                {
	                    return false;
	                }
	            }

private static string DescribeGameObject(GameObject go)
	            {
	                bool aS = false, aH = false;
	                try { aS = go.activeSelf; } catch { aS = false; }
	                try { aH = go.activeInHierarchy; } catch { aH = false; }
	                return $"GameObject(\"{go.name}\",activeSelf={aS},activeHier={aH})";
	            }

	            private static string GetHierarchyPath(Transform? t)
	            {
	                try
	                {
	                    if (t == null)
	                        return "";
	                    var sb = new StringBuilder();
	                    Transform? cur = t;
	                    int safety = 0;
	                    while (cur != null && safety++ < 64)
	                    {
	                        if (sb.Length == 0)
	                            sb.Insert(0, cur.name);
	                        else
	                            sb.Insert(0, cur.name + "/");
	                        cur = cur.parent;
	                    }
	                    return sb.ToString();
	                }
	                catch
	                {
	                    return "";
	                }
	            }

	            private static string FormatTransformPos(Transform? t)
	            {
	                try
	                {
	                    if (t == null)
	                        return "";
	                    Vector3 wp = t.position;
	                    Vector3 lp = t.localPosition;
	                    string extra = "";
	                    try
	                    {
	                        if (t is RectTransform rt)
	                        {
	                            var ap = rt.anchoredPosition;
	                            var sd = rt.sizeDelta;
	                            extra = $" anchored=({ap.x:0.###},{ap.y:0.###}) size=({sd.x:0.###},{sd.y:0.###})";
	                        }
	                    }
	                    catch
	                    {
	                        // ignore
	                    }
	                    return $"world=({wp.x:0.###},{wp.y:0.###},{wp.z:0.###}) local=({lp.x:0.###},{lp.y:0.###},{lp.z:0.###}){extra}";
	                }
	                catch
	                {
	                    return "";
	                }
	            }

	            private static string FormatVec3(Vector3 v)
	            {
	                return $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";
	            }

            private static string Trim(string s, int max)
            {
                if (s.Length <= max)
                    return s;
                return s.Substring(0, max) + "...";
            }

            private static string Safe(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return "";
                return s.Replace("\r", "\\r").Replace("\n", "\\n");
            }
        }
    }
}