#nullable enable
using System;
using MelonLoader;
using UnityEngine;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass 71: Live camp highlight trace (opt-in, throttled)
            // =========================================================
            //
            // Why this exists:
            // - When you're mapping menu flows, needing to press a dump hotkey constantly is slow.
            // - A lightweight "print one line when selection changes" trace lets us explore new
            //   screens quickly and capture a *timeline* of cursor behavior.
            //
            // Safety / performance:
            // - Completely opt-in (toggle hotkey).
            // - Throttled (once every N frames).
            // - If snapshot capture fails, it stays silent.

            private static bool s_campHlTraceEnabled;
            private static int s_campHlTraceLastFrame;
            private static long s_campHlTraceLastPtr;
            private static int s_campHlTraceLastUnitwork;
            private static int s_campHlTraceLastRootSel;
            private static int s_campHlTraceLastWorkSel;
            private static int s_campHlTraceLastSubSel;

            internal static void HotkeyToggleCampHighlightTrace()
            {
                s_campHlTraceEnabled = !s_campHlTraceEnabled;

                // Reset "last" so the next tick always prints a line (useful when toggling ON).
                s_campHlTraceLastFrame = 0;
                s_campHlTraceLastPtr = 0;
                s_campHlTraceLastUnitwork = int.MinValue;
                s_campHlTraceLastRootSel = int.MinValue;
                s_campHlTraceLastWorkSel = int.MinValue;
                s_campHlTraceLastSubSel = int.MinValue;

                MelonLogger.Msg($"[Reimagined] campHighlightTrace={(s_campHlTraceEnabled ? "ON" : "OFF")} (throttled)");
            }

            internal static void TickCampHighlightTrace()
            {
                if (!s_campHlTraceEnabled)
                    return;

                // Throttle to reduce reflection overhead. ~4-5 times per second at 60fps.
                int frame = ReimaginedMod.GetFrameCountSafe();
                if (frame - s_campHlTraceLastFrame < 12)
                    return;
                s_campHlTraceLastFrame = frame;

                if (!TryCaptureCampSelectionSnapshot(out CampSelectionSnapshot snap, out _))
                    return;

                bool haveProbe = TryGetCampProbeState(out CampProbeState st);

                if (!TryGetHighlightedUnit(snap, haveProbe, st, out UnitResolveInfo hi, out CampHighlightConfidence conf, out string why))
                    return;

                int rootSel = (haveProbe && st.HaveRootCursor) ? st.RootSel : -1;
                int workSel = (haveProbe && st.HaveWorkCursor) ? st.WorkSel : -1;
                int subSel = (haveProbe && st.HaveSubCursor) ? st.SubSel : -1;

                // Only emit when something changes in a meaningful way.
                if (hi.Ptr == s_campHlTraceLastPtr &&
                    hi.UnitworkIndex == s_campHlTraceLastUnitwork &&
                    rootSel == s_campHlTraceLastRootSel &&
                    workSel == s_campHlTraceLastWorkSel &&
                    subSel == s_campHlTraceLastSubSel)
                    return;

                s_campHlTraceLastPtr = hi.Ptr;
                s_campHlTraceLastUnitwork = hi.UnitworkIndex;
                s_campHlTraceLastRootSel = rootSel;
                s_campHlTraceLastWorkSel = workSel;
                s_campHlTraceLastSubSel = subSel;

                string rootText = (haveProbe && st.HaveSelectionLocalized) ? st.SelectionLocalized : $"rootSel={rootSel}";
                string subText = (haveProbe && st.HaveSubSelectionLocalized) ? st.SubSelectionLocalized : $"subSel={subSel}";
                string hiName = hi.NameTag ?? string.Empty;

                // Keep the trace line compact.
                string whyShort = why ?? string.Empty;
                if (whyShort.Length > 140)
                    whyShort = whyShort.Substring(0, 140) + "...";

                MelonLogger.Msg($"[Reimagined] CampHL root=\"{rootText}\" sub=\"{subText}\" drawMode={snap.DrawMode} hl=\"{hiName}\" unitwork={hi.UnitworkIndex} ptr=0x{hi.Ptr:X} conf={conf}  ({whyShort})");
            }
        }
    }
}
