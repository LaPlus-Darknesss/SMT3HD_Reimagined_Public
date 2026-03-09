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
            // Native debug menu skill-editor state 
            // =========================================================
			
            internal enum NativeDebugMenuSkillEditorSubphase
            {
                Unknown = 0,
                NotInSkillList = 1,   // Phase != Skill_List, or missing data required to interpret as editor
                SlotHighlight = 2,    // highlighted id matches current slot id
                ReplacementPick = 3,  // highlighted id differs from current slot id (candidate selection list)
            }

            internal readonly struct NativeDebugMenuSkillEditorState
            {
                public readonly bool Available;

                public readonly NativeDebugMenuPhase Phase;
                public readonly NativeDebugMenuSkillEditorSubphase Subphase;

                public readonly bool HasUnit;
                public readonly UnitResolveInfo Unit;

                // gSelSkill in cmpTest. In Skill_List this behaves like the selected slot.
                // Outside Skill_List it tends to track the last slot you were on (stale).
                public readonly int Slot;
                public readonly bool SlotValid;

                public readonly int SlotCurrentSkillId;
                public readonly string SlotCurrentSkillName;

                // Observed highlight value from cmpTest (Skill_id) + resolved name (datSkillName).
                // Only interpreted as "highlighted skill" during Skill_List.
                public readonly int ObservedSkillId;
                public readonly string ObservedSkillName;

                // Populated only during ReplacementPick.
                public readonly int CandidateSkillId;
                public readonly string CandidateSkillName;

                public readonly string Note;

                public NativeDebugMenuSkillEditorState(
                    bool available,
                    NativeDebugMenuPhase phase,
                    NativeDebugMenuSkillEditorSubphase subphase,
                    bool hasUnit,
                    UnitResolveInfo unit,
                    int slot,
                    bool slotValid,
                    int slotCurrentSkillId,
                    string slotCurrentSkillName,
                    int observedSkillId,
                    string observedSkillName,
                    int candidateSkillId,
                    string candidateSkillName,
                    string note)
                {
                    Available = available;

                    Phase = phase;
                    Subphase = subphase;

                    HasUnit = hasUnit;
                    Unit = unit;

                    Slot = slot;
                    SlotValid = slotValid;

                    SlotCurrentSkillId = slotCurrentSkillId;
                    SlotCurrentSkillName = slotCurrentSkillName ?? string.Empty;

                    ObservedSkillId = observedSkillId;
                    ObservedSkillName = observedSkillName ?? string.Empty;

                    CandidateSkillId = candidateSkillId;
                    CandidateSkillName = candidateSkillName ?? string.Empty;

                    Note = note ?? string.Empty;
                }
            }

            

            internal readonly struct NativeDebugMenuUnitSkillSlot
            {
                public readonly int Slot;
                public readonly int SkillId;
                public readonly string SkillName;

                public NativeDebugMenuUnitSkillSlot(int slot, int skillId, string skillName)
                {
                    Slot = slot;
                    SkillId = skillId;
                    SkillName = skillName ?? string.Empty;
                }
            }

            internal readonly struct UnitSkillSnapshot
            {
                public readonly int UnitworkIndex;
                public readonly int SkillCnt;
                public readonly int SkillArrLen;
                public readonly NativeDebugMenuUnitSkillSlot[] Slots;
                public readonly string Note;

                public UnitSkillSnapshot(int unitworkIndex, int skillCnt, int skillArrLen, NativeDebugMenuUnitSkillSlot[] slots, string note)
                {
                    UnitworkIndex = unitworkIndex;
                    SkillCnt = skillCnt;
                    SkillArrLen = skillArrLen;
                    Slots = slots ?? Array.Empty<NativeDebugMenuUnitSkillSlot>();
                    Note = note ?? string.Empty;
                }
            }

            internal static bool TryGetUnitSkillSnapshot(int unitworkIndex, out UnitSkillSnapshot snap)
            {
                snap = default;

                try
                {
                    if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                        return false;

                    if (!TryGetUnitworkObject(gbwkObj, unitworkIndex, out object? unitObj) || unitObj == null)
                        return false;

                    int skillCnt = TryReadInt32(unitObj, "skillcnt", -1);

                    object? skillArrObj = null;
                    TryGetInstanceMemberValue(unitObj, "skill", out skillArrObj);

                    int arrLen = -1;
                    if (skillArrObj != null && TryGetLengthOrCount(skillArrObj, out int tmpLen, out _))
                        arrLen = tmpLen;

                    // SMT3 demons effectively have 8 skill slots; the backing array length is usually 8.
                    // We always snapshot 8 positions for stable diffs (even if skillcnt is smaller).
                    int n = 8;
                    var slots = new NativeDebugMenuUnitSkillSlot[n];

                    for (int i = 0; i < n; i++)
                    {
                        int sid = -1;
                        if (skillArrObj != null && arrLen > i && TryReadSkillIdFromArray(skillArrObj, i, out int tmpSid))
                            sid = tmpSid;

                        // Name resolution notes:
                        // - If we can resolve a name but it's an empty string, we display <blank>.
                        // - If we cannot resolve a name at all, we display <unknown:ID>.
                        string nm = string.Empty;
                        if (sid >= 0 && TryGetSkillNameBestEffort(sid, 0, out string tmpNm))
                        {
                            nm = ShowBlankName(tmpNm);
                        }
                        else if (sid >= 0)
                        {
                            nm = $"<unknown:{sid}>";
                        }

                        slots[i] = new NativeDebugMenuUnitSkillSlot(i, sid, nm);
                    }

                    snap = new UnitSkillSnapshot(unitworkIndex, skillCnt, arrLen, slots, "");
                    return true;
                }
                catch (Exception ex)
                {
                    snap = new UnitSkillSnapshot(unitworkIndex, -1, -1, Array.Empty<NativeDebugMenuUnitSkillSlot>(), "exception: " + ex.GetType().Name);
                    return true;
                }
            }

