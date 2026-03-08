# 39 — Aisyo (resist) + badstatus surfaces

This tranche consolidates all **resist** and **status** seams that show up in the decompile wrappers.

---

## Aisyo (resist) lookups

### datAisyo: base table getter

`datAisyo` exposes a single `Get(...)` method in the wrapper layer:

| Native ptr name | Token |
|---|---|
| Get_Public_Static_UInt32_Int32_Int32_UInt32_0 | 100672540 |

From the ptr name, signature is:

- `UInt32 Get(Int32, Int32, UInt32)`

(Exact meaning of parameters is not proven yet; most likely `id/attr/flags` or similar.)

### datCalc: aisyo query helpers

| Native ptr name | Token |
|---|---|
| datGetAisyo_Public_Static_UInt32_datUnitWork_t_Int32_0 | 100672569 |
| datGetAisyoFlag_Public_Static_Int32_datUnitWork_t_Int32_0 | 100672570 |

### nbCalc: runtime aisyo helpers

| Native ptr name | Token |
|---|---|
| nbGetAisyoRitu_Public_Static_Single_Int32_Int32_Int32_0 | 100671085 |
| nbGetAisyoTable_Public_Static_Int32_Int32_0 | 100671100 |
| nbGetAisyo_Public_Static_UInt32_Int32_Int32_Int32_0 | 100671101 |
| nbGetVirtualAisyo_Public_Static_Int32_Int32_Int32_Int32_0 | 100671115 |

`nbGetAisyoRitu` returning `Single` strongly suggests a “ratio/multiplier” lookup is available at runtime.

---

## Status storage: datUnitWork_s.badstatus

`datUnitWork_s` is the unit work struct used per combatant.

### Fields (subset)

| Field | Field name |
|---|---|
| id | id |
| hp | hp |
| maxhp | maxhp |
| mp | mp |
| maxmp | maxmp |
| badstatus | badstatus |
| level | level |
| param | param |
| skillparam | skillparam |
| uniqueid | uniqueid |
| skillcnt | skillcnt |
| skill | skill |

`badstatus` is the primary status bitfield/storage surface worth mapping first.

---

## Status application + recovery (nbCalc)

These wrappers expose very direct status seams:

| Native ptr name | Token |
|---|---|
| nbGetKoukaBadDamage_Public_Static_UInt32_Int32_Int32_Int32_Single_Int32_0 | 100671091 |
| nbGetKoukaBadKaifuku_Public_Static_UInt32_Int32_Int32_Int32_Single_Int32_0 | 100671092 |
| nbSetBadStat_Public_Static_Void_Int32_Int32_Int32_0 | 100671096 |
| nbCheckBadKaifuku_Public_Static_Int32_datUnitWork_t_Int32_0 | 100671097 |
| nbGetBadStat_Public_Static_UInt32_Int32_0 | 100671098 |
| nbCheckBadCurse_Public_Static_Int32_Int32_0 | 100671099 |
| nbCheckBadPanicAction_Public_Static_Int32_datUnitWork_t_0 | 100671138 |
| nbCheckBadStatusGameOver_Public_Static_Int32_nbMainProcessData_t_0 | 100671139 |

Name-based notes:
- `nbSetBadStat` looks like a direct “apply status” helper.
- `nbCheckBadKaifuku` looks like “can recover?” or “recovery check” logic.
- `nbGetBadStat` likely reads the status bitfield.

---

## Skill-driven status: datNormalSkill_s

Status/ailment knobs in skill specs:

| Field | Field name |
|---|---|
| untargetbadstat | untargetbadstat |
| badtype | badtype |
| badlevel | badlevel |
| basstatus | basstatus |

These are the specific fields we’ll want on hand when later correlating:
- “skill ID → status effect type/strength”
- “resist → status application outcome”

