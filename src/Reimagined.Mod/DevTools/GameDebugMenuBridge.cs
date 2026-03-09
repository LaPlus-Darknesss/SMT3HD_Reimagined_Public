#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
                {
                    private static bool s_injected;
                    private static object? s_originalRootList;
                    private static object? s_reimaginedSubList;
                    private static bool s_stockPatched;

                    // Debug-menu assist toggles 
                    private static bool s_padProbeEnabled;
                    private static ushort s_padProbeLastData0 = 0xFFFF;
                    private static int s_padProbeLastFrame = -100000;

                    // Additional input diagnostics / mitigation
                    private static bool s_padmapTraceEnabled;
                    private static bool s_mesOkRemapEnabled;
                    private static bool s_mesOkRemapArmed;
                    private static object? s_mesOkSaved;
                    private static object? s_mesCancelSaved;


                    // Optional command-menu style pad disable 
                    private static bool s_cmdPadDisableEnabled = true;
                    private static bool s_cmdPadDisableOwned;


                    // Optional: hard gate for itfMesManager OK processing 
                    private static bool s_mesOkSwitchEnabled = true;
                    private static bool s_mesOkSwitchOwned;

                    // Remap target name (SDF_PADMAP enum). We default to L3 because keyboard usually doesn't bind it.
                    private static string s_mesOkRemapTargetName = "SDF_PADMAP_L3";


                    private static int s_lastFieldSuppressionLogFrame = -100000;
                    // Input bleed-through: while the in-game debug menu is visible, we optionally engage an input-capture strategy
                    // so gameplay controls (movement/confirm) don't also fire in the world while navigating the debug menu.
                    private static bool s_pauseWhileMenuOpen = true;// default ON (vanilla-style overlay capture)
                    private enum PauseCaptureStrategy
        {
            FieldUnitPadRelease = 0,
            FieldPlayerStop = 1,
            KernelPause = 2,
        }

                    // Default strategy is FieldPlayerStop because KernelPause can interfere with debug-menu navigation.
                    private static PauseCaptureStrategy s_pauseCaptureStrategy = PauseCaptureStrategy.FieldUnitPadRelease;

                    // Owned state for the field-player stop strategy (so we don't accidentally resume a stop we didn't start).
                    private static bool s_fieldStopOwned;

                    // When using FieldUnitPadRelease strategy, also engage fldPlayerEventStop for stronger suppression (blocks NPC talk/selection).
                    // This is enabled by default; toggle with Ctrl+Alt+F7 if it interferes with debug-menu navigation.
                    private static bool s_unitPadAlsoEngageFieldPlayerStop = true;
                    private static bool s_eveHitHardBlockEnabled = true;

                    // Immediate gating window: when toggling the debug menu on, we request a short hard-block
                    // period immediately so field interaction systems don't get a frame to react (camera snap,
                    // inspect kick, etc.) before our stable visibility latch converges.
                    private static int s_immediateHardBlockFrames;
                    private static string? s_immediateHardBlockReason;

                    // Per-frame latch to avoid spamming fldPlayerEventStop calls.
                    private static int s_fieldStopLastApplyFrame = -1;

// Additional overlay: scrub fldPlayer's immediate input accumulators (gKeyInputDir/gKeyInputCnt)
// each frame while the debug menu is visible. This is a pragmatic mitigation for Space/WASD
// bleed-through into field interactions (NPC talk / interactables) on some scenes/builds where
// fldCommand_PAD_DISABLE and/or fldPlayerEventStop are not sufficient by themselves.
// Enabled by default; toggle with Ctrl+Alt+F8.
private static bool s_unitPadAlsoScrubFieldPlayerInput = true;

// Per-frame latch to avoid spamming writes.

// Sequence trace: logs dds3SequenceList mode transitions to help map vanilla menu routing.
// OFF by default to avoid log spam; toggle with Ctrl+Alt+F10.
private static bool s_seqTraceEnabled = false;
private static int s_seqTraceLastField = int.MinValue;
private static int s_seqTraceLastEvent = int.MinValue;
private static int s_seqTraceLastCamp = int.MinValue;
private static int s_seqTraceLastTerminal = int.MinValue;
private static int s_seqTraceLastDebug = int.MinValue;

private static int s_fieldPlayerScrubLastApplyFrame = -1;


                    // Owned state for the field-unit pad release strategy (mirrors the camp/command menu style of locking field movement).
                    private static bool s_unitPadOwned;

                    // Polarity for fldPlayerEventStop(sw). Default uses the method's default (false) to engage stop.
                    // If this turns out inverted in runtime, we can flip it with Ctrl+Alt+F2.
                    private static bool s_fieldStopStopArg = false; // default: try FALSE for engage-stop; toggle with Ctrl+Alt+F2 if inverted at runtime

                    // Optional input block: attempt to prevent gameplay input bleed-through while the debug menu is visible.
                    // We start with a safe, reversible approach (ResetInputAxes). It may not catch every input path,
                    // but it helps us identify what the game is actually using for movement/confirm.
                    private static bool s_blockInputResetAxes;
                    private static int s_lastResetAxesLogFrame;
                    private static float s_blockResetAxesUntilUnscaled;
                    private static bool s_pauseOwned;
                    private static bool s_lastMenuVisible;
                    private static int s_menuVisibleStreak;
                    private static bool s_lastDrawVisible;
                    private static int s_lastMenuConfirmFrame = -999999;
                    // Some debug menu actions transition into other UIs; if the debug process isn't torn down cleanly,
                    // it can leave the game in a "camera moves but no interaction" limbo. We request a deferred close
                    // a couple frames after the action so we aren't destroying the process mid-callback.
                    private static int s_deferredCloseFrames;
                    private static string? s_deferredCloseReason;

                    // Debug menu visibility probe (field/battle debug menu canvases). We use a lightweight
                    // GameObject.Find(path) scan at a low cadence and cache the last hit.
                    private static float s_nextDbgMenuGoScanUnscaled;
                    private static string? s_dbgMenuGoPath;
                    private static bool s_dbgMenuGoVisible;


                    public static void PumpDeferredClose()
                    {
                        try
                        {
                            if (s_deferredCloseFrames <= 0)
                                return;

                            s_deferredCloseFrames--;
                            if (s_deferredCloseFrames > 0)
                                return;

                            var cmpTest = FindTypeInLoadedAssemblies("Il2Cpp.cmpTest");
                            if (cmpTest == null)
                                return;

                            if (TryCloseDebugMenuProcess(cmpTest, out string note))
                            {
                                if (note == "closed")
                                    MelonLogger.Msg($"[Reimagined] Game debug menu: deferred close closed ({s_deferredCloseReason ?? "unknown"})");
                                else
                                    MelonLogger.Msg($"[Reimagined] Game debug menu: deferred close requested ({s_deferredCloseReason ?? "unknown"}): {note}");
                            }
                            else
                            {
                                MelonLogger.Warning($"[Reimagined] Game debug menu: deferred close failed ({s_deferredCloseReason ?? "unknown"}): {note}");
                            }
        s_deferredCloseReason = null;
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    private static void RequestDeferredClose(string reason)
                    {
                        s_deferredCloseReason = reason;
                        // Give the game a couple frames to transition out of the debug UI callback.
                        s_deferredCloseFrames = Math.Max(s_deferredCloseFrames, 2);
                    }

                    private static MemberInfo? FindStaticMember(Type t, string name)
                    {
                        const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                        return (MemberInfo?)t.GetProperty(name, FLAGS) ?? (MemberInfo?)t.GetField(name, FLAGS);
                    }

                    private static bool TryReadSByteStatic(Type t, string name, out sbyte value)
                    {
                        value = 0;
                        try
                        {
                            var m = FindStaticMember(t, name);
                            if (m == null)
                                return false;
                            var v = GetStaticMemberValue(m);
                            if (v is sbyte sb)
                            {
                                value = sb;
                                return true;
                            }
                            if (v == null)
                                return false;
                            value = Convert.ToSByte(v);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    private static bool TryWriteSByteStatic(Type t, string name, sbyte value)
                    {
                        try
                        {
                            var m = FindStaticMember(t, name);
                            if (m == null)
                                return false;
                            SetStaticMemberValue(m, value);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    private static bool TryWriteNullStatic(Type t, string name)
                    {
                        try
                        {
                            var m = FindStaticMember(t, name);
                            if (m == null)
                                return false;
                            SetStaticMemberValue(m, null);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    private static bool IsDebugMenuLikelyVisible(Type cmpTest, out string diag)
                    {
                        diag = "";

                        // Primary signals (these tend to match what is actually drawn).
                        sbyte a1 = 0, a2 = 0, init = 0, term = 0;
                        bool haveA1 = TryReadSByteStatic(cmpTest, "gActive1", out a1);
                        bool haveA2 = TryReadSByteStatic(cmpTest, "gActive2", out a2);
                        bool haveInit = TryReadSByteStatic(cmpTest, "gInitFlag", out init);
                        bool haveTerm = TryReadSByteStatic(cmpTest, "gTerminateFlag", out term);

                        bool visible = (haveA1 && a1 != 0) || (haveA2 && a2 != 0);
                        // gInitFlag/gTerminateFlag are latches and are not reliable indicators of what is actually drawn.
                        // Treat the menu as visible primarily when the active draw flags are set.
                        bool active = visible;

                        // Secondary signals (process-oriented; useful when flags are missing).
                        object? pid = null;
                        sbyte chk = -1;
                        bool chkAvail = false;
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                            var pidMem = FindStaticMember(cmpTest, "cmpDbgPID");
                            if (pidMem != null)
                                pid = GetStaticMemberValue(pidMem);

                            var chkM = cmpTest.GetMethod("cmpChkDbgProcess", FLAGS);
                            if (chkM != null)
                            {
                                var res = chkM.Invoke(null, Array.Empty<object>());
                                if (res is sbyte sb)
                                {
                                    chk = sb;
                                    chkAvail = true;
                                }
                            }
                        }
                        catch
                        {
                            // ignore
                        }

                        if (!active)
                        {
                            bool pidSet = pid != null;
                            // Some builds leave cmpDbgPID set even when the menu is fully gone. Treat PID as authoritative only
                            // when the process checker also indicates activity.
                            bool pidStale = pidSet && chkAvail && chk == 0;
                            if (pidSet && !pidStale)
                                active = true;
                            else if (chkAvail && chk != 0)
                                active = true;
                        }

                        diag = $"flags(a1={(haveA1 ? a1 : (sbyte)-9)},a2={(haveA2 ? a2 : (sbyte)-9)},init={(haveInit ? init : (sbyte)-9)},term={(haveTerm ? term : (sbyte)-9)})";
                        if (pid != null) diag += " pid=set";
                        if (chkAvail) diag += $" chk={chk}";
                        return active;
                    }

                    private static bool TryCloseDebugMenuProcess(Type cmpTest, out string status)
                    {
                        status = "unknown";
                        try
                        {
                            // The debug menu is effectively latched by classic cmpTest flags.  If we don't clear those,
                            // the menu can remain on-screen even when the process signals say "closed", and opening again
                            // layers another instance.

                            bool didAnything = false;

                            // Ask the debug UI to terminate (if it has a running update loop, it should clean up).
                            if (TryWriteSByteStatic(cmpTest, "gTerminateFlag", 1))
                                didAnything = true;

                            // Force-clear the visible/latched flags so the draw path stops immediately.
                            // (This is what prevents the "menu closed" log while the menu keeps rendering.)
                            if (TryWriteSByteStatic(cmpTest, "gActive1", 0))
                                didAnything = true;
                            if (TryWriteSByteStatic(cmpTest, "gActive2", 0))
                                didAnything = true;
                            // Cursor section can stay latched in some flows; clearing it makes the next open deterministic.
                            TryWriteSByteStatic(cmpTest, "gCursorSection", 0);
                            // Best-effort: ask the process system to end/destroy the debug process if a PID is present.
                            // This prevents the game from thinking a debug process is still registered even when the UI is gone.
                            try
                            {
                                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                                object? pidObj = null;
                                var pidMem = FindStaticMember(cmpTest, "cmpDbgPID");
                                if (pidMem != null)
                                    pidObj = GetStaticMemberValue(pidMem);

                                var cmpInit = FindTypeInLoadedAssemblies("Il2Cpp.cmpInit");
                                if (cmpInit != null)
                                {
                                    var endM = cmpInit.GetMethod("cmpProcessEnd", FLAGS);
                                    endM?.Invoke(null, Array.Empty<object>());
                                    var destroyM = cmpInit.GetMethod("cmpDestroy", FLAGS);
                                    if (destroyM != null && pidObj != null)
                                        destroyM.Invoke(null, new object?[] { pidObj });
                                }
                            }
                            catch
                            {
                                // ignore
                            }

                            bool stillActive = IsDebugMenuLikelyVisible(cmpTest, out string diagAfter);
                            if (!stillActive)
                            {
                                status = "closed";
                                return true;
                            }

                            status = didAnything ? $"close requested (still active: {diagAfter})" : $"close requested (no writable signals; still active: {diagAfter})";
                            return didAnything;
                        }
                        catch (Exception ex)
                        {
                            ex = UnwrapInvokeException(ex);
                            status = $"{ex.GetType().Name}: {ex.Message}";
                            return false;
                        }
                    }

        private static Exception UnwrapInvokeException(Exception ex)
                    {
                        if (ex is TargetInvocationException tie && tie.InnerException != null)
                            return tie.InnerException;
                        return ex;
                    }

        private static void PatchSoftlockProneActionsDeep(object rootList, Type listEntryType)
                    {
                        _ = listEntryType;

                        if (s_stockPatched)
                            return;

                        int disabled = 0;
                        int arraysVisited = 0;
                        var q = new Queue<object>();
                        var visited = new HashSet<int>();

                        q.Enqueue(rootList);

                        while (q.Count > 0 && arraysVisited < 128)
                        {
                            var arr = q.Dequeue();
                            if (arr == null)
                                continue;

                            int key = RuntimeHelpers.GetHashCode(arr);
                            if (!visited.Add(key))
                                continue;

                            arraysVisited++;

                            int len = GetIl2CppRefArrayLength(arr);
                            for (int i = 0; i < len; i++)
                            {
                                var it = GetIl2CppRefArrayItem(arr, i);
                                if (it == null)
                                    continue;

                                var w = GetStringProp(it, "word");
                                if (!string.IsNullOrEmpty(w) && w.IndexOf("stock", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    // This debug entry appears to be invoked during the menu draw/update loop.
                                    // In some contexts it can throw or wedge the game (softlock). We disable it
                                    // silently to prevent both softlocks and per-frame log spam.
                                    if (TryDisableEntrySilent(it, "stock (disabled)", out _))
                                        disabled++;
                                }

                                var next = GetProp(it, "nextList");
                                if (next != null)
                                    q.Enqueue(next);
                            }
                        }

                        s_stockPatched = true;

                        if (disabled > 0)
                            MelonLogger.Msg($"[Reimagined] Game debug menu: disabled {disabled} stock-related entry(s) (safety).");
                    }


        public static bool TryToggleReimaginedMenu(ReimaginedMod mod, out string status)
                    {
                        status = "unknown";

                        var cmpTest = FindTypeInLoadedAssemblies("Il2Cpp.cmpTest");
                        if (cmpTest == null)
                        {
                            status = "Il2Cpp.cmpTest not found (cmp debug menu unavailable in current runtime).";
                            return false;
                        }

                        // Toggle behavior:
						// - If the debug process is active, attempt to close it (so F1 can act as a proper toggle).
						// - Otherwise, inject (once) and start it.
                        bool isActive = IsDebugMenuLikelyVisible(cmpTest, out _);

        if (isActive)
        {
            // Prefer graceful close (cmpProcessEnd) + fallbacks.
            if (TryCloseDebugMenuProcess(cmpTest, out string closeNote))
            {
                status = closeNote;
                // If we're still active after the best-effort close, queue a deferred close too.
                // This helps when the UI is latched by flags that only clear cleanly after the frame boundary.
                if (closeNote != "closed")
                    RequestDeferredClose("user_toggle");
                return true;
            }

            status = $"close failed: {closeNote}";
            return false;
        }
        bool injectedOk = s_injected;
                        string injectNote = s_injected ? "already injected" : "not attempted";

                        if (!s_injected)
                        {
                            injectedOk = TryInjectRootEntry(cmpTest, out injectNote);
                        }

                        var start = cmpTest.GetMethod("cmpDbgProcessStart", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (start == null)
                        {
                            status = "cmpDbgProcessStart not found.";
                            return false;
                        }

                        // Defensive reset: ensure we're not starting while a terminate flag is latched.
                        // (Some builds leave gTerminateFlag set if the close path didn't run.)
                        TryWriteSByteStatic(cmpTest, "gTerminateFlag", 0);
                        TryWriteSByteStatic(cmpTest, "gActive1", 0);
                        TryWriteSByteStatic(cmpTest, "gActive2", 0);

                        // Ensure initialization has run (some close paths can de-init the debug system).
                        try
                        {
                            if (TryReadSByteStatic(cmpTest, "gInitFlag", out sbyte init) && init == 0)
                                TryInvokeStatic(cmpTest, "cmpDbgInit", new object?[] { (sbyte)0 });
                        }
                        catch { /* ignore */ }

                        // Mode is game-defined; 0 appears to be the standard entry.
                        RequestImmediateHardBlock("dbgmenu-toggle-open", 8);
                        start.Invoke(null, new object?[] { (sbyte)0 });

                        if (injectedOk)
                            status = "started (Reimagined injected)";
                        else
                            status = $"started (native; inject failed: {injectNote})";

                        return true;
                    }

                    private static bool TryInjectRootEntry(Type cmpTest, out string status)
                    {
                        status = "unknown";

                        try
                        {
                            var listEntryType = cmpTest.GetNestedType("cmpDbgList_s", BindingFlags.Public | BindingFlags.NonPublic);
                            if (listEntryType == null)
                            {
                                status = "cmpDbgList_s not found (unexpected cmpTest shape).";
                                return false;
                            }

                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                            MemberInfo? rootMember = (MemberInfo?)cmpTest.GetProperty("cmpDbgRootList", FLAGS);
        if (rootMember == null) rootMember = (MemberInfo?)cmpTest.GetProperty("gRootList", FLAGS);
        if (rootMember == null) rootMember = (MemberInfo?)cmpTest.GetField("cmpDbgRootList", FLAGS);
        if (rootMember == null) rootMember = (MemberInfo?)cmpTest.GetField("gRootList", FLAGS);
        if (rootMember == null)
                            {
                                status = "cmpDbgRootList/gRootList not found (neither property nor field).";
                                return false;
                            }

                            object? rootList = GetStaticMemberValue(rootMember);

                            if (rootList == null)
                            {
                                // If the debug menu hasn't been initialized yet, try to do so.
                                TryInvokeStatic(cmpTest, "cmpDbgInit", new object?[] { (sbyte)0 });
                                rootList = GetStaticMemberValue(rootMember);
                            }

                            if (rootList == null)
                            {
                                status = "root list is null (debug menu not initialized?).";
                                return false;
                            }

                            s_originalRootList = rootList;
                            // One-time safety patch: stock-related debug entries can wedge the game depending on context.
                            PatchSoftlockProneActionsDeep(rootList, listEntryType);


                            int oldLen = GetIl2CppRefArrayLength(rootList);
                            if (oldLen <= 0 || oldLen > 512)
                            {
                                status = $"root list length looks wrong: {oldLen}";
                                return false;
                            }

                            // If already injected (e.g. reloading), detect by name and bail.
                            for (int i = 0; i < oldLen; i++)
                            {
                                var it = GetIl2CppRefArrayItem(rootList, i);
                                var w = GetStringProp(it, "word");
                                if (string.Equals(w, "Reimagined", StringComparison.Ordinal))
                                {
                                    s_injected = true;
                                    status = "already injected";
                                    return true;
                                }
                            }

                            // Build submenu once.
                            if (s_reimaginedSubList == null)
                                s_reimaginedSubList = BuildReimaginedSubList(listEntryType);

                            if (s_reimaginedSubList == null)
                            {
                                status = "failed to build Reimagined submenu";
                                return false;
                            }

                            // Extend root list by 1 and append our entry.
                            var newRoot = CreateIl2CppRefArray(listEntryType, oldLen + 1);
                            for (int i = 0; i < oldLen; i++)
                            {
                                var it = GetIl2CppRefArrayItem(rootList, i);
                                SetIl2CppRefArrayItem(newRoot, i, it);
                            }

                            var reEntry = Activator.CreateInstance(listEntryType);
                            if (reEntry == null)
                            {
                                status = "failed to instantiate cmpDbgList_s";
                                return false;
                            }

                            SetStringProp(reEntry, "word", "Reimagined");
                            SetIntProp(reEntry, "Para", 0);
                            // leaf/action callbacks are intentionally omitted in this pass (delegate creation is non-trivial).
                            SetProp(reEntry, "func", null);
                            SetProp(reEntry, "nextList", s_reimaginedSubList);
                            SetIntProp(reEntry, "nextSize", GetIl2CppRefArrayLength(s_reimaginedSubList));

                            SetIl2CppRefArrayItem(newRoot, oldLen, reEntry);

                            // Apply to the member we read from, and also mirror to both known names when available.
                            SetStaticMemberValue(rootMember, newRoot);

                            var pCmp = cmpTest.GetProperty("cmpDbgRootList", FLAGS);
                            if (pCmp != null)
                                pCmp.SetValue(null, newRoot);

                            var pG = cmpTest.GetProperty("gRootList", FLAGS);
                            if (pG != null)
                                pG.SetValue(null, newRoot);

                            var fCmp = cmpTest.GetField("cmpDbgRootList", FLAGS);
                            if (fCmp != null)
                                fCmp.SetValue(null, newRoot);

                            var fG = cmpTest.GetField("gRootList", FLAGS);
                            if (fG != null)
                                fG.SetValue(null, newRoot);

                            s_injected = true;
                            status = "injected";
                            return true;
                        }
                        catch (TargetInvocationException tie)
                        {
                            status = $"inject failed (invoke): {tie.InnerException?.GetType().Name ?? tie.GetType().Name}: {tie.InnerException?.Message ?? tie.Message}";
                            return false;
                        }
                        catch (Exception ex)
                        {
                            status = $"inject failed: {ex.GetType().Name}: {ex.Message}";
                            return false;
                        }
                    }


                    public static void HotkeyTogglePadmapTrace()
                    {
                        s_padmapTraceEnabled = !s_padmapTraceEnabled;
                        var _state = s_padmapTraceEnabled ? "ON" : "OFF";
                        MelonLogger.Msg($"[Reimagined] Padmap trace: {_state}. When ON, logs TRIG2/PRESS2 padmaps when keys are pressed (while debug menu is visible).");
                    }

                    public static void HotkeyToggleCmdPadDisable()
                    {
                        s_cmdPadDisableEnabled = !s_cmdPadDisableEnabled;
                        var _state = s_cmdPadDisableEnabled ? "ON" : "OFF";
                        MelonLogger.Msg($"[Reimagined] Cmd PAD_DISABLE: {_state}. When ON (and capture+FieldUnitPadRelease is active), calls fldCommand_PAD_DISABLE while debug menu is visible to suppress some field interactions (camera/command), but NPC talk may still require the FieldPlayerStop overlay.");
                        if (!s_cmdPadDisableEnabled && s_cmdPadDisableOwned)
                        {
                            // If the user disables it while owned, immediately release.
                            ReleaseCmdPadDisable("hotkey_off");
                        }
                    }



                    
                    public static void HotkeyToggleUnitPadFieldStopOverlay()
                    {
                        s_unitPadAlsoEngageFieldPlayerStop = !s_unitPadAlsoEngageFieldPlayerStop;
                        var _state = s_unitPadAlsoEngageFieldPlayerStop ? "ON" : "OFF";
                        MelonLogger.Msg($"[Reimagined] FieldPlayerStop overlay (for FieldUnitPadRelease): {_state}. When ON, calls fldPlayerEventStop each frame while the debug menu is visible to block NPC talk / dialogue selection bleed-through.");
                        if (!s_unitPadAlsoEngageFieldPlayerStop && s_fieldStopOwned && s_pauseCaptureStrategy != PauseCaptureStrategy.FieldPlayerStop)
                            ReleaseFieldPlayerStop("overlay_toggle_off");
                    }

public static void HotkeyToggleMesOkSwitch()
                    {
                        s_mesOkSwitchEnabled = !s_mesOkSwitchEnabled;
                        var _state = s_mesOkSwitchEnabled ? "ON" : "OFF";
                        MelonLogger.Msg($"[Reimagined] MesOK SwitchOK gate: {_state}. When ON (and capture+FieldUnitPadRelease is active), calls itfMesManager.SwitchOK(false) while debug menu is visible (restores SwitchOK(true) on close). This is intended to stop dialog confirm/advance leaking through (Space/mouse/shift variants) while keeping debug menu navigation intact.");

                        if (!s_mesOkSwitchEnabled && s_mesOkSwitchOwned)
                        {
                            // If the user disables it while owned, immediately release.
                            ReleaseMesOkSwitch("hotkey_off");
                        }
                    }

                    public static void HotkeyToggleMesOkRemap()
                    {
                        s_mesOkRemapEnabled = !s_mesOkRemapEnabled;
                        var _state = s_mesOkRemapEnabled ? "ON" : "OFF";
                        MelonLogger.Msg($"[Reimagined] itfMesManager.OK remap during capture: {_state} (target={s_mesOkRemapTargetName}).");
                        if (!s_mesOkRemapEnabled)
                        {
                            // If we were actively remapping, restore immediately.
                            TryRestoreMesOk("hotkey_off");
                        }
                    }


                    public static void ForceCleanup(string reason)
                    {
                        // Best-effort: restore any global state we might have touched.
                        try
                        {
                            ReleaseCmdPadDisable($"cleanup:{reason}");
                        }
                        catch { }

                        try
                        {
                            ReleaseMesOkSwitch($"cleanup:{reason}");
                        }
                        catch { }

                        try
                        {
                            ReleaseMesOkRemap($"cleanup:{reason}");
                        }
                        catch { }

                        try
                        {
                            ReleaseFieldPlayerStop($"cleanup:{reason}");
                        }
                        catch { }

                        try
                        {
                            ReleaseFieldUnitPadRelease($"cleanup:{reason}");
                        }
                        catch { }

                        try
                        {
                            ReleaseMenuPause($"cleanup:{reason}");
                        }
                        catch { }
                    }



                    private static void PumpPadmapTrace(bool menuVisibleStable)
                    {
                        if (!s_padmapTraceEnabled) return;
                        if (!menuVisibleStable) return;

                        // Keep this very low-noise: only log when Unity reports a keydown edge this frame.
                        if (!Input.anyKeyDown) return;

                        TryDumpPadmapStatesOnce("[PadmapTrace]");
                    }

                    private static void PumpMesOkRemap(bool menuVisibleStable)
                    {
                        // The remap is only meant to run when:
                        //  - debug menu is stably visible
                        //  - capture is ON
                        //  - capture strategy is FieldUnitPadRelease (the one that gives us good movement-lock behavior)
                        // This is an experimental "surgical" mitigation for remaining bleed-through keys like SPACE/confirm.
                        bool wantRemap =
                            s_mesOkRemapEnabled &&
                            s_pauseWhileMenuOpen &&
                            menuVisibleStable &&
                            (s_pauseCaptureStrategy == PauseCaptureStrategy.FieldUnitPadRelease) &&
                            s_unitPadOwned;

                        if (wantRemap)
                        {
                            if (!s_mesOkRemapArmed)
                                TryApplyMesOk("capture_active");
                        }
                        else
                        {
                            if (s_mesOkRemapArmed)
                                TryRestoreMesOk("capture_inactive");
                        }
                    }
                    private static void PumpMesOkSwitch(bool menuVisibleStable)
                    {
                        // We only want to gate OK while:
                        //  - debug menu is stably visible
                        //  - capture is ON
                        //  - strategy is FieldUnitPadRelease (non-freeze strategy)
                        //  - AND the user enabled this gate
                        if (!s_mesOkSwitchEnabled)
                        {
                            if (s_mesOkSwitchOwned)
                                ReleaseMesOkSwitch("disabled");
                            return;
                        }

                        if (!menuVisibleStable || !s_pauseWhileMenuOpen || s_pauseCaptureStrategy != PauseCaptureStrategy.FieldUnitPadRelease)
                        {
                            if (s_mesOkSwitchOwned)
                                ReleaseMesOkSwitch("not_applicable");
                            return;
                        }

                        AcquireMesOkSwitchBestEffort("menu_visible_stable");
                    }

                                private static Type? s_mesOkSwitchTypeCached;
                    private static MethodInfo? s_mesOkSwitchMiCached;
                    private static int s_mesOkSwitchLastApplyFrame = -100000;

                    private static void AcquireMesOkSwitchBestEffort(string reason)
                    {
                        // Treat this as "enforce desired state" while capture is active.
                        // Dialogue/field transitions can re-enable OK; we re-apply each frame while the debug menu is visible.
                        try
                        {
                            bool wasOwned = s_mesOkSwitchOwned;

                            if (s_mesOkSwitchTypeCached == null)
                                s_mesOkSwitchTypeCached = FindTypeInLoadedAssemblies("Il2Cpp.itfMesManager");

                            var mesType = s_mesOkSwitchTypeCached;
                            if (mesType == null)
                            {
                                s_mesOkSwitchOwned = false;
                                if (!wasOwned)
                                    MelonLogger.Warning($"[Reimagined] MesOK SwitchOK gate: could not find Il2Cpp.itfMesManager (reason={reason}).");
                                return;
                            }

                            if (s_mesOkSwitchMiCached == null)
                                s_mesOkSwitchMiCached = FindStaticMethod(mesType, "SwitchOK", typeof(void), typeof(bool));

                            var mi = s_mesOkSwitchMiCached;
                            if (mi == null)
                            {
                                s_mesOkSwitchOwned = false;
                                if (!wasOwned)
                                    MelonLogger.Warning($"[Reimagined] MesOK SwitchOK gate: could not find itfMesManager.SwitchOK(bool) (reason={reason}).");
                                return;
                            }

                            int f = GetFrameCountSafe();
                            if (s_mesOkSwitchLastApplyFrame != f)
                            {
                                mi.Invoke(null, new object[] { false });
                                s_mesOkSwitchLastApplyFrame = f;
                            }

                            s_mesOkSwitchOwned = true;
                            if (!wasOwned)
                                MelonLogger.Msg($"[Reimagined] MesOK SwitchOK gate engaged: itfMesManager.SwitchOK(false) (reason={reason}).");
                        }
                        catch (Exception ex)
                        {
                            s_mesOkSwitchOwned = false;
                            MelonLogger.Warning($"[Reimagined] MesOK SwitchOK gate: exception while engaging (reason={reason}): {ex.GetType().Name}: {ex.Message}");
                        }
                    }


                                private static void ReleaseMesOkSwitch(string reason)
                    {
                        if (!s_mesOkSwitchOwned) return;

                        try
                        {
                            var mesType = s_mesOkSwitchTypeCached ?? FindTypeInLoadedAssemblies("Il2Cpp.itfMesManager");
                            var mi = s_mesOkSwitchMiCached;
                            if (mi == null && mesType != null)
                                mi = FindStaticMethod(mesType, "SwitchOK", typeof(void), typeof(bool));

                            if (mi != null)
                            {
                                mi.Invoke(null, new object[] { true });
                                MelonLogger.Msg($"[Reimagined] MesOK SwitchOK gate released: itfMesManager.SwitchOK(true) (reason={reason}).");
                            }
                            else
                            {
                                MelonLogger.Warning($"[Reimagined] MesOK SwitchOK gate release: could not resolve SwitchOK; leaving best-effort (reason={reason}).");
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[Reimagined] MesOK SwitchOK gate: exception while releasing (reason={reason}): {ex.GetType().Name}: {ex.Message}");
                        }
                        finally
                        {
                            s_mesOkSwitchOwned = false;
                            s_mesOkSwitchLastApplyFrame = -100000;
                        }
                    }




                    private static void PumpCmdPadDisable(bool menuVisibleStableAndStableFrames)
                    {
                        // Only meaningful for the FieldUnitPadRelease strategy: we want the world sim to keep running,
                        // but to suppress field "confirm/interact" style actions (NPC talk, advancing dialogue, etc.).
                        bool want =
                            s_cmdPadDisableEnabled &&
                            menuVisibleStableAndStableFrames &&
                            s_pauseWhileMenuOpen &&
                            s_pauseCaptureStrategy == PauseCaptureStrategy.FieldUnitPadRelease;

                        if (want)
                        {
                            AcquireCmdPadDisableBestEffort("menu_visible_stable");
                        }
                        else
                        {
                            if (s_cmdPadDisableOwned)
                                ReleaseCmdPadDisable("not_wanted");
                        }
                    }


                    private static void TryApplyMesOk(string reason)
                    {
                        try
                        {
                            var mesType = FindTypeInLoadedAssemblies("Il2Cpp.itfMesManager");
                            if (mesType == null)
                            {
                                MelonLogger.Warning("[Reimagined] MesOK remap: itfMesManager type not found; cannot remap.");
                                return;
                            }

                            var okProp = mesType.GetProperty("OK", BindingFlags.Public | BindingFlags.Static);
                            var cancelProp = mesType.GetProperty("CANCEL", BindingFlags.Public | BindingFlags.Static);
                            if (okProp == null)
                            {
                                MelonLogger.Warning("[Reimagined] MesOK remap: itfMesManager.OK property not found; cannot remap.");
                                return;
                            }

                            object? okVal = okProp.GetValue(null, null);
                            object? cancelVal = cancelProp != null ? cancelProp.GetValue(null, null) : null;

                            if (okVal == null)
                            {
                                MelonLogger.Warning("[Reimagined] MesOK remap: itfMesManager.OK is null; cannot remap.");
                                return;
                            }

                            if (!s_mesOkRemapArmed)
                            {
                                s_mesOkSaved = okVal;
                                s_mesCancelSaved = cancelVal;
                            }

                            var enumType = okProp.PropertyType;

                            // Prefer parsing exact enum name; fallback to adding SDF_PADMAP_ prefix if needed.
                            object remapVal;
                            try
                            {
                                remapVal = Enum.Parse(enumType, s_mesOkRemapTargetName, ignoreCase: false);
                            }
                            catch
                            {
                                string alt = s_mesOkRemapTargetName.StartsWith("SDF_PADMAP_") ? s_mesOkRemapTargetName : ("SDF_PADMAP_" + s_mesOkRemapTargetName);
                                remapVal = Enum.Parse(enumType, alt, ignoreCase: false);
                                s_mesOkRemapTargetName = alt;
                            }

                            okProp.SetValue(null, remapVal, null);
                            s_mesOkRemapArmed = true;

                            string savedStr = s_mesOkSaved != null ? s_mesOkSaved.ToString() ?? "(null)" : "(null)";
                            string nowStr = remapVal.ToString() ?? "(null)";
                            MelonLogger.Msg($"[Reimagined] MesOK remap applied ({reason}): OK {ShortPadName(savedStr)} -> {ShortPadName(nowStr)}");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[Reimagined] MesOK remap: apply failed ({reason}): {ex.GetType().Name}: {ex.Message}");
                        }
                    }

                    private static void TryRestoreMesOk(string reason)
                    {
                        try
                        {
                            if (!s_mesOkRemapArmed) return;

                            var mesType = FindTypeInLoadedAssemblies("Il2Cpp.itfMesManager");
                            if (mesType == null) return;

                            var okProp = mesType.GetProperty("OK", BindingFlags.Public | BindingFlags.Static);
                            if (okProp == null) return;

                            if (s_mesOkSaved != null)
                            {
                                okProp.SetValue(null, s_mesOkSaved, null);
                                MelonLogger.Msg($"[Reimagined] MesOK remap restored ({reason}): OK -> {ShortPadName(s_mesOkSaved.ToString() ?? "(null)")}");
                            }

                            s_mesOkRemapArmed = false;
                            s_mesOkSaved = null;
                            s_mesCancelSaved = null;
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[Reimagined] MesOK remap: restore failed ({reason}): {ex.GetType().Name}: {ex.Message}");
                        }
                    }

                    private static void ReleaseMesOkRemap(string reason)
                    {
                        // Back-compat: previous passes used ReleaseMesOkRemap for cleanup.
                        TryRestoreMesOk(reason);
                    }


                    private static string ShortPadName(string s)
                    {
                        if (string.IsNullOrEmpty(s)) return s;
                        const string PFX = "SDF_PADMAP_";
                        int idx = s.IndexOf(PFX, StringComparison.Ordinal);
                        if (idx >= 0) return s.Substring(idx + PFX.Length);
                        return s;
                    }

                    private static void TryDumpPadmapStatesOnce(string tag)
                    {
                        try
                        {
                            // Find dds3PadManager + PADCHECK methods.
                            var padMgrType = FindTypeInLoadedAssemblies("Il2Cpp.dds3PadManager");
                            var padMapType = FindTypeInLoadedAssemblies("Il2Cpplibsdf_H.SDF_PADMAP");
                            if (padMgrType == null || padMapType == null)
                            {
                                MelonLogger.Warning($"{tag} padmap dump: required types not found (dds3PadManager or SDF_PADMAP).");
                                return;
                            }

                            var trig2 = padMgrType.GetMethod("DDS3_PADCHECK_TRIG2", BindingFlags.Public | BindingFlags.Static);
                            var press2 = padMgrType.GetMethod("DDS3_PADCHECK_PRESS2", BindingFlags.Public | BindingFlags.Static);

                            // Some dumps used older names without the "2" suffix; fall back if needed.
                            if (trig2 == null) trig2 = padMgrType.GetMethod("DDS3_PADCHECK_TRIG", BindingFlags.Public | BindingFlags.Static);
                            if (press2 == null) press2 = padMgrType.GetMethod("DDS3_PADCHECK_PRESS", BindingFlags.Public | BindingFlags.Static);

                            if (trig2 == null || press2 == null)
                            {
                                MelonLogger.Warning($"{tag} padmap dump: PADCHECK methods not found.");
                                return;
                            }

                            // Player index is almost certainly 0 for single-player.
                            const int PADNO = 0;

                            var trigList = new List<string>();
                            var pressList = new List<string>();

                            var errList = new List<string>();
                            var values = Enum.GetValues(padMapType);
                            foreach (var v in values)
                            {
                                string name = v?.ToString() ?? "(null)";

                                // The enum contains alias and sentinel members (like *_COUNT) that are not valid for PADCHECK.
                                if (!name.StartsWith("SDF_PADMAP_", StringComparison.Ordinal))
                                    continue;
                                if (name.EndsWith("_COUNT", StringComparison.Ordinal) || name.Equals("SDF_PADMAP_COUNT", StringComparison.Ordinal))
                                    continue;

                                try
                                {
                                    var tObj = trig2.Invoke(null, new object[] { v!, PADNO });
                                    var pObj = press2.Invoke(null, new object[] { v!, PADNO });
                                    bool t = (tObj is bool tb) && tb;
                                    bool p = (pObj is bool pb) && pb;
                                    if (t) trigList.Add(ShortPadName(name));
                                    if (p) pressList.Add(ShortPadName(name));
                                }
                                catch (TargetInvocationException tie)
                                {
                                    // Usually wraps an Il2CppException.
                                    Exception? inner = tie.InnerException;
                                    string innerMsg = inner != null ? $"{inner.GetType().Name}: {inner.Message}" : "(no inner)";
                                    errList.Add($"{ShortPadName(name)}[{innerMsg}]");
                                }
                                catch (Exception ex)
                                {
                                    errList.Add($"{ShortPadName(name)}[{ex.GetType().Name}: {ex.Message}]");
                                }
                            }

                            // Also dump itfMesManager.OK/CANCEL so we can correlate "SPACE" to internal padmaps.
                            string mesOk = "(n/a)";
                            string mesCancel = "(n/a)";
                            var mesType = FindTypeInLoadedAssemblies("Il2Cpp.itfMesManager");
                            if (mesType != null)
                            {
                                var okProp = mesType.GetProperty("OK", BindingFlags.Public | BindingFlags.Static);
                                var cancelProp = mesType.GetProperty("CANCEL", BindingFlags.Public | BindingFlags.Static);
                                if (okProp != null)
                                {
                                    var ov = okProp.GetValue(null, null);
                                    if (ov != null) mesOk = ShortPadName(ov.ToString() ?? "(null)");
                                }
                                if (cancelProp != null)
                                {
                                    var cv = cancelProp.GetValue(null, null);
                                    if (cv != null) mesCancel = ShortPadName(cv.ToString() ?? "(null)");
                                }
                            }

                            string trigStr = trigList.Count > 0 ? string.Join(",", trigList) : "(none)";
                            string pressStr = pressList.Count > 0 ? string.Join(",", pressList) : "(none)";

                            // Keep padmap-dump errors non-fatal: report a short summary instead of throwing.
                            string errStr = "(none)";
                            string errMore = "";
                            if (errList.Count > 0)
                            {
                                int take = Math.Min(6, errList.Count);
                                errStr = string.Join(" ; ", errList.GetRange(0, take));
                                if (errList.Count > take)
                                    errMore = $" (+{errList.Count - take} more)";
                            }

                            // Extended trace: include raw Unity key edges + current global padmap bits (best-effort).
                            string unityDown;
                            {
                                var u = new List<string>(8);

                                if (Input.GetKeyDown(KeyCode.Space)) u.Add("Space");
                                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) u.Add("Enter");
                                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) u.Add("Shift");
                                if (Input.GetKeyDown(KeyCode.Escape)) u.Add("Esc");
                                if (Input.GetMouseButtonDown(0)) u.Add("Mouse0");
                                if (Input.GetMouseButtonDown(1)) u.Add("Mouse1");

                                var s = GetInputStringBestEffort();
                                if (!string.IsNullOrEmpty(s))
                                {
                                    s = s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                                    if (s.Length > 16) s = s.Substring(0, 16) + "…";
                                    u.Add($"inputString=\"{s}\"");
                                }

                                u.Add($"anyKeyDown={(Input.anyKeyDown ? "1" : "0")}");

                                unityDown = u.Count == 0 ? "(none)" : string.Join(",", u);
                            }

                            string padMapHex = "(n/a)";
                            string padMapSet = "(n/a)";
                            try
                            {
                                var miGetPadMap = FindStaticMethod(padMgrType, "dds3PadGetPadMap", typeof(uint)) ?? FindStaticMethod(padMgrType, "dds3PadGetPadMap", typeof(int));
                                if (miGetPadMap != null)
                                {
                                    object? o = miGetPadMap.Invoke(null, null);
                                    uint bits = 0;
                                    if (o is uint u) bits = u;
                                    else if (o is int i) bits = unchecked((uint)i);
                                    else if (o is long l) bits = unchecked((uint)l);

                                    padMapHex = $"0x{bits:X8}";

                                    if (padMapType.IsEnum)
                                    {
                                        var names = new List<string>(16);
                                        foreach (var evObj in Enum.GetValues(padMapType))
                                        {
                                            int ev;
                                            try { ev = Convert.ToInt32(evObj); }
                                            catch { continue; }

                                            if (ev == 0) continue;

                                            uint mask = unchecked((uint)ev);
                                            bool set;

                                            // Heuristic: if enum value is power-of-two, treat as bitmask; otherwise if 0..31, treat as bit index.
                                            if ((mask & (mask - 1u)) == 0u)
                                                set = (bits & mask) != 0u;
                                            else if (ev >= 0 && ev < 32)
                                                set = (bits & (1u << ev)) != 0u;
                                            else
                                                set = false;

                                            if (set)
                                            {
                                                var n = evObj.ToString() ?? "";
                                                if (n.StartsWith("SDF_PADMAP_")) n = n.Substring("SDF_PADMAP_".Length);
                                                names.Add(n);
                                            }
                                        }

                                        if (names.Count == 0)
                                            padMapSet = "(none)";
                                        else
                                            padMapSet = names.Count <= 8 ? string.Join(",", names) : string.Join(",", names.Take(8)) + ",…";
                                    }
                                }
                            }
                            catch { /* ignore */ }

                            MelonLogger.Msg($"{tag} PADCHECK: TRIG2={trigStr} | PRESS2={pressStr} | PadMap={padMapHex} [{padMapSet}] | UnityDown={unityDown} | MesOK={mesOk} MesCANCEL={mesCancel} | ERR={errStr}{errMore} | Remap={(s_mesOkRemapArmed ? "ARMED" : "off")} | SwitchOKGate={(s_mesOkSwitchEnabled ? (s_mesOkSwitchOwned ? "OWNED" : "ON") : "off")}");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"{tag} padmap dump failed: {ex.GetType().Name}: {ex.Message}");
                        }
                    }

        private static void DbgTogglePadProbe(int pX, int pY, uint z)
                    {
                        if (!TryConsumeMenuConfirm()) return;
                        s_padProbeEnabled = !s_padProbeEnabled;
                        s_padProbeLastData0 = 0xFFFF;
                        var _state = s_padProbeEnabled ? "ON" : "OFF";
                        MelonLogger.Msg($"[Reimagined] Pad probe now {_state}");
                    }


                                private static void DbgToggleMenuPauseCapture(int pX, int pY, uint z)
                    {
                        if (!TryConsumeMenuConfirm()) return;
                        HotkeyTogglePauseCapture();
                    }

        private static void DbgToggleSeqTrace(int pX, int pY, uint z)
        {
            if (!TryConsumeMenuConfirm()) return;
            HotkeyToggleSeqTrace();
        }


            public static void HotkeyToggleUnitPadFieldPlayerInputScrubOverlay()
{
    s_unitPadAlsoScrubFieldPlayerInput = !s_unitPadAlsoScrubFieldPlayerInput;
    var _state = s_unitPadAlsoScrubFieldPlayerInput ? "ON" : "OFF";
    MelonLogger.Msg($"[Reimagined] fldPlayer input-scrub overlay (for FieldUnitPadRelease): {_state}. When ON, clears fldPlayer.gKeyInputDir/gKeyInputCnt each frame while the debug menu is visible (best-effort).");
}



                                public static void HotkeyTogglePauseCapture()
                    {
                        s_pauseWhileMenuOpen = !s_pauseWhileMenuOpen;

                        string strat = s_pauseCaptureStrategy.ToString();
                        MelonLogger.Msg($"[Reimagined] Game debug menu: input-capture = {(s_pauseWhileMenuOpen ? "ON" : "OFF")} (hotkey), strategy={strat}.");

                        if (!s_pauseWhileMenuOpen)
                        {
                            // Release any capture mechanisms we "own".
                            if (s_pauseOwned)
                                ReleaseMenuPause("toggle_off");
                            if (s_fieldStopOwned)
                                ReleaseFieldPlayerStop("toggle_off");
                        }
                        else
                        {
                            // If the user left ResetAxes enabled, warn only when it is likely to interfere.
                            if (s_blockInputResetAxes && s_pauseCaptureStrategy == PauseCaptureStrategy.KernelPause)
                                MelonLogger.Msg("[Reimagined] Note: input-block(ResetAxes) is ignored while input-capture(strategy=KernelPause) is ON.");
                        }
                    }

                    public static void HotkeyCyclePauseCaptureStrategy()
                    {
                        // Cycle between strategies:
                        //   FieldUnitPadRelease -> KernelPause -> FieldPlayerStop -> FieldUnitPadRelease ...
                        switch (s_pauseCaptureStrategy)
                        {
                            case PauseCaptureStrategy.FieldUnitPadRelease:
                                s_pauseCaptureStrategy = PauseCaptureStrategy.KernelPause;
                                break;
                            case PauseCaptureStrategy.KernelPause:
                                s_pauseCaptureStrategy = PauseCaptureStrategy.FieldPlayerStop;
                                break;
                            default:
                                s_pauseCaptureStrategy = PauseCaptureStrategy.FieldUnitPadRelease;
                                break;
                        }

                        MelonLogger.Msg($"[Reimagined] Game debug menu: input-capture strategy = {s_pauseCaptureStrategy} (hotkey).");

                        // On a strategy flip, clean up any owned state from strategies we are *leaving*.
                        if (s_pauseCaptureStrategy != PauseCaptureStrategy.KernelPause && s_pauseOwned)
                            ReleaseMenuPause("strategy_cycle");

                        if (s_pauseCaptureStrategy != PauseCaptureStrategy.FieldPlayerStop && s_fieldStopOwned)
                            {
                                // If we switched to FieldUnitPadRelease and the overlay is enabled, keep the stop engaged.
                                if (!(s_pauseCaptureStrategy == PauseCaptureStrategy.FieldUnitPadRelease && s_unitPadAlsoEngageFieldPlayerStop))
                                    ReleaseFieldPlayerStop("strategy_cycle");
                            }

                        if (s_pauseCaptureStrategy != PauseCaptureStrategy.FieldUnitPadRelease && s_unitPadOwned)
                            ReleaseFieldUnitPadRelease("strategy_cycle");
                    }
        public static void HotkeyToggleFieldStopPolarity()
        {
            // Flip the boolean we pass to fldPlayerEventStop when engaging the stop.
            // Default: stopArg=false (matches the method's default parameter). If the game uses the opposite
            // polarity in practice, flipping this will make FieldPlayerStop immediately testable.
            s_fieldStopStopArg = !s_fieldStopStopArg;

            // If we currently own a field-stop, release it so we don't get stuck with the wrong polarity.
            if (s_fieldStopOwned)
                ReleaseFieldPlayerStop("polarity_toggle");

            MelonLogger.Msg($"[Reimagined] Game debug menu: field-stop polarity toggled. stopArg={s_fieldStopStopArg} resumeArg={!s_fieldStopStopArg}.");
        }

        public static void HotkeyToggleEveHitHardBlock()
        {
            s_eveHitHardBlockEnabled = !s_eveHitHardBlockEnabled;
            MelonLogger.Msg($"[Reimagined] Game debug menu: field interaction hard-block (EveHit soft-freeze gate) = {(s_eveHitHardBlockEnabled ? "ON" : "OFF")} (hotkey).");
        }


        public static void HotkeyToggleSeqTrace()
        {
            s_seqTraceEnabled = !s_seqTraceEnabled;
            // Reset latches so the next PumpSequenceTrace emits immediately.
            s_seqTraceLastField = int.MinValue;
            s_seqTraceLastEvent = int.MinValue;
            s_seqTraceLastCamp = int.MinValue;
            s_seqTraceLastTerminal = int.MinValue;
            s_seqTraceLastDebug = int.MinValue;

            ResetCampTraceLatches();

            MelonLogger.Msg($"[Reimagined] Sequence trace = {(s_seqTraceEnabled ? "ON" : "OFF")} (hotkey Ctrl+Alt+F10).");
        }

        public static bool IsEveHitHardBlockActiveNow()
        {
            // Only block while our capture system is active.
            // If capture is OFF, keep vanilla field interaction behavior.
            if (!s_eveHitHardBlockEnabled || !s_pauseWhileMenuOpen)
                return false;

            // Prefer stable latch, but also honor the short grace window for immediate gating on toggle.
            bool menuGate = s_lastMenuVisible || (s_immediateHardBlockFrames > 0);
            return menuGate;
        }

        private static void RequestImmediateHardBlock(string reason, int frames)
        {
            if (frames <= 0)
                return;

            if (frames > s_immediateHardBlockFrames)
                s_immediateHardBlockFrames = frames;

            s_immediateHardBlockReason = reason;
        }




                                public static void HotkeyToggleInputBlockResetAxes()
                    {
                        s_blockInputResetAxes = !s_blockInputResetAxes;
                        s_lastResetAxesLogFrame = -100000;

                        if (s_blockInputResetAxes)
                        {
                            const float TIMEOUT_SEC = 8.0f;
                            s_blockResetAxesUntilUnscaled = Time.unscaledTime + TIMEOUT_SEC;
                            MelonLogger.Msg($"[Reimagined] Game debug menu: input-block(ResetAxes) = ON (hotkey, auto-timeout {TIMEOUT_SEC:0}s).");
                        }
                        else
                        {
                            s_blockResetAxesUntilUnscaled = 0;
                            MelonLogger.Msg("[Reimagined] Game debug menu: input-block(ResetAxes) = OFF (hotkey).");
                        }

                        if (s_blockInputResetAxes && s_pauseWhileMenuOpen)
                            MelonLogger.Msg("[Reimagined] Note: input-block(ResetAxes) only runs when input-capture is OFF.");
                    }

                    public static void ForceDisableInputBlockResetAxes(string reason)
                    {
                        try
                        {
                            if (!s_blockInputResetAxes)
                                return;

                            s_blockInputResetAxes = false;
                            s_blockResetAxesUntilUnscaled = 0;
                            s_lastResetAxesLogFrame = -100000;
                            MelonLogger.Msg($"[Reimagined] Game debug menu: input-block(ResetAxes) forced OFF ({reason}).");
                        }
                        catch
                        {
                            s_blockInputResetAxes = false;
                            s_blockResetAxesUntilUnscaled = 0;
                        }
                    }




                                private static void DbgPing(int pX, int pY, uint z)
                    {
                        if (!TryConsumeMenuConfirm()) return;
                        MelonLogger.Msg("[Reimagined] Ping (menu action confirmed).");
                    }


                                private static void DbgToggleInputBlockResetAxes(int pX, int pY, uint z)
                    {
                        if (!TryConsumeMenuConfirm()) return;
                        HotkeyToggleInputBlockResetAxes();
                    }


                                private static void DbgDumpInputSnapshot(int pX, int pY, uint z)
                    {
                        if (!TryConsumeMenuConfirm()) return;
                        HotkeyDumpInputSnapshot();
                    }

        private static bool TryConsumeMenuConfirm()
        {
            int frame = GetFrameCountSafe();
            if (frame == s_lastMenuConfirmFrame) return false;

            bool ok = false;

            // Prefer the game's own pad mapping (works for controller and often for keyboard, depending on bindings).
            ok |= TryPadConfirmEdge();

            // Fallback to Unity key edges (useful if pad mapping isn't available in a given context).
            ok |= TryUnityConfirmEdge();

            if (!ok) return false;

            s_lastMenuConfirmFrame = frame;
            return true;
        }

        private static bool TryPadConfirmEdge()
        {
            try
            {
                // Prefer the game's own pad mapping so controller confirm can trigger menu actions.
                // We keep this reflection-based so we compile even if Il2Cpp wrapper namespaces/types differ.
                var padMgr = FindTypeInLoadedAssemblies("Il2Cpp.dds3PadManager");
                if (padMgr == null)
                    return false;

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var m = padMgr.GetMethod("DDS3_PADCHECK_TRIG", FLAGS);
                if (m == null)
                    return false;

                var ps = m.GetParameters();
                if (ps.Length < 2)
                    return false;

                // Resolve "OK/Confirm" from the enum param if possible.
                object arg0;
                var p0 = ps[0].ParameterType;
                if (p0.IsEnum)
                {
                    string? bestName = null;
                    foreach (var n in Enum.GetNames(p0))
                    {
                        if (n == "SDF_PADMAP_OK") { bestName = n; break; }
                        if (bestName == null && (n.EndsWith("_OK", StringComparison.Ordinal) || n.Equals("OK", StringComparison.Ordinal)))
                            bestName = n;
                        if (bestName == null && n.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
                            bestName = n;
                    }

                    if (bestName == null)
                        return false;

                    arg0 = Enum.Parse(p0, bestName);
                }
                else
                {
                    // Unknown signature; we don't guess constants.
                    return false;
                }

                object arg1;
                var p1 = ps[1].ParameterType;
                if (p1 == typeof(int)) arg1 = 0;
                else if (p1 == typeof(uint)) arg1 = 0u;
                else if (p1 == typeof(short)) arg1 = (short)0;
                else if (p1 == typeof(byte)) arg1 = (byte)0;
                else if (p1.IsEnum) arg1 = Enum.ToObject(p1, 0);
                else arg1 = 0;

                var res = m.Invoke(null, new object?[] { arg0, arg1 });
                if (res is bool b) return b;
                if (res is int i) return i != 0;
                if (res is byte bb) return bb != 0;
                if (res is sbyte sb) return sb != 0;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryUnityConfirmEdge()
        {
            try
            {
                return UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)
                    || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)
                    || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter);
            }
            catch
            {
                return false;
            }
        }



                    public static void HotkeyDumpInputSnapshot()
                    {
                        DumpInputSnapshot("hotkey");
                    }

                                public static void PumpInputBlock()
                    {
                        try
                        {
                            if (!s_blockInputResetAxes)
                                return;

                            // IMPORTANT:
                            // - This mitigation is only intended when input-capture is OFF.
                            // - Calling ResetInputAxes every frame can starve the game's own input reads depending on update order,
                            //   so we never run it while we own kernel input-capture.
                            if (s_pauseWhileMenuOpen || s_pauseOwned)
                                return;

                            // If the menu isn't even open anymore, don't keep a "global input nuke" sticky.
                            if (!s_lastMenuVisible)
                            {
                                s_blockInputResetAxes = false;
                                s_blockResetAxesUntilUnscaled = 0;
                                return;
                            }

                            // Safety: if ResetInputAxes ends up blocking our own recovery hotkeys on some builds,
                            // we automatically disable this after a short timeout.
                            if (s_blockResetAxesUntilUnscaled > 0 && Time.unscaledTime >= s_blockResetAxesUntilUnscaled)
                            {
                                s_blockInputResetAxes = false;
                                s_blockResetAxesUntilUnscaled = 0;
                                MelonLogger.Msg("[Reimagined] Input-block: auto-timeout reached; disabling ResetInputAxes block.");
                                return;
                            }

                            // Best-effort: clear Unity input state so gameplay scripts see fewer presses.
                            // NOTE: This is a blunt instrument and will also disrupt UI/menu navigation if the UI reads Unity Input.
                            bool anyKeyDownBefore = Input.anyKeyDown;
                            bool anyKeyBefore = Input.anyKey;

                            ResetInputAxesSafe();

                            bool anyKeyDownAfter = Input.anyKeyDown;
                            bool anyKeyAfter = Input.anyKey;

                            int _f = GetFrameCountSafe();
                            if (_f - s_lastResetAxesLogFrame >= 30)
                            {
                                s_lastResetAxesLogFrame = _f;
                                float remain = s_blockResetAxesUntilUnscaled > 0 ? (s_blockResetAxesUntilUnscaled - Time.unscaledTime) : -1f;
                                string remainStr = remain >= 0 ? $"{remain:0.0}s" : "?";
                                MelonLogger.Msg($"[Reimagined] Input-block: ResetInputAxes tick (frame={_f} anyKeyDown {anyKeyDownBefore}->{anyKeyDownAfter} anyKey {anyKeyBefore}->{anyKeyAfter} remain={remainStr}).");
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    private static void ResetInputAxesSafe()
                    {
                        try
                        {
                            // Some Unity stubs/interop surfaces for SMT3HD do not expose Input.ResetInputAxes at compile-time.
                            // Use reflection so we compile everywhere, and call it when present at runtime.
                            var mi = typeof(Input).GetMethod("ResetInputAxes", BindingFlags.Public | BindingFlags.Static);
                            if (mi != null)
                                mi.Invoke(null, null);
                        }
                        catch
                        {
                            // ignore
                        }
                    }


                    private static bool TryCallCmpChkDbgProcess(Type cmpTest, out sbyte chk)
                    {
                        chk = 0;
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                            var m = cmpTest.GetMethod("cmpChkDbgProcess", FLAGS);
                            if (m == null)
                                return false;

                            var res = m.Invoke(null, Array.Empty<object>());
                            if (res is sbyte sb)
                            {
                                chk = sb;
                                return true;
                            }
                            if (res is int i)
                            {
                                chk = (sbyte)i;
                                return true;
                            }
                            if (res is byte bb)
                            {
                                chk = (sbyte)bb;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    private static bool TryCallIntStaticMethod(Type t, string methodName, out int value)
                    {
                        value = -999;
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                            var m = t.GetMethod(methodName, FLAGS);
                            if (m == null)
                                return false;

                            var res = m.Invoke(null, Array.Empty<object>());
                            if (res is int i) { value = i; return true; }
                            if (res is sbyte sb) { value = sb; return true; }
                            if (res is byte bb) { value = bb; return true; }
                            if (res is short sh) { value = sh; return true; }
                            if (res is ushort us) { value = us; return true; }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

        private static void DumpInputSnapshot(string reason)
                    {
                        try
                        {
                            ushort d0 = 0, dl0 = 0;
                            bool haveD0 = TryGetSdfPadU16("sdfPadData", 0, out d0);
                            bool haveDL0 = TryGetSdfPadU16("sdfPadDataLast", 0, out dl0);

                            int frame = GetFrameCountSafe();

                            var kernel = FindTypeInLoadedAssemblies("Il2Cpp.dds3KernelMain");
                            int pauseSign = 0, pauseTime = 0, pauseEffect = 0, captureSign = 0, captureCount = 0;
                            bool havePauseSign = kernel != null && TryReadIntStatic(kernel, "dds3PauseSign", out pauseSign);
                            bool havePauseTime = kernel != null && TryReadIntStatic(kernel, "dds3PauseTime", out pauseTime);
                            bool havePauseEffect = kernel != null && TryReadIntStatic(kernel, "dds3PauseEffect", out pauseEffect);
                            bool haveCaptureSign = kernel != null && TryReadIntStatic(kernel, "dds3CaptureSign", out captureSign);
                            bool haveCaptureCount = kernel != null && TryReadIntStatic(kernel, "dds3CaptureCount", out captureCount);

                            // Kernel UI state: useful for emulating vanilla overlay menus (command menu, terminal UI, etc.).
                            int uidispFlag = 0, uidispCount = 0, debugSign = 0, kernelDebugSign = 0;
                            Vector3 uidispGuide = default;
                            int uidispBackLen = 0, uidispBackTrue = 0;
                            string uidispBackBits = "";
                            bool haveUIDispFlag = kernel != null && TryReadIntStatic(kernel, "UIDispFlag", out uidispFlag);
                            bool haveUIDispGuide = kernel != null && TryReadVector3Static(kernel, "UIDispGuide", out uidispGuide);
                            bool haveUIDispBack = kernel != null && TryReadBoolArrayBitsStatic(kernel, "UIDispBackActive", 8, out uidispBackLen, out uidispBackBits, out uidispBackTrue);
                            bool haveUIDispCount = kernel != null && TryReadIntStatic(kernel, "UIDispCount", out uidispCount);
                            bool haveDebugSign = kernel != null && TryReadIntStatic(kernel, "dds3DebugSign", out debugSign);
                            bool haveKernelDebugSign = kernel != null && TryReadIntStatic(kernel, "dds3KernelDebugSign", out kernelDebugSign);


                            var cmpTest = FindTypeInLoadedAssemblies("Il2Cpp.cmpTest");
                            sbyte g1 = 0, g2 = 0, init = 0, term = 0;
                            bool haveG1 = cmpTest != null && TryReadSByteStatic(cmpTest, "gActive1", out g1);
                            bool haveG2 = cmpTest != null && TryReadSByteStatic(cmpTest, "gActive2", out g2);
                            bool haveInit = cmpTest != null && TryReadSByteStatic(cmpTest, "gInitFlag", out init);
                            bool haveTerm = cmpTest != null && TryReadSByteStatic(cmpTest, "gTerminateFlag", out term);

                            sbyte chk = 0;
                            bool haveChk = cmpTest != null && TryCallCmpChkDbgProcess(cmpTest, out chk);

                            bool pidSet = false;
                            bool havePid = false;
                            try
                            {
                                if (cmpTest != null)
                                {
                                    var pidMem = FindStaticMember(cmpTest, "cmpDbgPID");
                                    if (pidMem != null)
                                    {
                                        havePid = true;
                                        var pidObj = GetStaticMemberValue(pidMem);
                                        pidSet = pidObj != null;
                                    }
                                }
                            }
                            catch
                            {
                                // ignore
                            }

                            string diag = "";
                            bool processActive = cmpTest != null && IsDebugMenuLikelyVisible(cmpTest, out diag);
                            bool drawVisible = cmpTest != null && IsDebugMenuVisibleByDrawFlags(cmpTest, out _);
                            bool goVisible = IsDebugMenuVisibleBySceneGameObject(out string goDiag);

                            MelonLogger.Msg("[Reimagined] ---- Input snapshot ----");
                            MelonLogger.Msg($"[Reimagined] reason={reason} frame={frame} unscaled={Time.unscaledTime:0.000}");
                            MelonLogger.Msg($"[Reimagined] dbg: gActive1/gActive2={(haveG1 ? g1.ToString() : "?")}/{(haveG2 ? g2.ToString() : "?")} init={(haveInit ? init.ToString() : "?")} term={(haveTerm ? term.ToString() : "?")} chk={(haveChk ? chk.ToString() : "?")} pid={(havePid ? (pidSet ? "set" : "null") : "?")}");
                            MelonLogger.Msg($"[Reimagined] dbg: goVisible={goVisible} go={goDiag} drawVisible={drawVisible} processActive={processActive} diag={diag} lastDrawVisible={s_lastDrawVisible} lastMenuVisible={s_lastMenuVisible} streak={s_menuVisibleStreak}");
                            MelonLogger.Msg($"[Reimagined] pause: sign={(havePauseSign ? pauseSign.ToString() : "?")} time={(havePauseTime ? pauseTime.ToString() : "?")} eff={(havePauseEffect ? pauseEffect.ToString() : "?")} captureSign={(haveCaptureSign ? captureSign.ToString() : "?")} captureCount={(haveCaptureCount ? captureCount.ToString() : "?")} (ownedPause={s_pauseOwned} ownedFieldStop={s_fieldStopOwned} ownedUnitPad={s_unitPadOwned} cap={(s_pauseWhileMenuOpen ? "ON" : "OFF")} strat={s_pauseCaptureStrategy})");
                            MelonLogger.Msg($"[Reimagined] kernelUI: UIDispFlag={(haveUIDispFlag ? uidispFlag.ToString() : "?")} UIDispGuide={(haveUIDispGuide ? $"{uidispGuide.x:F2},{uidispGuide.y:F2},{uidispGuide.z:F2}" : "?")} UIDispBackActive={(haveUIDispBack ? $"{uidispBackLen} [{uidispBackBits}] true={uidispBackTrue}" : "?")} UIDispCount={(haveUIDispCount ? uidispCount.ToString() : "?")} dds3DebugSign={(haveDebugSign ? debugSign.ToString() : "?")} dds3KernelDebugSign={(haveKernelDebugSign ? kernelDebugSign.ToString() : "?")}");

                            // Sequence state is how vanilla routes big UI/field modes.
                            int seqField = -999, seqEvent = -999, seqCamp = -999, seqTerminal = -999, seqDebug = -999;
                            bool haveSeq = false;
                            try
                            {
                                var seqType = FindTypeInLoadedAssemblies("Il2Cpp.dds3SequenceList");
                                if (seqType != null)
                                {
                                    bool okAny = false;
                                    if (TryCallIntStaticMethod(seqType, "CheckField", out seqField)) okAny = true;
                                    if (TryCallIntStaticMethod(seqType, "CheckEvent", out seqEvent)) okAny = true;
                                    if (TryCallIntStaticMethod(seqType, "CheckCamp", out seqCamp)) okAny = true;
                                    if (TryCallIntStaticMethod(seqType, "CheckTerminal", out seqTerminal)) okAny = true;
                                    if (TryCallIntStaticMethod(seqType, "CheckDebug", out seqDebug)) okAny = true;
                                    haveSeq = okAny;
                                }
                            }
                            catch
                            {
                                haveSeq = false;
                            }

                            MelonLogger.Msg($"[Reimagined] gates: eveHitHardBlock={(IsEveHitHardBlockActiveNow() ? "ON" : "OFF")} immediate={(s_immediateHardBlockFrames > 0 ? s_immediateHardBlockFrames.ToString() : "0")} reason={(s_immediateHardBlockReason ?? "-")}");
                            MelonLogger.Msg($"[Reimagined] seq: field={(haveSeq ? seqField.ToString() : "?")} event={(haveSeq ? seqEvent.ToString() : "?")} camp={(haveSeq ? seqCamp.ToString() : "?")} terminal={(haveSeq ? seqTerminal.ToString() : "?")} debug={(haveSeq ? seqDebug.ToString() : "?")}");

                            // EveHit globals: capturing these in A/B/C/D gives us concrete targets to emulate.
                            var fldEveHit = FindTypeInLoadedAssemblies("Il2Cpp.fldEveHit");
                            int evnNum = -999, idx = -999, idxN = -999, idxT = -999, oldDoor = -999, oldInf = -999;
                            bool haveEvnNum = fldEveHit != null && TryReadIntStatic(fldEveHit, "gEvnRngNum", out evnNum);
                            bool haveIdx = fldEveHit != null && TryReadIntStatic(fldEveHit, "gFldEveRngIdx", out idx);
                            bool haveIdxN = fldEveHit != null && TryReadIntStatic(fldEveHit, "gFldEveRngIdx_N", out idxN);
                            bool haveIdxT = fldEveHit != null && TryReadIntStatic(fldEveHit, "gFldEveRngIdx_T", out idxT);
                            bool haveOldDoor = fldEveHit != null && TryReadIntStatic(fldEveHit, "OldHitDoor", out oldDoor);
                            bool haveOldInf = fldEveHit != null && TryReadIntStatic(fldEveHit, "OldHitInf", out oldInf);
                            MelonLogger.Msg($"[Reimagined] evehit: evnNum={(haveEvnNum ? evnNum.ToString() : "?")} idx={(haveIdx ? idx.ToString() : "?")}/{(haveIdxN ? idxN.ToString() : "?")}/{(haveIdxT ? idxT.ToString() : "?")} oldDoor={(haveOldDoor ? oldDoor.ToString() : "?")} oldInf={(haveOldInf ? oldInf.ToString() : "?")}");
        var fldPlayer = FindTypeInLoadedAssemblies("Il2Cpp.fldPlayer");
        int fMsk = 0, fBak = 0, fDir = 0, fCnt = 0;
        bool haveFMsk = fldPlayer != null && TryReadIntStatic(fldPlayer, "gPlayerMsk", out fMsk);
        bool haveFBak = fldPlayer != null && TryReadIntStatic(fldPlayer, "gPlayerMskBak", out fBak);
        bool haveFDir = fldPlayer != null && TryReadIntStatic(fldPlayer, "gKeyInputDir", out fDir);
        bool haveFCnt = fldPlayer != null && TryReadIntStatic(fldPlayer, "gKeyInputCnt", out fCnt);
        MelonLogger.Msg($"[Reimagined] fldPlayer: msk={(haveFMsk ? fMsk.ToString() : "?")} bak={(haveFBak ? fBak.ToString() : "?")} keyDir={(haveFDir ? fDir.ToString() : "?")} keyCnt={(haveFCnt ? fCnt.ToString() : "?")} (stopArg={s_fieldStopStopArg})");
                            MelonLogger.Msg($"[Reimagined] inputBlockResetAxes={(s_blockInputResetAxes ? "ON" : "OFF")}");
                            MelonLogger.Msg($"[Reimagined] sdfPadData[0]={(haveD0 ? $"0x{d0:X4}" : "?")}  sdfPadDataLast[0]={(haveDL0 ? $"0x{dl0:X4}" : "?")}");
                            MelonLogger.Msg($"[Reimagined] unity: anyKey={Input.anyKey} anyKeyDown={Input.anyKeyDown} mouseBtn0={Input.GetMouseButton(0)} mouseBtn1={Input.GetMouseButton(1)}");
                            // one-shot padmap summary + MesOK mapping to correlate keyboard keys (e.g. SPACE) to internal padmaps.
                            DumpCampProbeSnapshot(reason);
                            AppendUiFocusSnapshotLines(s => MelonLogger.Msg(s));
                            TryDumpPadmapStatesOnce("[Snapshot]");
                            MelonLogger.Msg("[Reimagined] -----------------------");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[Reimagined] Input snapshot failed: {ex.GetType().Name}: {ex.Message}");
                        }
                    }

                    private static bool TryGetSdfPadU16(string memberName, int index, out ushort value)
                    {
                        value = 0;
                        try
                        {
                            var padType = FindTypeInLoadedAssemblies("Il2Cpp.sdf_PadManager");
                            if (padType == null)
                                return false;

                            object? arr = null;
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                            var prop = padType.GetProperty(memberName, FLAGS);
                            if (prop != null)
                                arr = prop.GetValue(null, null);
                            else
                            {
                                var fi = padType.GetField(memberName, FLAGS);
                                if (fi != null)
                                    arr = fi.GetValue(null);
                            }

                            if (arr == null)
                                return false;

                            return TryGetIl2CppStructArrayU16Item(arr, index, out value);
                        }
                        catch
                        {
                            return false;
                        }
                    }



                                

        private static void PumpSequenceTrace()
        {
            try
            {
                if (!s_seqTraceEnabled)
                    return;

                var seqType = FindTypeInLoadedAssemblies("Il2Cpp.dds3SequenceList");
                if (seqType == null)
                    return;

                int seqField = -999, seqEvent = -999, seqCamp = -999, seqTerminal = -999, seqDebug = -999;
                bool okAny = false;
                if (TryCallIntStaticMethod(seqType, "CheckField", out seqField)) okAny = true;
                if (TryCallIntStaticMethod(seqType, "CheckEvent", out seqEvent)) okAny = true;
                if (TryCallIntStaticMethod(seqType, "CheckCamp", out seqCamp)) okAny = true;
                if (TryCallIntStaticMethod(seqType, "CheckTerminal", out seqTerminal)) okAny = true;
                if (TryCallIntStaticMethod(seqType, "CheckDebug", out seqDebug)) okAny = true;

                if (!okAny)
                    return;
                bool seqChanged = !(seqField == s_seqTraceLastField && seqEvent == s_seqTraceLastEvent && seqCamp == s_seqTraceLastCamp &&
                    seqTerminal == s_seqTraceLastTerminal && seqDebug == s_seqTraceLastDebug);

                // Also track deeper Command Menu (camp) state, even if the coarse sequence gates do not change.
                string campExtra = TryGetCampProbeTraceSuffix(false);
                bool campChanged = campExtra.Length > 0;

                if (!seqChanged && !campChanged)
                    return;

                if (seqChanged)
                {
                    s_seqTraceLastField = seqField;
                    s_seqTraceLastEvent = seqEvent;
                    s_seqTraceLastCamp = seqCamp;
                    s_seqTraceLastTerminal = seqTerminal;
                    s_seqTraceLastDebug = seqDebug;
                }

                var kernel = FindTypeInLoadedAssemblies("Il2Cpp.dds3KernelMain");
                int uidispFlag = 0, uidispCount = 0, captureSign = 0, captureCount = 0;
                Vector3 uidispGuide = default;
                int uidispBackLen = 0, uidispBackTrue = 0;
                string uidispBackBits = "";
                bool haveUIDispFlag = kernel != null && TryReadIntStatic(kernel, "UIDispFlag", out uidispFlag);
                bool haveUIDispGuide = kernel != null && TryReadVector3Static(kernel, "UIDispGuide", out uidispGuide);
                bool haveUIDispBack = kernel != null && TryReadBoolArrayBitsStatic(kernel, "UIDispBackActive", 8, out uidispBackLen, out uidispBackBits, out uidispBackTrue);
                bool haveUIDispCount = kernel != null && TryReadIntStatic(kernel, "UIDispCount", out uidispCount);
                bool haveCapSign = kernel != null && TryReadIntStatic(kernel, "dds3CaptureSign", out captureSign);
                bool haveCapCount = kernel != null && TryReadIntStatic(kernel, "dds3CaptureCount", out captureCount);

                var fldEveHit = FindTypeInLoadedAssemblies("Il2Cpp.fldEveHit");
                int evnNum = -999, idx2 = -999;
                bool haveEvn = fldEveHit != null && TryReadIntStatic(fldEveHit, "gEvnRngNum", out evnNum);
                bool haveIdx = fldEveHit != null && TryReadIntStatic(fldEveHit, "gFldEveRngIdx", out idx2);

                MelonLogger.Msg($"[Reimagined] seqTrace: field={seqField} event={seqEvent} camp={seqCamp} terminal={seqTerminal} debug={seqDebug}  uidispFlag={(haveUIDispFlag ? uidispFlag.ToString() : "?")} guide={(haveUIDispGuide ? $"{uidispGuide.x:F2},{uidispGuide.y:F2},{uidispGuide.z:F2}" : "?")} back={(haveUIDispBack ? $"{uidispBackLen}[{uidispBackBits}]true={uidispBackTrue}" : "?")} count={(haveUIDispCount ? uidispCount.ToString() : "?")}  capture={(haveCapSign ? captureSign.ToString() : "?")}/{(haveCapCount ? captureCount.ToString() : "?")}  evehit={(haveEvn ? evnNum.ToString() : "?")}/{(haveIdx ? idx2.ToString() : "?")}  menuVisible={s_lastMenuVisible} drawVisible={s_lastDrawVisible} streak={s_menuVisibleStreak}{campExtra}");
            }
            catch
            {
                // ignore
            }
        }

public static void PumpMenuPause()
                    {
                        try
                        {
                            // optional trace of sequence-mode transitions (vanilla menu routing).
                            PumpSequenceTrace();

                        if (s_immediateHardBlockFrames > 0)
                        {
                            s_immediateHardBlockFrames--;
                            if (s_immediateHardBlockFrames <= 0)
                                s_immediateHardBlockReason = null;
                        }

							// Two independent probes:
                            //  (1) Scene GameObject probe for actual UI presence (good for "is the menu drawn?").
                            //  (2) cmpTest process probe for "is the debug menu process alive?" (good for transitions/diagnostics).
                            var cmpTest = FindTypeInLoadedAssemblies("Il2Cpp.cmpTest");

                            bool goVisible = IsDebugMenuVisibleBySceneGameObject(out _);
                            bool processActive = cmpTest != null && IsDebugMenuLikelyVisible(cmpTest, out _);
                            bool drawVisible = cmpTest != null && IsDebugMenuVisibleByDrawFlags(cmpTest, out _);

                            // Some scenes keep the debug menu GameObject alive while closing/terminating.
                            // Treat terminate as "not visible" so our other systems don't false-positive.
                            sbyte term = 0;
                            bool haveTerm = cmpTest != null && TryReadSByteStatic(cmpTest, "gTerminateFlag", out term);

                            bool goVisibleStable = goVisible && (!haveTerm || term == 0);
                            bool processActiveStable = processActive && (!haveTerm || term == 0);

                            // For other systems (ResetAxes input-block, snapshots, etc.) we want a pragmatic
                            // "menu is open" signal. Prefer the UI probe; fall back to the process probe.
                            s_lastMenuVisible = goVisibleStable || processActiveStable;

                            // vA13: optional padmap trace + optional Mes OK remap while capture is active.
                            // s_lastMenuVisible is our *stable* menu-visible heuristic (go + draw/process streak).
                            PumpPadmapTrace(s_lastMenuVisible);
                            PumpMesOkRemap(s_lastMenuVisible);


                            // For anything that needs "draw-latched" behavior, accept either the UI probe
                            // or the legacy draw flags (some scenes/builds may populate them).
                            s_lastDrawVisible = goVisibleStable || drawVisible;

                            if (s_lastDrawVisible)
                                s_menuVisibleStreak = Math.Min(s_menuVisibleStreak + 1, 1000000);
                            else
                                s_menuVisibleStreak = 0;

                            // If the menu isn't open, ensure we don't hold any capture we "own".
                            if (!s_lastMenuVisible)
                            {
                                if (s_pauseOwned)
                                    ReleaseMenuPause("menu_hidden");
                                if (s_fieldStopOwned)
                                    ReleaseFieldPlayerStop("menu_hidden");
                                if (s_unitPadOwned)
                                    ReleaseFieldUnitPadRelease("menu_hidden");
                                if (s_cmdPadDisableOwned)
                                    ReleaseCmdPadDisable("menu_hidden");
                                if (s_mesOkSwitchOwned)
                                    ReleaseMesOkSwitch("menu_hidden");
                                return;
                            }

                            // Capture can be toggled; default is ON so the debug menu behaves like vanilla overlay menus.
                            if (!s_pauseWhileMenuOpen)
                            {
                                if (s_pauseOwned)
                                    ReleaseMenuPause("capture_off");
                                if (s_fieldStopOwned)
                                    ReleaseFieldPlayerStop("capture_off");
                                if (s_unitPadOwned)
                                    ReleaseFieldUnitPadRelease("capture_off");
                                if (s_cmdPadDisableOwned)
                                    ReleaseCmdPadDisable("capture_off");
                                if (s_mesOkSwitchOwned)
                                    ReleaseMesOkSwitch("capture_off");
                                return;
                            }

                            // We wait a few frames so the menu has time to finish initializing before we engage capture.
                            const int MIN_VISIBLE_FRAMES = 5;
                            bool stable = s_lastDrawVisible && s_menuVisibleStreak >= MIN_VISIBLE_FRAMES;

                            if (stable)
                            {
                                // best-effort hard gate for itfMesManager OK processing while debug menu is visible.
                                PumpMesOkSwitch(menuVisibleStable: stable);

                                if (s_pauseCaptureStrategy == PauseCaptureStrategy.KernelPause)
                                {
                                    if (s_cmdPadDisableOwned)
                                        ReleaseCmdPadDisable("strategy_kernel_pause");
                                    // Kernel pause is EXPERIMENTAL: it can interfere with debug-menu input in some scenes.
                                    if (!s_pauseOwned)
                                        AcquireMenuPauseBestEffort("menu_visible_stable");

                                    // Ensure other strategies aren't left latched.
                                    if (s_fieldStopOwned)
                                        ReleaseFieldPlayerStop("strategy_switch_cleanup");
                                    if (s_unitPadOwned)
                                        ReleaseFieldUnitPadRelease("strategy_switch_cleanup");
                                }
                                else if (s_pauseCaptureStrategy == PauseCaptureStrategy.FieldUnitPadRelease)
                                {
                                    // Field-unit pad release: mirrors how the camp/command menu prevents field movement
                                    // while leaving the world simulation running.
                                    AcquireFieldUnitPadReleaseBestEffort();

                                    // Optional: command-menu style pad disable to suppress non-movement field interactions (e.g., Space/NPC talk)
                                    // while keeping simulation running and debug-menu navigation intact.
                                    PumpCmdPadDisable(stable);

                                    // Ensure other strategies aren't left latched.
                                    if (s_pauseOwned)
                                        ReleaseMenuPause("strategy_switch_cleanup");

                                    // Optional overlay: even in FieldUnitPadRelease, we can also engage fldPlayerEventStop for stronger suppression
                                    // (useful for blocking NPC talk / dialogue selection bleed-through from Space/WASD).
                                    if (s_unitPadAlsoEngageFieldPlayerStop)
                                        EnsureFieldPlayerStopSilent();

                                    // Optional overlay: clear fldPlayer's immediate key accumulators (separate toggle).
                                    EnsureFieldPlayerInputScrubSilent();

                                    if (!s_unitPadAlsoEngageFieldPlayerStop && s_fieldStopOwned)
                                        ReleaseFieldPlayerStop("strategy_switch_cleanup");
                                }
                                else
                                {
                                    if (s_cmdPadDisableOwned)
                                        ReleaseCmdPadDisable("strategy_field_player_stop");

                                    // Field-player stop: best-effort freeze of field gameplay control while leaving debug menu navigation intact.
                                    if (!s_fieldStopOwned)
                                        AcquireFieldPlayerStopBestEffort("menu_visible_stable");
                                    ApplyFieldInputSuppressionTick();

                                    // Ensure other strategies aren't left latched.
                                    if (s_pauseOwned)
                                        ReleaseMenuPause("strategy_switch_cleanup");
                                    if (s_unitPadOwned)
                                        ReleaseFieldUnitPadRelease("strategy_switch_cleanup");
                                }
                            }
                            else
                            {
                                // Not stable yet; release anything we own so we don't fight menu init/teardown.
                                if (s_pauseOwned)
                                    ReleaseMenuPause("menu_not_stable_yet");
                                if (s_fieldStopOwned)
                                    ReleaseFieldPlayerStop("menu_not_stable_yet");
                                if (s_unitPadOwned)
                                    ReleaseFieldUnitPadRelease("menu_not_stable_yet");
                                if (s_cmdPadDisableOwned)
                                    ReleaseCmdPadDisable("menu_not_stable_yet");
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }

        public static void PumpPadProbe()
                    {
                        try
                        {
                            if (!s_padProbeEnabled)
                                return;

                            int fc = GetFrameCountSafe();
                            if (fc - s_padProbeLastFrame > 600)
                            {
                                // Reprint a short header occasionally so logs stay readable.
                                MelonLogger.Msg("[Reimagined] Pad probe: active. Press keys/buttons; watch for keydowns + sdfPadData[0] changes.");
                                s_padProbeLastData0 = 0xFFFF;
                            }
                            s_padProbeLastFrame = fc;

                            // Always log keydowns (helps confirm the probe is alive even if sdfPadData doesn't change for keyboard).
                            if (Input.anyKeyDown)
                            {
                                var keys = new List<string>(12);
                                if (Input.GetKeyDown(KeyCode.W)) keys.Add("W");
                                if (Input.GetKeyDown(KeyCode.A)) keys.Add("A");
                                if (Input.GetKeyDown(KeyCode.S)) keys.Add("S");
                                if (Input.GetKeyDown(KeyCode.D)) keys.Add("D");
                                if (Input.GetKeyDown(KeyCode.Space)) keys.Add("Space");
                                if (Input.GetKeyDown(KeyCode.Return)) keys.Add("Enter");
                                if (Input.GetKeyDown(KeyCode.Escape)) keys.Add("Esc");
                                if (Input.GetKeyDown(KeyCode.E)) keys.Add("E");
                                if (Input.GetKeyDown(KeyCode.Q)) keys.Add("Q");
                                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) keys.Add("Shift");
                                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) keys.Add("Ctrl");
                                if (Input.GetKeyDown(KeyCode.Tab)) keys.Add("Tab");

                                if (keys.Count > 0)
                                    MelonLogger.Msg($"[Reimagined] Pad probe: keyDown={string.Join(",", keys)}");
                                else
                                    MelonLogger.Msg("[Reimagined] Pad probe: (some key down)");
                            }


                            // Also probe Atlus pad mapping via dds3PadManager checks. This is *very* useful for keyboard
                            // because the game's internal input layer often maps keys -> SDF_PADMAP even when sdfPadData[0]
                            // doesn't reflect it directly.
                            try
                            {
                                bool goVisibleNow = IsDebugMenuVisibleBySceneGameObject(out string goDiagNow);

                                var tPadMgr = FindTypeInLoadedAssemblies("Il2Cpp.dds3PadManager");
                                var tPadMap = FindTypeInLoadedAssemblies("Il2Cpplibsdf_H.SDF_PADMAP");
                                if (tPadMgr != null && tPadMap != null && tPadMap.IsEnum)
                                {
                                    var miTrig2 = FindStaticMethod(tPadMgr, "DDS3_PADCHECK_TRIG2", 2);
                                    var miPress2 = FindStaticMethod(tPadMgr, "DDS3_PADCHECK_PRESS2", 2);

                                    if (miTrig2 != null)
                                    {
                                        var trig = new List<string>();
                                        for (int i = 0; i <= 15; i++)
                                        {
                                            object e = Enum.ToObject(tPadMap, i);
                                            object? ret = miTrig2.Invoke(null, new object[] { e, 0 });
                                            if (ret is bool b && b)
                                                trig.Add(Enum.GetName(tPadMap, e) ?? i.ToString());
                                        }

                                        if (trig.Count > 0)
                                            MelonLogger.Msg($"[Reimagined] Pad probe: TRIG2={string.Join(",", trig)} (dbgMenu={goVisibleNow} {goDiagNow})");
                                    }

                                    if (miPress2 != null)
                                    {
                                        var press = new List<string>();
                                        for (int i = 0; i <= 15; i++)
                                        {
                                            object e = Enum.ToObject(tPadMap, i);
                                            object? ret = miPress2.Invoke(null, new object[] { e, 0 });
                                            if (ret is bool b && b)
                                                press.Add(Enum.GetName(tPadMap, e) ?? i.ToString());
                                        }

                                        if (press.Count > 0)
                                            MelonLogger.Msg($"[Reimagined] Pad probe: PRESS2={string.Join(",", press)} (dbgMenu={goVisibleNow} {goDiagNow})");
                                    }
                                }
                            }
                            catch
                            {
                                // ignore
                            }

                            // Then try Atlus pad bitmask 
                            if (!TryGetSdfPadU16("sdfPadData", 0, out ushort v0))
                                return;

                            if (v0 != s_padProbeLastData0)
                            {
                                ushort prev = s_padProbeLastData0;
                                ushort delta = (prev == 0xFFFF) ? v0 : (ushort)(prev ^ v0);
                                MelonLogger.Msg($"[Reimagined] Pad probe: sdfPadData[0]=0x{v0:X4} delta=0x{delta:X4}");
                                s_padProbeLastData0 = v0;
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }



                    private static bool IsDebugMenuVisibleBySceneGameObject(out string diag)
                    {
                        diag = "";
                        try
                        {
                            float now = Time.unscaledTime;
                            if (now < s_nextDbgMenuGoScanUnscaled)
                            {
                                diag = $"cached path={(s_dbgMenuGoPath ?? "null")} visible={s_dbgMenuGoVisible}";
                                return s_dbgMenuGoVisible;
                            }

                            s_nextDbgMenuGoScanUnscaled = now + 0.25f;
                            s_dbgMenuGoVisible = false;
                            s_dbgMenuGoPath = null;

                            // Field debug menu (overworld): Canvas_UI/FldDbgMenu(Clone)
                            // Other scenes may use different roots; we keep a small, safe candidate list.
                            string[] paths = new[]
                            {
                                "Canvas_UI/FldDbgMenu(Clone)",
                                "Canvas_UI/BtlDbgMenu(Clone)",
                                "Canvas_UI/FldDbgMenu",
                                "Canvas_UI/BtlDbgMenu",
                                "FldDbgMenu(Clone)",
                                "BtlDbgMenu(Clone)",
                                "Main Canvas/DebugMenu",
                            };

                            foreach (var p in paths)
                            {
                                var go = GameObject.Find(p);
                                if (go == null)
                                    continue;

                                // GameObject.Find generally only returns active objects, but be defensive anyway.
                                if (!go.activeInHierarchy)
                                    continue;

                                s_dbgMenuGoVisible = true;
                                s_dbgMenuGoPath = p;
                                break;
                            }

                            diag = $"path={(s_dbgMenuGoPath ?? "null")} visible={s_dbgMenuGoVisible}";
                            return s_dbgMenuGoVisible;
                        }
                        catch (Exception ex)
                        {
                            diag = $"ex={ex.GetType().Name}";
                            return false;
                        }
                    }

        private static bool IsDebugMenuVisibleByDrawFlags(Type cmpTest, out string diag)
                    {
                        sbyte a1 = 0, a2 = 0;
                        bool haveA1 = TryReadSByteStatic(cmpTest, "gActive1", out a1);
                        bool haveA2 = TryReadSByteStatic(cmpTest, "gActive2", out a2);

                        diag = $"gActive1={(haveA1 ? a1.ToString() : "?")} gActive2={(haveA2 ? a2.ToString() : "?")}";
                        return (haveA1 && a1 != 0) || (haveA2 && a2 != 0);
                    }

                    private static bool IsDebugMenuVisibleByDrawFlags(Type cmpTest)
                    {
                        return IsDebugMenuVisibleByDrawFlags(cmpTest, out _);
                    }

                    private static void AcquireMenuPauseBestEffort(string? reason)
                    {
                        if (!string.IsNullOrEmpty(reason))
                        {
                            MelonLogger.Msg($"[Reimagined] AcquireMenuPauseBestEffort reason={reason}");
                        }
                        AcquireMenuPauseBestEffort();
                    }

                    private static void AcquireMenuPauseBestEffort()
                    {
                        try
                        {
                            var kernel = FindTypeInLoadedAssemblies("Il2Cpp.dds3KernelMain");
                            if (kernel == null)
                                return;

                            // Don't steal ownership if the game is already paused for some other reason.
                            if (TryReadIntStatic(kernel, "dds3PauseSign", out int sign) && sign != 0)
                                return;

                            TryInvokeStatic(kernel, "dds3PauseOn", Array.Empty<object>());
                            s_pauseOwned = true;
                            MelonLogger.Msg("[Reimagined] Game debug menu: kernel-pause ON (owned).");
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    private static void ReleaseMenuPause(string reason)
                    {
                        try
                        {
                            if (!s_pauseOwned)
                                return;

                            var kernel = FindTypeInLoadedAssemblies("Il2Cpp.dds3KernelMain");
                            if (kernel != null)
                                TryInvokeStatic(kernel, "dds3PauseOff", Array.Empty<object>());

                            s_pauseOwned = false;
                            MelonLogger.Msg($"[Reimagined] Game debug menu: kernel-pause OFF (owned release: {reason}).");
                        }
                        catch
                        {
                            s_pauseOwned = false;
                        }
                    }



                    private static void AcquireFieldUnitPadReleaseBestEffort(string? reason)
                    {
                        if (!string.IsNullOrEmpty(reason))
                            MelonLogger.Msg($"[Reimagined] AcquireFieldUnitPadReleaseBestEffort reason={reason}");
                        AcquireFieldUnitPadReleaseBestEffort();
                    }

                                private static Type? s_fldMainTypeCached;
                    private static MethodInfo? s_fldReleaseUnitPadMiCached;
                    private static MethodInfo? s_fldRegistUnitPadMiCached;
                    private static int s_unitPadLastApplyFrame = -100000;

                    private static void AcquireFieldUnitPadReleaseBestEffort()
                    {
                        try
                        {
                            bool wasOwned = s_unitPadOwned;

                            if (s_fldMainTypeCached == null)
                                s_fldMainTypeCached = FindTypeInLoadedAssemblies("Il2Cpp.fldMain") ?? FindTypeInLoadedAssemblies("fldMain");

                            var tFldMain = s_fldMainTypeCached;
                            if (tFldMain == null)
                            {
                                s_unitPadOwned = false;
                                if (!wasOwned)
                                    MelonLogger.Warning("[Reimagined] FieldUnitPadRelease: type not found (Il2Cpp.fldMain).");
                                return;
                            }

                            if (s_fldReleaseUnitPadMiCached == null)
                                s_fldReleaseUnitPadMiCached = tFldMain.GetMethod("fldReleaseUnitPad", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                            var mi = s_fldReleaseUnitPadMiCached;
                            if (mi == null)
                            {
                                s_unitPadOwned = false;
                                if (!wasOwned)
                                    MelonLogger.Warning("[Reimagined] FieldUnitPadRelease: fldReleaseUnitPad method not found/invokable.");
                                return;
                            }

                            int f = GetFrameCountSafe();
                            if (s_unitPadLastApplyFrame != f)
                            {
                                mi.Invoke(null, Array.Empty<object?>());
                                s_unitPadLastApplyFrame = f;
                            }

                            s_unitPadOwned = true;
                            if (!wasOwned)
                                MelonLogger.Msg("[Reimagined] Game debug menu: FieldUnitPadRelease engaged (fldReleaseUnitPad invoked).");
                        }
                        catch (Exception ex)
                        {
                            s_unitPadOwned = false;
                            MelonLogger.Warning($"[Reimagined] FieldUnitPadRelease: failed to invoke fldReleaseUnitPad: {ex.GetType().Name}: {ex.Message}");
                        }
                    }


                                private static void ReleaseFieldUnitPadRelease(string? reason)
                    {
                        try
                        {
                            var tFldMain = s_fldMainTypeCached ?? FindTypeInLoadedAssemblies("Il2Cpp.fldMain") ?? FindTypeInLoadedAssemblies("fldMain");
                            if (tFldMain == null)
                            {
                                MelonLogger.Warning("[Reimagined] FieldUnitPadRelease: type not found (Il2Cpp.fldMain) during release.");
                                return;
                            }

                            if (s_fldRegistUnitPadMiCached == null)
                                s_fldRegistUnitPadMiCached = tFldMain.GetMethod("fldRegistUnitPad", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                            var mi = s_fldRegistUnitPadMiCached;
                            if (mi != null)
                            {
                                mi.Invoke(null, Array.Empty<object?>());
                                MelonLogger.Msg($"[Reimagined] Game debug menu: FieldUnitPadRelease released (fldRegistUnitPad invoked) reason={reason ?? "?"}.");
                            }
                            else
                            {
                                MelonLogger.Warning("[Reimagined] FieldUnitPadRelease: fldRegistUnitPad method not found/invokable.");
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[Reimagined] FieldUnitPadRelease: failed to invoke fldRegistUnitPad: {ex.GetType().Name}: {ex.Message}");
                        }
                        finally
                        {
                            s_unitPadOwned = false;
                            s_unitPadLastApplyFrame = -100000;
                        }
                    }


                                private static Type? s_cmdPadTypeCached;
                    private static MethodInfo? s_cmdPadDisableMiCached;
                    private static MethodInfo? s_cmdPadEnableMiCached;
                    private static int s_cmdPadDisableLastApplyFrame = -100000;

                    // Backoff + refresh to avoid log spam and brittle reflection during fade/scene transitions.
                    private const int CMD_PAD_DISABLE_REFRESH_FRAMES = 6; // apply at most every N frames while visible
                    private static int s_cmdPadDisableFailureStreak = 0;
                    private static int s_cmdPadDisableNextAttemptFrame = -100000;
                    private static int s_cmdPadDisableLastErrorLogFrame = -100000;


                    private static void AcquireCmdPadDisableBestEffort(string? reason)
                    {
                        // Enforce PAD_DISABLE while capture is active. Some transitions can re-enable it.
                        int f = GetFrameCountSafe();

                        // Backoff while we are in a known-bad window (commonly during fades / scene transitions).
                        if (f < s_cmdPadDisableNextAttemptFrame)
                            return;

                        try
                        {
                            bool wasOwned = s_cmdPadDisableOwned;

                            if (s_cmdPadTypeCached == null)
                                s_cmdPadTypeCached = FindTypeInLoadedAssemblies("Il2Cpp.fldCommand") ?? FindTypeInLoadedAssemblies("fldCommand");

                            var tCmd = s_cmdPadTypeCached;
                            if (tCmd == null)
                            {
                                s_cmdPadDisableOwned = false;
                                if (!wasOwned)
                                    MelonLogger.Warning("[Reimagined] Cmd PAD_DISABLE: type not found (Il2Cpp.fldCommand).");
                                return;
                            }

                            if (s_cmdPadDisableMiCached == null)
                                s_cmdPadDisableMiCached = FindStaticMethod(tCmd, "fldCommand_PAD_DISABLE", 0);

                            var mi = s_cmdPadDisableMiCached;
                            if (mi == null)
                            {
                                s_cmdPadDisableOwned = false;
                                if (!wasOwned)
                                    MelonLogger.Warning("[Reimagined] Cmd PAD_DISABLE: method fldCommand_PAD_DISABLE not found.");
                                return;
                            }

                            // Refresh at most every N frames to reduce overhead and avoid brittle reflection spam.
                            if (s_cmdPadDisableLastApplyFrame > -100000 && (f - s_cmdPadDisableLastApplyFrame) < CMD_PAD_DISABLE_REFRESH_FRAMES)
                            {
                                s_cmdPadDisableOwned = true;
                                return;
                            }

                            object? retObj = mi.Invoke(null, Array.Empty<object?>());

                            // Keep ret only for first log (it's noisy otherwise).
                            if (!wasOwned)
                            {
                                int ret = (retObj is int ri) ? ri : 0;
                                MelonLogger.Msg($"[Reimagined] Game debug menu: fldCommand_PAD_DISABLE engaged (ret={ret}).");
                            }

                            s_cmdPadDisableLastApplyFrame = f;
                            s_cmdPadDisableOwned = true;

                            // Success resets backoff.
                            s_cmdPadDisableFailureStreak = 0;
                            s_cmdPadDisableNextAttemptFrame = -100000;
                        }
                        catch (Exception ex)
                        {
                            // Don't spam: backoff + rate-limit warnings. Keep previous owned state (the disable may still be active).
                            s_cmdPadDisableFailureStreak = Math.Min(1000, s_cmdPadDisableFailureStreak + 1);

                            int exp = Math.Min(s_cmdPadDisableFailureStreak, 4);
                            int backoff = 10 * (1 << exp); // 10,20,40,80,160...
                            backoff = Math.Min(240, backoff);

                            s_cmdPadDisableNextAttemptFrame = f + backoff;

                            if ((f - s_cmdPadDisableLastErrorLogFrame) >= 60)
                            {
                                s_cmdPadDisableLastErrorLogFrame = f;
                                MelonLogger.Warning($"[Reimagined] Cmd PAD_DISABLE failed (backoff {backoff}f): {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }

private static void ReleaseCmdPadDisable(string reason)
                    {
                        try
                        {
                            if (!s_cmdPadDisableOwned)
                                return;

                            var tCmd = s_cmdPadTypeCached ?? FindTypeInLoadedAssemblies("Il2Cpp.fldCommand") ?? FindTypeInLoadedAssemblies("fldCommand");
                            if (tCmd == null)
                            {
                                s_cmdPadDisableOwned = false;
                                MelonLogger.Warning("[Reimagined] Cmd PAD_ENABLE: type not found (Il2Cpp.fldCommand).");
                                return;
                            }

                            if (s_cmdPadEnableMiCached == null)
                                s_cmdPadEnableMiCached = tCmd.GetMethod("fldCommand_PAD_ENABLE", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                            var mi = s_cmdPadEnableMiCached;
                            if (mi != null)
                            {
                                mi.Invoke(null, Array.Empty<object?>());
                                MelonLogger.Msg($"[Reimagined] Game debug menu: fldCommand_PAD_ENABLE (owned release: {reason}).");
                            }
                            else
                            {
                                MelonLogger.Warning("[Reimagined] Cmd PAD_ENABLE: method fldCommand_PAD_ENABLE not found/invokable.");
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[Reimagined] Cmd PAD_ENABLE failed: {ex.GetType().Name}: {ex.Message}");
                        }
                        finally
                        {
                            s_cmdPadDisableOwned = false;
                            s_cmdPadDisableLastApplyFrame = -100000;
                        }
                    }



                    private static void AcquireFieldPlayerStopBestEffort(string? reason)
        {
            if (!string.IsNullOrEmpty(reason))
                MelonLogger.Msg($"[Reimagined] AcquireFieldPlayerStopBestEffort reason={reason}");
            AcquireFieldPlayerStopBestEffort();
        }

        private static void AcquireFieldPlayerStopBestEffort()
        {
            try
            {
                var fldPlayer = FindTypeInLoadedAssemblies("Il2Cpp.fldPlayer");
                if (fldPlayer == null)
                    return;

                // In SMT3HD, the wrapper signature is:
                //     public static void fldPlayerEventStop(bool sw = false)
                // The default is sw=false, which strongly suggests "false engages stop" at most callsites.
                // We treat s_fieldStopStopArg as the value to pass when ENGAGING the stop (default false),
                // and we resume with the inverted value.
                bool stopArg = s_fieldStopStopArg;
                bool resumeArg = !s_fieldStopStopArg;

                // Diagnostics (helps us prove whether we're actually touching the right runtime state).
                int preMsk = 0, preBak = 0, preDir = 0, preCnt = 0;
                bool havePreMsk = TryReadIntStatic(fldPlayer, "gPlayerMsk", out preMsk);
                bool havePreBak = TryReadIntStatic(fldPlayer, "gPlayerMskBak", out preBak);
                bool havePreDir = TryReadIntStatic(fldPlayer, "gKeyInputDir", out preDir);
                bool havePreCnt = TryReadIntStatic(fldPlayer, "gKeyInputCnt", out preCnt);

                bool invoked = TryInvokeStaticBool(fldPlayer, "fldPlayerEventStop", new object?[] { stopArg });

                int postMsk = 0, postBak = 0, postDir = 0, postCnt = 0;
                bool havePostMsk = TryReadIntStatic(fldPlayer, "gPlayerMsk", out postMsk);
                bool havePostBak = TryReadIntStatic(fldPlayer, "gPlayerMskBak", out postBak);
                bool havePostDir = TryReadIntStatic(fldPlayer, "gKeyInputDir", out postDir);
                bool havePostCnt = TryReadIntStatic(fldPlayer, "gKeyInputCnt", out postCnt);

                // Only claim ownership if we successfully invoked the method. If we can't invoke it,
                // we can still do per-frame key suppression (see ApplyFieldInputSuppressionTick), but we
                // must not "own" a stop we didn't actually engage.
                s_fieldStopOwned = invoked;

                MelonLogger.Msg($"[Reimagined] Game debug menu: field-player stop {(invoked ? "ON" : "FAILED")} (owned={s_fieldStopOwned}) stopArg={stopArg} resumeArg={resumeArg} " +
                                $"pre(msk={(havePreMsk ? preMsk.ToString() : "?")},bak={(havePreBak ? preBak.ToString() : "?")},dir={(havePreDir ? preDir.ToString() : "?")},cnt={(havePreCnt ? preCnt.ToString() : "?")}) " +
                                $"post(msk={(havePostMsk ? postMsk.ToString() : "?")},bak={(havePostBak ? postBak.ToString() : "?")},dir={(havePostDir ? postDir.ToString() : "?")},cnt={(havePostCnt ? postCnt.ToString() : "?")})");
            }
            catch
            {
                // Fail closed: don't claim ownership if we failed unexpectedly.
                s_fieldStopOwned = false;
            }
        }

        private static void ReleaseFieldPlayerStop(string reason)
        {
            try
            {
                if (!s_fieldStopOwned)
                    return;

                var fldPlayer = FindTypeInLoadedAssemblies("Il2Cpp.fldPlayer");
                if (fldPlayer == null)
                {
                    s_fieldStopOwned = false;
                    s_fieldStopLastApplyFrame = -1;
                    return;
                }

                bool resumeArg = !s_fieldStopStopArg;

                int preMsk = 0, preDir = 0, preCnt = 0;
                bool havePreMsk = TryReadIntStatic(fldPlayer, "gPlayerMsk", out preMsk);
                bool havePreDir = TryReadIntStatic(fldPlayer, "gKeyInputDir", out preDir);
                bool havePreCnt = TryReadIntStatic(fldPlayer, "gKeyInputCnt", out preCnt);

                bool invoked = TryInvokeStaticBool(fldPlayer, "fldPlayerEventStop", new object?[] { resumeArg });

                int postMsk = 0, postDir = 0, postCnt = 0;
                bool havePostMsk = TryReadIntStatic(fldPlayer, "gPlayerMsk", out postMsk);
                bool havePostDir = TryReadIntStatic(fldPlayer, "gKeyInputDir", out postDir);
                bool havePostCnt = TryReadIntStatic(fldPlayer, "gKeyInputCnt", out postCnt);

                s_fieldStopOwned = false;
                s_fieldStopLastApplyFrame = -1;

                MelonLogger.Msg($"[Reimagined] Game debug menu: field-player stop OFF (owned release: {reason}) invoked={(invoked ? "yes" : "no")} resumeArg={resumeArg} " +
                                $"pre(msk={(havePreMsk ? preMsk.ToString() : "?")},dir={(havePreDir ? preDir.ToString() : "?")},cnt={(havePreCnt ? preCnt.ToString() : "?")}) " +
                                $"post(msk={(havePostMsk ? postMsk.ToString() : "?")},dir={(havePostDir ? postDir.ToString() : "?")},cnt={(havePostCnt ? postCnt.ToString() : "?")})");
            }
            catch
            {
                s_fieldStopOwned = false;
                s_fieldStopLastApplyFrame = -1;
            }
        }
        // Extra "belt and suspenders": while the debug menu is visibly open AND we're using the FieldPlayerStop strategy,
        // we also aggressively zero the field-player input direction/counter each tick. This is intentionally narrow:
        // it should prevent movement/camera drift even if fldPlayerEventStop ends up being a no-op in some states.
        private static void ApplyFieldInputSuppressionTick()
        {
            try
            {
                var fldPlayer = FindTypeInLoadedAssemblies("Il2Cpp.fldPlayer");
                if (fldPlayer == null)
                    return;

                // These are public static properties on Il2Cpp.fldPlayer.
                // If they don't exist in this runtime for some reason, the TryWrite* helpers will just return false.
                TryWriteIntStatic(fldPlayer, "gKeyInputDir", 0);
                TryWriteIntStatic(fldPlayer, "gKeyInputCnt", 0);

                // Light-rate log (every ~300 frames) so we can confirm we're touching the values without spamming.
                int frame = GetFrameCountSafe();
                if (frame - s_lastFieldSuppressionLogFrame > 300)
                {
                    int msk = 0, dir = 0, cnt = 0;
                    bool haveMsk = TryReadIntStatic(fldPlayer, "gPlayerMsk", out msk);
                    bool haveDir = TryReadIntStatic(fldPlayer, "gKeyInputDir", out dir);
                    bool haveCnt = TryReadIntStatic(fldPlayer, "gKeyInputCnt", out cnt);

                    MelonLogger.Msg($"[Reimagined] FieldInputSuppressionTick: msk={(haveMsk ? msk.ToString() : "?")} dir={(haveDir ? dir.ToString() : "?")} cnt={(haveCnt ? cnt.ToString() : "?")} (stopArg={s_fieldStopStopArg} ownedFieldStop={s_fieldStopOwned})");
                    s_lastFieldSuppressionLogFrame = frame;
                }
            }
            catch
            {
                // best-effort
            }
        }

        // Extra-safety overlay: engage fldPlayerEventStop even while using FieldUnitPadRelease strategy.
        // This tends to block NPC talk and message selection bleed-through (Space/WASD) that aren't always covered by pad-release alone.
        // We keep this silent (log only on first engage) to avoid spamming the log.
        private static void EnsureFieldPlayerStopSilent()
        {
            try
            {
                // Only meaningful while capture is active and the debug menu is actually visible.
                if (!s_pauseWhileMenuOpen)
                    return;

                int f = GetFrameCountSafe();
                if (s_fieldStopLastApplyFrame == f)
                    return;
                s_fieldStopLastApplyFrame = f;

                Type? fldPlayer = FindTypeInLoadedAssemblies("Il2Cpp.fldPlayer") ?? FindTypeInLoadedAssemblies("fldPlayer");
                if (fldPlayer == null)
                    return;

                MethodInfo? mi = fldPlayer.GetMethod("fldPlayerEventStop", BindingFlags.Public | BindingFlags.Static);
                if (mi == null)
                    return;

                bool wasOwned = s_fieldStopOwned;
                mi.Invoke(null, new object[] { s_fieldStopStopArg });
                s_fieldStopOwned = true;

                if (!wasOwned)
                    MelonLogger.Msg($"[Reimagined] FieldPlayerStop overlay engaged (fldPlayerEventStop({s_fieldStopStopArg})) while using FieldUnitPadRelease.");
            }
            catch (Exception ex)
            {
                if (s_fieldStopOwned)
                    MelonLogger.Warning($"[Reimagined] FieldPlayerStop overlay failed: {ex.GetType().Name}: {ex.Message}");
                s_fieldStopOwned = false;
                s_fieldStopLastApplyFrame = -1;
            }
        }

// Best-effort: clear fldPlayer's immediate key accumulators so gameplay-side "confirm/interact" and
// menu selection movement don't also fire while navigating the game debug menu.
// This is intentionally silent (no per-frame log spam).
private static void EnsureFieldPlayerInputScrubSilent()
{
    try
    {
        if (!s_pauseWhileMenuOpen || !s_unitPadAlsoScrubFieldPlayerInput)
            return;

        int f = GetFrameCountSafe();
        if (s_fieldPlayerScrubLastApplyFrame == f)
            return;
        s_fieldPlayerScrubLastApplyFrame = f;

        Type? fldPlayer = FindTypeInLoadedAssemblies("Il2Cpp.fldPlayer") ?? FindTypeInLoadedAssemblies("fldPlayer");
        if (fldPlayer == null)
            return;

        // Clear immediate input accumulators. If these fields are not writable on a given build, TryWriteIntStatic will fail silently.
        TryWriteIntStatic(fldPlayer, "gKeyInputDir", 0);
        TryWriteIntStatic(fldPlayer, "gKeyInputCnt", 0);
    }
    catch
    {
        // Best-effort only.
    }
}





                    private static bool TryReadIntStatic(Type t, string name, out int value)
                    {
                        value = 0;
                        try
                        {
                            var m = FindStaticMember(t, name);
                            if (m == null)
                                return false;

                            var v = GetStaticMemberValue(m);
                            if (v is int i)
                            {
                                value = i;
                                return true;
                            }
                            if (v == null)
                                return false;

                            value = Convert.ToInt32(v);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }


        // kernel UI probes - these members are not ints on SMT3HD (UIDispGuide is Vector3, UIDispBackActive is a bool array).
        private static bool TryReadVector3Static(Type t, string name, out Vector3 value)
        {
            value = default;
            try
            {
                var m = FindStaticMember(t, name);
                if (m == null)
                    return false;

                var v = GetStaticMemberValue(m);
                if (v is Vector3 vv)
                {
                    value = vv;
                    return true;
                }

                if (v == null)
                    return false;

                // Fallback: reflect x/y/z fields/properties if this comes back as an Il2Cpp struct wrapper.
                var vt = v.GetType();
                float x = 0, y = 0, z = 0;
                bool ok = TryReadFloatMember(v, vt, "x", out x)
                       && TryReadFloatMember(v, vt, "y", out y)
                       && TryReadFloatMember(v, vt, "z", out z);
                if (!ok)
                    return false;

                value = new Vector3(x, y, z);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadFloatMember(object obj, Type t, string name, out float value)
        {
            value = 0f;
            try
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null)
                {
                    object? v = p.GetValue(obj, null);
                    if (v is float f) { value = f; return true; }
                    if (v != null) { value = Convert.ToSingle(v); return true; }
                }

                var f2 = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f2 != null)
                {
                    object? v = f2.GetValue(obj);
                    if (v is float f) { value = f; return true; }
                    if (v != null) { value = Convert.ToSingle(v); return true; }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetArrayLength(object arr, out int length)
        {
            length = 0;
            try
            {
                var t = arr.GetType();
                var p = t.GetProperty("Length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null)
                {
                    object? v = p.GetValue(arr, null);
                    if (v is int i) { length = i; return true; }
                    if (v != null) { length = Convert.ToInt32(v); return true; }
                }

                var m = t.GetMethod("get_Length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null)
                {
                    object? v = m.Invoke(arr, null);
                    if (v is int i) { length = i; return true; }
                    if (v != null) { length = Convert.ToInt32(v); return true; }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetIl2CppBoolArrayItem(object arr, int index, out bool value)
        {
            value = false;
            try
            {
                var t = arr.GetType();
                var getItem = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (getItem != null)
                {
                    object? v = getItem.Invoke(arr, new object[] { index });
                    if (v is bool b) { value = b; return true; }
                    if (v is byte bb) { value = bb != 0; return true; }
                    if (v is sbyte sb) { value = sb != 0; return true; }
                    if (v is int i) { value = i != 0; return true; }
                    if (v is short sh) { value = sh != 0; return true; }
                    if (v is ushort us) { value = us != 0; return true; }
                    return false;
                }

                // Fallback indexer.
                var idxer = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, new[] { typeof(int) }, null);
                if (idxer != null)
                {
                    object? v = idxer.GetValue(arr, new object[] { index });
                    if (v is bool b) { value = b; return true; }
                    if (v != null) { value = Convert.ToInt32(v) != 0; return true; }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadBoolArrayBitsStatic(Type t, string name, int sampleCount, out int length, out string bits, out int trueCount)
        {
            length = 0;
            bits = "";
            trueCount = 0;
            try
            {
                var m = FindStaticMember(t, name);
                if (m == null)
                    return false;

                object? arr = GetStaticMemberValue(m);
                if (arr == null)
                    return false;

                if (!TryGetArrayLength(arr, out length))
                    length = -1;

                int sample = sampleCount;
                if (length >= 0)
                    sample = Math.Min(sample, length);
                sample = Math.Max(sample, 0);

                if (sample == 0)
                {
                    bits = "";
                    trueCount = 0;
                    return true;
                }

                char[] buf = new char[sample];
                int trues = 0;
                for (int i = 0; i < sample; i++)
                {
                    bool b = false;
                    bool ok = TryGetIl2CppBoolArrayItem(arr, i, out b);
                    buf[i] = (ok && b) ? '1' : '0';
                    if (ok && b)
                        trues++;
                }

                bits = new string(buf);
                trueCount = trues;
                return true;
            }
            catch
            {
                return false;
            }
        }


        private static bool TryWriteIntStatic(Type t, string name, int value)
        {
            try
            {
                var m = FindStaticMember(t, name);
                if (m == null)
                    return false;
                SetStaticMemberValue(m, value);
                return true;
            }
            catch
            {
                return false;
            }
        }


                    private static bool TryGetIl2CppStructArrayU16Item(object arr, int index, out ushort value)
                    {
                        value = 0;
                        try
                        {
                            var t = arr.GetType();

                            var getItem = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                            if (getItem != null)
                            {
                                var res = getItem.Invoke(arr, new object[] { index });
                                if (res is ushort u)
                                {
                                    value = u;
                                    return true;
                                }
                                if (res != null)
                                {
                                    value = Convert.ToUInt16(res);
                                    return true;
                                }
                            }

                            // Some Il2CppStructArray versions expose an indexer property named "Item".
                            var idxProp = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (idxProp != null)
                            {
                                var res = idxProp.GetValue(arr, new object[] { index });
                                if (res is ushort u2)
                                {
                                    value = u2;
                                    return true;
                                }
                                if (res != null)
                                {
                                    value = Convert.ToUInt16(res);
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
                    private static object? BuildReimaginedSubList(Type listEntryType)
                    {
                        // Keep the in-game debug menu extremely safe. We avoid scene transitions and deep engine writes here.
                        // This submenu exists primarily to:
                        //  - expose our hotkey-driven diagnostics in a native UI shell
                        //  - provide a couple of low-risk toggles for debugging input routing
                        string[] helpLines =
                        {
                            "Hotkeys: F5 UI sig | F6 UI delta | F7 UI watch | F9 anchors",
                            "More:   F11 inventory | F12 controller state | F10 reflection surface",
                            "Vanilla RE: Ctrl+Alt+F11 dumps camp/command-menu reflection surface (use while (F) menu is open)",
                            "Terminal seam: F3 (use inside terminal scene)",
                            "Overlay fallback: Shift+F1 (on-screen notes)",
                            "Input-capture: Ctrl+F1 toggles (default ON). Ctrl+Alt+F1 cycles strategy.",
                            "Trace: Ctrl+Alt+F10 toggles sequence trace (mode transitions) for vanilla menu RE.",
                            "Tip: Use Tools -> Input snapshot to compare states vs the normal (F) command menu.",
                            "Exit: Back/Cancel (native)"
                        };

                        object MakeText(string s)
                        {
                            var e = Activator.CreateInstance(listEntryType);
                            if (e == null) throw new InvalidOperationException("list entry ctor returned null");
                            SetStringProp(e, "word", s);
                            SetIntProp(e, "Para", 0);
                            SetProp(e, "func", null);
                            SetProp(e, "nextList", null);
                            SetIntProp(e, "nextSize", 0);
                            return e;
                        }

                        object MakeFolder(string name, object nextList, int nextSize)
                        {
                            var e = Activator.CreateInstance(listEntryType);
                            if (e == null) throw new InvalidOperationException("list entry ctor returned null");
                            SetStringProp(e, "word", name);
                            SetIntProp(e, "Para", 0);
                            SetProp(e, "func", null);
                            SetProp(e, "nextList", nextList);
                            SetIntProp(e, "nextSize", nextSize);
                            return e;
                        }

                        object MakeLeaf(string name, Action<int, int, uint> action, out string note)
                        {
                            var e = Activator.CreateInstance(listEntryType);
                            if (e == null) throw new InvalidOperationException("list entry ctor returned null");

                            SetStringProp(e, "word", name);

                            // Empirically, leaf entries need a non-zero Para to be considered "actionable" by the native menu.
                            // (Para=0 appears to behave like informational text in some contexts.)
                            SetIntProp(e, "Para", 1);

                            var il2cppDel = CreateIl2CppFuncDelegate(listEntryType, action, out note);
                            if (il2cppDel != null)
                                SetProp(e, "func", il2cppDel);
                            else
                                SetStringProp(e, "word", $"{name} (unavailable: {note})");

                            SetProp(e, "nextList", null);
                            SetIntProp(e, "nextSize", 0);
                            return e;
                        }

                        // Help list
                        var helpList = CreateIl2CppRefArray(listEntryType, helpLines.Length);
                        for (int i = 0; i < helpLines.Length; i++)
                            SetIl2CppRefArrayItem(helpList, i, MakeText(helpLines[i]));

                        // Tools list 
                        // we intentionally keep these self-contained and read-only where possible.
                        const int TOOLS_COUNT = 7;
                        var toolsList = CreateIl2CppRefArray(listEntryType, TOOLS_COUNT);
                        int ti = 0;

                        string n0;
                        SetIl2CppRefArrayItem(toolsList, ti++, MakeLeaf("Pad probe: toggle (logs sdfPadData[0] + keydowns)", DbgTogglePadProbe, out n0));

                        string n1;
                        SetIl2CppRefArrayItem(toolsList, ti++, MakeLeaf("Input snapshot: dump once (pad + pause + dbg flags)", DbgDumpInputSnapshot, out n1));

                        string nSeq;
                        SetIl2CppRefArrayItem(toolsList, ti++, MakeLeaf("Sequence trace (logs mode transitions; Ctrl+Alt+F10): toggle", DbgToggleSeqTrace, out nSeq));

                        string n2;
                        SetIl2CppRefArrayItem(toolsList, ti++, MakeLeaf("Flush Unity input edges while debug menu open (ResetInputAxes; input-capture OFF): toggle", DbgToggleInputBlockResetAxes, out n2));

                        string n3;
                        SetIl2CppRefArrayItem(toolsList, ti++, MakeLeaf("Pause world while debug menu open (kernel pause): toggle", DbgToggleMenuPauseCapture, out n3));

                        string n4;
                        SetIl2CppRefArrayItem(toolsList, ti++, MakeLeaf("Ping: menu action test (prints a log line)", DbgPing, out n4));

                        SetIl2CppRefArrayItem(toolsList, ti++, MakeText("Note: keyboard may not affect sdfPadData; try controller for bit mapping."));

                        // Root list for Reimagined submenu
                        var root = CreateIl2CppRefArray(listEntryType, 2);
                        SetIl2CppRefArrayItem(root, 0, MakeFolder("Help / Hotkeys", helpList, helpLines.Length));
                        SetIl2CppRefArrayItem(root, 1, MakeFolder("Tools (safe)", toolsList, TOOLS_COUNT));
                        return root;
                    }

                    

// --- Il2CppInterop type resolution helpers (compile-safe) ---
private static Type? s_il2cppRefArrayOpenType;
private static Type? s_delegateSupportType;
private static MethodInfo? s_delegateSupportConvertMi;

private static Type? GetIl2CppReferenceArrayOpenType()
{
    if (s_il2cppRefArrayOpenType != null)
        return s_il2cppRefArrayOpenType;

    // Search loaded assemblies for an open generic named "Il2CppReferenceArray`1"
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        // Fast path: try common full name
        try
        {
            var t = asm.GetType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1", throwOnError: false);
            if (t != null && t.IsGenericTypeDefinition)
            {
                s_il2cppRefArrayOpenType = t;
                return t;
            }
        }
        catch { /* ignore */ }

        // Fallback: enumerate types
        try
        {
            var types = asm.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var cand = types[i];
                if (cand == null) continue;
                if (!cand.IsGenericTypeDefinition) continue;
                if (cand.Name == "Il2CppReferenceArray`1")
                {
                    s_il2cppRefArrayOpenType = cand;
                    return cand;
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            var types = ex.Types;
            if (types != null)
            {
                for (int i = 0; i < types.Length; i++)
                {
                    var cand = types[i];
                    if (cand == null) continue;
                    if (!cand.IsGenericTypeDefinition) continue;
                    if (cand.Name == "Il2CppReferenceArray`1")
                    {
                        s_il2cppRefArrayOpenType = cand;
                        return cand;
                    }
                }
            }
        }
        catch { /* ignore */ }
    }

    return null;
}

private static Type? GetDelegateSupportType()
{
    if (s_delegateSupportType != null)
        return s_delegateSupportType;

    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        // Fast path: try common full names first
        try
        {
            var t = asm.GetType("Il2CppInterop.Runtime.DelegateSupport", throwOnError: false);
            if (t != null)
            {
                s_delegateSupportType = t;
                return t;
            }
        }
        catch { /* ignore */ }

        // Fallback: enumerate types by short name
        try
        {
            var types = asm.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var cand = types[i];
                if (cand == null) continue;
                if (cand.Name == "DelegateSupport")
                {
                    s_delegateSupportType = cand;
                    return cand;
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            var types = ex.Types;
            if (types != null)
            {
                for (int i = 0; i < types.Length; i++)
                {
                    var cand = types[i];
                    if (cand == null) continue;
                    if (cand.Name == "DelegateSupport")
                    {
                        s_delegateSupportType = cand;
                        return cand;
                    }
                }
            }
        }
        catch { /* ignore */ }
    }

    return null;
}

private static MethodInfo? GetDelegateSupportConvertDelegateMethod()
{
    if (s_delegateSupportConvertMi != null)
        return s_delegateSupportConvertMi;

    var t = GetDelegateSupportType();
    if (t == null)
        return null;

    try
    {
        var mi = t.GetMethod("ConvertDelegate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        s_delegateSupportConvertMi = mi;
        return mi;
    }
    catch { return null; }
}

private static object CreateIl2CppRefArray(Type elementType, int length)
                    {
                        // Il2CppReferenceArray<T>
                        var open = GetIl2CppReferenceArrayOpenType();
                        if (open == null)
                            throw new InvalidOperationException("Il2CppReferenceArray<> type not found (Il2CppInterop).");
                        var arrType = open.MakeGenericType(elementType);
                        var inst = Activator.CreateInstance(arrType, new object?[] { length });
                        if (inst == null)
                            throw new InvalidOperationException("Il2CppReferenceArray ctor returned null.");
                        return inst;
                    }

                    private static int GetIl2CppRefArrayLength(object arr)
                    {
                        var t = arr.GetType();
                        var p = t.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(int))
                            return (int)(p.GetValue(arr) ?? 0);

                        // Some versions expose Count.
                        p = t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(int))
                            return (int)(p.GetValue(arr) ?? 0);

                        throw new InvalidOperationException("Il2CppReferenceArray missing Length/Count");
                    }

                    private static object? GetIl2CppRefArrayItem(object arr, int index)
                    {
                        var t = arr.GetType();
                        var p = t.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
                        if (p == null)
                            throw new InvalidOperationException("Il2CppReferenceArray missing indexer");
                        return p.GetValue(arr, new object?[] { index });
                    }

                    private static void SetIl2CppRefArrayItem(object arr, int index, object? value)
                    {
                        var t = arr.GetType();
                        var p = t.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
                        if (p == null)
                            throw new InvalidOperationException("Il2CppReferenceArray missing indexer");
                        p.SetValue(arr, value, new object?[] { index });
                    }

                    private static string? GetStringProp(object? obj, string prop)
                    {
                        if (obj == null) return null;
                        var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p == null) return null;
                        return p.GetValue(obj) as string;
                    }

                    private static void SetStringProp(object obj, string prop, string value)
                    {
                        var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null)
                            p.SetValue(obj, value);
                    }

                    private static void SetIntProp(object obj, string prop, int value)
                    {
                        var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null)
                        {
                            // Some props are Int32, some are UInt32 (Para is int).
                            if (p.PropertyType == typeof(int))
                                p.SetValue(obj, value);
                            else if (p.PropertyType == typeof(uint))
                                p.SetValue(obj, (uint)value);
                            else if (p.PropertyType == typeof(short))
                                p.SetValue(obj, (short)value);
                            else if (p.PropertyType == typeof(sbyte))
                                p.SetValue(obj, (sbyte)value);
                        }
                    }

                    private static void SetProp(object obj, string prop, object? value)
                    {
                        var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        p?.SetValue(obj, value);
                    }

                    private static object? GetProp(object obj, string prop)
                    {
                        var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        return p?.GetValue(obj);
                    }

                    private static bool TryDisableEntryForSafety(Type listEntryType, object entry, string reason, out string note)
                    {
                        note = "unknown";
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic;

                            var funcDelegateType = listEntryType.GetNestedType("funcDelegate", FLAGS);
                            if (funcDelegateType == null)
                            {
                                note = "funcDelegate type not found";
                                return false;
                            }

                            // Best-effort rename so it's obvious in-menu that this entry is intentionally disabled.
                            try
                            {
                                var label = reason.ToUpperInvariant() + " (disabled)";
                                SetProp(entry, "word", label);
                            }
                            catch
                            {
                                // ignore
                            }

                            Action<int, int, uint> wrapper = (a, b, c) =>
                            {
                                // We intentionally do NOT call the original action here.
                                // Some built-in debug actions (notably stock) can leave the game in a broken state depending on context.
                                MelonLogger.Msg($"[Reimagined] Debug menu action '{reason}' is disabled (safety).");
                                RequestDeferredClose(reason);
                            };

                            // Convert managed delegate to an Il2Cpp-compatible delegate.
                            var convert = GetDelegateSupportConvertDelegateMethod();
                            if (convert == null)
                            {
                                note = "DelegateSupport.ConvertDelegate not found";
                                return false;
                            }

                            var gm = convert.MakeGenericMethod(funcDelegateType);
                            var il2cppDel = gm.Invoke(null, new object?[] { wrapper });
                            if (il2cppDel == null)
                            {
                                note = "ConvertDelegate returned null";
                                return false;
                            }

                            SetProp(entry, "func", il2cppDel);
                            note = "disabled";
                            return true;
                        }
                        catch (Exception ex)
                        {
                            note = $"{ex.GetType().Name}: {ex.Message}";
                            return false;
                        }
                    }

                    private static bool TryDisableEntrySilent(object entry, string newLabel, out string note)
                    {
                        note = "unknown";
                        try
                        {
                            SetStringProp(entry, "word", newLabel);
                            SetProp(entry, "func", null);
                            SetProp(entry, "nextList", null);
                            SetIntProp(entry, "nextSize", 0);
                            note = "disabled_silent";
                            return true;
                        }
                        catch (Exception ex)
                        {
                            note = $"{ex.GetType().Name}: {ex.Message}";
                            return false;
                        }
                    }

                    private static object? CreateIl2CppFuncDelegate(Type listEntryType, Action<int, int, uint> managed, out string note)
                    {
                        note = "unknown";
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic;
                            var funcDelegateType = listEntryType.GetNestedType("funcDelegate", FLAGS);
                            if (funcDelegateType == null)
                            {
                                note = "funcDelegate type not found";
                                return null;
                            }

                            var convert = GetDelegateSupportConvertDelegateMethod();
                            if (convert == null)
                            {
                                note = "DelegateSupport.ConvertDelegate not found";
                                return null;
                            }

                            var gm = convert.MakeGenericMethod(funcDelegateType);
                            var il2cppDel = gm.Invoke(null, new object?[] { managed });
                            if (il2cppDel == null)
                            {
                                note = "ConvertDelegate returned null";
                                return null;
                            }

                            note = "ok";
                            return il2cppDel;
                        }
                        catch (Exception ex)
                        {
                            note = $"{ex.GetType().Name}: {ex.Message}";
                            return null;
                        }
                    }




                    private static bool TryWrapEntryForDeferredClose(Type listEntryType, object entry, string reason, out string note)
                    {
                        note = "unknown";
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic;

                            var funcObj = GetProp(entry, "func");
                            if (funcObj == null)
                            {
                                note = "entry.func is null";
                                return false;
                            }

                            var invoke = funcObj.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (invoke == null)
                            {
                                note = "entry.func missing Invoke";
                                return false;
                            }

                            var funcDelegateType = listEntryType.GetNestedType("funcDelegate", FLAGS);
                            if (funcDelegateType == null)
                            {
                                note = "funcDelegate type not found";
                                return false;
                            }

                            Action<int, int, uint> wrapper = (a, b, c) =>
                            {
                                try
                                {
                                    invoke.Invoke(funcObj, new object?[] { a, b, c });
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Warning($"[Reimagined] Debug menu action '{reason}' threw: {ex.GetType().Name}: {ex.Message}");
                                }
                                finally
                                {
                                    RequestDeferredClose(reason);
                                }
                            };

                            // Convert managed delegate to an Il2Cpp-compatible delegate.
                            var convert = GetDelegateSupportConvertDelegateMethod();
                            if (convert == null)
                            {
                                note = "DelegateSupport.ConvertDelegate not found";
                                return false;
                            }

                            var gm = convert.MakeGenericMethod(funcDelegateType);
                            var il2cppDel = gm.Invoke(null, new object?[] { wrapper });
                            if (il2cppDel == null)
                            {
                                note = "ConvertDelegate returned null";
                                return false;
                            }

                            SetProp(entry, "func", il2cppDel);
                            note = "wrapped";
                            return true;
                        }
                        catch (Exception ex)
                        {
                            note = $"{ex.GetType().Name}: {ex.Message}";
                            return false;
                        }
                    }

                    private static object? GetStaticMemberValue(MemberInfo m)
                    {
                        if (m is PropertyInfo pi)
                            return pi.GetValue(null);
                        if (m is FieldInfo fi)
                            return fi.GetValue(null);
                        return null;
                    }

                    private static void SetStaticMemberValue(MemberInfo m, object? value)
                    {
                        if (m is PropertyInfo pi)
                            pi.SetValue(null, value);
                        else if (m is FieldInfo fi)
                            fi.SetValue(null, value);
                    }

                    private static bool TryInvokeStaticBool(Type t, string method, object?[] args)
        {
            try
            {
                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var mi = t.GetMethod(method, FLAGS);
                if (mi == null)
                    return false;

                mi.Invoke(null, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryInvokeStatic(Type t, string method, object?[] args)
                    {
                        try
                        {
                            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                            var mi = t.GetMethod(method, FLAGS);
                            mi?.Invoke(null, args);
                        }
                        catch
                        {
                            // best-effort
                        }
                    }
                }
    }
}