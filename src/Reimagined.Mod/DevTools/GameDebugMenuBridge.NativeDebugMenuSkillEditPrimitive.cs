#nullable enable
using System;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass_B10: Native debug menu SKILL edit primitive (apply + undo)
            // =========================================================
            // Design intent:
            // - Small, conservative building block for long-term tooling.
            // - Fail-closed: only operates in Skill_List/ReplacementPick with a valid slot + candidate.
            // - Single-step undo buffer (also fail-closed).
            // - Best-effort only; never throws.

                        private readonly struct NativeSkillEditRecord
            {
                public readonly bool Valid;
                public readonly int UnitworkIndex;
                public readonly int UnitId;

                public readonly int SlotA;
                public readonly int BeforeA;
                public readonly int AfterA;

                public readonly int SlotB;
                public readonly int BeforeB;
                public readonly int AfterB;

                public bool HasB => SlotB >= 0;

                public NativeSkillEditRecord(
                    bool valid,
                    int unitworkIndex,
                    int unitId,
                    int slotA,
                    int beforeA,
                    int afterA,
                    int slotB,
                    int beforeB,
                    int afterB)
                {
                    Valid = valid;
                    UnitworkIndex = unitworkIndex;
                    UnitId = unitId;
                    SlotA = slotA;
                    BeforeA = beforeA;
                    AfterA = afterA;
                    SlotB = slotB;
                    BeforeB = beforeB;
                    AfterB = afterB;
                }
            }

            private const int SkillEditHistoryCap = 8;

            private static readonly NativeSkillEditRecord[] s_skillEditUndo = new NativeSkillEditRecord[SkillEditHistoryCap];
            private static readonly NativeSkillEditRecord[] s_skillEditRedo = new NativeSkillEditRecord[SkillEditHistoryCap];
            private static int s_skillEditUndoCount;
            private static int s_skillEditRedoCount;

            private static string? s_lastSkillEditSummary;

            internal static bool TryGetLastSkillEditSummary(out string summary)
            {
                summary = s_lastSkillEditSummary ?? string.Empty;
                return !string.IsNullOrEmpty(summary);
            }

            internal static void GetSkillEditHistoryCounts(out int undoCount, out int redoCount)
            {
                undoCount = s_skillEditUndoCount;
                redoCount = s_skillEditRedoCount;
            }

            private static void ClearRedoHistory()
            {
                s_skillEditRedoCount = 0;
            }

            private static void PushUndoHistory(in NativeSkillEditRecord rec)
            {
                if (!rec.Valid)
                    return;

                if (s_skillEditUndoCount < SkillEditHistoryCap)
                {
                    s_skillEditUndo[s_skillEditUndoCount++] = rec;
                    return;
                }

                // Drop the oldest (small cap, shift is fine).
                for (int i = 1; i < SkillEditHistoryCap; i++)
                    s_skillEditUndo[i - 1] = s_skillEditUndo[i];
                s_skillEditUndo[SkillEditHistoryCap - 1] = rec;
                s_skillEditUndoCount = SkillEditHistoryCap;
            }

            private static void PushRedoHistory(in NativeSkillEditRecord rec)
            {
                if (!rec.Valid)
                    return;

                if (s_skillEditRedoCount < SkillEditHistoryCap)
                {
                    s_skillEditRedo[s_skillEditRedoCount++] = rec;
                    return;
                }

                for (int i = 1; i < SkillEditHistoryCap; i++)
                    s_skillEditRedo[i - 1] = s_skillEditRedo[i];
                s_skillEditRedo[SkillEditHistoryCap - 1] = rec;
                s_skillEditRedoCount = SkillEditHistoryCap;
            }

            private static bool TryPopUndoHistory(out NativeSkillEditRecord rec)
            {
                if (s_skillEditUndoCount <= 0)
                {
                    rec = default;
                    return false;
                }

                rec = s_skillEditUndo[s_skillEditUndoCount - 1];
                s_skillEditUndoCount--;
                return true;
            }


            private static bool TryPeekUndoHistory(out NativeSkillEditRecord rec)
            {
                if (s_skillEditUndoCount <= 0)
                {
                    rec = default;
                    return false;
                }

                rec = s_skillEditUndo[s_skillEditUndoCount - 1];
                return true;
            }

            private static void PopUndoHistory()
            {
                if (s_skillEditUndoCount > 0)
                    s_skillEditUndoCount--;
            }

            private static bool TryPopRedoHistory(out NativeSkillEditRecord rec)
            {
                if (s_skillEditRedoCount <= 0)
                {
                    rec = default;
                    return false;
                }

                rec = s_skillEditRedo[s_skillEditRedoCount - 1];
                s_skillEditRedoCount--;
                return true;
            }


            private static bool TryPeekRedoHistory(out NativeSkillEditRecord rec)
            {
                if (s_skillEditRedoCount <= 0)
                {
                    rec = default;
                    return false;
                }

                rec = s_skillEditRedo[s_skillEditRedoCount - 1];
                return true;
            }

            private static void PopRedoHistory()
            {
                if (s_skillEditRedoCount > 0)
                    s_skillEditRedoCount--;
            }

            private static void RecordNewSkillEdit(in NativeSkillEditRecord rec)
            {
                PushUndoHistory(rec);
                ClearRedoHistory();
            }

            internal static bool TryApplyNativeSkillCandidateToSlot(out string summary, out string note)
            {
                summary = string.Empty;
                note = string.Empty;

                try
                {
                    if (!TryGetNativeDebugMenuSkillEditorState(out var st))
                    {
                        note = "native skill state unavailable (open native debug menu → SKILL)";
                        return false;
                    }

                    if (st.Phase != NativeDebugMenuPhase.Skill_List)
                    {
                        note = $"wrong phase ({st.Phase}); enter Skill_List";
                        return false;
                    }

                    if (st.Subphase != NativeDebugMenuSkillEditorSubphase.ReplacementPick)
                    {
                        note = $"wrong subphase ({st.Subphase}); enter ReplacementPick";
                        return false;
                    }

                    if (!st.HasUnit)
                    {
                        note = "no unit resolved (demon not selected?)";
                        return false;
                    }

                    if (!st.SlotValid || st.Slot < 0)
                    {
                        note = "invalid slot (gSelSkill out of range)";
                        return false;
                    }

                    int unitworkIndex = st.Unit.UnitworkIndex;
                    int unitId = st.Unit.UnitId;
                    int slot = st.Slot;
                    int cand = st.CandidateSkillId;

                    if (cand < 0)
                    {
                        note = "candidate unavailable";
                        return false;
                    }

                    if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                    {
                        note = "dds3GlobalWork unavailable";
                        return false;
                    }

                    if (!TryGetUnitworkObject(gbwkObj, unitworkIndex, out object? unitObj) || unitObj == null)
                    {
                        note = $"unitwork[{unitworkIndex}] unavailable";
                        return false;
                    }

                    if (!TryGetInstanceMemberValue(unitObj, "skill", out object? skillArrObj) || skillArrObj == null)
                    {
                        note = "unitwork.skill array unavailable";
                        return false;
                    }

                    // Snapshot BEFORE (analysis-grade artifact). Best-effort.
                    TryGetUnitSkillSnapshot(unitworkIndex, out var before);

                    if (!TryGetLengthOrCount(skillArrObj, out int arrLen, out _))
                    {
                        note = "unitwork.skill length unavailable";
                        return false;
                    }

                    if (slot < 0 || slot >= arrLen)
                    {
                        note = $"slot out of range (slot={slot} len={arrLen})";
                        return false;
                    }

                    if (!TryReadSkillIdFromArray(skillArrObj, slot, out int oldSlotSkill))
                    {
                        note = "failed to read current slot skill";
                        return false;
                    }

                    if (oldSlotSkill == cand)
                    {
                        note = "no-op (candidate already in slot)";
                        summary = $"skill edit: slot[{slot}] already {cand}";
                        s_lastSkillEditSummary = summary;
                        return true;
                    }

                    // Conservative duplicate handling: if candidate exists in another slot, swap.
                    int foundAt = -1;
                    int scanN = Math.Min(8, arrLen);
                    for (int i = 0; i < scanN; i++)
                    {
                        if (i == slot)
                            continue;
                        if (TryReadSkillIdFromArray(skillArrObj, i, out int sid) && sid == cand)
                        {
                            foundAt = i;
                            break;
                        }
                    }

                    if (foundAt >= 0)
                    {
                        // swap slot <-> foundAt
                        if (!TryWriteSkillIdToArray(skillArrObj, slot, cand) || !TryWriteSkillIdToArray(skillArrObj, foundAt, oldSlotSkill))
                        {
                            note = "write failed (swap)";
                            return false;
                        }

                        // Undo restores both slots to their prior values.
                        RecordNewSkillEdit(new NativeSkillEditRecord(true, unitworkIndex, unitId, slot, oldSlotSkill, cand, foundAt, cand, oldSlotSkill));

                        string candName = st.CandidateSkillName;
                        summary = $"skill edit: swap slot[{slot}] {oldSlotSkill}→{cand} ({candName}) with slot[{foundAt}]";
                        s_lastSkillEditSummary = summary;

                        MelonLogger.Msg($"[Reimagined] {summary} (unitwork={unitworkIndex})");

                        // Snapshot AFTER + emit event file.
                        TryGetUnitSkillSnapshot(unitworkIndex, out var after);
                        EmitSkillEditEventFile("candidate_swap", st, before, after, summary, note);
                        return true;
                    }
                    else
                    {
                        if (!TryWriteSkillIdToArray(skillArrObj, slot, cand))
                        {
                            note = "write failed";
                            return false;
                        }

                        // Undo restores this slot to its prior value.
                        RecordNewSkillEdit(new NativeSkillEditRecord(true, unitworkIndex, unitId, slot, oldSlotSkill, cand, -1, 0, 0));

                        string candName = st.CandidateSkillName;
                        summary = $"skill edit: set slot[{slot}] {oldSlotSkill}→{cand} ({candName})";
                        s_lastSkillEditSummary = summary;

                        MelonLogger.Msg($"[Reimagined] {summary} (unitwork={unitworkIndex})");

                        // Snapshot AFTER + emit event file.
                        TryGetUnitSkillSnapshot(unitworkIndex, out var after);
                        EmitSkillEditEventFile("candidate_set", st, before, after, summary, note);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    note = "exception: " + ex.GetType().Name;
                    return false;
                }
            }

            internal static bool TryUndoLastNativeSkillEdit(out string summary, out string note)
            {
                summary = string.Empty;
                note = string.Empty;

                try
                {
                    if (!TryPeekUndoHistory(out var rec) || !rec.Valid)
                    {
                        note = "no undo available";
                        return false;
                    }

                    // Safety: require that the native debug menu SKILL state is available and that the
                    // currently resolved unit matches the unit that was edited.
                    if (!TryGetNativeDebugMenuSkillEditorState(out var st) || !st.HasUnit)
                    {
                        note = "undo requires native debug menu → SKILL with the edited demon resolved";
                        return false;
                    }

                    if (st.Unit.UnitworkIndex != rec.UnitworkIndex || st.Unit.UnitId != rec.UnitId)
                    {
                        note = $"undo target mismatch (expected unitwork={rec.UnitworkIndex} unitId={rec.UnitId}, current unitwork={st.Unit.UnitworkIndex} unitId={st.Unit.UnitId})";
                        return false;
                    }

                    if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                    {
                        note = "dds3GlobalWork unavailable";
                        return false;
                    }

                    if (!TryGetUnitworkObject(gbwkObj, rec.UnitworkIndex, out object? unitObj) || unitObj == null)
                    {
                        note = $"unitwork[{rec.UnitworkIndex}] unavailable";
                        return false;
                    }

                    if (!TryGetInstanceMemberValue(unitObj, "skill", out object? skillArrObj) || skillArrObj == null)
                    {
                        note = "unitwork.skill array unavailable";
                        return false;
                    }

                    // Snapshot BEFORE undo.
                    TryGetUnitSkillSnapshot(rec.UnitworkIndex, out var before);

                    if (!TryGetLengthOrCount(skillArrObj, out int arrLen, out _))
                    {
                        note = "unitwork.skill length unavailable";
                        return false;
                    }

                    if (rec.SlotA < 0 || rec.SlotA >= arrLen)
                    {
                        note = "undo slotA out of range";
                        return false;
                    }

                    if (!TryWriteSkillIdToArray(skillArrObj, rec.SlotA, rec.BeforeA))
                    {
                        note = "undo write failed (slotA)";
                        return false;
                    }

                    if (rec.HasB)
                    {
                        if (rec.SlotB >= arrLen)
                        {
                            note = "undo slotB out of range";
                            return false;
                        }

                        if (!TryWriteSkillIdToArray(skillArrObj, rec.SlotB, rec.BeforeB))
                        {
                            note = "undo write failed (slotB)";
                            return false;
                        }
                    }

                    // Move record: undo-stack -> redo-stack
                    PopUndoHistory();
                    PushRedoHistory(rec);

                    summary = rec.HasB
                        ? $"skill edit: undo restore slot[{rec.SlotA}] and slot[{rec.SlotB}]"
                        : $"skill edit: undo restore slot[{rec.SlotA}]";

                    s_lastSkillEditSummary = summary;
                    MelonLogger.Msg($"[Reimagined] {summary} (unitwork={rec.UnitworkIndex})");

                    // Snapshot AFTER undo + emit event file.
                    TryGetUnitSkillSnapshot(rec.UnitworkIndex, out var after);
                    EmitSkillEditEventFile("undo", st, before, after, summary, note);
                    return true;
                }
                catch (Exception ex)
                {
                    note = "exception: " + ex.GetType().Name;
                    return false;
                }
            }

            internal static bool TryRedoLastNativeSkillEdit(out string summary, out string note)
            {
                summary = string.Empty;
                note = string.Empty;

                try
                {
                    if (!TryPeekRedoHistory(out var rec) || !rec.Valid)
                    {
                        note = "no redo available";
                        return false;
                    }

                    if (!TryGetNativeDebugMenuSkillEditorState(out var st) || !st.HasUnit)
                    {
                        note = "redo requires native debug menu → SKILL with the edited demon resolved";
                        return false;
                    }

                    if (st.Unit.UnitworkIndex != rec.UnitworkIndex || st.Unit.UnitId != rec.UnitId)
                    {
                        note = $"redo target mismatch (expected unitwork={rec.UnitworkIndex} unitId={rec.UnitId}, current unitwork={st.Unit.UnitworkIndex} unitId={st.Unit.UnitId})";
                        return false;
                    }

                    if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                    {
                        note = "dds3GlobalWork unavailable";
                        return false;
                    }

                    if (!TryGetUnitworkObject(gbwkObj, rec.UnitworkIndex, out object? unitObj) || unitObj == null)
                    {
                        note = $"unitwork[{rec.UnitworkIndex}] unavailable";
                        return false;
                    }

                    if (!TryGetInstanceMemberValue(unitObj, "skill", out object? skillArrObj) || skillArrObj == null)
                    {
                        note = "unitwork.skill array unavailable";
                        return false;
                    }

                    // Snapshot BEFORE redo.
                    TryGetUnitSkillSnapshot(rec.UnitworkIndex, out var before);

                    if (!TryGetLengthOrCount(skillArrObj, out int arrLen, out _))
                    {
                        note = "unitwork.skill length unavailable";
                        return false;
                    }

                    if (rec.SlotA < 0 || rec.SlotA >= arrLen)
                    {
                        note = "redo slotA out of range";
                        return false;
                    }

                    if (!TryWriteSkillIdToArray(skillArrObj, rec.SlotA, rec.AfterA))
                    {
                        note = "redo write failed (slotA)";
                        return false;
                    }

                    if (rec.HasB)
                    {
                        if (rec.SlotB >= arrLen)
                        {
                            note = "redo slotB out of range";
                            return false;
                        }

                        if (!TryWriteSkillIdToArray(skillArrObj, rec.SlotB, rec.AfterB))
                        {
                            note = "redo write failed (slotB)";
                            return false;
                        }
                    }

                    // Move record: redo-stack -> undo-stack
                    PopRedoHistory();
                    PushUndoHistory(rec);

                    summary = rec.HasB
                        ? $"skill edit: redo apply slot[{rec.SlotA}] and slot[{rec.SlotB}]"
                        : $"skill edit: redo apply slot[{rec.SlotA}]";

                    s_lastSkillEditSummary = summary;
                    MelonLogger.Msg($"[Reimagined] {summary} (unitwork={rec.UnitworkIndex})");

                    // Snapshot AFTER redo + emit event file.
                    TryGetUnitSkillSnapshot(rec.UnitworkIndex, out var after);
                    EmitSkillEditEventFile("redo", st, before, after, summary, note);
                    return true;
                }
                catch (Exception ex)
                {
                    note = "exception: " + ex.GetType().Name;
                    return false;
                }
            }

private static bool TryWriteSkillIdToArray(object arrObj, int idx, int sid)
            {
                try
                {
                    // NOTE: We intentionally avoid direct index assignment on Il2CppStructArray<T>
                    // because the indexer setter shape can vary across Il2CppInterop versions.
                    // Instead, we do bounds checks (when possible) and then use reflection-based
                    // setter paths that work for both Il2CppStructArray<T> and other array-like types.

                    if (arrObj is Il2CppStructArray<int> ia)
                    {
                        if (idx < 0 || idx >= ia.Length)
                            return false;
                        // fall through to reflection-based set
                    }

                    // System.Array
                    if (arrObj is Array a)
                    {
                        if (idx < 0 || idx >= a.Length)
                            return false;
                        a.SetValue(sid, idx);
                        return true;
                    }

                    // Indexer setter Item[int]
                    var t = arrObj.GetType();
                    var props = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    for (int i = 0; i < props.Length; i++)
                    {
                        var p = props[i];
                        if (p == null || !p.CanWrite)
                            continue;
                        var ip = p.GetIndexParameters();
                        if (ip.Length == 1 && ip[0].ParameterType == typeof(int))
                        {
                            p.SetValue(arrObj, sid, new object[] { idx });
                            return true;
                        }
                    }

                    // Common method: set_Item(int, int)
                    var mSetItem = t.GetMethod("set_Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(int), typeof(int) }, null);
                    if (mSetItem != null)
                    {
                        mSetItem.Invoke(arrObj, new object[] { idx, sid });
                        return true;
                    }

                    // Common method: Set(int, int)
                    var mSet = t.GetMethod("Set", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(int), typeof(int) }, null);
                    if (mSet != null)
                    {
                        mSet.Invoke(arrObj, new object[] { idx, sid });
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static void EmitSkillEditEventFile(
                string op,
                NativeDebugMenuSkillEditorState st,
                UnitSkillSnapshot before,
                UnitSkillSnapshot after,
                string summary,
                string note)
            {
                try
                {
                    Directory.CreateDirectory(DumpsDir);
                    string safeOp = string.IsNullOrEmpty(op) ? "edit" : op;
                    int uw = st.HasUnit ? st.Unit.UnitworkIndex : after.UnitworkIndex;

                    string path = Path.Combine(DumpsDir, $"skill_edit_{DateTime.Now:yyyyMMdd_HHmmss}_{safeOp}_u{uw}.txt");
                    using var w = new StreamWriter(path);

                    w.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    w.WriteLine($"Operation: {safeOp}");
                    if (st.Available)
                        w.WriteLine($"NativeState: {st.Phase} / {st.Subphase}");
                    if (st.HasUnit)
                        w.WriteLine($"Unit: unitworkIndex={st.Unit.UnitworkIndex} unitId={st.Unit.UnitId} name=\"{st.Unit.NameTag}\" lvl={st.Unit.Level} hp={st.Unit.HP}/{st.Unit.MaxHP} mp={st.Unit.MP}/{st.Unit.MaxMP}");
                    if (st.SlotValid)
                        w.WriteLine($"Slot: {st.Slot}  slotCurrent={st.SlotCurrentSkillId} \"{st.SlotCurrentSkillName}\"");
                    if (st.Subphase == NativeDebugMenuSkillEditorSubphase.ReplacementPick && st.CandidateSkillId >= 0)
                        w.WriteLine($"Candidate: {st.CandidateSkillId} \"{st.CandidateSkillName}\"");

                    w.WriteLine($"Summary: {summary}");

                    GetSkillEditHistoryCounts(out int hcUndo, out int hcRedo);
                    w.WriteLine($"History: undoCount={hcUndo} redoCount={hcRedo}");
                    if (!string.IsNullOrEmpty(note))
                        w.WriteLine($"Note: {note}");

                    w.WriteLine();
                    w.WriteLine("[Before]");
                    w.WriteLine($"skillcnt={before.SkillCnt} arrLen={before.SkillArrLen} slots={before.Slots.Length}");
                    for (int i = 0; i < before.Slots.Length; i++)
                    {
                        var s = before.Slots[i];
                        w.WriteLine($"  [{s.Slot}] id={s.SkillId} \"{s.SkillName}\"");
                    }

                    w.WriteLine();
                    w.WriteLine("[After]");
                    w.WriteLine($"skillcnt={after.SkillCnt} arrLen={after.SkillArrLen} slots={after.Slots.Length}");
                    for (int i = 0; i < after.Slots.Length; i++)
                    {
                        var s = after.Slots[i];
                        w.WriteLine($"  [{s.Slot}] id={s.SkillId} \"{s.SkillName}\"");
                    }

                    w.WriteLine();
                    w.WriteLine("[Diff]");
                    int dn = Math.Min(before.Slots.Length, after.Slots.Length);
                    bool any = false;
                    for (int i = 0; i < dn; i++)
                    {
                        if (before.Slots[i].SkillId != after.Slots[i].SkillId)
                        {
                            any = true;
                            w.WriteLine($"  slot[{i}] {before.Slots[i].SkillId}→{after.Slots[i].SkillId}");
                        }
                    }
                    if (!any)
                        w.WriteLine("  (no slot changes detected)");

                    // Keep a breadcrumb in the overlay log as well.
                    try
                    {
                        MelonLogger.Msg($"[Reimagined] skill edit event dump: {Path.GetFileName(path)}");
                    }
                    catch { }
                }
                catch
                {
                    // never throw from devtools
                }
            }
        }
    }
}
