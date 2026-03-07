#nullable enable
using System;
using System.IO;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass 67: CampSelectionSnapshot (immutable POD for overlay/logging)
            // =========================================================
            // Design intent:
            // - Centralize the *canonical* "what is currently selected?" record.
            // - Keep it tiny + immutable so it can be safely copied and rendered by overlays later.
            // - Do NOT depend on live pointers after capture (pointers are logged as numeric IDs only).

            internal enum CampSelectionRowKind
            {
                Unknown = 0,
                Party = 1,
                Stock = 2,
                OutOfRange = 3,
            }

            internal readonly struct CampSelectionUnit
            {
                public readonly int Cursor;
                public readonly int UnitworkIndex;
                public readonly long Ptr;
                public readonly string Name;

                public CampSelectionUnit(int cursor, int unitworkIndex, long ptr, string name)
                {
                    Cursor = cursor;
                    UnitworkIndex = unitworkIndex;
                    Ptr = ptr;
                    Name = name ?? string.Empty;
                }
            }

            // A compact per-cursor record. This is the missing link for menu mapping:
            // many flows (Summon / Return-to-stock) do not populate cmpCalc.src/dst pointers,
            // so we need to understand what each cursor is *currently* pointing at.
            //
            // NOTE:
            // - ListIdxVal / StockIdxVal are read from StockInfo.LocalStock[cursor] (or CMP_GBWK.DrawList fallback)
            //   at Overall = Index + Shift.
            // - Resolved* is best-effort: it tries to interpret either index as a UnitWork index.
            internal readonly struct CampSelectionCursorEntry
            {
                public readonly int Cursor;
                public readonly int Index;
                public readonly int Shift;
                public readonly int Overall;

                public readonly int ListNums;

                public readonly int PartyCnt;
                public readonly int StockCnt;
                public readonly CampSelectionRowKind RowKind;

                public readonly int ListIdxVal;
                public readonly int StockIdxVal;

                public readonly int ResolvedUnitworkIndex;
                public readonly long ResolvedPtr;
                public readonly string ResolvedName;

                public CampSelectionCursorEntry(
                    int cursor,
                    int index,
                    int shift,
                    int overall,
                    int listNums,
                    int partyCnt,
                    int stockCnt,
                    CampSelectionRowKind rowKind,
                    int listIdxVal,
                    int stockIdxVal,
                    int resolvedUnitworkIndex,
                    long resolvedPtr,
                    string resolvedName)
                {
                    Cursor = cursor;
                    Index = index;
                    Shift = shift;
                    Overall = overall;
                    ListNums = listNums;
                    PartyCnt = partyCnt;
                    StockCnt = stockCnt;
                    RowKind = rowKind;
                    ListIdxVal = listIdxVal;
                    StockIdxVal = stockIdxVal;
                    ResolvedUnitworkIndex = resolvedUnitworkIndex;
                    ResolvedPtr = resolvedPtr;
                    ResolvedName = resolvedName ?? string.Empty;
                }
            }

            internal readonly struct CampSelectionSnapshot
            {
                public readonly int DrawMode;
                public readonly bool CmpItemMeaningful;
                public readonly int ItemId;
                public readonly string ItemName;
                public readonly int CmpDstIndex;
                public readonly bool CmpDstIndexMatchesDstUnitworkIndex;
                public readonly CampSelectionUnit Src;
                public readonly CampSelectionUnit Dst;
                public readonly CampSelectionCursorEntry[] CursorEntries;

                public CampSelectionSnapshot(
                    int drawMode,
                    bool cmpItemMeaningful,
                    int itemId,
                    string itemName,
                    int cmpDstIndex,
                    bool cmpDstIndexMatchesDstUnitworkIndex,
                    CampSelectionUnit src,
                    CampSelectionUnit dst,
                    CampSelectionCursorEntry[]? cursorEntries)
                {
                    DrawMode = drawMode;
                    CmpItemMeaningful = cmpItemMeaningful;
                    ItemId = itemId;
                    ItemName = itemName ?? string.Empty;
                    CmpDstIndex = cmpDstIndex;
                    CmpDstIndexMatchesDstUnitworkIndex = cmpDstIndexMatchesDstUnitworkIndex;
                    Src = src;
                    Dst = dst;
                    CursorEntries = cursorEntries ?? Array.Empty<CampSelectionCursorEntry>();
                }

                public void WriteSummaryLines(TextWriter w)
                {
                    // Keep these lines stable: they are our "canonical selection summary" used for diffs.
                    w.WriteLine($"summary.drawMode={DrawMode}");
                    if (CmpItemMeaningful)
                        w.WriteLine("summary.cmpItemMeaningful=yes");
                    else
                        w.WriteLine("summary.cmpItemMeaningful=no  (cmpCalc.itemId may be stale outside target selection)");

                    if (CmpItemMeaningful)
                        w.WriteLine($"summary.itemId={ItemId}  itemName=\"{Safe(ItemName)}\"  cmpDstIndex={CmpDstIndex}  (cmpDstIndexMatchesDstUnitworkIndex={(CmpDstIndexMatchesDstUnitworkIndex ? "yes" : "no")})");
                    else
                        w.WriteLine($"summary.itemId(stale)={ItemId}  itemName(stale)=\"{Safe(ItemName)}\"  cmpDstIndex={CmpDstIndex}  (cmpDstIndexMatchesDstUnitworkIndex={(CmpDstIndexMatchesDstUnitworkIndex ? "yes" : "no")})");

                    w.WriteLine($"summary.srcCursor={Src.Cursor}  srcUnitworkIndex={Src.UnitworkIndex}  srcPtr=0x{Src.Ptr:X}  srcName=\"{Safe(Src.Name)}\"");
                    w.WriteLine($"summary.dstCursor={Dst.Cursor}  dstUnitworkIndex={Dst.UnitworkIndex}  dstPtr=0x{Dst.Ptr:X}  dstName=\"{Safe(Dst.Name)}\"");

                    if (CursorEntries.Length > 0)
                    {
                        w.WriteLine();
                        w.WriteLine("-- cursorTable (Index/Shift/Overall + ListIdx/StockIdx + best-effort unit resolve) --");
                        for (int i = 0; i < CursorEntries.Length; i++)
                        {
                            var e = CursorEntries[i];
                            string resolved = (e.ResolvedPtr != 0)
                                ? $"unitwork={e.ResolvedUnitworkIndex} ptr=0x{e.ResolvedPtr:X} name=\"{Safe(e.ResolvedName)}\""
                                : "unitwork=(unresolved)";

                            w.WriteLine($"cursor[{e.Cursor}]: idx={e.Index} shift={e.Shift} overall={e.Overall}  listIdx={e.ListIdxVal} stockIdx={e.StockIdxVal}  {resolved}");
                        }
                    }
                }
            }
        }
    }
}
