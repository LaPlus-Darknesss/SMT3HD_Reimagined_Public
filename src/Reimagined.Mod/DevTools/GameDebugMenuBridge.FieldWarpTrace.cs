#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // Warp tracing is intentionally "polling based" (no Harmony/patching) so it works reliably
            // across Il2Cpp reflection boundaries and still captures the *real* door-warp pipeline.

            private static bool s_fieldWarpTraceEnabled = false;
            private static string? s_fieldWarpTracePath = null;
            private static int s_fieldWarpTraceLastFlushTick = 0;
            private static int s_fieldWarpTraceSeq = 0;

            // Auto-capture: temporarily record a short trace window even if you forget to toggle trace on.
            private static bool s_fieldWarpTraceAutoCapture = false;
            private static int s_fieldWarpTraceAutoCaptureFramesLeft = 0;
            private static string? s_fieldWarpTraceAutoCaptureReason = null;
            private static bool s_fieldWarpTraceAutoCaptureOpenedFile = false;

            // Cache fldWap static fields once per activation.
            private static Type? s_fieldWarpTrace_fldWapType = null;
            private static FieldInfo[]? s_fieldWarpTrace_fldWapStaticFields = null;

            // Cache fldWap static properties too (Il2CppInterop often exposes real statics as properties).
            private static PropertyInfo[]? s_fieldWarpTrace_fldWapStaticProps = null;
            private static MethodInfo? s_fieldWarpTrace_miCheckDoorWarpType = null;

            // Cache fldEveHit type so we can correlate real door-hits to door table indices.
            private static Type? s_fieldWarpTrace_fldEveHitType = null;

            // Last-seen values to throttle expensive scans.
            private static int s_fieldWarpTrace_lastOldHitDoor = int.MinValue;
            private static int s_fieldWarpTrace_lastDoorWorkEveResid = int.MinValue;
            private static string[]? s_fieldWarpTrace_lastDoorWorkSummaries = null;

            // Previous snapshot of selected values.
            private static readonly Dictionary<string, string> s_fieldWarpTracePrev = new Dictionary<string, string>(StringComparer.Ordinal);
            private static readonly StringBuilder s_fieldWarpTraceSb = new StringBuilder(1024);

            public static void HotkeyToggleFieldWarpTrace()
            {
                s_fieldWarpTraceEnabled = !s_fieldWarpTraceEnabled;

                if (s_fieldWarpTraceEnabled)
                {
                    s_fieldWarpTraceSeq++;
                    s_fieldWarpTraceLastFlushTick = Environment.TickCount;
                    s_fieldWarpTracePath = MakeDumpPath("field_warp_trace", "txt");

                    s_fieldWarpTracePrev.Clear();
                    s_fieldWarpTraceSb.Length = 0;

                    TryInstallFieldWarpCallTracePatchesOnce();

                    // Resolve fldWap type and cache its static fields/properties.
s_fieldWarpTrace_fldWapType = FindTypeInLoadedAssemblies("Il2Cpp.fldWap") ?? FindTypeInLoadedAssemblies("fldWap");
s_fieldWarpTrace_fldWapStaticFields = null;
s_fieldWarpTrace_fldWapStaticProps = null;
                    s_fieldWarpTrace_miCheckDoorWarpType = null;
if (s_fieldWarpTrace_fldWapType != null)
{
    try
    {
        s_fieldWarpTrace_fldWapStaticFields = s_fieldWarpTrace_fldWapType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[WarpTrace] Failed caching fldWap static fields: {ex.GetType().Name}: {ex.Message}");
    }

    try
    {
        s_fieldWarpTrace_fldWapStaticProps = s_fieldWarpTrace_fldWapType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[WarpTrace] Failed caching fldWap static properties: {ex.GetType().Name}: {ex.Message}");
    }
}

// Resolve fldEveHit (tracks what door/trigger the player is colliding with).
s_fieldWarpTrace_fldEveHitType =
    FindTypeInLoadedAssemblies("Il2Cpp.fldEveHit") ??
    FindTypeInLoadedAssemblies("fldEveHit") ??
    FindTypeInLoadedAssemblies("field.fldEveHit");

s_fieldWarpTrace_lastOldHitDoor = int.MinValue;
s_fieldWarpTrace_lastDoorWorkEveResid = int.MinValue;

AppendTraceHeader();
                    PumpFieldWarpTrace(forceLogEvenIfNoChange: true);

                    MelonLogger.Msg($"[WarpTrace] ENABLED → {s_fieldWarpTracePath}");
                }
                else
                {
                    // Final flush
                    FlushTraceToDisk();
                    MelonLogger.Msg("[WarpTrace] DISABLED");
                }
            }

            
            public static void StartAutoCapture(string reason, int frames = 360)
            {
                try
                {
                    if (frames < 30) frames = 30;
                    if (frames > 3600) frames = 3600;

                    s_fieldWarpTraceAutoCapture = true;
                    s_fieldWarpTraceAutoCaptureFramesLeft = frames;
                    s_fieldWarpTraceAutoCaptureReason = reason ?? "";
                    s_fieldWarpTraceAutoCaptureOpenedFile = false;

                    // If trace is not already enabled, temporarily enable it for the capture window.
                    if (!s_fieldWarpTraceEnabled)
                    {
                        s_fieldWarpTraceAutoCaptureOpenedFile = true;
                        HotkeyToggleFieldWarpTrace(); // enables + allocates a new trace file
                    }

                    AppendMarker($"# AUTOCAP START frames={frames} reason={SanitizeOneLine(reason)}");
                    PumpFieldWarpTrace(forceLogEvenIfNoChange: true);
                    FlushTraceToDisk();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[WarpTrace] AutoCapture start failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            public static void Mark(string reason)
            {
                AppendMarker($"# MARK {SanitizeOneLine(reason)}");
            }

            public static void ForceSnapshot(string reason)
            {
                AppendMarker($"# SNAPSHOT {SanitizeOneLine(reason)}");
                PumpFieldWarpTrace(forceLogEvenIfNoChange: true);
                FlushTraceToDisk();
            }

            public static string DescribeDoorEntryForLogs(object doorObj)
            {
                return DescribeDoorObjSafe(doorObj);
            }

            private static string SanitizeOneLine(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return "";
                return s.Replace("\r", " ").Replace("\n", " ").Trim();
            }

            private static void AppendMarker(string markerLine)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(s_fieldWarpTracePath))
                    {
                        s_fieldWarpTraceSb.AppendLine(markerLine);
                    }
                    else
                    {
                        // No file currently open - still print marker to console for visibility.
                        MelonLogger.Msg(markerLine);
                    }
                }
                catch
                {
                    // ignore
                }
            }

