#nullable enable
using System;
using System.IO;
using System.Text;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Lightweight camp selection summary dumper
            // =========================================================
            //

            internal static void HotkeyDumpCampSelectionSummary()
            {
                string path = MakeDumpPath("camp_selection_summary");
                MelonLogger.Msg($"[Reimagined] Camp selection summary dump -> {path}");

                try
                {
                    using var w = new StreamWriter(path, append: false, Encoding.UTF8);
                    w.WriteLine("=== Camp / Command Menu Selection Summary ===");
                    w.WriteLine("dumpSchema=CampSelectionSummary v3 (cmpCalc + cursorTable + derived highlight meta)");
                    w.WriteLine($"timeLocal={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    w.WriteLine($"timeUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}  ticksUtc={DateTime.UtcNow.Ticks}");
                    w.WriteLine();

                    // Headline state (cheap context, helps explain why selection might be null).
                    var cmpInit = FindTypeInLoadedAssemblies("Il2Cpp.cmpInit");
                    if (cmpInit != null)
                    {
                        if (TryReadSByteStatic(cmpInit, "gProcessStat", out sbyte ps))
                            w.WriteLine($"cmpInit.gProcessStat={ps}");
                        if (TryReadSByteStatic(cmpInit, "gExitState", out sbyte ex))
                            w.WriteLine($"cmpInit.gExitState={ex}");

                        if (TryReadByteArrayHexStatic(cmpInit, "gCommandFlag", 32, out int cLen, out string cHex, out int cHash))
                            w.WriteLine($"cmpInit.gCommandFlag(len={cLen},hash={cHash})={cHex}");
                    }

                    var seqType = FindTypeInLoadedAssemblies("Il2Cpp.dds3SequenceList");
                    if (seqType != null && TryCallIntStaticMethod(seqType, "CheckCamp", out int campGate))
                        w.WriteLine($"dds3SequenceList.CheckCamp()={campGate}  (empirical: 0=open, 1=closed)");

                    // These indices are the fastest way to correlate "what I did" with a dump.
                    // They match the seqTrace log format: idx[m=?,a=?,i=?,p=?,s=?,d=?].
                    if (cmpInit != null)
                    {
                        if (TryReadIntStatic(cmpInit, "MenuIndex", out int mi)) w.WriteLine($"cmpInit.MenuIndex={mi}");
                        if (TryReadIntStatic(cmpInit, "ArrowIndex", out int ai)) w.WriteLine($"cmpInit.ArrowIndex={ai}");
                        if (TryReadIntStatic(cmpInit, "ItemIndex", out int ii)) w.WriteLine($"cmpInit.ItemIndex={ii}");
                        if (TryReadIntStatic(cmpInit, "PartyIndex", out int pi)) w.WriteLine($"cmpInit.PartyIndex={pi}");
                        if (TryReadIntStatic(cmpInit, "StockIndex", out int si)) w.WriteLine($"cmpInit.StockIndex={si}");
                        if (TryReadIntStatic(cmpInit, "DrawIndex", out int di)) w.WriteLine($"cmpInit.DrawIndex={di}");
                    }
                    else
                    {
                        w.WriteLine("cmpInit=(not found)");
                    }

                    // Higher-level probe (root/sub selection text + derived cursor info).
                    // This is intentionally best-effort: if it fails, the summary still works.
                    bool haveProbe = TryGetCampProbeState(out var st);
                    if (haveProbe)
                    {
                        w.WriteLine();
                        w.WriteLine("-- campProbe (best-effort) --");

                        if (st.HaveRootCursor)
                        {
                            w.WriteLine($"campProbe.rootSel={st.RootSel}  rootCI[0] idx={st.RootIndex} shift={st.RootShift} listNums={st.RootListNums}");
                            if (st.HaveSelectionLocalized)
                                w.WriteLine($"campProbe.rootText=\"{Safe(st.SelectionLocalized)}\"");
                        }
                        else
                        {
                            w.WriteLine("campProbe.rootSel=(unavailable)");
                        }

                        if (st.HaveWorkCursor)
                        {
                            w.WriteLine($"campProbe.workSel={st.WorkSel}  workCI[0] idx={st.WorkIndex} shift={st.WorkShift} listNums={st.WorkListNums}");
                        }

                        // Sub cursor: which submenu is active depends on root selection. We log both the derived kind and the raw cursor.
                        w.WriteLine($"campProbe.subKind={st.SubKind}  subCI[{st.SubCiIndex}] sel={st.SubSel} idx={st.SubIndex} shift={st.SubShift} listNums={st.SubListNums}");
                        if (st.HaveSubSelectionLocalized)
                            w.WriteLine($"campProbe.subText=\"{Safe(st.SubSelectionLocalized)}\"");

                        if (st.HaveMenuSlotLen)
                            w.WriteLine($"campProbe.menuSlots={st.MenuSlotLen}");
                    }

                    w.WriteLine();

                    // Build selection snapshot.
                    if (!TryCaptureCampSelectionSnapshot(out CampSelectionSnapshot snap, out string? err))
                    {
                        w.WriteLine("ERROR: Failed to capture camp selection snapshot.");
                        w.WriteLine($"error={Safe(err)}");
                        return;
                    }

                    snap.WriteSummaryLines(w);

                    w.WriteLine();

                    w.WriteLine("-- derived highlight (best-effort) --");
                    if (TryDeriveHighlightedSelection(snap, haveProbe, st, out CampHighlightedSelection sel, out string why))
                    {
                        string hiName = sel.Unit.NameTag ?? string.Empty;

                        // Context first: helps explain why a particular cursor was chosen.
                        w.WriteLine($"highlight.drawMode={sel.DrawMode}");
                        if (sel.HaveProbe)
                        {
                            w.WriteLine($"highlight.rootSel={sel.RootSel}  rootText=\"{Safe(sel.RootText)}\"");
                            w.WriteLine($"highlight.subKind={sel.SubKind}  subSel={sel.SubSel}  subText=\"{Safe(sel.SubText)}\"");
                        }
                        else
                        {
                            w.WriteLine("highlight.probe=(unavailable)");
                        }

                        // Cursor identity + list indices.
                        w.WriteLine($"highlight.kind={sel.Kind}  confidence={sel.Confidence}");
                        w.WriteLine($"highlight.cursor={sel.Cursor}  idx={sel.Index} shift={sel.Shift} overall={sel.Overall}");
                        w.WriteLine($"highlight.listIdx={sel.ListIdxVal}  stockIdx={sel.StockIdxVal}");
                        w.WriteLine($"highlight.listNums={sel.ListNums}  partyCnt={sel.PartyCnt}  stockCnt={sel.StockCnt}  rowKind={sel.RowKind}");

                        if (TryGetDds3GlobalWorkObject(out object? gbwkObj) && gbwkObj != null)
                        {
                            int sc = TryReadInt32(gbwkObj, "stockcnt", -1);
                            int ms = TryReadInt32(gbwkObj, "maxstock", -1);
                            if (sc >= 0 || ms >= 0)
                                w.WriteLine($"gbwk.stockcnt={sc}  gbwk.maxstock={ms}");
                        }

                        // If listIdx can be proven to be a stocklist index, we log that mapping explicitly.
                        if (sel.ListIdxIsStocklistIndex)
                        {
                            w.WriteLine($"highlight.listIdxRole=stocklistIndex  stocklist[{sel.ListIdxVal}]={sel.StocklistUwAtListIdx}");
                        }
                        else
                        {
                            if (sel.StocklistUwAtListIdx != int.MinValue)
                                w.WriteLine($"highlight.listIdxRole=unknown  stocklist[{sel.ListIdxVal}]={sel.StocklistUwAtListIdx}  (does not match resolved unit)");
                            else
                                w.WriteLine("highlight.listIdxRole=unknown");
                        }

                        // Resolved unit details.
                        w.WriteLine($"highlight.unitworkIndex={sel.Unit.UnitworkIndex}  ptr=0x{sel.Unit.Ptr:X}  id={sel.Unit.UnitId}  lvl={sel.Unit.Level}  hp={sel.Unit.HP}/{sel.Unit.MaxHP}  mp={sel.Unit.MP}/{sel.Unit.MaxMP}  name=\"{Safe(hiName)}\"");
                        w.WriteLine($"highlight.key={BuildStableHighlightKey(sel)}");
                        w.WriteLine($"highlight.reason=\"{Safe(sel.Reason)}\"");
                    }
                    else
                    {
                        w.WriteLine("highlight=(unresolved)");
                        w.WriteLine($"highlight.reason=\"{Safe(why)}\"");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Reimagined] Camp selection summary dump failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            internal static bool TryCaptureCampSelectionSnapshot(out CampSelectionSnapshot snapshot, out string? error)
            {
                snapshot = default;
                error = null;

                try
                {
                    var cmpInit = FindTypeInLoadedAssemblies("Il2Cpp.cmpInit");
                    if (cmpInit == null)
                    {
                        error = "type Il2Cpp.cmpInit not found";
                        return false;
                    }

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

                    if (gbwkObj == null)
                    {
                        error = "cmpInit.CMP_GBWK is null/unavailable";
                        return false;
                    }

                    // cmpCalc selection pointers (best-effort)
                    CmpCalcSelection calcSel = TryGetCmpCalcSelectionBestEffort();

                    // Reuse the same cursor-mapping logic as the big reflection surface dump,
                    // but only keep the final summary output.
                    if (!TryBuildSelectionSummaryFromGbwk(gbwkObj, calcSel, out snapshot, out error))
                        return false;

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
            }

            private static bool TryBuildSelectionSummaryFromGbwk(object gbwkObj, CmpCalcSelection calcSel, out CampSelectionSnapshot snapshot, out string? error)
            {
                snapshot = default;
                error = null;

                try
                {
                    object? srcObj = calcSel.SrcObj;
                    object? dstObj = calcSel.DstObj;
                    int itemId = calcSel.ItemId;
                    int dstIndex = calcSel.DstIndex;

                    if (!TryGetInstanceMemberValue(gbwkObj, "StockInfo", out object? stockInfo) || stockInfo == null)
                    {
                        error = "CMP_GBWK.StockInfo unavailable";
                        return false;
                    }

                    int drawMode = TryReadInt32(stockInfo, "DrawMode", int.MinValue);

                    if (!TryGetInstanceMemberValue(stockInfo, "SelPos", out object? selPosArr) || selPosArr == null || !TryGetLengthOrCount(selPosArr, out int selLen, out _))
                    {
                        error = "StockInfo.SelPos unavailable";
                        return false;
                    }

                    // LocalStock is usually parallel to SelPos.
                    object? localStockArr = null;
                    int localStockLen = -1;
                    if (TryGetInstanceMemberValue(stockInfo, "LocalStock", out object? lsArr) && lsArr != null && TryGetLengthOrCount(lsArr, out int lsLen, out _))
                    {
                        localStockArr = lsArr;
                        localStockLen = lsLen;
                    }

                    // Some flows expose a single DrawList on CMP_GBWK.
                    object? drawListFallback = null;
                    if (TryGetInstanceMemberValue(gbwkObj, "DrawList", out object? dl) && dl != null)
                        drawListFallback = dl;

                    object? dds3Gbwk = null;
                    TryGetDds3GlobalWorkObject(out dds3Gbwk);

                    long srcPtr = 0;
                    long dstPtr = 0;
                    try { if (srcObj != null) srcPtr = TryGetIl2CppPointer(srcObj).ToInt64(); } catch { }
                    try { if (dstObj != null) dstPtr = TryGetIl2CppPointer(dstObj).ToInt64(); } catch { }

                    int srcCursor = -1;
                    int dstCursor = -1;
                    UnitResolveInfo srcInfo = default;
                    UnitResolveInfo dstInfo = default;
                    bool haveSrcInfo = false;
                    bool haveDstInfo = false;

                    // New: always capture a compact cursor table so we can map menus
                    // even when cmpCalc pointers are null/stale.
                    var cursorEntries = new CampSelectionCursorEntry[selLen];

                    if (dds3Gbwk != null && selLen > 0)
                    {
                        for (int cursor = 0; cursor < selLen; cursor++)
                        {
                            object? ci = null;
                            try { ci = TryGetIndexValue(selPosArr, cursor); } catch { ci = null; }
                            if (ci == null || !TryGetInstanceMemberValue(ci, "CursorPos", out object? cp) || cp == null)
                            {
                                cursorEntries[cursor] = new CampSelectionCursorEntry(cursor, -1, -1, -1, -1, -1, -1, CampSelectionRowKind.Unknown, int.MinValue, int.MinValue, -1, 0, "");
                                continue;
                            }

                            int idx = TryReadInt32(cp, "Index", -1);
                            int shift = TryReadInt32(cp, "Shift", -1);
                            int overall = (idx >= 0 && shift >= 0) ? (idx + shift) : -1;
                            int listNums = TryReadInt32(cp, "ListNums", -1);
                            if (overall < 0)
                            {
                                cursorEntries[cursor] = new CampSelectionCursorEntry(cursor, idx, shift, overall, listNums, -1, -1, CampSelectionRowKind.Unknown, int.MinValue, int.MinValue, -1, 0, "");
                                continue;
                            }

                            object? localStock = null;
                            if (localStockArr != null && cursor >= 0 && cursor < localStockLen)
                            {
                                try { localStock = TryGetIndexValue(localStockArr, cursor); } catch { localStock = null; }
                            }
                            if (localStock == null)
                                localStock = drawListFallback;
                            if (localStock == null)
                            {
                                cursorEntries[cursor] = new CampSelectionCursorEntry(cursor, idx, shift, overall, listNums, -1, -1, CampSelectionRowKind.Unknown, int.MinValue, int.MinValue, -1, 0, "");
                                continue;
                            }

                            int partyCnt = TryReadInt32(localStock, "PartyCnt", -1);
                            int stockCnt = TryReadInt32(localStock, "StockCnt", -1);
                            CampSelectionRowKind rowKind = CampSelectionRowKind.Unknown;
                            if (overall >= 0 && partyCnt >= 0 && stockCnt >= 0)
                            {
                                if (overall < partyCnt) rowKind = CampSelectionRowKind.Party;
                                else if (overall < (partyCnt + stockCnt)) rowKind = CampSelectionRowKind.Stock;
                                else rowKind = CampSelectionRowKind.OutOfRange;
                            }

                            int listIdxVal = int.MinValue;
                            int stockIdxVal = int.MinValue;
                            if (TryGetIntArrayAt(localStock, "ListIdx", overall, out int li)) listIdxVal = li;
                            if (TryGetIntArrayAt(localStock, "StockIdx", overall, out int si)) stockIdxVal = si;

                            // Best-effort unit resolve for this cursor's highlighted row.
                            int resolvedUw = -1;
                            long resolvedPtr = 0;
                            string resolvedName = "";
	                            UnitResolveInfo resolvedInfo = default;
                            bool haveResolved = false;
                            if (TryResolveUnitFromUnitworkIndex(dds3Gbwk, stockIdxVal, out UnitResolveInfo infoE))
                            {
                                resolvedInfo = infoE;
                                haveResolved = true;
                                resolvedUw = infoE.UnitworkIndex;
                                resolvedPtr = infoE.Ptr;
                                resolvedName = infoE.NameTag ?? "";
                            }
                            else if (TryResolveUnitFromStocklistIndex(dds3Gbwk, listIdxVal, out infoE))
                            {
                                resolvedInfo = infoE;
                                haveResolved = true;
                                resolvedUw = infoE.UnitworkIndex;
                                resolvedPtr = infoE.Ptr;
                                resolvedName = infoE.NameTag ?? "";
                            }

                            cursorEntries[cursor] = new CampSelectionCursorEntry(cursor, idx, shift, overall, listNums, partyCnt, stockCnt, rowKind, listIdxVal, stockIdxVal, resolvedUw, resolvedPtr, resolvedName);

                            // Pointer-based match for cmpCalc src/dst.
	                            if ((srcPtr != 0 || dstPtr != 0) && haveResolved && resolvedPtr != 0)
                            {
                                if (srcPtr != 0 && resolvedPtr == srcPtr && srcCursor < 0)
                                {
                                    srcCursor = cursor;
	                                    srcInfo = resolvedInfo;
                                    haveSrcInfo = true;
                                }

                                if (dstPtr != 0 && resolvedPtr == dstPtr && dstCursor < 0)
                                {
                                    dstCursor = cursor;
	                                    dstInfo = resolvedInfo;
                                    haveDstInfo = true;
                                }
                            }
                        }
                    }

                    string itemName = "";
                    if (itemId >= 0 && TryGetItemName(itemId, out string nm) && !string.IsNullOrEmpty(nm))
                        itemName = nm;

                    // NOTE: cmpCalc.itemId is only reliably meaningful during target selection.
                    bool meaningfulItem = (drawMode == 5 && itemId > 0 && !string.IsNullOrEmpty(itemName));

                    // UnitResolveInfo.NameTag is a struct field; its default value is null.
                    // Coalesce to "" to keep this warning-clean and stable.
                    string srcName = haveSrcInfo ? (srcInfo.NameTag ?? "") : "";
                    string dstName = haveDstInfo ? (dstInfo.NameTag ?? "") : "";

                    int srcUw = haveSrcInfo ? srcInfo.UnitworkIndex : -1;
                    int dstUw = haveDstInfo ? dstInfo.UnitworkIndex : -1;

                    bool dstIndexMatches = (dstIndex >= 0 && dstUw >= 0 && dstIndex == dstUw);

                    var srcUnit = new CampSelectionUnit(srcCursor, srcUw, srcPtr, srcName);
                    var dstUnit = new CampSelectionUnit(dstCursor, dstUw, dstPtr, dstName);

                    snapshot = new CampSelectionSnapshot(
                        drawMode,
                        meaningfulItem,
                        itemId,
                        itemName,
                        dstIndex,
                        dstIndexMatches,
                        srcUnit,
                        dstUnit,
                        cursorEntries);

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    snapshot = default;
                    return false;
                }
            }
        }
    }
}
