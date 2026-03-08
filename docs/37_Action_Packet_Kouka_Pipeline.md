# 37 — Action → Packet → Kouka pipeline

This doc ties together the three core layers that “make battle happen”:

1) **Action execution** (`nbActionProcessData_t` + `nbActionProcess`)
2) **Packet creation** (`nbMakePacket`)
3) **Packet scheduling + execution** (`nbKoukaProcessData_t` + `nbKoukaProcess` + `nbKoukaPacket_t`)

Everything here is still decompile-derived: names, tokens, and field layouts.

---

## 1) Action execution state: nbActionProcessData_t

`nbActionProcessData_t` carries the per-action state and (critically) **press-turn bookkeeping**.

### Key fields

| Field | Field name |
|---|---|
| uniqueid | uniqueid |
| seq | seq |
| nextseq | nextseq |
| press | press |
| newpresstype | newpresstype |
| newpress_p | newpress_p |
| newpress_ten | newpress_ten |
| newaddpresstype | newaddpresstype |
| party | party |
| form | form |
| work | work |
| partyindex | partyindex |
| type | type |
| target | target |
| select | select |
| timer | timer |
| runerr | runerr |
| autoskill | autoskill |
| negotype | negotype |

---

## 2) nbActionProcess seams (selection)

These are high-value method seams for:
- logging action sequence transitions
- observing press-turn changes
- seeing when/why packets are emitted

| Native ptr name | Token |
|---|---|
| AddTimeListHp_Public_Static_Int32_byref_nbActionProcessData_t_Int32_Int32_0 | 100670809 |
| CheckTimeListDead_Public_Static_Int32_byref_nbActionProcessData_t_Int32_0 | 100670813 |
| CheckCounter_Public_Static_Int32_byref_nbActionProcessData_t_Int32_Int32_0 | 100670814 |
| SetDeadPacket_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670817 |
| SetPress_Public_Static_Void_byref_nbActionProcessData_t_Single_0 | 100670818 |
| SetNewPress_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670819 |
| SetAddPressPacket_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670823 |
| SetAddFlareChargePacket_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670824 |
| SetMissPacket_Public_Static_Void_byref_nbActionProcessData_t_Int32_Int32_Int32_Int32_Int32_0 | 100670830 |
| SetAllPacket_Public_Static_Void_byref_nbActionProcessData_t_Int32_Int32_Int32_Int32_Int32_Int32_Single_0 | 100670833 |
| SetTarget_Public_Static_Int32_byref_nbActionProcessData_t_0 | 100670834 |
| CheckTimeListDeadEnemyParty_Public_Static_Int32_byref_nbActionProcessData_t_Int32_0 | 100670837 |
| nbAddTimeListHp_Public_Static_Int32_byref_nbActionProcessData_t_Int32_Int32_0 | 100670838 |
| GetKoukaList_Public_Static_Void_byref_nbActionProcessData_t_Il2CppReferenceArray_1_Il2CppStructArray_1_SByte_UInt32_Int32_0 | 100670842 |
| SetTargetPlayer_Public_Static_Void_byref_nbActionProcessData_t_0 | 100670843 |
| SetTargetParty_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670844 |

**Name-based notes (verify later):**
- `SetPress` / `SetNewPress` / `SetAddPressPacket` suggest “press-turn delta” events are surfaced *during* action processing.
- `SetAllPacket`, `SetMissPacket`, `SetDeadPacket` indicate “results” are expressed as packets rather than direct side effects.

---

## 3) Packet creation: nbMakePacket

`nbMakePacket` is the “packet factory.” Many `nbAdd*Packet` methods return `nbKoukaPacket_t`.

### High-signal packet builders

