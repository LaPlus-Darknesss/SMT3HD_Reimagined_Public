#nullable enable
using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        /// <summary>
        /// Field interaction suppression that matches the spirit of vanilla menus:
        /// when a menu overlay is active, the world remains rendered but "confirm/interact"
        /// should not trigger NPC talk, terminals, doors, etc.
        ///
        /// Key design choice:
        /// - Avoid IL2CPP detours here; they are easy to get subtly wrong and create false confidence.
        /// - Prefer reversible state changes that we can explicitly own + restore.
        ///
        /// Current approach (vA30):
        /// - DO NOT call fldEveHit_End. It appears to tear down registrations that do not reliably
        ///   return until a field reload.
        /// - Instead, do a reversible "soft-freeze" of EveHit evaluation while the overlay is active:
        ///   snapshot a small set of fldEveHit static state (indices + counts), then override them
        ///   to values that should make EveHit think there are no active ranges/hits.
        ///
        /// This is owned only while BOTH:
        /// - debug-menu capture is enabled, AND
        /// - the debug menu is (stably) visible OR we're in the short "opening" grace window.
        /// </summary>
        private static class FieldInteractionSuppression
        {
            private static bool s_eveHitFreezeOwned;
            private static EveHitFreezeSnapshot s_freeze;

            private struct EveHitFreezeSnapshot
            {
                public bool Valid;
                public int gEvnRngNum;
                public int gFldEveRngIdx;
                public int gFldEveRngIdx_N;
                public int gFldEveRngIdx_T;
                public int OldHitDoor;
                public int OldHitInf;
            }

            private static int s_lastAttemptFrame = -100000;
            private static int s_lastLogFrame = -100000;
            private static int s_lastWarnFrame = -100000;

            // If something transient fails (e.g., during load), retry slowly instead of spamming.
            private const int RetryFrames = 30;

            /// <summary>
            /// Historical name: this is a per-frame pump called from ReimaginedMod.OnUpdate.
            /// </summary>
            internal static void TryInstall()
            {
                bool want = false;
                try
                {
                    want = GameDebugMenuBridge.IsEveHitHardBlockActiveNow();
                }
                catch
                {
                    want = false;
                }

                int frame = SafeFrameCount();

                if (want)
                {
                    if (!s_eveHitFreezeOwned && (frame - s_lastAttemptFrame) >= RetryFrames)
                    {
                        s_lastAttemptFrame = frame;
                        if (TryFreezeEveHit(out string diag))
                            ThrottledLog(frame, $"[Reimagined] EveHit: FREEZE (soft) {diag}");
                    }
                    return;
                }

                // Not wanted: if we previously froze EveHit state, restore it.
                if (s_eveHitFreezeOwned && (frame - s_lastAttemptFrame) >= RetryFrames)
                {
                    s_lastAttemptFrame = frame;
                    if (TryUnfreezeEveHit(out string diag))
                        ThrottledLog(frame, $"[Reimagined] EveHit: UNFREEZE {diag}");
                }
            }

            /// <summary>
            /// Snapshot a few fldEveHit static ints, then override them to values that should
            /// remove the current "hit" and prevent new ones from being evaluated while the menu
            /// overlay is active.
            /// </summary>
            private static bool TryFreezeEveHit(out string diag)
            {
                diag = "";

                try
                {
                    if (s_eveHitFreezeOwned)
                        return true;

                    var t = FindTypeInLoadedAssemblies("Il2Cpp.fldEveHit");
                    if (t == null)
                        return false;

                    EveHitFreezeSnapshot snap = default;
                    snap.Valid = true;

                    // If any of these fail, we avoid partial ownership.
                    if (!TryReadIntStatic(t, "gEvnRngNum", out snap.gEvnRngNum)) snap.Valid = false;
                    if (!TryReadIntStatic(t, "gFldEveRngIdx", out snap.gFldEveRngIdx)) snap.Valid = false;
                    if (!TryReadIntStatic(t, "gFldEveRngIdx_N", out snap.gFldEveRngIdx_N)) snap.Valid = false;
                    if (!TryReadIntStatic(t, "gFldEveRngIdx_T", out snap.gFldEveRngIdx_T)) snap.Valid = false;
                    if (!TryReadIntStatic(t, "OldHitDoor", out snap.OldHitDoor)) snap.Valid = false;
                    if (!TryReadIntStatic(t, "OldHitInf", out snap.OldHitInf)) snap.Valid = false;

                    if (!snap.Valid)
                        return false;

                    // Apply the freeze.
                    // NOTE: we intentionally only touch *indices + counts*, not the arrays themselves.
                    bool okAny = false;
                    okAny |= TryWriteIntStatic(t, "gFldEveRngIdx", -1);
                    okAny |= TryWriteIntStatic(t, "gFldEveRngIdx_N", -1);
                    okAny |= TryWriteIntStatic(t, "gFldEveRngIdx_T", -1);
                    okAny |= TryWriteIntStatic(t, "OldHitDoor", -1);
                    okAny |= TryWriteIntStatic(t, "OldHitInf", -1);
                    okAny |= TryWriteIntStatic(t, "gEvnRngNum", 0);

                    if (!okAny)
                        return false;

                    s_freeze = snap;
                    s_eveHitFreezeOwned = true;

                    diag = $"(saved evnNum={snap.gEvnRngNum} idx={snap.gFldEveRngIdx}/{snap.gFldEveRngIdx_N}/{snap.gFldEveRngIdx_T} oldDoor={snap.OldHitDoor} oldInf={snap.OldHitInf})";
                    return true;
                }
                catch (Exception ex)
                {
                    ThrottledWarn(SafeFrameCount(), $"[Reimagined] EveHit FREEZE failed: {ex.GetType().Name}: {ex.Message}");
                    s_eveHitFreezeOwned = false;
                    s_freeze = default;
                    return false;
                }
            }

            private static bool TryUnfreezeEveHit(out string diag)
            {
                diag = "";

                try
                {
                    if (!s_eveHitFreezeOwned)
                        return true;

                    if (!s_freeze.Valid)
                    {
                        s_eveHitFreezeOwned = false;
                        s_freeze = default;
                        return true;
                    }

                    var t = FindTypeInLoadedAssemblies("Il2Cpp.fldEveHit");
                    if (t == null)
                        return false;

                    var snap = s_freeze;

                    bool okAny = false;
                    okAny |= TryWriteIntStatic(t, "gEvnRngNum", snap.gEvnRngNum);
                    okAny |= TryWriteIntStatic(t, "gFldEveRngIdx", snap.gFldEveRngIdx);
                    okAny |= TryWriteIntStatic(t, "gFldEveRngIdx_N", snap.gFldEveRngIdx_N);
                    okAny |= TryWriteIntStatic(t, "gFldEveRngIdx_T", snap.gFldEveRngIdx_T);
                    okAny |= TryWriteIntStatic(t, "OldHitDoor", snap.OldHitDoor);
                    okAny |= TryWriteIntStatic(t, "OldHitInf", snap.OldHitInf);

                    // Best-effort nudge so prompts re-evaluate immediately.
                    TryInvokeEveHit("fldEveHit_ChkEveHitAfter");

                    s_eveHitFreezeOwned = false;
                    s_freeze = default;

                    diag = okAny
                        ? $"(restored evnNum={snap.gEvnRngNum} idx={snap.gFldEveRngIdx}/{snap.gFldEveRngIdx_N}/{snap.gFldEveRngIdx_T} oldDoor={snap.OldHitDoor} oldInf={snap.OldHitInf})"
                        : "(restore wrote 0 fields)";

                    return true;
                }
                catch (Exception ex)
                {
                    ThrottledWarn(SafeFrameCount(), $"[Reimagined] EveHit UNFREEZE failed: {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }

            private static bool TryInvokeEveHit(string methodName)
            {
                var t = FindTypeInLoadedAssemblies("Il2Cpp.fldEveHit");
                if (t == null)
                    return false;

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var m = t.GetMethod(methodName, FLAGS);
                if (m == null)
                    return false;

                m.Invoke(null, Array.Empty<object>());
                return true;
            }

            private static bool TryReadIntStatic(Type t, string memberName, out int value)
            {
                value = 0;
                try
                {
                    const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                    var p = t.GetProperty(memberName, FLAGS);
                    if (p != null)
                    {
                        object? v = p.GetValue(null, null);
                        if (v is int i) { value = i; return true; }
                        if (v != null) { value = Convert.ToInt32(v); return true; }
                        return false;
                    }

                    var f = t.GetField(memberName, FLAGS);
                    if (f != null)
                    {
                        object? v = f.GetValue(null);
                        if (v is int i) { value = i; return true; }
                        if (v != null) { value = Convert.ToInt32(v); return true; }
                    }
                }
                catch
                {
                    // ignore
                }
                return false;
            }

            private static bool TryWriteIntStatic(Type t, string memberName, int value)
            {
                try
                {
                    const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                    var p = t.GetProperty(memberName, FLAGS);
                    if (p != null && p.CanWrite)
                    {
                        p.SetValue(null, value, null);
                        return true;
                    }

                    var f = t.GetField(memberName, FLAGS);
                    if (f != null)
                    {
                        f.SetValue(null, value);
                        return true;
                    }
                }
                catch
                {
                    // ignore
                }

                return false;
            }

            private static int SafeFrameCount()
            {
                // Some Unity reference stubs omit Time.frameCount at compile-time.
                // Reuse the outer mod's safe frame counter, which probes via reflection then falls back.
                return GetFrameCountSafe();
            }

            private static void ThrottledLog(int frame, string msg)
            {
                if ((frame - s_lastLogFrame) < 60)
                    return;
                s_lastLogFrame = frame;
                MelonLogger.Msg(msg);
            }

            private static void ThrottledWarn(int frame, string msg)
            {
                if ((frame - s_lastWarnFrame) < 120)
                    return;
                s_lastWarnFrame = frame;
                MelonLogger.Warning(msg);
            }
        }
    }
}
