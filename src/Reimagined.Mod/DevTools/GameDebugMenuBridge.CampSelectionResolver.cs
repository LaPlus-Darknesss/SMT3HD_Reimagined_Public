#nullable enable
using System;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            
            internal enum CampHighlightConfidence
            {
                None = 0,
                Low = 1,
                Medium = 2,
                High = 3,
            }

			// =========================================================
            //  Derive a single "highlighted unit" from the current camp snapshot
            // =========================================================
            //

            private static bool TryGetHighlightedUnit(in CampSelectionSnapshot snap, bool haveProbe, in CampProbeState st, out UnitResolveInfo hi, out CampHighlightConfidence confidence, out string reason)
            {
                hi = default;
                confidence = CampHighlightConfidence.None;
                reason = string.Empty;

                if (!TryGetDds3GlobalWorkObject(out object? dds3Gbwk) || dds3Gbwk == null)
                {
                    reason = "dds3GlobalWork.DDS3_GBWK unavailable";
                    return false;
                }

                int chosenCursor = -1;
                string pickWhy = string.Empty;

                // 1) Target selection mode: highlighted row is the destination unit (the target).
                if (snap.DrawMode == 5 && snap.Dst.Cursor >= 0)
                {
                    chosenCursor = snap.Dst.Cursor;
                    pickWhy = "drawMode=5 => choose Dst cursor (target selection)";
                    confidence = CampHighlightConfidence.Medium;
                }
                // 2) Party submenu special-cases.
                else if (haveProbe && st.SubKind == CampSubCursorKind.Party)
                {
                    string sub = st.HaveSubSelectionLocalized ? (st.SubSelectionLocalized ?? string.Empty) : string.Empty;

                    if (ContainsIgnoreCase(sub, "Summon"))
                    {
                        if (snap.Src.Cursor >= 0)
                        {
                            chosenCursor = snap.Src.Cursor;
                            pickWhy = "Party/Summon => choose Src cursor (stock highlight)";
                            confidence = CampHighlightConfidence.Medium;
                        }
                    }
                    else if (ContainsIgnoreCase(sub, "Return"))
                    {
                        if (snap.Src.Cursor >= 0)
                        {
                            chosenCursor = snap.Src.Cursor;
                            pickWhy = "Party/Return-to-stock => choose Src cursor (stock highlight)";
                            confidence = CampHighlightConfidence.Medium;
                        }
                    }
                }
                // 3) Stats root often behaves like "party highlight": Src is usually the moving one.
                else if (haveProbe && st.HaveRootCursor && st.RootSel == 4)
                {
                    if (snap.Src.Cursor >= 0)
                    {
                        chosenCursor = snap.Src.Cursor;
                        pickWhy = "root=Stats => choose Src cursor";
                        confidence = CampHighlightConfidence.Medium;
                    }
                }

                // 4) Fallback: pick the best cursor entry by scoring (most informative & least "fixed").
                if (chosenCursor < 0)
                {
                    chosenCursor = PickBestCursorByScore(snap, out pickWhy);
                    if (confidence == CampHighlightConfidence.None)
                        confidence = CampHighlightConfidence.Medium;
                }

                if (chosenCursor >= 0 && TryGetCursorEntry(snap, chosenCursor, out CampSelectionCursorEntry entry))
                {
                    if (TryResolveFromEntry(dds3Gbwk, entry, out hi, out string entryWhy))
                    {
                        if (confidence < CampHighlightConfidence.High)
                            confidence = CampHighlightConfidence.High;
                        reason = $"{pickWhy}; {entryWhy}";
                        return true;
                    }

                    // Sometimes the cursor entry couldn't resolve but cmpCalc src/dst did.
                    if (snap.Src.UnitworkIndex >= 0 && TryResolveUnitFromCandidateIndex(dds3Gbwk, snap.Src.UnitworkIndex, out hi))
                    {
                        confidence = CampHighlightConfidence.Low;
                        reason = $"{pickWhy}; fallback=cmpCalc.Src unitworkIndex={snap.Src.UnitworkIndex}";
                        return true;
                    }
                    if (snap.Dst.UnitworkIndex >= 0 && TryResolveUnitFromCandidateIndex(dds3Gbwk, snap.Dst.UnitworkIndex, out hi))
                    {
                        confidence = CampHighlightConfidence.Low;
                        reason = $"{pickWhy}; fallback=cmpCalc.Dst unitworkIndex={snap.Dst.UnitworkIndex}";
                        return true;
                    }

                    reason = $"{pickWhy}; cursor[{chosenCursor}] unresolved (no viable unit indices)";
                    return false;
                }

                reason = $"{pickWhy}; no usable cursor entry";
                return false;
            }

            private static bool TryResolveFromEntry(object dds3Gbwk, in CampSelectionCursorEntry e, out UnitResolveInfo hi, out string why)
            {
                hi = default;
                why = string.Empty;

                // Prefer the already-resolved unitwork index (derived from StockIdx/ListIdx probing).
                if (e.ResolvedUnitworkIndex >= 0 && TryResolveUnitFromUnitworkIndex(dds3Gbwk, e.ResolvedUnitworkIndex, out hi))
                {
                    why = $"cursor[{e.Cursor}] resolved via resolvedUnitworkIndex={e.ResolvedUnitworkIndex} (overall={e.Overall}, stockIdx={e.StockIdxVal}, listIdx={e.ListIdxVal})";
                    return true;
                }

                // Fall back to interpreting StockIdx/ListIdx as unitwork indices.
                if (e.StockIdxVal != int.MinValue && e.StockIdxVal >= 0 && TryResolveUnitFromUnitworkIndex(dds3Gbwk, e.StockIdxVal, out hi))
                {
                    why = $"cursor[{e.Cursor}] resolved via stockIdx={e.StockIdxVal} (overall={e.Overall}, listIdx={e.ListIdxVal})";
                    return true;
                }

                if (e.ListIdxVal != int.MinValue && e.ListIdxVal >= 0 && TryResolveUnitFromStocklistIndex(dds3Gbwk, e.ListIdxVal, out hi))
                {
                    why = $"cursor[{e.Cursor}] resolved via listIdx={e.ListIdxVal} (overall={e.Overall}, stockIdx={e.StockIdxVal})";
                    return true;
                }

                return false;
            }

            private static bool TryGetCursorEntry(in CampSelectionSnapshot snap, int cursor, out CampSelectionCursorEntry entry)
            {
                entry = default;
                var arr = snap.CursorEntries;
                if (arr == null || arr.Length == 0)
                    return false;

                // CursorEntries are typically stored densely (cursor == array index),
                // but we keep this robust in case the capture logic changes later.
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i].Cursor == cursor)
                    {
                        entry = arr[i];
                        return true;
                    }
                }

                return false;
            }

            private static int PickBestCursorByScore(in CampSelectionSnapshot snap, out string why)
            {
                why = "fallback scoring";
                var arr = snap.CursorEntries;
                if (arr == null || arr.Length == 0)
                {
                    return -1;
                }

                long dstPtr = snap.Dst.Ptr;
                long srcPtr = snap.Src.Ptr;

                int bestCursor = -1;
                int bestScore = int.MinValue;

                for (int i = 0; i < arr.Length; i++)
                {
                    var e = arr[i];
                    int score = 0;

                    if (e.Overall >= 0) score += 1;
                    if (e.StockIdxVal != int.MinValue && e.StockIdxVal != 0) score += 1;
                    if (e.ListIdxVal != int.MinValue && e.ListIdxVal != 0) score += 1;

                    if (e.ResolvedPtr != 0) score += 3;
                    if (!string.IsNullOrEmpty(e.ResolvedName)) score += 2;

                    // Prefer the moving cursor over a fixed "anchor" cursor.
                    if (dstPtr != 0 && e.ResolvedPtr == dstPtr && srcPtr != 0 && srcPtr != dstPtr)
                        score -= 2;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCursor = e.Cursor;
                    }
                }

                why = $"fallback scoring => cursor[{bestCursor}] (score={bestScore})";
                return bestCursor;
            }

            private static bool ContainsIgnoreCase(string haystack, string needle)
            {
                if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
                    return false;
                return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}