internal static bool TryGetNativeDebugMenuSkillEditorState(out NativeDebugMenuSkillEditorState state, TextWriter? dbg = null)
            {
                state = default;

                Type? cmpTest = null;
                try { cmpTest = FindTypeInLoadedAssemblies("Il2Cpp.cmpTest"); } catch { cmpTest = null; }

                if (cmpTest == null)
                {
                    state = new NativeDebugMenuSkillEditorState(
                        available: false,
                        phase: NativeDebugMenuPhase.Unknown,
                        subphase: NativeDebugMenuSkillEditorSubphase.Unknown,
                        hasUnit: false,
                        unit: default,
                        slot: -1,
                        slotValid: false,
                        slotCurrentSkillId: -1,
                        slotCurrentSkillName: "",
                        observedSkillId: -1,
                        observedSkillName: "",
                        candidateSkillId: -1,
                        candidateSkillName: "",
                        note: "cmpTest not found (native debug menu likely not loaded yet)");
                    return false;
                }

                if (!TryCaptureNativeDebugMenuSelectionSnapshot(cmpTest, out NativeDebugMenuSelectionSnapshot snap, dbg))
                {
                    state = new NativeDebugMenuSkillEditorState(
                        available: false,
                        phase: NativeDebugMenuPhase.Unknown,
                        subphase: NativeDebugMenuSkillEditorSubphase.Unknown,
                        hasUnit: false,
                        unit: default,
                        slot: -1,
                        slotValid: false,
                        slotCurrentSkillId: -1,
                        slotCurrentSkillName: "",
                        observedSkillId: -1,
                        observedSkillName: "",
                        candidateSkillId: -1,
                        candidateSkillName: "",
                        note: "failed to capture selection snapshot (best-effort resolver)");
                    return false;
                }

                // Pull "observed" skill highlight (Skill_id).
                int observedId = snap.HasSkill ? snap.SkillId : -1;
                string observedName = snap.HasSkill ? snap.SkillName : string.Empty;

                bool interpretEditor = (snap.Phase == NativeDebugMenuPhase.Skill_List);

                // Default to "not interpreted".
                var sub = interpretEditor ? NativeDebugMenuSkillEditorSubphase.Unknown : NativeDebugMenuSkillEditorSubphase.NotInSkillList;

                int slot = snap.gSelSkill;
                bool slotValid = false;

                int slotCurId = -1;
                string slotCurName = string.Empty;

                int candId = -1;
                string candName = string.Empty;

                string note = string.Empty;

                // Resolve slot current from unitwork skill array if available.
                bool hasUnit = snap.HasUnit;
                UnitResolveInfo unit = snap.Unit;

                object? gbwkObj = null;
                object? unitObj = null;
                object? skillArrObj = null;
                int skillArrLen = -1;

                try
                {
                    if (hasUnit && TryGetDds3GlobalWorkObject(out gbwkObj) && gbwkObj != null)
                    {
                        if (TryGetUnitworkObject(gbwkObj, unit.UnitworkIndex, out unitObj) && unitObj != null)
                        {
                            TryGetInstanceMemberValue(unitObj, "skill", out skillArrObj);

                            if (skillArrObj != null && TryGetLengthOrCount(skillArrObj, out int tmpLen, out _))
                                skillArrLen = tmpLen;
                        }
                        else
                        {
                            note = $"unitwork[{unit.UnitworkIndex}] unavailable";
                        }
                    }
                    else if (hasUnit)
                    {
                        note = "dds3GlobalWork unavailable";
                    }
                }
                catch (Exception ex)
                {
                    note = "exception while resolving unit skills: " + ex.GetType().Name;
                }

                if (slot >= 0 && skillArrLen >= 0 && slot < skillArrLen && skillArrObj != null)
                {
                    slotValid = true;

                    if (TryReadSkillIdFromArray(skillArrObj, slot, out int selSid))
                    {
                        slotCurId = selSid;

                        if (!TryGetSkillNameBestEffort(selSid, 0, out slotCurName))
                            slotCurName = string.Empty;
                    }
                }

                if (interpretEditor)
                {
                    // During Skill_List, if we can read slot and we have an observed highlight, classify.
                    if (slotValid && observedId >= 0)
                    {
                        if (slotCurId == observedId)
                        {
                            sub = NativeDebugMenuSkillEditorSubphase.SlotHighlight;
                        }
                        else
                        {
                            sub = NativeDebugMenuSkillEditorSubphase.ReplacementPick;
                            candId = observedId;
                            candName = observedName;
                        }
                    }
                    else
                    {
                        sub = NativeDebugMenuSkillEditorSubphase.Unknown;

                        if (!slotValid)
                            note = string.IsNullOrEmpty(note) ? "slot unavailable (gSelSkill out of range or skill array missing)" : note;

                        if (observedId < 0)
                            note = string.IsNullOrEmpty(note) ? "no observed Skill_id highlight" : note;
                    }
                }
                else
                {
                    // Not in Skill_List: do NOT treat observed skill id as current highlight; it's often stale/placeholder.
                    if (observedId >= 0)
                    {
                        string p = snap.Phase.ToString();
                        note = string.IsNullOrEmpty(note)
                            ? $"phase={p}; observed Skill_id may be stale/placeholder"
                            : note + $"; phase={p}; observed Skill_id may be stale/placeholder";
                    }
                }

                // Normalize names for display.
                slotCurName = ShowBlankName(slotCurName);
                observedName = ShowBlankName(observedName);
                candName = ShowBlankName(candName);

                state = new NativeDebugMenuSkillEditorState(
                    available: true,
                    phase: snap.Phase,
                    subphase: sub,
                    hasUnit: hasUnit,
                    unit: unit,
                    slot: slot,
                    slotValid: slotValid,
                    slotCurrentSkillId: slotCurId,
                    slotCurrentSkillName: slotCurName,
                    observedSkillId: observedId,
                    observedSkillName: observedName,
                    candidateSkillId: candId,
                    candidateSkillName: candName,
                    note: note);

                return true;
            }
        }
    }
}