public static void PumpFieldWarpTrace()
            {
                PumpFieldWarpTrace(forceLogEvenIfNoChange: false);
            }

            private static void PumpFieldWarpTrace(bool forceLogEvenIfNoChange)
            {
                if (!s_fieldWarpTraceEnabled && !s_fieldWarpTraceAutoCapture)
                    return;

	                // We group changes per-frame to avoid log spam.
	                // NOTE: Some Unity reference stubs omit Time.frameCount at compile-time.
	                // Use the mod's safe frame counter (reflection-backed) so this file always compiles.
	                int frame = GetFrameCountSafe();

                // Build a snapshot of relevant values.
                var changed = new List<string>(16);

                string sceneName = SafeSceneName();
                Track("scene", sceneName, changed);

	                // Field scene param core ints.
	                object? sceneParam = TryGetFldSceneParam(out string spErr);
	                if (sceneParam != null)
                {
                    int loadField = GetInt(sceneParam, "loadField", -999);
                    int loadArea = GetInt(sceneParam, "loadArea", -999);
                    int loadScript = GetInt(sceneParam, "loadScript", -999);
                    int callMode = GetInt(sceneParam, "callMode", -999);
                    int camMode = GetInt(sceneParam, "camMode", -999);
                    int bgm = GetInt(sceneParam, "bgm", -999);

                    Track("loadField", loadField.ToString(CultureInfo.InvariantCulture), changed);
                    Track("loadArea", loadArea.ToString(CultureInfo.InvariantCulture), changed);
                    Track("loadScript", loadScript.ToString(CultureInfo.InvariantCulture), changed);
                    Track("callMode", callMode.ToString(CultureInfo.InvariantCulture), changed);
                    Track("camMode", camMode.ToString(CultureInfo.InvariantCulture), changed);
                    Track("bgm", bgm.ToString(CultureInfo.InvariantCulture), changed);

	                    // These are usually fixed-size char buffers in Il2Cpp. Use the same helper as FieldMapProbe.
	                    string pointRes = GetMaybeFixedCharString(sceneParam, "pointResName", 96);
	                    string camName = GetMaybeFixedCharString(sceneParam, "camName", 96);
	                    Track("pointRes", San(pointRes) ?? "", changed);
	                    Track("camName", San(camName) ?? "", changed);
                }
                else
                {
	                    // Keep the null marker stable, but also include the reason if we have one.
	                    if (!string.IsNullOrWhiteSpace(spErr))
	                        Track("fldSceneParam", "<null>: " + San(spErr), changed);
	                    else
	                        Track("fldSceneParam", "<null>", changed);
                }

                // Door buff presence + count (and we track when the object identity changes)
                if (TryGetDoorBuff(out var doorBuff, out int doorCount))
                {
                    string doorBuffId;
                    if (doorBuff == null)
                    {
                        doorBuffId = "<null>";
                    }
                    else
                    {
                        try
                        {
                            doorBuffId = $"<{doorBuff.GetType().Name}#{doorBuff.GetHashCode():X8}>";
                        }
                        catch
                        {
                            doorBuffId = "<non-null>";
                        }
                    }

                    Track("doorBuff", doorBuffId, changed);
                    Track("doorCount", doorCount.ToString(CultureInfo.InvariantCulture), changed);
                }
                else
                {
                    Track("doorBuff", "<unresolved>", changed);
                }

                // Door-hit correlation (what the player is actually colliding with).
// This is the missing link between "real door transitions" and "door table indices".
if (s_fieldWarpTrace_fldEveHitType != null)
{
    int oldHitDoor = TryReadStaticInt(s_fieldWarpTrace_fldEveHitType, "OldHitDoor", fallback: -9999);
    int oldHitInf = TryReadStaticInt(s_fieldWarpTrace_fldEveHitType, "OldHitInf", fallback: -9999);

    Track("hit.OldHitDoor", oldHitDoor.ToString(CultureInfo.InvariantCulture), changed);
    Track("hit.OldHitInf", oldHitInf.ToString(CultureInfo.InvariantCulture), changed);

    // Only scan the door table when OldHitDoor changes.
    if (oldHitDoor != s_fieldWarpTrace_lastOldHitDoor)
    {
        s_fieldWarpTrace_lastOldHitDoor = oldHitDoor;

        if (oldHitDoor >= 0 && TryGetDoorBuff(out var doorBuff2, out int doorCount2) && doorBuff2 != null)
        {
            string match = DescribeDoorEntrySafe(doorBuff2, doorCount2, oldHitDoor);
            Track("hit.DoorEntry", match, changed);
        }
        else
        {
            Track("hit.DoorEntry", "<none>", changed);
        }
    }
}

// fldWap: key global warp state (these are properties in Il2CppInterop, not plain fields).
if (s_fieldWarpTrace_fldWapType != null)
{
    int gWarpIdx = TryReadStaticInt(s_fieldWarpTrace_fldWapType, "gFldWarpIdx", fallback: -9999);
    int suspendIdx = TryReadStaticInt(s_fieldWarpTrace_fldWapType, "gSuspendWarpIndex", fallback: -9999);
    int panel = TryReadStaticInt(s_fieldWarpTrace_fldWapType, "gFldWapPanel", fallback: -9999);
    int afterNext = TryReadStaticInt(s_fieldWarpTrace_fldWapType, "gFldWap_after_nextidx", fallback: -9999);
    int afterFlag = TryReadStaticInt(s_fieldWarpTrace_fldWapType, "gFldWap_after_flag", fallback: -9999);
    string afterScr = TryReadStaticCharArrayString(s_fieldWarpTrace_fldWapType, "gFldWap_after_scr", 128);

    Track("wap.gFldWarpIdx", gWarpIdx.ToString(CultureInfo.InvariantCulture), changed);
    Track("wap.gSuspendWarpIndex", suspendIdx.ToString(CultureInfo.InvariantCulture), changed);
    Track("wap.gFldWapPanel", panel.ToString(CultureInfo.InvariantCulture), changed);
    Track("wap.gFldWap_after_nextidx", afterNext.ToString(CultureInfo.InvariantCulture), changed);
    Track("wap.gFldWap_after_flag", afterFlag.ToString(CultureInfo.InvariantCulture), changed);
    Track("wap.gFldWap_after_scr", San(afterScr) ?? "", changed);

    // Door-work quick peek (throttled): this is the "in-flight" door transition state machine.
    if ((frame % 5) == 0)
    {
        int eveResid0;
        string dw0 = DescribeDoorWork0Safe(s_fieldWarpTrace_fldWapType, out eveResid0);
        Track("wap.door_work0", dw0, changed);
        TryTrackDoorWorkActiveSlots(s_fieldWarpTrace_fldWapType, changed);

        // Scan the door table only when the evehit resid changes (expensive).
        if (eveResid0 != 0 && eveResid0 != s_fieldWarpTrace_lastDoorWorkEveResid)
        {
            s_fieldWarpTrace_lastDoorWorkEveResid = eveResid0;
            string residMatch = DescribeDoorMatchesByEveResid(eveResid0, maxMatches: 3);
            Track("wap.doorMatchByEveResid", residMatch, changed);
        }
    }
}

// fldWap: enumerate interesting statics (filtered by name) and track primitive-ish values.
                if (s_fieldWarpTrace_fldWapType != null && s_fieldWarpTrace_fldWapStaticFields != null)
                {
                    foreach (var fi in s_fieldWarpTrace_fldWapStaticFields)
                    {
                        if (fi == null) continue;
                        string n = fi.Name ?? string.Empty;
                        if (!IsWarpRelevantStaticName(n))
                            continue;

                        // Avoid crazy large output for buffers/tables: only track nullness + array length if possible.
                        string key = "fldWap." + n;
                        string valueStr = ReadStaticFieldSummarySafe(fi);
                        Track(key, valueStr, changed);

                    }
                    // Also scan static properties (these typically expose real il2cpp statics).
                    if (s_fieldWarpTrace_fldWapStaticProps != null)
                    {
                        foreach (var pi in s_fieldWarpTrace_fldWapStaticProps)
                        {
                            if (pi == null) continue;
                            if (!pi.CanRead) continue;
                            if (pi.GetIndexParameters().Length != 0) continue;
                    
                            string pn = pi.Name ?? string.Empty;
                            if (!IsWarpRelevantStaticName(pn))
                                continue;
                    
                            string pkey = "fldWap." + pn;
                            string pvalueStr = ReadStaticPropertySummarySafe(pi);
                            Track(pkey, pvalueStr, changed);
                        }
                    }
                }
                else
                {
                    Track("fldWap", s_fieldWarpTrace_fldWapType == null ? "<type-missing>" : "<fields-missing>", changed);
                }

                if (changed.Count == 0 && !forceLogEvenIfNoChange)
                    return;

                // Compose one line per frame.
                int lineStart = s_fieldWarpTraceSb.Length;

                s_fieldWarpTraceSb.Append("[WarpTrace] ");
                s_fieldWarpTraceSb.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
                s_fieldWarpTraceSb.Append(" frame=");
                s_fieldWarpTraceSb.Append(frame);
                s_fieldWarpTraceSb.Append(" :: ");

                if (changed.Count == 0)
                {
                    s_fieldWarpTraceSb.Append("(no change)");
                }
                else
                {
                    for (int i = 0; i < changed.Count; i++)
                    {
                        if (i != 0) s_fieldWarpTraceSb.Append(" | ");
                        s_fieldWarpTraceSb.Append(changed[i]);
                    }
                }

                s_fieldWarpTraceSb.AppendLine();

                // Also mirror to console occasionally (only when changes happened) so you can see it live.
                if (changed.Count != 0)
                {
                    try
                    {
                        int lineLen = s_fieldWarpTraceSb.Length - lineStart;
                        string line = s_fieldWarpTraceSb.ToString(lineStart, lineLen).TrimEnd('\r', '\n');
                        MelonLogger.Msg(line);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                // Flush to disk periodically.
                int nowTick = Environment.TickCount;
                if (unchecked(nowTick - s_fieldWarpTraceLastFlushTick) > 1000)
                {
                    FlushTraceToDisk();
                    s_fieldWarpTraceLastFlushTick = nowTick;
                }


                // Auto-capture countdown (used by devtools warp attempts so we always get a trace window).
                if (s_fieldWarpTraceAutoCapture)
                {
                    s_fieldWarpTraceAutoCaptureFramesLeft--;
                    if (s_fieldWarpTraceAutoCaptureFramesLeft <= 0)
                    {
                        AppendMarker($"# AUTOCAP END reason={SanitizeOneLine(s_fieldWarpTraceAutoCaptureReason)}");
                        s_fieldWarpTraceAutoCapture = false;
                        s_fieldWarpTraceAutoCaptureFramesLeft = 0;
                        s_fieldWarpTraceAutoCaptureReason = null;

                        bool opened = s_fieldWarpTraceAutoCaptureOpenedFile;
                        s_fieldWarpTraceAutoCaptureOpenedFile = false;

                        FlushTraceToDisk();

                        // If we had to temporarily enable trace for this capture, turn it back off.
                        if (opened && s_fieldWarpTraceEnabled)
                        {
                            HotkeyToggleFieldWarpTrace();
                        }
                    }
                }
            }

            private static void AppendTraceHeader()
            {
                if (string.IsNullOrWhiteSpace(s_fieldWarpTracePath))
                    return;

                s_fieldWarpTraceSb.AppendLine("# SMT3HD_Reimagined · Field Warp Trace");
                s_fieldWarpTraceSb.Append("# time=");
                s_fieldWarpTraceSb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                s_fieldWarpTraceSb.Append("# seq=");
                s_fieldWarpTraceSb.AppendLine(s_fieldWarpTraceSeq.ToString(CultureInfo.InvariantCulture));
                s_fieldWarpTraceSb.AppendLine("# Notes:");
                s_fieldWarpTraceSb.AppendLine("#  - This trace is polling-based: it logs whenever relevant field/warp statics change.");
                s_fieldWarpTraceSb.AppendLine("#  - Use it while doing a *real* door transition to discover the actual pipeline.");
                s_fieldWarpTraceSb.AppendLine();
            }

            private static void FlushTraceToDisk()
            {
                if (string.IsNullOrWhiteSpace(s_fieldWarpTracePath))
                    return;

                try
                {
                    // Append mode: we keep a growing trace file.
                    System.IO.File.AppendAllText(s_fieldWarpTracePath, s_fieldWarpTraceSb.ToString());
                    s_fieldWarpTraceSb.Length = 0;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[WarpTrace] Failed writing trace: {ex.GetType().Name}: {ex.Message}");
                }
            }

            private static void Track(string key, string value, List<string> changed)
            {
                if (s_fieldWarpTracePrev.TryGetValue(key, out var prev))
                {
                    if (string.Equals(prev, value, StringComparison.Ordinal))
                        return;

                    s_fieldWarpTracePrev[key] = value;
                    changed.Add($"{key}={value}");
                    return;
                }

                s_fieldWarpTracePrev[key] = value;
                changed.Add($"{key}={value}");
            }

            private static string SafeSceneName()
            {
                try
                {
                    return SceneManager.GetActiveScene().name ?? "";
                }
                catch
                {
                    return "";
                }
            }

            private static bool IsWarpRelevantStaticName(string n)
{
    if (string.IsNullOrEmpty(n))
        return false;

    // Il2CppInterop generated pointer stubs; extremely noisy and not useful for tracing.
    if (n.StartsWith("NativeFieldInfoPtr_", StringComparison.Ordinal) ||
        n.StartsWith("NativeMethodInfoPtr_", StringComparison.Ordinal) ||
        n.StartsWith("NativeClassPtr", StringComparison.Ordinal) ||
        n.StartsWith("NativeObjectPtr", StringComparison.Ordinal))
        return false;

    // NOTE: keep this filter intentionally broad; we can tighten once we see what actually moves.
    string lower = n.ToLowerInvariant();
    if (lower.Contains("warp") ||
        lower.Contains("door") ||
        lower.Contains("wap") ||
        lower.Contains("evename") ||
        lower.Contains("idx") ||
        lower.Contains("buff") ||
        lower.Contains("after") ||
        lower.Contains("suspend") ||
        lower.Contains("panel"))
        return true;

    return false;
}

private static string ReadStaticPropertySummarySafe(PropertyInfo pi)
{
    try
    {
        object? v = pi.GetValue(null, null);
        if (v == null)
            return "<null>";

        Type vt = v.GetType();

        // Cheap primitives.
        if (v is int i) return i.ToString(CultureInfo.InvariantCulture);
        if (v is short s2) return s2.ToString(CultureInfo.InvariantCulture);
        if (v is byte b) return b.ToString(CultureInfo.InvariantCulture);
        if (v is bool bo) return bo ? "1" : "0";
        if (v is float f) return f.ToString("0.###", CultureInfo.InvariantCulture);
        if (v is double d) return d.ToString("0.###", CultureInfo.InvariantCulture);
        if (v is string str) return '"' + (San(str) ?? "") + '"';

        if (vt.FullName != null && vt.FullName.IndexOf("Il2CppSystem.String", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string s3 = v.ToString() ?? "";
            s3 = San(s3) ?? "";
            if (s3.Length > 96) s3 = s3.Substring(0, 96) + "…";
            return '"' + s3 + '"';
        }

        if (TryGetArrayLength(v, out int len))
            return $"<{vt.Name} len={len}>";

        return $"<{vt.Name}>";
    }
    catch (Exception ex)
    {
        return $"<err:{ex.GetType().Name}>";
    }
}



private static bool TryGetFldWapDoorWork(Type fldWapType, out object? doorWorkObj)
{
    doorWorkObj = null;
    try
    {
        // fldWap.gFldWarpBuff.{door_work} (best-effort; reflection-only)
        object? warpBuff = GetStaticMemberValueLoose(fldWapType, "gFldWarpBuff");
        if (warpBuff == null)
            return false;

        // Common member names across versions/bindings.
        doorWorkObj = GetMember(warpBuff, "door_work")
                   ?? GetMember(warpBuff, "doorWork")
                   ?? GetMember(warpBuff, "doorwork");

        return doorWorkObj != null;
    }
    catch
    {
        return false;
    }
}

private static bool TryGetFldWapDoorBuff(Type fldWapType, out object? doorBuffObj)
{
    doorBuffObj = null;
    try
    {
        // Prefer the canonical helper if available; it also computes count.
        if (TryGetDoorBuff(out var doorBuff, out int _))
        {
            doorBuffObj = doorBuff;
            return doorBuffObj != null;
        }

        object? warpBuff = GetStaticMemberValueLoose(fldWapType, "gFldWarpBuff");
        if (warpBuff == null)
            return false;

        doorBuffObj = GetMember(warpBuff, "door_buff")
                   ?? GetMember(warpBuff, "doorBuff")
                   ?? GetMember(warpBuff, "doorbuff");

        return doorBuffObj != null;
    }
    catch
    {
        return false;
    }
}


private static void TryTrackDoorWorkActiveSlots(Type fldWapType, List<string> changed)
{
    try
    {
        if (!TryGetFldWapDoorWork(fldWapType, out object? doorWorkObj) || doorWorkObj == null)
            return;

        if (!TryGetArrayLength(doorWorkObj, out int len) || len <= 0)
            return;

        int cap = Math.Min(len, 8);

        if (s_fieldWarpTrace_lastDoorWorkSummaries == null || s_fieldWarpTrace_lastDoorWorkSummaries.Length != cap)
            s_fieldWarpTrace_lastDoorWorkSummaries = new string[cap];

        for (int i = 0; i < cap; i++)
        {
            if (!TryGetArrayElement(doorWorkObj, i, out object? slotObj) || slotObj == null)
                continue;

            int stat = GetInt(slotObj, "stat", 0);
            string summary;

            if (stat == 0)
            {
                summary = "stat=0";
            }
            else
            {
                int ehit = GetInt(slotObj, "evehit_resid", 0);
                int mode1 = GetInt(slotObj, "mode1", 0);
                int mr1 = GetInt(slotObj, "matter_resid1", 0);
                int cnt = GetInt(slotObj, "cnt", 0);
                int max = GetInt(slotObj, "max", 0);
                summary = $"stat={stat} evehit={ehit} mode1={mode1} mr1={mr1} cnt={cnt}/{max}";
            }

            bool hadPrev = !string.IsNullOrEmpty(s_fieldWarpTrace_lastDoorWorkSummaries[i]);
            bool isActive = stat != 0;

            // Don't spam: if it's inactive and we had no prior state, ignore it.
            if (!isActive && !hadPrev)
                continue;

            if (!string.Equals(summary, s_fieldWarpTrace_lastDoorWorkSummaries[i], StringComparison.Ordinal))
            {
                s_fieldWarpTrace_lastDoorWorkSummaries[i] = isActive ? summary : "";
                changed.Add($"wap.door_work[{i}]={summary}");
            }
        }
    }
    catch
    {
        // ignore
    }
}

private static string DescribeDoorWork0Safe(Type fldWapType, out int eveResid0)
{
    eveResid0 = 0;

    try
    {
        object? doorWork = GetStaticMemberValueLoose(fldWapType, "door_work");
        if (doorWork == null)
            return "<null>";

        if (!TryGetArrayLength(doorWork, out int len) || len <= 0)
            return "<empty>";

        if (!TryGetArrayElement(doorWork, 0, out object? w0) || w0 == null)
            return "<unreadable>";

        int stat = GetInt(w0, "stat", 0);
        int evehit = GetInt(w0, "evehit_resid", 0);
        int mode1 = GetInt(w0, "mode1", 0);
        int mr1 = GetInt(w0, "matter_resid1", 0);
        int mode2 = GetInt(w0, "mode2", 0);
        int mr2 = GetInt(w0, "matter_resid2", 0);
        int cnt1 = GetInt(w0, "cnt1", 0);
        int cnt2 = GetInt(w0, "cnt2", 0);
        int cntmax = GetInt(w0, "cntmax", 0);

        eveResid0 = evehit;

        return $"stat={stat} evehit={evehit} mode1={mode1} mr1={mr1} mode2={mode2} mr2={mr2} cnt={cnt1}/{cnt2} max={cntmax}";
    }
    catch (Exception ex)
    {
        return $"<err:{ex.GetType().Name}>";
    }
}

private static string DescribeDoorEntrySafe(object doorBuff, int doorCount, int doorIndex)
{
    try
    {
        if (doorIndex < 0 || doorIndex >= doorCount)
            return "<oob>";

        if (!TryGetArrayElement(doorBuff, doorIndex, out object? doorObj) || doorObj == null)
            return "<null-entry>";

        return DescribeDoorObjSafe(doorObj);
    }
    catch (Exception ex)
    {
        return $"<err:{ex.GetType().Name}>";
    }
}

private static string DescribeDoorObjSafe(object doorObj)
{
    try
    {
        int warptype = GetInt(doorObj, "warptype", -1);
        int warptype2 = GetInt(doorObj, "warptype2", -1);
        int warpAttr = GetInt(doorObj, "warp_attr", -1);

	    // GetUShort expects a ushort fallback; we use 0xFFFF as "missing" and convert to -1.
	    int flagmode = GetUShort(doorObj, "flagmode", ushort.MaxValue);
	    if (flagmode == ushort.MaxValue)
	        flagmode = -1;
        int flag = GetInt(doorObj, "flag", -1);

        int u01 = GetInt(doorObj, "union01", -1);
        int u03 = GetInt(doorObj, "union03", -1);
        int u04 = GetInt(doorObj, "union04", -1);
        int u05 = GetInt(doorObj, "union05", -1);

        int u08 = GetInt(doorObj, "union08", -1);
        int u09 = GetInt(doorObj, "union09", -1);
        int u10 = GetInt(doorObj, "union10", -1);

        int warpCammode = GetInt(doorObj, "warp_cammode", -1);
        int warpBgm = GetInt(doorObj, "warp_bgm", -1);
        int warpFoot = GetInt(doorObj, "warp_foot", -1);

        int afterFlag = GetInt(doorObj, "after_flag", -1);

        string? union02 = GetMaybeFixedCharString(doorObj, "union02");
        string? union06 = GetMaybeFixedCharString(doorObj, "union06");
        string? union07 = GetMaybeFixedCharString(doorObj, "union07");

        string? warpPos = GetMaybeFixedCharString(doorObj, "warp_pos");
        string? warpCamname = GetMaybeFixedCharString(doorObj, "warp_camname");
        string? afterScr = GetMaybeFixedCharString(doorObj, "after_scr");

        string? warpTypeStr = TryCheckDoorWarpTypeSafe(doorObj);

        var sb = new StringBuilder();
        sb.Append("wt=");
        sb.Append(warptype.ToString(CultureInfo.InvariantCulture));
        if (warptype2 != -1)
        {
            sb.Append("/");
            sb.Append(warptype2.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(warpTypeStr))
        {
            sb.Append("[");
            sb.Append(SanitizeOneLine(warpTypeStr));
            sb.Append("]");
        }

        sb.Append(" attr=");
        sb.Append(warpAttr.ToString(CultureInfo.InvariantCulture));
        if (flagmode != -1 || flag != -1)
        {
            sb.Append(" fmode=");
            sb.Append(flagmode.ToString(CultureInfo.InvariantCulture));
            sb.Append(" flag=");
            sb.Append(flag.ToString(CultureInfo.InvariantCulture));
        }

        sb.Append(" u01=");
        sb.Append(u01.ToString(CultureInfo.InvariantCulture));
        sb.Append(" u03=");
        sb.Append(u03.ToString(CultureInfo.InvariantCulture));
        sb.Append(" u04=");
        sb.Append(u04.ToString(CultureInfo.InvariantCulture));
        sb.Append(" u05=");
        sb.Append(u05.ToString(CultureInfo.InvariantCulture));

        sb.Append(" u08=");
        sb.Append(u08.ToString(CultureInfo.InvariantCulture));
        sb.Append(" u09=");
        sb.Append(u09.ToString(CultureInfo.InvariantCulture));
        sb.Append(" u10=");
        sb.Append(u10.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(warpPos))
        {
            sb.Append(" pos=");
            sb.Append(SanitizeOneLine(warpPos));
        }

        if (!string.IsNullOrWhiteSpace(warpCamname))
        {
            sb.Append(" cam=");
            sb.Append(SanitizeOneLine(warpCamname));
            if (warpCammode != -1)
            {
                sb.Append(":");
                sb.Append(warpCammode.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (warpBgm != -1 || warpFoot != -1)
        {
            sb.Append(" bgm=");
            sb.Append(warpBgm.ToString(CultureInfo.InvariantCulture));
            sb.Append(" foot=");
            sb.Append(warpFoot.ToString(CultureInfo.InvariantCulture));
        }

        if (afterFlag != -1 || !string.IsNullOrWhiteSpace(afterScr))
        {
            sb.Append(" after=");
            sb.Append(afterFlag.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(afterScr))
            {
                sb.Append(" scr=");
                sb.Append(SanitizeOneLine(afterScr));
            }
        }

        // Only append these if they look populated; they're often empty in normal door entries.
        if (!string.IsNullOrWhiteSpace(union02))
        {
            sb.Append(" u02=");
            sb.Append(SanitizeOneLine(union02));
        }
        if (!string.IsNullOrWhiteSpace(union06))
        {
            sb.Append(" u06=");
            sb.Append(SanitizeOneLine(union06));
        }
        if (!string.IsNullOrWhiteSpace(union07))
        {
            sb.Append(" u07=");
            sb.Append(SanitizeOneLine(union07));
        }

        return sb.ToString();
    }
    catch (Exception ex)
    {
        return $"<err:{ex.GetType().Name}>";
    }
}

private static string? TryCheckDoorWarpTypeSafe(object doorObj)
{
    try
    {
        if (s_fieldWarpTrace_miCheckDoorWarpType == null)
        {
            Type? fldWapType = doorObj.GetType().Assembly.GetType("Il2Cpp.fldWap");
            if (fldWapType == null)
                return null;

            foreach (var mi in fldWapType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!string.Equals(mi.Name, "CheckDoorWarpType", StringComparison.Ordinal))
                    continue;

                var ps = mi.GetParameters();
                if (ps.Length != 1)
                    continue;

                if (!ps[0].ParameterType.IsAssignableFrom(doorObj.GetType()))
                    continue;

                if (mi.ReturnType != typeof(string))
                    continue;

                s_fieldWarpTrace_miCheckDoorWarpType = mi;
                break;
            }
        }

        if (s_fieldWarpTrace_miCheckDoorWarpType == null)
            return null;

        object? ret = s_fieldWarpTrace_miCheckDoorWarpType.Invoke(null, new object[] { doorObj });
        return ret as string;
    }
    catch
    {
        return null;
    }
}


private static string DescribeDoorMatchesByEveResid(int evehitResid, int maxMatches)
{
    try
    {
        if (s_fieldWarpTrace_fldWapType == null)
            return "";

        if (!TryGetFldWapDoorBuff(s_fieldWarpTrace_fldWapType, out object? doorBuffObj) || doorBuffObj == null)
            return "";

        if (!TryGetArrayLength(doorBuffObj, out int doorCount) || doorCount <= 0)
            return "";

        int matchCount = 0;

        var sb = new StringBuilder();
        sb.Append("resid=");
        sb.Append(evehitResid.ToString(CultureInfo.InvariantCulture));
        sb.Append(" -> ");

        for (int i = 0; i < doorCount; i++)
        {
            if (!TryGetArrayElement(doorBuffObj, i, out object? doorObj) || doorObj == null)
                continue;

            int u01 = GetInt(doorObj, "union01", int.MinValue);
            int u03 = GetInt(doorObj, "union03", int.MinValue);
            int u04 = GetInt(doorObj, "union04", int.MinValue);
            int u05 = GetInt(doorObj, "union05", int.MinValue);
            int u08 = GetInt(doorObj, "union08", int.MinValue);
            int u09 = GetInt(doorObj, "union09", int.MinValue);
            int u10 = GetInt(doorObj, "union10", int.MinValue);

            string? matchField = null;

            if (u01 == evehitResid) matchField = "u01";
            else if (u03 == evehitResid) matchField = "u03";
            else if (u04 == evehitResid) matchField = "u04";
            else if (u05 == evehitResid) matchField = "u05";
            else if (u08 == evehitResid) matchField = "u08";
            else if (u09 == evehitResid) matchField = "u09";
            else if (u10 == evehitResid) matchField = "u10";

            if (matchField == null)
                continue;

            int warptype = GetInt(doorObj, "warptype", -1);

            if (matchCount > 0)
                sb.Append(" | ");

            sb.Append("idx=");
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append("(");
            sb.Append(matchField);
            sb.Append(")");
            sb.Append(" wt=");
            sb.Append(warptype.ToString(CultureInfo.InvariantCulture));

            matchCount++;
            if (matchCount >= maxMatches)
            {
                sb.Append(" | ...");
                break;
            }
        }

        if (matchCount == 0)
            return "";

        return sb.ToString();
    }
    catch
    {
        return "";
    }
}


private static string ReadStaticFieldSummarySafe(FieldInfo fi)
            {
                try
                {
                    object? v = fi.GetValue(null);
                    if (v == null)
                        return "<null>";

                    Type vt = v.GetType();

                    // Cheap primitives.
                    if (v is int i) return i.ToString(CultureInfo.InvariantCulture);
                    if (v is short s) return s.ToString(CultureInfo.InvariantCulture);
                    if (v is byte b) return b.ToString(CultureInfo.InvariantCulture);
                    if (v is bool bo) return bo ? "1" : "0";
                    if (v is float f) return f.ToString("0.###", CultureInfo.InvariantCulture);
                    if (v is double d) return d.ToString("0.###", CultureInfo.InvariantCulture);
                    if (v is string str) return '"' + (San(str) ?? "") + '"';

                    // Il2Cpp string wrappers sometimes show up as Il2CppSystem.String.
                    // We attempt ToString() but clamp length.
                    if (vt.FullName != null && vt.FullName.IndexOf("Il2CppSystem.String", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string s2 = v.ToString() ?? "";
                        s2 = San(s2) ?? "";
                        if (s2.Length > 96) s2 = s2.Substring(0, 96) + "…";
                        return '"' + s2 + '"';
                    }

                    // Arrays: show length when possible.
                    if (TryGetArrayLength(v, out int len))
                        return $"<{vt.Name} len={len}>";

                    return $"<{vt.Name}>";
                }
                catch (Exception ex)
                {
                    return $"<err:{ex.GetType().Name}>";
                }
            }
        }
    }
}