| Native ptr name | Token |
|---|---|
| nbAddSkillEffectKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_byref_effBTLCutHandle_t_0 | 100671639 |
| nbAddSkillEffectKoukaPacket3_Public_Static_nbKoukaPacket_t_Int32_Int32_Il2CppStructArray_1_Byte_Int32_byref_nbFormation_t_0 | 100671640 |
| nbAddSkillEffectKoukaPacket2_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_byref_effBTL_t_Int32_byref_nbFormation_t_Int32_0 | 100671641 |
| nbAddSkillEffectKoukaPacket1_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_byref_effBTLCutHandle_t_0 | 100671642 |
| nbAddHitEffKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_byref_nbFormation_t_Int32_0 | 100671663 |
| nbAddBadEffKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_byref_nbFormation_t_UInt32_0 | 100671665 |
| nbAddNumEffKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_byref_nbFormation_t_Int32_Int32_Int32_0 | 100671666 |
| nbAddMojiEffKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_byref_nbFormation_t_Int32_0 | 100671667 |
| nbAddHpMpKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_byref_nbFormation_t_Int32_Int32_0 | 100671668 |
| nbAddPressPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Single_0 | 100671669 |
| nbAddNewPressPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_Int32_0 | 100671670 |
| nbAddBadStatKoukaPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_byref_nbFormation_t_UInt32_Int32_0 | 100671671 |

This provides a concrete catalog of **what kinds of packets exist** (press, hp/mp numbers, bad-status, skill effects, etc).

---

## 4) Packet schema: nbKoukaPacket_t

`nbKoukaPacket_t` is the common “effect unit of work.”

### Fields

| Field | Field name |
|---|---|
| startframe | startframe |
| framelength | framelength |
| type | type |
| flag | flag |
| time | time |
| uniqueid | uniqueid |
| next | next |
| prev | prev |
| form | form |
| unit | unit |
| party | party |
| partyno | partyno |
| cuteff | cuteff |
| data1 | data1 |
| data2 | data2 |
| v | v |
| startframeF | startframeF |
| framelengthF | framelengthF |

**Interpretation (name-based):**
- `type` likely dispatches to different `Run*` handlers in `nbKoukaProcess`.
- `startframe` / `framelength` imply packet execution is timeline-based (animation orchestration).
- `uniqueid` is a natural key for “all packets associated with this unit/action.”

---

## 5) Packet scheduling/execution: nbKoukaProcess

`nbKoukaProcess` appears to maintain packet lists and run them.

### High-value seams

| Native ptr name | Token |
|---|---|
| RunReturnKouka_Public_Static_Void_byref_nbKoukaProcessData_t_nbKoukaPacket_t_0 | 100671506 |
| nbGetKoukaProcessData_Public_Static_nbKoukaProcessData_t_0 | 100671507 |
| LinkKoukaPacket_Public_Static_Void_byref_nbKoukaProcessData_t_nbKoukaPacket_t_0 | 100671510 |
| UnlinkKoukaPacket_Public_Static_Void_byref_nbKoukaProcessData_t_byref_nbKoukaPacket_t_0 | 100671511 |
| nbAddKoukaPacket_Public_Static_nbKoukaPacket_t_byref_nbKoukaProcessData_t_Int32_Int32_Int32_0 | 100671512 |
| nbDelAllKoukaPacketType_Public_Static_Void_Int32_0 | 100671515 |
| RunSkillEffectKouka_Public_Static_Void_byref_nbKoukaProcessData_t_byref_nbKoukaPacket_t_0 | 100671522 |
| RunDamageMotionKouka_Public_Static_Void_byref_nbKoukaProcessData_t_byref_nbKoukaPacket_t_0 | 100671528 |
| RunPressKouka_Public_Static_Void_byref_nbKoukaProcessData_t_byref_nbKoukaPacket_t_0 | 100671538 |

Even without runtime, this gives a clear structural story:

- packets are linked/unlinked (`LinkKoukaPacket`, `UnlinkKoukaPacket`)
- packets can be added/deleted (`nbAddKoukaPacket`, `nbDelAllKoukaPacketType`, etc)
- packets execute through specialized `Run*` handlers (`RunSkillEffectKouka`, `RunDamageMotionKouka`, ...)

---

## Practical uses for modding

Without writing any new code yet, this map already suggests “where to look” for common mod goals:

- **Press-turn behavior** → `nbActionProcess` + `nbMakePacket` press packet builders
- **Damage/status application** → `nbCalc` formulas + packet emission + kouka execution
- **Battle UI feedback** → packets that create “number effects” + `nbPanelProcess` gauge functions (see doc 41)

