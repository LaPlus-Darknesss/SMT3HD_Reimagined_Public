#nullable enable
using System;
using System.Text;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass 73: Camp selection API + stable identity helpers
            // =========================================================
            //
            // Motivation:
            // - We now have a best-effort derived highlighted selection (CampHighlightedSelection),
            //   but QuickDump is still the primary consumer.
            // - This file introduces a small, stable API surface we can reuse from:
            //     * future "builders" (swap party, inject actions)
            //     * live inspectors / watchers
            //     * automated tracing/validation (unit list invariants)
            //
            // NOTE:
            // - Everything here is INTERNAL to the mod and intentionally "narrow":
            //   it avoids exposing raw Il2Cpp objects and keeps the caller on the safe side.

            internal readonly struct CampSelectionIdentity
            {
                // Context
                internal readonly int DrawMode;
                internal readonly int RootSel;
                internal readonly CampSubCursorKind SubKind;
                internal readonly int SubSel;

                // Highlight metadata
                internal readonly CampHighlightKind Kind;
                internal readonly CampHighlightConfidence Confidence;

                // Cursor + indices
                internal readonly int Cursor;
                internal readonly int Overall;
                internal readonly int ListIdxVal;
                internal readonly int StockIdxVal;

                // Partition context
                internal readonly int ListNums;
                internal readonly int PartyCnt;
                internal readonly int StockCnt;
                internal readonly CampSelectionRowKind RowKind;

                // Resolved unit (best-effort)
                internal readonly int UnitworkIndex;
                internal readonly int UnitId;
                internal readonly int UniqueId;
                internal readonly uint NameCodeSig;
                internal readonly uint FullNameCodeSig;
                internal readonly string NameCodeStr;
                internal readonly string FullNameCodeStr;
                internal readonly string UnitNameTag;

                internal CampSelectionIdentity(in CampHighlightedSelection sel)
                {
                    DrawMode = sel.DrawMode;
                    RootSel = sel.RootSel;
                    SubKind = sel.SubKind;
                    SubSel = sel.SubSel;

                    Kind = sel.Kind;
                    Confidence = sel.Confidence;

                    Cursor = sel.Cursor;
                    Overall = sel.Overall;
                    ListIdxVal = sel.ListIdxVal;
                    StockIdxVal = sel.StockIdxVal;

                    ListNums = sel.ListNums;
                    PartyCnt = sel.PartyCnt;
                    StockCnt = sel.StockCnt;
                    RowKind = sel.RowKind;

                    UnitworkIndex = sel.Unit.UnitworkIndex;
                    UnitId = sel.Unit.UnitId;
                    UniqueId = sel.Unit.UniqueId;
                    NameCodeSig = sel.Unit.NameCodeSig;
                    FullNameCodeSig = sel.Unit.FullNameCodeSig;
                    NameCodeStr = sel.Unit.NameCodeStr ?? string.Empty;
                    FullNameCodeStr = sel.Unit.FullNameCodeStr ?? string.Empty;
                    UnitNameTag = sel.Unit.NameTag ?? string.Empty;
                }

                internal string ToStableKey()
                {
                    // Key should be:
                    // - stable across runs (prefer numeric ids over localized text)
                    // - compact (diff-friendly)
                    // - explicit enough to correlate selection changes
                    //
                    // Format:
                    //   R{root}/K{subKind}:{subSel}/DM{drawMode}/{kind}/C{cursor}/O{overall}/L{listIdx}/S{stockIdx}/UW{uw}/ID{unitId}/UID{uniqueId}
                    var sb = new StringBuilder(128);
                    sb.Append("R").Append(RootSel);
                    sb.Append("/K").Append(SubKind).Append(":").Append(SubSel);
                    sb.Append("/DM").Append(DrawMode);
                    sb.Append("/").Append(Kind);
                    sb.Append("/C").Append(Cursor);
                    sb.Append("/O").Append(Overall);
                    sb.Append("/L").Append(ListIdxVal);
                    sb.Append("/S").Append(StockIdxVal);
                    sb.Append("/UW").Append(UnitworkIndex);
                    sb.Append("/ID").Append(UnitId);
                    const uint FnvBasis = 2166136261u; // 0x811C9DC5
                    if (NameCodeSig == FnvBasis) sb.Append("/NC-"); else sb.Append("/NC").Append(NameCodeSig.ToString("X8"));
                    if (FullNameCodeSig == FnvBasis) sb.Append("/FNC-"); else sb.Append("/FNC").Append(FullNameCodeSig.ToString("X8"));
                    sb.Append("/UID").Append(UniqueId);
                    return sb.ToString();
                }
            }

            internal static bool TryGetCampSelectionIdentity(out CampSelectionIdentity id, out string why)
            {
                id = default;
                if (!TryGetCampHighlightedSelection(out CampHighlightedSelection sel, out string? err))
                {
                    why = err ?? string.Empty;
                    return false;
                }

                id = new CampSelectionIdentity(sel);
                why = sel.Reason;
                return true;
            }

            internal static string BuildStableHighlightKey(in CampHighlightedSelection sel)
            {
                // Helper used by dumps/logs; avoids forcing callers to construct CampSelectionIdentity.
                // This is intentionally independent from localized strings.
                CampSelectionIdentity id = new CampSelectionIdentity(sel);
                return id.ToStableKey();
            }
        }
    }
}
