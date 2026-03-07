#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            private const string WarpCatalogFileName = "warp_catalog.jsonl";

            private static bool s_warpCatalogArmed = false;
            private static float s_warpCatalogArmRealtimeDeadline = 0.0f;

            private static int s_warpCatalogSrcF = -1;
            private static int s_warpCatalogSrcA = -1;
            private static int s_warpCatalogSrcS = -1;
            private static string s_warpCatalogSrcPointRes = "";
            private static string s_warpCatalogSrcCamName = "";
            private static int s_warpCatalogSrcBgmId = -1;

            private static int s_warpCatalogHitDoorIndex = -1;
            private static int s_warpCatalogHitInf = -1;

            // Door metadata: keep these aligned with the field warp-table dumper so recorded catalog rows
            // can be correlated directly against field_warp_table_*.txt without guesswork.
            private static int s_warpCatalogDoorMoveType = -1;
            private static int s_warpCatalogDoorWarptype = -1;
            private static int s_warpCatalogDoorWarptype2 = -1;
            private static int s_warpCatalogDoorWarpAttr = -1;
            private static int s_warpCatalogDoorFlagMode = -1;
            private static int s_warpCatalogDoorFlag = -1;
            private static int s_warpCatalogDoorUnion01 = -1;
            private static int s_warpCatalogDoorUnion03 = -1;
            private static int s_warpCatalogDoorUnion04 = -1;
            private static int s_warpCatalogDoorExitNumber = -1; // union05 in doormovetbl_s
            private static string s_warpCatalogDoorEvename = ""; // union02
            private static string s_warpCatalogDoorUnion06 = "";
            private static string s_warpCatalogDoorUnion07 = "";
            private static int s_warpCatalogDoorUnion08 = -1;
            private static int s_warpCatalogDoorUnion09 = -1;
            private static int s_warpCatalogDoorUnion10 = -1;
            private static string s_warpCatalogDoorPos = ""; // warp_pos
            private static int s_warpCatalogDoorCamMode = -1;
            private static int s_warpCatalogDoorCamTbl = -1;
            private static string s_warpCatalogDoorCam = ""; // warp_camname
            private static int s_warpCatalogDoorBgm = -1;
            private static int s_warpCatalogDoorFoot = -1;
            private static int s_warpCatalogDoorAfterFlag = -1;
            private static string s_warpCatalogDoorAfterScr = "";

            // Terminal-state hints. These let us distinguish:
            //  - a real door transition
            //  - a terminal-mediated warp where a door hit may only represent "open the terminal"
            private static bool s_warpCatalogSawTerminal = false;
            private static bool s_warpCatalogSawTerminalTransport = false;
            private static int s_warpCatalogTerminalSeq = -1;
            private static int s_warpCatalogTerminalCallMode = -1;
            private static int s_warpCatalogTerminalProcessStat = -1;
            private static int s_warpCatalogTerminalEventStat = -1;
            private static int s_warpCatalogTerminalJumpNo = -1;
            private static int s_warpCatalogTerminalType = -1;
            private static int s_warpCatalogTerminalNo = -1;
            private static int s_warpCatalogTerminalCnt = -1;

            // Sticky transport snapshot. Terminal state can partially reset while the field is changing,
            // so we preserve the first/most-informative transport selection data instead of relying on
            // whatever the terminal globals look like at write-time.
            private static int s_warpCatalogTransportSeq = -1;
            private static int s_warpCatalogTransportCallMode = -1;
            private static int s_warpCatalogTransportProcessStat = -1;
            private static int s_warpCatalogTransportEventStat = -1;
            private static int s_warpCatalogTransportJumpNo = -1;
            private static int s_warpCatalogTransportType = -1;
            private static int s_warpCatalogTransportNo = -1;
            private static int s_warpCatalogTransportCnt = -1;

            // Baseline terminal snapshot at arm-time. We only promote a route to "terminal"
            // when transport-related terminal state becomes meaningfully different during the armed window.
            private static int s_warpCatalogArmTerminalSeq = -1;
            private static int s_warpCatalogArmTerminalCallMode = -1;
            private static int s_warpCatalogArmTerminalProcessStat = -1;
            private static int s_warpCatalogArmTerminalEventStat = -1;
            private static int s_warpCatalogArmTerminalJumpNo = -1;
            private static int s_warpCatalogArmTerminalType = -1;
            private static int s_warpCatalogArmTerminalNo = -1;
            private static int s_warpCatalogArmTerminalCnt = -1;

            private static int s_warpCatalogLastSeenOldHitDoor = int.MinValue;

            public static void HotkeyToggleWarpCatalogRecord()
            {
                if (!s_warpCatalogArmed)
                {
                    ArmWarpCatalogRecord();
                    return;
                }

                CancelWarpCatalogRecord("cancel_hotkey");
            }

            private static void ArmWarpCatalogRecord()
            {
                // NOTE: our bridge's TryGetCurrentFieldContext is best-effort + void.
                // Treat negative values as "unavailable".
                TryGetCurrentFieldContext(out int f, out int a, out int s, out _, out _);
                if (f < 0 || a < 0 || s < 0)
                {
                    MelonLogger.Warning("[WarpCatalog] Cannot arm: current field context unavailable (not in field?)");
                    return;
                }

                object? sceneParam = TryGetFldSceneParam(out string spErr);
                if (sceneParam == null)
                    MelonLogger.Warning($"[WarpCatalog] Note: fldSceneParam unavailable at arm-time: {San(spErr)}");

                s_warpCatalogArmed = true;
                s_warpCatalogArmRealtimeDeadline = Time.realtimeSinceStartup + 30.0f;

                s_warpCatalogSrcF = f;
                s_warpCatalogSrcA = a;
                s_warpCatalogSrcS = s;

                s_warpCatalogHitDoorIndex = -1;
                s_warpCatalogHitInf = -1;
                ResetDoorSnapshot();
                ResetTerminalSnapshot();
                CaptureWarpCatalogTerminalBaseline();
                s_warpCatalogLastSeenOldHitDoor = int.MinValue;

                // Best-effort: snapshot common scene-param strings.
                s_warpCatalogSrcPointRes = "";
                s_warpCatalogSrcCamName = "";
                s_warpCatalogSrcBgmId = -1;
                if (sceneParam != null)
                {
                    s_warpCatalogSrcPointRes = GetMaybeFixedCharString(sceneParam, "pointResName", 96);
                    s_warpCatalogSrcCamName = GetMaybeFixedCharString(sceneParam, "camName", 96);
                    s_warpCatalogSrcBgmId = GetInt(sceneParam, "bgmId", -1);
                }

                MelonLogger.Msg($"[WarpCatalog] Armed: walk through a door or do a terminal warp within 30s. src=F{s_warpCatalogSrcF} A{s_warpCatalogSrcA} S{s_warpCatalogSrcS} point={San(s_warpCatalogSrcPointRes)} cam={San(s_warpCatalogSrcCamName)}");
            }

            private static void CancelWarpCatalogRecord(string reason)
            {
                if (!s_warpCatalogArmed)
                    return;

                s_warpCatalogArmed = false;
                s_warpCatalogArmRealtimeDeadline = 0.0f;

                MelonLogger.Msg($"[WarpCatalog] Cancelled ({reason}). src=F{s_warpCatalogSrcF} A{s_warpCatalogSrcA} S{s_warpCatalogSrcS} door={s_warpCatalogHitDoorIndex} terminalSeen={s_warpCatalogSawTerminal} terminalTransport={s_warpCatalogSawTerminalTransport}");
            }

            /// <summary>
            /// Called once per frame from ReimaginedMod.OnUpdate().
            /// </summary>
            public static void TickWarpCatalogRecord()
            {
                if (!s_warpCatalogArmed)
                    return;

                float now = Time.realtimeSinceStartup;
                if (now > s_warpCatalogArmRealtimeDeadline)
                {
                    CancelWarpCatalogRecord("timeout");
                    return;
                }

                // Step 1: observe door hits while armed (best-effort; normal door transitions).
                TryObserveOldHitDoorOnce();

                // Step 2: observe whether a terminal process became active during the armed window.
                // This lets us classify terminal-mediated warps separately from ordinary doors.
                TryObserveTerminalStateOnce();

                // Step 3: detect context change (dest).
                TryGetCurrentFieldContext(out int curF, out int curA, out int curS, out _, out _);
                if (curF < 0 || curA < 0 || curS < 0)
                    return;

                if (curF == s_warpCatalogSrcF && curA == s_warpCatalogSrcA && curS == s_warpCatalogSrcS)
                    return;

                // Dest: best-effort metadata
                object? sceneParam = TryGetFldSceneParam(out _);
                string dstPointRes = "";
                string dstCamName = "";
                int dstBgmId = -1;
                if (sceneParam != null)
                {
                    dstPointRes = GetMaybeFixedCharString(sceneParam, "pointResName", 96);
                    dstCamName = GetMaybeFixedCharString(sceneParam, "camName", 96);
                    dstBgmId = GetInt(sceneParam, "bgmId", -1);
                }

                WriteWarpCatalogRecord(
                    curF, curA, curS,
                    dstPointRes, dstCamName, dstBgmId
                );

                s_warpCatalogArmed = false;
                s_warpCatalogArmRealtimeDeadline = 0.0f;

                string kind = GetCurrentRecordedKind();
                string terminalExtra = s_warpCatalogSawTerminalTransport
                    ? $" transportTermType={s_warpCatalogTransportType} transportTermNo={s_warpCatalogTransportNo} transportJumpNo={s_warpCatalogTransportJumpNo}"
                    : "";
                MelonLogger.Msg($"[WarpCatalog] Recorded transition: kind={kind} src=F{s_warpCatalogSrcF} A{s_warpCatalogSrcA} S{s_warpCatalogSrcS} -> dst=F{curF} A{curA} S{curS} (door={s_warpCatalogHitDoorIndex} terminalSeen={s_warpCatalogSawTerminal} terminalTransport={s_warpCatalogSawTerminalTransport}{terminalExtra})");
            }

            private static void ResetDoorSnapshot()
            {
                s_warpCatalogDoorMoveType = -1;
                s_warpCatalogDoorWarptype = -1;
                s_warpCatalogDoorWarptype2 = -1;
                s_warpCatalogDoorWarpAttr = -1;
                s_warpCatalogDoorFlagMode = -1;
                s_warpCatalogDoorFlag = -1;
                s_warpCatalogDoorUnion01 = -1;
                s_warpCatalogDoorUnion03 = -1;
                s_warpCatalogDoorUnion04 = -1;
                s_warpCatalogDoorExitNumber = -1;
                s_warpCatalogDoorEvename = "";
                s_warpCatalogDoorUnion06 = "";
                s_warpCatalogDoorUnion07 = "";
                s_warpCatalogDoorUnion08 = -1;
                s_warpCatalogDoorUnion09 = -1;
                s_warpCatalogDoorUnion10 = -1;
                s_warpCatalogDoorPos = "";
                s_warpCatalogDoorCamMode = -1;
                s_warpCatalogDoorCamTbl = -1;
                s_warpCatalogDoorCam = "";
                s_warpCatalogDoorBgm = -1;
                s_warpCatalogDoorFoot = -1;
                s_warpCatalogDoorAfterFlag = -1;
                s_warpCatalogDoorAfterScr = "";
            }

            private static void ResetTerminalSnapshot()
            {
                s_warpCatalogSawTerminal = false;
                s_warpCatalogSawTerminalTransport = false;
                s_warpCatalogTerminalSeq = -1;
                s_warpCatalogTerminalCallMode = -1;
                s_warpCatalogTerminalProcessStat = -1;
                s_warpCatalogTerminalEventStat = -1;
                s_warpCatalogTerminalJumpNo = -1;
                s_warpCatalogTerminalType = -1;
                s_warpCatalogTerminalNo = -1;
                s_warpCatalogTerminalCnt = -1;
                s_warpCatalogTransportSeq = -1;
                s_warpCatalogTransportCallMode = -1;
                s_warpCatalogTransportProcessStat = -1;
                s_warpCatalogTransportEventStat = -1;
                s_warpCatalogTransportJumpNo = -1;
                s_warpCatalogTransportType = -1;
                s_warpCatalogTransportNo = -1;
                s_warpCatalogTransportCnt = -1;
                s_warpCatalogArmTerminalSeq = -1;
                s_warpCatalogArmTerminalCallMode = -1;
                s_warpCatalogArmTerminalProcessStat = -1;
                s_warpCatalogArmTerminalEventStat = -1;
                s_warpCatalogArmTerminalJumpNo = -1;
                s_warpCatalogArmTerminalType = -1;
                s_warpCatalogArmTerminalNo = -1;
                s_warpCatalogArmTerminalCnt = -1;
            }

            private static void TryObserveOldHitDoorOnce()
            {
                // Use the same reflection surface as WarpFavorites capture, so behavior is consistent.
                s_warpFav_fldEveHitType ??= TryResolveType("fldEveHit");

                if (s_warpFav_fldEveHitType == null)
                    return;

                int oldHitDoor = ReadStaticIntMemberSafe(s_warpFav_fldEveHitType, "OldHitDoor", -1);
                int oldHitInf = ReadStaticIntMemberSafe(s_warpFav_fldEveHitType, "OldHitInf", -1);

                if (oldHitDoor == s_warpCatalogLastSeenOldHitDoor)
                    return;

                s_warpCatalogLastSeenOldHitDoor = oldHitDoor;

                if (oldHitDoor < 0)
                    return;

                s_warpCatalogHitDoorIndex = oldHitDoor;
                s_warpCatalogHitInf = oldHitInf;

                // Door entry snapshot (best-effort).
                if (TrySnapshotDoorEntryForIndex(oldHitDoor))
                {
                    MelonLogger.Msg(
                        $"[WarpCatalog] HitDoor: idx={oldHitDoor} mt={s_warpCatalogDoorMoveType} wt={s_warpCatalogDoorWarptype} wt2={s_warpCatalogDoorWarptype2} " +
                        $"u03={s_warpCatalogDoorUnion03} u05={s_warpCatalogDoorExitNumber} u09={s_warpCatalogDoorUnion09} " +
                        $"pos={San(s_warpCatalogDoorPos)} cam={San(s_warpCatalogDoorCam)} after={San(s_warpCatalogDoorAfterScr)}");
                }
                else
                {
                    MelonLogger.Msg($"[WarpCatalog] HitDoor: idx={oldHitDoor} (door entry unavailable)");
                }
            }

            private static void CaptureWarpCatalogTerminalBaseline()
            {
                if (!TryReadCurrentTerminalState(
                    out _,
                    out s_warpCatalogArmTerminalSeq,
                    out s_warpCatalogArmTerminalCallMode,
                    out s_warpCatalogArmTerminalProcessStat,
                    out s_warpCatalogArmTerminalEventStat,
                    out s_warpCatalogArmTerminalJumpNo,
                    out s_warpCatalogArmTerminalType,
                    out s_warpCatalogArmTerminalNo,
                    out s_warpCatalogArmTerminalCnt))
                {
                    s_warpCatalogArmTerminalSeq = -1;
                    s_warpCatalogArmTerminalCallMode = -1;
                    s_warpCatalogArmTerminalProcessStat = -1;
                    s_warpCatalogArmTerminalEventStat = -1;
                    s_warpCatalogArmTerminalJumpNo = -1;
                    s_warpCatalogArmTerminalType = -1;
                    s_warpCatalogArmTerminalNo = -1;
                    s_warpCatalogArmTerminalCnt = -1;
                }
            }

            private static void TryObserveTerminalStateOnce()
            {
                if (!TryReadCurrentTerminalState(
                    out bool terminalActive,
                    out int seqTerminal,
                    out int callMode,
                    out int processStat,
                    out int eventStat,
                    out int jumpNo,
                    out int terminalType,
                    out int terminalNo,
                    out int terminalCnt))
                {
                    return;
                }

                if (!terminalActive)
                    return;

                bool firstSeen = !s_warpCatalogSawTerminal;
                s_warpCatalogSawTerminal = true;
                s_warpCatalogTerminalSeq = seqTerminal;
                s_warpCatalogTerminalCallMode = callMode;
                s_warpCatalogTerminalProcessStat = processStat;
                s_warpCatalogTerminalEventStat = eventStat;
                s_warpCatalogTerminalJumpNo = jumpNo;
                s_warpCatalogTerminalType = terminalType;
                s_warpCatalogTerminalNo = terminalNo;
                s_warpCatalogTerminalCnt = terminalCnt;

                if (firstSeen)
                {
                    MelonLogger.Msg(
                        $"[WarpCatalog] Terminal active: seq={s_warpCatalogTerminalSeq} callMode={s_warpCatalogTerminalCallMode} " +
                        $"proc={s_warpCatalogTerminalProcessStat} evt={s_warpCatalogTerminalEventStat} " +
                        $"termType={s_warpCatalogTerminalType} termNo={s_warpCatalogTerminalNo} jumpNo={s_warpCatalogTerminalJumpNo} cnt={s_warpCatalogTerminalCnt}");
                }

                bool transportMeaningful =
                    s_warpCatalogTerminalEventStat == 6 ||
                    s_warpCatalogTerminalJumpNo >= 0 ||
                    s_warpCatalogTerminalType >= 0 ||
                    s_warpCatalogTerminalNo >= 0 ||
                    s_warpCatalogTerminalCnt >= 0;

                bool transportChanged =
                    s_warpCatalogTerminalEventStat != s_warpCatalogArmTerminalEventStat ||
                    s_warpCatalogTerminalJumpNo != s_warpCatalogArmTerminalJumpNo ||
                    s_warpCatalogTerminalType != s_warpCatalogArmTerminalType ||
                    s_warpCatalogTerminalNo != s_warpCatalogArmTerminalNo ||
                    s_warpCatalogTerminalCnt != s_warpCatalogArmTerminalCnt;

                if (transportMeaningful && transportChanged)
                {
                    MergeTerminalTransportSnapshot(
                        s_warpCatalogTerminalSeq,
                        s_warpCatalogTerminalCallMode,
                        s_warpCatalogTerminalProcessStat,
                        s_warpCatalogTerminalEventStat,
                        s_warpCatalogTerminalJumpNo,
                        s_warpCatalogTerminalType,
                        s_warpCatalogTerminalNo,
                        s_warpCatalogTerminalCnt);

                    if (!s_warpCatalogSawTerminalTransport)
                    {
                        s_warpCatalogSawTerminalTransport = true;
                        MelonLogger.Msg(
                            $"[WarpCatalog] Terminal transport selected: evt={s_warpCatalogTransportEventStat} " +
                            $"termType={s_warpCatalogTransportType} termNo={s_warpCatalogTransportNo} jumpNo={s_warpCatalogTransportJumpNo} cnt={s_warpCatalogTransportCnt}");
                    }
                }
            }

            private static void MergeTerminalTransportSnapshot(
                int seqTerminal,
                int callMode,
                int processStat,
                int eventStat,
                int jumpNo,
                int terminalType,
                int terminalNo,
                int terminalCnt)
            {
                if (seqTerminal >= 0)
                    s_warpCatalogTransportSeq = seqTerminal;
                if (callMode >= 0)
                    s_warpCatalogTransportCallMode = callMode;
                if (processStat >= 0)
                    s_warpCatalogTransportProcessStat = processStat;
                if (eventStat >= 0)
                    s_warpCatalogTransportEventStat = eventStat;
                if (jumpNo >= 0)
                    s_warpCatalogTransportJumpNo = jumpNo;
                if (terminalType >= 0)
                    s_warpCatalogTransportType = terminalType;
                if (terminalNo >= 0)
                    s_warpCatalogTransportNo = terminalNo;
                if (terminalCnt >= 0)
                    s_warpCatalogTransportCnt = terminalCnt;
            }

            private static bool TryReadCurrentTerminalState(
                out bool terminalActive,
                out int seqTerminal,
                out int callMode,
                out int processStat,
                out int eventStat,
                out int jumpNo,
                out int terminalType,
                out int terminalNo,
                out int terminalCnt)
            {
                terminalActive = false;
                seqTerminal = -1;
                callMode = -1;
                processStat = -1;
                eventStat = -1;
                jumpNo = -1;
                terminalType = -1;
                terminalNo = -1;
                terminalCnt = -1;

                bool readAnything = false;

                try
                {
                    var seqType = FindTypeInLoadedAssemblies("Il2Cpp.dds3SequenceList");
                    if (seqType != null && TryCallIntStaticMethod(seqType, "CheckTerminal", out seqTerminal))
                    {
                        readAnything = true;
                        terminalActive = (seqTerminal != 0);
                    }
                }
                catch
                {
                    // best-effort only
                }

                var initType = FindGameType("Il2Cpp.fclTerminalInit", "fclTerminalInit");
                int processChk = -1;
                if (initType != null)
                {
                    readAnything = true;

                    if (TryCallIntStaticMethod(initType, "fclChkTerminalProcess", out processChk) && processChk != 0)
                        terminalActive = true;

                    callMode = TryReadStaticInt(initType, "gCallMode", callMode);
                    processStat = TryReadStaticInt(initType, "gProcessStat", processStat);
                    eventStat = TryReadStaticInt(initType, "gEventStat", eventStat);

                    object? gbwk = GetStaticMemberValueLoose(initType, "GBWK");
                    if (gbwk != null)
                    {
                        terminalType = GetInt(gbwk, "TerminalType", terminalType);
                        terminalNo = GetInt(gbwk, "TerminalNo", terminalNo);
                        terminalCnt = GetInt(gbwk, "TerminalCnt", terminalCnt);
                    }
                }

                var updateType = FindGameType("Il2Cpp.fclTerminalUpdate", "fclTerminalUpdate");
                if (updateType != null)
                {
                    readAnything = true;
                    if (TryCallIntStaticMethod(updateType, "trmGetJumpTerminalNo", out int observedJumpNo))
                        jumpNo = observedJumpNo;
                }

                return readAnything;
            }

            private static bool TrySnapshotDoorEntryForIndex(int doorIndex)
            {
                ResetDoorSnapshot();

                if (!TryGetDoorBuff(out object? doorBuff, out int doorCount) || doorBuff == null)
                    return false;

                if (doorIndex < 0 || (doorCount >= 0 && doorIndex >= doorCount))
                    return false;

                if (!TryGetArrayElement(doorBuff, doorIndex, out object? entry) || entry == null)
                    return false;

                s_warpCatalogDoorMoveType = GetByte(entry, "movetype", 0);
                s_warpCatalogDoorWarptype = GetByte(entry, "warptype", 0);
                s_warpCatalogDoorWarptype2 = GetByte(entry, "warptype2", 0);
                s_warpCatalogDoorWarpAttr = GetUShort(entry, "warp_attr", 0);
                s_warpCatalogDoorFlagMode = GetUShort(entry, "flagmode", 0);
                s_warpCatalogDoorFlag = GetUShort(entry, "flag", 0);
                s_warpCatalogDoorUnion01 = GetInt(entry, "union01", -1);
                s_warpCatalogDoorUnion03 = GetInt(entry, "union03", -1);
                s_warpCatalogDoorUnion04 = GetInt(entry, "union04", -1);
                s_warpCatalogDoorExitNumber = GetDoorExitNumber(entry);
                s_warpCatalogDoorEvename = GetMaybeFixedCharString(entry, "union02", 96);
                s_warpCatalogDoorUnion06 = GetMaybeFixedCharString(entry, "union06", 96);
                s_warpCatalogDoorUnion07 = GetMaybeFixedCharString(entry, "union07", 96);
                s_warpCatalogDoorUnion08 = GetInt(entry, "union08", -1);
                s_warpCatalogDoorUnion09 = GetInt(entry, "union09", -1);
                s_warpCatalogDoorUnion10 = GetInt(entry, "union10", -1);
                s_warpCatalogDoorPos = GetMaybeFixedCharString(entry, "warp_pos", 96);
                s_warpCatalogDoorCamMode = GetInt(entry, "warp_cammode", -1);
                s_warpCatalogDoorCamTbl = GetInt(entry, "warp_camtbl", -1);
                s_warpCatalogDoorCam = GetMaybeFixedCharString(entry, "warp_camname", 96);
                s_warpCatalogDoorBgm = GetInt(entry, "warp_bgm", -1);
                s_warpCatalogDoorFoot = GetInt(entry, "warp_foot", -1);
                s_warpCatalogDoorAfterFlag = GetInt(entry, "after_flag", -1);
                s_warpCatalogDoorAfterScr = GetMaybeFixedCharString(entry, "after_scr", 96);
                return true;
            }

            // In doormovetbl_s this is stored in union05.
            private static int GetDoorExitNumber(object doorEntry)
            {
                try
                {
                    return GetInt(doorEntry, "union05", -1);
                }
                catch
                {
                    return -1;
                }
            }

            private static void WriteWarpCatalogRecord(
                int dstF, int dstA, int dstS,
                string dstPointRes, string dstCamName, int dstBgmId)
            {
                try
                {
                    Directory.CreateDirectory(DumpsDir);

                    string path = Path.Combine(DumpsDir, WarpCatalogFileName);
                    string line = BuildJsonLine(
                        dstF, dstA, dstS,
                        dstPointRes, dstCamName, dstBgmId
                    );

                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[WarpCatalog] Failed to write record: {ex}");
                }
            }

            private static string BuildJsonLine(
                int dstF, int dstA, int dstS,
                string dstPointRes, string dstCamName, int dstBgmId)
            {
                // Tiny JSON builder to avoid any runtime serializer surprises.
                // All strings are escaped with minimal JSON escaping.
                string now = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);

                var sb = new StringBuilder(1024);
                sb.Append('{');

                AppendJsonKvp(sb, "t", now); sb.Append(',');
                AppendJsonKvp(sb, "kind", GetCurrentRecordedKind()); sb.Append(',');

                sb.Append("\"src\":{");
                AppendJsonKvp(sb, "F", s_warpCatalogSrcF); sb.Append(',');
                AppendJsonKvp(sb, "A", s_warpCatalogSrcA); sb.Append(',');
                AppendJsonKvp(sb, "S", s_warpCatalogSrcS); sb.Append(',');
                AppendJsonKvp(sb, "pointRes", s_warpCatalogSrcPointRes); sb.Append(',');
                AppendJsonKvp(sb, "camName", s_warpCatalogSrcCamName); sb.Append(',');
                AppendJsonKvp(sb, "bgmId", s_warpCatalogSrcBgmId);
                sb.Append("},");

                sb.Append("\"dst\":{");
                AppendJsonKvp(sb, "F", dstF); sb.Append(',');
                AppendJsonKvp(sb, "A", dstA); sb.Append(',');
                AppendJsonKvp(sb, "S", dstS); sb.Append(',');
                AppendJsonKvp(sb, "pointRes", dstPointRes); sb.Append(',');
                AppendJsonKvp(sb, "camName", dstCamName); sb.Append(',');
                AppendJsonKvp(sb, "bgmId", dstBgmId);
                sb.Append("},");

                sb.Append("\"door\":{");
                AppendJsonKvp(sb, "idx", s_warpCatalogHitDoorIndex); sb.Append(',');
                AppendJsonKvp(sb, "hitInf", s_warpCatalogHitInf); sb.Append(',');
                AppendJsonKvp(sb, "movetype", s_warpCatalogDoorMoveType); sb.Append(',');
                AppendJsonKvp(sb, "warptype", s_warpCatalogDoorWarptype); sb.Append(',');
                AppendJsonKvp(sb, "warptype2", s_warpCatalogDoorWarptype2); sb.Append(',');
                AppendJsonKvp(sb, "warpAttr", s_warpCatalogDoorWarpAttr); sb.Append(',');
                AppendJsonKvp(sb, "flagmode", s_warpCatalogDoorFlagMode); sb.Append(',');
                AppendJsonKvp(sb, "flag", s_warpCatalogDoorFlag); sb.Append(',');
                AppendJsonKvp(sb, "union01", s_warpCatalogDoorUnion01); sb.Append(',');
                AppendJsonKvp(sb, "union03", s_warpCatalogDoorUnion03); sb.Append(',');
                AppendJsonKvp(sb, "union04", s_warpCatalogDoorUnion04); sb.Append(',');
                AppendJsonKvp(sb, "exitNo", s_warpCatalogDoorExitNumber); sb.Append(',');
                AppendJsonKvp(sb, "evename", s_warpCatalogDoorEvename); sb.Append(',');
                AppendJsonKvp(sb, "union06", s_warpCatalogDoorUnion06); sb.Append(',');
                AppendJsonKvp(sb, "union07", s_warpCatalogDoorUnion07); sb.Append(',');
                AppendJsonKvp(sb, "union08", s_warpCatalogDoorUnion08); sb.Append(',');
                AppendJsonKvp(sb, "union09", s_warpCatalogDoorUnion09); sb.Append(',');
                AppendJsonKvp(sb, "union10", s_warpCatalogDoorUnion10); sb.Append(',');
                AppendJsonKvp(sb, "pos", s_warpCatalogDoorPos); sb.Append(',');
                AppendJsonKvp(sb, "camMode", s_warpCatalogDoorCamMode); sb.Append(',');
                AppendJsonKvp(sb, "camTbl", s_warpCatalogDoorCamTbl); sb.Append(',');
                AppendJsonKvp(sb, "cam", s_warpCatalogDoorCam); sb.Append(',');
                AppendJsonKvp(sb, "bgm", s_warpCatalogDoorBgm); sb.Append(',');
                AppendJsonKvp(sb, "foot", s_warpCatalogDoorFoot); sb.Append(',');
                AppendJsonKvp(sb, "afterFlag", s_warpCatalogDoorAfterFlag); sb.Append(',');
                AppendJsonKvp(sb, "afterScr", s_warpCatalogDoorAfterScr);
                sb.Append("},");

                sb.Append("\"terminal\":{");
                AppendJsonKvp(sb, "seen", s_warpCatalogSawTerminal ? 1 : 0); sb.Append(',');
                AppendJsonKvp(sb, "transportSeen", s_warpCatalogSawTerminalTransport ? 1 : 0); sb.Append(',');
                AppendJsonKvp(sb, "seq", s_warpCatalogTerminalSeq); sb.Append(',');
                AppendJsonKvp(sb, "callMode", s_warpCatalogTerminalCallMode); sb.Append(',');
                AppendJsonKvp(sb, "processStat", s_warpCatalogTerminalProcessStat); sb.Append(',');
                AppendJsonKvp(sb, "eventStat", s_warpCatalogTerminalEventStat); sb.Append(',');
                AppendJsonKvp(sb, "jumpNo", s_warpCatalogTerminalJumpNo); sb.Append(',');
                AppendJsonKvp(sb, "terminalType", s_warpCatalogTerminalType); sb.Append(',');
                AppendJsonKvp(sb, "terminalNo", s_warpCatalogTerminalNo); sb.Append(',');
                AppendJsonKvp(sb, "terminalCnt", s_warpCatalogTerminalCnt); sb.Append(',');
                AppendJsonKvp(sb, "transportSeq", s_warpCatalogTransportSeq); sb.Append(',');
                AppendJsonKvp(sb, "transportCallMode", s_warpCatalogTransportCallMode); sb.Append(',');
                AppendJsonKvp(sb, "transportProcessStat", s_warpCatalogTransportProcessStat); sb.Append(',');
                AppendJsonKvp(sb, "transportEventStat", s_warpCatalogTransportEventStat); sb.Append(',');
                AppendJsonKvp(sb, "transportJumpNo", s_warpCatalogTransportJumpNo); sb.Append(',');
                AppendJsonKvp(sb, "transportTerminalType", s_warpCatalogTransportType); sb.Append(',');
                AppendJsonKvp(sb, "transportTerminalNo", s_warpCatalogTransportNo); sb.Append(',');
                AppendJsonKvp(sb, "transportTerminalCnt", s_warpCatalogTransportCnt);
                sb.Append("}");

                sb.Append('}');
                return sb.ToString();
            }

            private static string GetCurrentRecordedKind()
            {
                if (s_warpCatalogSawTerminalTransport)
                    return "terminal";

                if (s_warpCatalogHitDoorIndex >= 0)
                    return "door";

                if (s_warpCatalogSawTerminal)
                    return "facility";

                return "non_door";
            }

            private static void AppendJsonKvp(StringBuilder sb, string key, int value)
            {
                sb.Append('"').Append(EscapeJson(key)).Append('"').Append(':');
                sb.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            private static void AppendJsonKvp(StringBuilder sb, string key, string value)
            {
                sb.Append('"').Append(EscapeJson(key)).Append('"').Append(':');
                sb.Append('"').Append(EscapeJson(value ?? "")).Append('"');
            }

            private static string EscapeJson(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return "";

                // Minimal JSON escaping for our strings.
                return s
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
            }

            public static bool TryGetWarpCatalogSummary(out string summary)
            {
                if (!s_warpCatalogArmed)
                {
                    summary = "idle";
                    return true;
                }

                float remain = Math.Max(0.0f, s_warpCatalogArmRealtimeDeadline - Time.realtimeSinceStartup);
                summary = $"ARMED src=F{s_warpCatalogSrcF} A{s_warpCatalogSrcA} S{s_warpCatalogSrcS} door={s_warpCatalogHitDoorIndex} terminalSeen={s_warpCatalogSawTerminal} terminalTransport={s_warpCatalogSawTerminalTransport} tRemain={remain.ToString("0.0", CultureInfo.InvariantCulture)}s";
                return true;
            }
        }
    }
}
