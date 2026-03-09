#nullable enable
using System;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Native debug menu selection capture/resolver
            // =========================================================

            private static bool TryCaptureNativeDebugMenuSelectionSnapshot(Type cmpTest, out NativeDebugMenuSelectionSnapshot snap, TextWriter? dbg = null)
            {
                // Default snapshot (best effort).
                snap = new NativeDebugMenuSelectionSnapshot(
                    NativeDebugMenuPhase.Unknown,
                    gActive1: TryReadStaticInt(cmpTest, "gActive1", -1),
                    gActive2: TryReadStaticInt(cmpTest, "gActive2", -1),
                    gSelSkill: TryReadStaticInt(cmpTest, "gSelSkill", -1),
                    gCursorSection: TryReadStaticInt(cmpTest, "gCursorSection", -1),
                    activeCursorIdx: -1,
                    listNums: -1,
                    shiftMax: -1,
                    index: -1,
                    shift: -1,
                    sel: -1,
                    rootWord: string.Empty,
                    rootWordKnown: false,
                    hasUnit: false,
                    unitRow: -1,
                    unitStockIndex: -1,
                    mappingKind: SkillDemonSlotMappingKind.Unknown,
                    unit: default(UnitResolveInfo),
                    hasSkill: false,
                    skillId: -1,
                    skillName: string.Empty
                );

                object? cursorArr = null;
                if (!TryReadStaticMember(cmpTest, "gCursorInfo", out cursorArr) || cursorArr == null)
                    return false;
                // Determine active cursor.
                // On your build, cmpTest.gCursorSection is the authoritative "currently active" cursor index.
                // Older logic (first ListNums>0) incorrectly picked cursor[0] (root menu) even when cursor[1]
                // was the active demon list / skill context.
                int activeIdx = -1;
                int listNums = 0;
                int shiftMax = 0;
                int index = 0;
                int shift = 0;

                int cursorLen = GetIl2CppRefArrayLength(cursorArr);

                int preferred = snap.gCursorSection;
                if (preferred >= 0 && preferred < cursorLen)
                {
                    var ci = GetIl2CppRefArrayItem(cursorArr, preferred);
                    if (ci != null)
                    {
                        var cp = GetProp(ci, "CursorPos");
                        int ln = GetIntProp(cp, "ListNums", 0);
                        if (ln > 0)
                        {
                            activeIdx = preferred;
                            listNums = ln;
                            shiftMax = GetIntProp(cp, "ShiftMax", 0);
                            index = GetIntProp(cp, "Index", 0);
                            shift = GetIntProp(cp, "Shift", 0);
                        }
                    }
                }

                // Fallback: pick the first cursor section that reports a non-zero ListNums.
                if (activeIdx < 0)
                {
                    for (int i = 0; i < cursorLen; i++)
                    {
                        var ci = GetIl2CppRefArrayItem(cursorArr, i);
                        if (ci == null)
                            continue;

                        var cp = GetProp(ci, "CursorPos");
                        int ln = GetIntProp(cp, "ListNums", 0);
                        if (ln > 0)
                        {
                            activeIdx = i;
                            listNums = ln;
                            shiftMax = GetIntProp(cp, "ShiftMax", 0);
                            index = GetIntProp(cp, "Index", 0);
                            shift = GetIntProp(cp, "Shift", 0);
                            break;
                        }
                    }
                }

                int sel = shift + index;
				// Root word inference (best effort).
                string rootWord = TryInferRootMenuWord(cmpTest, cursorArr) ?? string.Empty;
                bool rootKnown = !string.IsNullOrEmpty(rootWord);

                // Phase inference:
                // - We only lock into Skill phases if the root word looks like SKILL.
                // - Otherwise we treat it as Root/Unknown.
                NativeDebugMenuPhase phase = NativeDebugMenuPhase.Unknown;
                if (rootKnown)
                {
                    if (rootWord.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int a1 = snap.gActive1;
                        int a2 = snap.gActive2;
                        if (a1 == 1 && a2 == 0)
                            phase = NativeDebugMenuPhase.Skill_DemonSelect;
                        else if (a1 == 1 && a2 == 1)
                            phase = NativeDebugMenuPhase.Skill_List;
                        else
                            phase = NativeDebugMenuPhase.Root;
                    }
                    else
                    {
                        phase = NativeDebugMenuPhase.Root;
                    }
                }
                else
                {
                    phase = (snap.gActive1 == 0 && snap.gActive2 == 0) ? NativeDebugMenuPhase.Root : NativeDebugMenuPhase.Unknown;
                }

                // Attempt unit resolution when the active cursor looks like the demon list AND root looks like SKILL.
                bool hasUnit = false;
                UnitResolveInfo unit = default(UnitResolveInfo);
                int unitRow = -1;
                int unitStockIdx = -1;
                SkillDemonSlotMappingKind mappingKind = SkillDemonSlotMappingKind.Unknown;

                if (activeIdx >= 0 && listNums > 0 && listNums <= 20)
                {
                    // Even when gActive flags say we're not inside the subflow, hovering SKILL at root uses this same 9-row list in your build.
                    if (rootKnown && rootWord.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (TryGetDds3GlobalWorkObject(out object? gbwkObj) && gbwkObj != null)
                        {
                            mappingKind = DetectSkillDemonSlotMappingKind(gbwkObj, dbg);

                            if (sel >= 0 && sel < listNums)
                            {
                                unitRow = sel;
                                if (TryResolveSkillDemonSlot(gbwkObj, unitRow, mappingKind, out unitStockIdx, out unit))
                                    hasUnit = true;
                            }
                            else
                            {
                                unitRow = sel;
                            }
                    }
                    }
                }

                // Skill highlight resolution: Skill_id is the true highlighted skill id when gActive1=1 gActive2=1.
                bool hasSkill = false;
                int skillId = TryReadStaticInt(cmpTest, "Skill_id", -1);
                string skillName = string.Empty;
                if (skillId >= 0)
                {
                    hasSkill = true;
                    if (TryGetSkillNameBestEffort(skillId, 0, out string tmp))
                        skillName = tmp;
                    else
                        skillName = string.Empty;
                }

                snap = new NativeDebugMenuSelectionSnapshot(
                    phase,
                    gActive1: snap.gActive1,
                    gActive2: snap.gActive2,
                    gSelSkill: snap.gSelSkill,
                    gCursorSection: snap.gCursorSection,
                    activeCursorIdx: activeIdx,
                    listNums: listNums,
                    shiftMax: shiftMax,
                    index: index,
                    shift: shift,
                    sel: sel,
                    rootWord: rootWord,
                    rootWordKnown: rootKnown,
                    hasUnit: hasUnit,
                    unitRow: unitRow,
                    unitStockIndex: unitStockIdx,
                    mappingKind: mappingKind,
                    unit: unit,
                    hasSkill: hasSkill,
                    skillId: skillId,
                    skillName: skillName
                );

                return true;
            }

            private static SkillDemonSlotMappingKind DetectSkillDemonSlotMappingKind(object gbwkObj, TextWriter? dbg)
            {
                try
                {
                    int stockCnt = TryReadInt32(gbwkObj, "stockcnt", -1);
                    int maxStock = TryReadInt32(gbwkObj, "maxstock", -1);

                    int stocklistLen = -1;
                    if (TryGetInstanceMemberValue(gbwkObj, "stocklist", out object? stocklistArr) && stocklistArr != null)
                    {
                        if (TryGetLengthOrCount(stocklistArr, out int len, out _))
                            stocklistLen = len;
                    }

                    bool looksLikeCntPattern = (stockCnt > 0 && maxStock >= 0 && stockCnt == maxStock + 1);
                    bool firstIsProtag = (TryGetIntArrayAt(gbwkObj, "stocklist", 0, out int firstUwIdx) && firstUwIdx == 0);

                    // If stocklist is at least stockCnt long and its first entry is unitwork[0], treat it as "protag included".
                    if (looksLikeCntPattern && firstIsProtag && stocklistLen >= stockCnt && stockCnt > 0)
                        return SkillDemonSlotMappingKind.Stocklist0IsProtag;

                    // If we can't read length, still prefer the protag-included mapping when the pattern matches.
                    if (looksLikeCntPattern && firstIsProtag && stocklistLen < 0)
                        return SkillDemonSlotMappingKind.Stocklist0IsProtag;
                }
                catch (Exception ex)
                {
                    dbg?.WriteLine($"[WARN] DetectSkillDemonSlotMappingKind failed: {ex.GetType().Name}: {ex.Message}");
                }

                return SkillDemonSlotMappingKind.ProtagPlusStocklistMinus1;
            }


            private static bool TryResolveSkillDemonSlot(object gbwkObj, int row, SkillDemonSlotMappingKind kind, out int stockIdxUsed, out UnitResolveInfo info)
            {
                stockIdxUsed = -1;
                info = default(UnitResolveInfo);

                int stockCnt = TryReadInt32(gbwkObj, "stockcnt", -1);
                int rowLimit = (stockCnt > 0) ? stockCnt : 9;
                if (row < 0 || row >= rowLimit)
                    return false;

                // Mapping: row directly maps to stocklist[row] (stocklist includes protagonist at [0]).
                // We still keep a guarded fallback in case stocklist is shorter than expected on a given build/state.
                if (kind == SkillDemonSlotMappingKind.Stocklist0IsProtag)
                {
                    stockIdxUsed = row;
                    if (TryResolveUnitFromStocklistIndex(gbwkObj, stockIdxUsed, out info))
                        return true;

                    // Fallback (treat as legacy) if stocklist[row] can't be read.
                    if (row == 0)
                    {
                        stockIdxUsed = -1;
                        return TryResolveUnitFromUnitworkIndex(gbwkObj, 0, out info);
                    }

                    stockIdxUsed = row - 1;
                    return TryResolveUnitFromStocklistIndex(gbwkObj, stockIdxUsed, out info);
                }

                // Mapping B (legacy fallback): protagonist is implicit (unitwork[0]) and stocklist begins at row 1.
                if (row == 0)
                {
                    stockIdxUsed = -1;
                    return TryResolveUnitFromUnitworkIndex(gbwkObj, 0, out info);
                }

                stockIdxUsed = row - 1;
                return TryResolveUnitFromStocklistIndex(gbwkObj, stockIdxUsed, out info);
            }



            private static void WriteNativeDebugMenuSelectionSnapshot(TextWriter w, NativeDebugMenuSelectionSnapshot s)
            {
                w.WriteLine("--- Native Debug Menu Selection Snapshot (best-effort) ---");
                w.WriteLine($"phase={s.Phase}  rootWord={(s.RootWordKnown ? s.RootWord : "(unknown)")}  gActive1={s.gActive1} gActive2={s.gActive2} gSelSkill={s.gSelSkill} gCursorSection={s.gCursorSection}");
                w.WriteLine($"cursor.activeIdx={s.ActiveCursorIdx}  listNums={s.ListNums} shiftMax={s.ShiftMax} index={s.Index} shift={s.Shift} sel={s.Sel}");

                if (s.HasUnit)
                {
                    string nm = !string.IsNullOrEmpty(s.Unit.NameTag) ? s.Unit.NameTag : "(no devil name)";
                    w.WriteLine($"unit.row={s.UnitRow}  mapping={s.UnitMappingKind}  stockIdxUsed={s.UnitStockIndex}");
                    w.WriteLine($"unit.unitworkIdx={s.Unit.UnitworkIndex} id={s.Unit.UnitId} lvl={s.Unit.Level} hp={s.Unit.HP}/{s.Unit.MaxHP} mp={s.Unit.MP}/{s.Unit.MaxMP} ptr=0x{s.Unit.Ptr:X} nameTag=\"{Safe(nm)}\" namecode=\"{Safe(s.Unit.NameCodeStr)}\" fullname=\"{Safe(s.Unit.FullNameCodeStr)}\"");
                    WriteUnitSkillsBestEffort(w, s.Unit.UnitworkIndex, s.HasSkill ? s.SkillId : -1, s.gSelSkill, s.Phase);

                }
                else
                {
                    w.WriteLine("unit=(unresolved)");
                }

                if (s.HasSkill)
                {
                    string sk = !string.IsNullOrEmpty(s.SkillName) ? s.SkillName : "<blank>";
                    w.WriteLine($"skill.id={s.SkillId}  name=\"{Safe(sk)}\"");
                }
                else
                {
                    w.WriteLine("skill=(unresolved)");
                }
            }


            // ---------------------------------------------------------
            // Resolve the currently highlighted demon's skill list
            // ---------------------------------------------------------
            private static void WriteUnitSkillsBestEffort(TextWriter w, int unitworkIdx, int highlightedSkillId, int gSelSkill, NativeDebugMenuPhase phase)
            {
                try
                {
                    if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                    {
                        w.WriteLine("unit.skills=(dds3GlobalWork unavailable)");
                        return;
                    }

                    if (!TryGetUnitworkObject(gbwkObj, unitworkIdx, out object? unitObj) || unitObj == null)
                    {
                        w.WriteLine($"unit.skills=(unitwork[{unitworkIdx}] unavailable)");
                        return;
                    }

                    int skillCnt = TryReadInt32(unitObj, "skillcnt", -1);

                    object? skillArrObj = null;
                    TryGetInstanceMemberValue(unitObj, "skill", out skillArrObj);

                    int skillArrLen = -1;
                    if (skillArrObj != null && TryGetLengthOrCount(skillArrObj, out int tmpLen, out _))
                        skillArrLen = tmpLen;

                    w.WriteLine($"unit.skills.skillcnt={skillCnt}  skillArr.len={skillArrLen}");

                    int n = 0;
                    if (skillCnt >= 0 && skillArrLen >= 0)
                        n = Math.Min(skillCnt, skillArrLen);
                    else if (skillArrLen >= 0)
                        n = Math.Min(skillArrLen, 16);
                    else if (skillCnt >= 0)
                        n = Math.Min(skillCnt, 16);
                    else
                        n = 8;

                    if (n <= 0)
                    {
                        w.WriteLine("unit.skills=(empty)");
                        return;
                    }

                    int slotGuess = -1;

                    for (int i = 0; i < n; i++)
                    {
                        if (!TryReadSkillIdFromArray(skillArrObj, i, out int sid))
                            continue;

                        string nm = string.Empty;
                        if (!TryGetSkillNameBestEffort(sid, 0, out nm))
                            nm = string.Empty;

                        if (slotGuess < 0 && highlightedSkillId >= 0 && sid == highlightedSkillId)
                            slotGuess = i;

                        w.WriteLine($"  unit.skills[{i}] id={sid}  name=\"{Safe(ShowBlankName(nm))}\"{(string.IsNullOrEmpty(nm) ? "  note=blank name" : "")}");
                    }

                    
                    // Skill editor semantics:
                    // - In Skill_List phase, gSelSkill behaves like the selected skill slot (0-based).
                    // - Outside Skill_List, gSelSkill/Skill_id can be stale; we only treat them as correlation.
                    bool interpretEditor = (phase == NativeDebugMenuPhase.Skill_List);

                    if (gSelSkill >= 0 && n > 0)
                    {
                        if (gSelSkill < n)
                        {
                            if (TryReadSkillIdFromArray(skillArrObj, gSelSkill, out int selSid))
                            {
                                string selNm = string.Empty;
                                if (!TryGetSkillNameBestEffort(selSid, 0, out selNm))
                                    selNm = string.Empty;

                                if (interpretEditor)
                                    w.WriteLine($"unit.skills.selSlot={gSelSkill}  currentId={selSid}  currentName=\"{Safe(ShowBlankName(selNm))}\"");
                                else
                                    w.WriteLine($"unit.skills.lastSelSlot={gSelSkill}  lastId={selSid}  lastName=\"{Safe(ShowBlankName(selNm))}\"  note=phase={phase}; gSelSkill tracks last-selected slot here.");

                                if (interpretEditor && highlightedSkillId >= 0)
                                {
                                    if (selSid == highlightedSkillId)
                                    {
                                        w.WriteLine($"skill.subphase=SlotHighlight  note=highlighted skill id matches current slot (gSelSkill={gSelSkill}).");
                                    }
                                    else
                                    {
                                        string candNm = string.Empty;
                                        if (!TryGetSkillNameBestEffort(highlightedSkillId, 0, out candNm))
                                            candNm = string.Empty;

                                        w.WriteLine($"skill.subphase=ReplacementPick  editSlot={gSelSkill}  slotCurrent={selSid}  candidate={highlightedSkillId}  candidateName=\"{Safe(ShowBlankName(candNm))}\"");
                                    }
                                }
                                else if (!interpretEditor && highlightedSkillId >= 0)
                                {
                                    // We still print the observed Skill_id later, but do not interpret it as an editor subphase here.
                                    w.WriteLine($"note=phase={phase}; skipping skill.subphase classification outside Skill_List (Skill_id may be stale/placeholder).");
                                }
                            }
                        }
                        else
                        {
                            w.WriteLine($"unit.skills.selSlot={gSelSkill}  note=gSelSkill out of range for current dump (n={n}). phase={phase}");
                        }
                    }

                    if (highlightedSkillId >= 0)
                    {
                        if (slotGuess >= 0)
                        {
                            if (interpretEditor)
                                w.WriteLine($"unit.skills.slotGuess={slotGuess}  reason=skill.id matches unit.skills[{slotGuess}]");
                            else
                                w.WriteLine($"unit.skills.slotGuess={slotGuess}  reason=skill.id matches unit.skills[{slotGuess}]  note=phase={phase}; Skill_id may be stale here.");
                        }
                        else
                        {
                            if (interpretEditor)
                                w.WriteLine($"unit.skills.slotGuess=(none)  note=skill.id {highlightedSkillId} not found in unit.skills[]; you may be on a reserved/blank entry, or in a different subphase.");
                            else
                                w.WriteLine($"unit.skills.slotGuess=(none)  note=phase={phase}; Skill_id {highlightedSkillId} not found in unit.skills[]. Skill_id may be stale/placeholder here.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"[WARN] unit.skills dump failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            private static bool TryGetUnitworkObject(object dds3GbwkObj, int uwIdx, out object? unitObj)
            {
                unitObj = null;

                if (uwIdx == int.MinValue || uwIdx < 0)
                    return false;

                try
                {
                    if (!TryGetInstanceMemberValue(dds3GbwkObj, "unitwork", out object? unitworkArr) || unitworkArr == null)
                        return false;

                    if (!TryGetLengthOrCount(unitworkArr, out int uwLen, out _))
                        return false;

                    if (uwIdx < 0 || uwIdx >= uwLen)
                        return false;

                    try { unitObj = TryGetIndexValue(unitworkArr, uwIdx); } catch { unitObj = null; }
                    return unitObj != null;
                }
                catch
                {
                    unitObj = null;
                    return false;
                }
            }

            private static bool TryReadSkillIdFromArray(object? arrObj, int idx, out int sid)
            {
                sid = -1;

                if (arrObj == null)
                    return false;

                try
                {
                    if (arrObj is Il2CppStructArray<int> ia)
                    {
                        if (idx < 0 || idx >= ia.Length)
                            return false;

                        sid = ia[idx];
                        return true;
                    }

                    object? v = null;
                    try { v = TryGetIndexValue(arrObj, idx); } catch { v = null; }

                    if (v == null)
                        return false;

                    if (v is int i32) { sid = i32; return true; }
                    if (v is short s16) { sid = s16; return true; }
                    if (v is ushort u16) { sid = u16; return true; }
                    if (v is byte u8) { sid = u8; return true; }

                    return false;
                }
                catch
                {
                    sid = -1;
                    return false;
                }
            }

        }
    }
}