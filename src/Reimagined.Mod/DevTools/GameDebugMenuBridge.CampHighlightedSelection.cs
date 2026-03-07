#nullable enable
using System;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass 71: CampHighlightedSelection
            // =========================================================
            // Goal:
            // - Provide a single, structured record of "what is highlighted right now".
            // - Carry enough cursor metadata to support future *safe* tooling:
            //     - distinguish "unitwork index" vs "stocklist index" when possible
            //     - keep the original cursor indices (idx/shift/overall)
            //     - attach the probe context (root/sub) used to pick the cursor
            // - Stay read-only for now (builders/mutators come later).

            internal enum CampHighlightKind
            {
                Unknown = 0,
                TargetSelectionDst = 1,
                PartySummonStock = 2,
                PartyReturnStock = 3,
                StatsList = 4,
                FallbackScore = 5,
            }

            internal readonly struct CampHighlightedSelection
            {
                // Context
                internal readonly bool HaveProbe;
                internal readonly int DrawMode;

                internal readonly int RootSel;
                internal readonly string RootText;

                internal readonly CampSubCursorKind SubKind;
                internal readonly int SubSel;
                internal readonly string SubText;

                // Chosen cursor + indices
                internal readonly CampHighlightKind Kind;
                internal readonly CampHighlightConfidence Confidence;
                internal readonly int Cursor;
                internal readonly int Index;
                internal readonly int Shift;
                internal readonly int Overall;


                // Partition context (from cursor entry)
                internal readonly int ListNums;
                internal readonly int PartyCnt;
                internal readonly int StockCnt;
                internal readonly CampSelectionRowKind RowKind;

                // Raw list values at Overall
                internal readonly int ListIdxVal;
                internal readonly int StockIdxVal;

                // When ListIdxVal appears to be a stocklist index:
                internal readonly bool ListIdxIsStocklistIndex;
                internal readonly int StocklistUwAtListIdx;

                // Resolved unit
                internal readonly UnitResolveInfo Unit;

                // Human-readable explanation (stable enough for diffs)
                internal readonly string Reason;

                internal CampHighlightedSelection(
                    bool haveProbe,
                    int drawMode,
                    int rootSel,
                    string rootText,
                    CampSubCursorKind subKind,
                    int subSel,
                    string subText,
                    CampHighlightKind kind,
                    CampHighlightConfidence confidence,
                    in CampSelectionCursorEntry entry,
                    bool listIdxIsStocklistIndex,
                    int stocklistUwAtListIdx,
                    in UnitResolveInfo unit,
                    string reason)
                {
                    HaveProbe = haveProbe;
                    DrawMode = drawMode;
                    RootSel = rootSel;
                    RootText = rootText ?? string.Empty;
                    SubKind = subKind;
                    SubSel = subSel;
                    SubText = subText ?? string.Empty;

                    Kind = kind;
                    Confidence = confidence;

                    Cursor = entry.Cursor;
                    Index = entry.Index;
                    Shift = entry.Shift;
                    Overall = entry.Overall;


                    ListNums = entry.ListNums;
                    PartyCnt = entry.PartyCnt;
                    StockCnt = entry.StockCnt;
                    RowKind = entry.RowKind;

                    ListIdxVal = entry.ListIdxVal;
                    StockIdxVal = entry.StockIdxVal;

                    ListIdxIsStocklistIndex = listIdxIsStocklistIndex;
                    StocklistUwAtListIdx = stocklistUwAtListIdx;

                    Unit = unit;
                    Reason = reason ?? string.Empty;
                }
            }

            // Public-ish (internal) entry point for other devtools.
            // Captures a snapshot + probe and then derives highlight metadata.
            internal static bool TryGetCampHighlightedSelection(out CampHighlightedSelection sel, out string? error)
            {
                sel = default;
                error = null;

                if (!TryCaptureCampSelectionSnapshot(out CampSelectionSnapshot snap, out string? snapErr))
                {
                    error = snapErr ?? "capture failed";
                    return false;
                }

                bool haveProbe = TryGetCampProbeState(out var st);

                if (!TryDeriveHighlightedSelection(snap, haveProbe, st, out sel, out string why))
                {
                    error = why;
                    return false;
                }

                return true;
            }

            private static bool TryDeriveHighlightedSelection(in CampSelectionSnapshot snap, bool haveProbe, in CampProbeState st, out CampHighlightedSelection sel, out string why)
            {
                sel = default;
                why = string.Empty;

                if (!TryGetDds3GlobalWorkObject(out object? dds3Gbwk) || dds3Gbwk == null)
                {
                    why = "dds3GlobalWork.DDS3_GBWK unavailable";
                    return false;
                }

                if (!TryPickHighlightCursor(snap, haveProbe, st, out CampHighlightKind kind, out CampHighlightConfidence conf, out int chosenCursor, out string pickWhy))
                {
                    why = pickWhy;
                    return false;
                }

                if (chosenCursor < 0 || !TryGetCursorEntry(snap, chosenCursor, out CampSelectionCursorEntry entry))
                {
                    why = pickWhy + "; no usable cursor entry";
                    return false;
                }

                // Resolve unit.
                if (!TryResolveFromEntry(dds3Gbwk, entry, out UnitResolveInfo hi, out string entryWhy))
                {
                    // Last-resort: cmpCalc src/dst indices.
                    if (snap.Src.UnitworkIndex >= 0 && TryResolveUnitFromCandidateIndex(dds3Gbwk, snap.Src.UnitworkIndex, out hi))
                    {
                        conf = CampHighlightConfidence.Low;
                        entryWhy = $"fallback=cmpCalc.Src unitworkIndex={snap.Src.UnitworkIndex}";
                    }
                    else if (snap.Dst.UnitworkIndex >= 0 && TryResolveUnitFromCandidateIndex(dds3Gbwk, snap.Dst.UnitworkIndex, out hi))
                    {
                        conf = CampHighlightConfidence.Low;
                        entryWhy = $"fallback=cmpCalc.Dst unitworkIndex={snap.Dst.UnitworkIndex}";
                    }
                    else
                    {
                        why = pickWhy + "; cursor unresolved";
                        return false;
                    }
                }

                // Determine whether ListIdxVal is plausibly a stocklist index.
                // We treat this as a *hint* only: not all screens use stocklist.
                bool listIsStockIdx = false;
                int stockUwAtList = int.MinValue;

                if (entry.ListIdxVal != int.MinValue && entry.ListIdxVal >= 0)
                {
                    if (TryGetIntArrayAt(dds3Gbwk, "stocklist", entry.ListIdxVal, out int uwIdx))
                    {
                        stockUwAtList = uwIdx;
                        // We consider it a match if it agrees with either the resolved unitwork or the StockIdxVal.
                        if (uwIdx == hi.UnitworkIndex || (entry.StockIdxVal != int.MinValue && uwIdx == entry.StockIdxVal) || (entry.ResolvedUnitworkIndex >= 0 && uwIdx == entry.ResolvedUnitworkIndex))
                            listIsStockIdx = true;
                    }
                }

                if (conf == CampHighlightConfidence.Medium && listIsStockIdx)
                {
                    // If we can prove listIdx is a stocklist index that maps to the resolved unit,
                    // this is strong evidence we truly understand the current highlight.
                    conf = CampHighlightConfidence.High;
                }

                string rootText = (haveProbe && st.HaveSelectionLocalized) ? (st.SelectionLocalized ?? string.Empty) : string.Empty;
                string subText = (haveProbe && st.HaveSubSelectionLocalized) ? (st.SubSelectionLocalized ?? string.Empty) : string.Empty;

                sel = new CampHighlightedSelection(
                    haveProbe,
                    snap.DrawMode,
                    haveProbe ? st.RootSel : -1,
                    rootText,
                    haveProbe ? st.SubKind : CampSubCursorKind.Unknown,
                    haveProbe ? st.SubSel : -1,
                    subText,
                    kind,
                    conf,
                    entry,
                    listIsStockIdx,
                    stockUwAtList,
                    hi,
                    pickWhy + "; " + entryWhy);

                return true;
            }

            private static bool TryPickHighlightCursor(in CampSelectionSnapshot snap, bool haveProbe, in CampProbeState st, out CampHighlightKind kind, out CampHighlightConfidence confidence, out int chosenCursor, out string why)
            {
                kind = CampHighlightKind.Unknown;
                confidence = CampHighlightConfidence.None;
                chosenCursor = -1;
                why = string.Empty;

                // 1) Target selection mode (medicine etc): destination cursor is the active highlight.
                if (snap.DrawMode == 5 && snap.Dst.Cursor >= 0)
                {
                    kind = CampHighlightKind.TargetSelectionDst;
                    chosenCursor = snap.Dst.Cursor;
                    confidence = CampHighlightConfidence.Medium;
                    why = "drawMode=5 => choose Dst cursor (target selection)";
                    return true;
                }

                // 2) Party submenu special-cases (Summon / Return-to-stock).
                if (haveProbe && st.SubKind == CampSubCursorKind.Party)
                {
                    // We intentionally still look for the localized sub text as a sanity check.
                    string sub = st.HaveSubSelectionLocalized ? (st.SubSelectionLocalized ?? string.Empty) : string.Empty;

                    if (ContainsIgnoreCase(sub, "Summon") && snap.Src.Cursor >= 0)
                    {
                        kind = CampHighlightKind.PartySummonStock;
                        chosenCursor = snap.Src.Cursor;
                        confidence = CampHighlightConfidence.Medium;
                        why = "Party/Summon => choose Src cursor (stock highlight)";
                        return true;
                    }

                    if (ContainsIgnoreCase(sub, "Return") && snap.Src.Cursor >= 0)
                    {
                        kind = CampHighlightKind.PartyReturnStock;
                        chosenCursor = snap.Src.Cursor;
                        confidence = CampHighlightConfidence.Medium;
                        why = "Party/Return-to-stock => choose Src cursor (stock highlight)";
                        return true;
                    }
                }

                // 3) Stats root often behaves like "party highlight" (cursor[0] moves through a list).
                if (haveProbe && st.HaveRootCursor && st.RootSel == 4 && snap.Src.Cursor >= 0)
                {
                    kind = CampHighlightKind.StatsList;
                    chosenCursor = snap.Src.Cursor;
                    confidence = CampHighlightConfidence.Medium;
                    why = "root=Stats => choose Src cursor";
                    return true;
                }

                // 4) Fallback: pick the best cursor entry by scoring.
                chosenCursor = PickBestCursorByScore(snap, out string scoredWhy);
                if (chosenCursor >= 0)
                {
                    kind = CampHighlightKind.FallbackScore;
                    confidence = CampHighlightConfidence.Medium;
                    why = scoredWhy;
                    return true;
                }

                why = "no cursor entries";
                return false;
            }
        }
    }
}
