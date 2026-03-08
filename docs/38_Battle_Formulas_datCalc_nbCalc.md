# 38 — Battle formulas: datCalc (global) + nbCalc (runtime helpers)

This doc is a “formula surface map” — it does **not** claim exact arithmetic, but it pins down:

- which functions are responsible for which computations
- which structs feed those computations
- where resist/status multipliers live

This is the highest-ROI foundation for any difficulty/balance mod.

---

## datCalc: global battle math entry points

`datCalc` is a static class with many “datGet*” and “dat*” helpers.

### Base stat/derived power helpers (selection)

| Native ptr name | Token |
|---|---|
| datGetBaseMaxHp_Public_Static_Int32_datUnitWork_t_0 | 100672544 |
| datGetBaseMaxMp_Public_Static_Int32_datUnitWork_t_0 | 100672545 |
| datGetNormalAtkPow_Public_Static_Int32_datUnitWork_t_0 | 100672546 |
| datGetMagicHitPow_Public_Static_Int32_datUnitWork_t_0 | 100672547 |
| datGetDefPow_Public_Static_Int32_datUnitWork_t_0 | 100672548 |
| datGetSakePow_Public_Static_Int32_datUnitWork_t_0 | 100672549 |
| datGetBaseParam_Public_Static_Int32_datUnitWork_t_Int32_0 | 100672566 |

### Skill execution + costs

| Native ptr name | Token |
|---|---|
| datGetSkillKouka_Public_Static_Int32_Int32_Int32_datUnitWork_t_datUnitWork_t_0 | 100672556 |
| datGetSkillBadKouka_Public_Static_Int32_Int32_datUnitWork_t_datUnitWork_t_0 | 100672557 |
| datExecSkill_Public_Static_Int32_Int32_datUnitWork_t_datUnitWork_t_0 | 100672558 |
| datGetSkillCost_Public_Static_Int32_datUnitWork_t_Int32_0 | 100672612 |

### Resist lookup

| Native ptr name | Token |
|---|---|
| datGetAisyo_Public_Static_UInt32_datUnitWork_t_Int32_0 | 100672569 |
| datGetAisyoFlag_Public_Static_Int32_datUnitWork_t_Int32_0 | 100672570 |

### Boss press init

| Native ptr name | Token |
|---|---|
| datInitBossPress_Public_Static_Void_0 | 100672614 |

---

## nbCalc: battle-runtime calc helpers

`nbCalc` reads like the “battle runtime” calculation helper for effects, resist multipliers, and status.

### Hit / fail / on-hit effects

| Native ptr name | Token |
|---|---|
| GetFailpoint_Private_Static_Int16_Int32_0 | 100671079 |
| nbGetHitType_Public_Static_Int32_nbActionProcessData_t_Int32_Int32_Int32_0 | 100671080 |
| nbCheckHitEffect_Public_Static_UInt32_Int32_Int32_Int32_0 | 100671093 |
| nbGetCriticalPow_Public_Static_Single_Int32_Single_0 | 100671142 |

### Aisyo (resist) surfaces

| Native ptr name | Token |
|---|---|
| nbGetAisyoRitu_Public_Static_Single_Int32_Int32_Int32_0 | 100671085 |
| nbGetAisyoTable_Public_Static_Int32_Int32_0 | 100671100 |
| nbGetAisyo_Public_Static_UInt32_Int32_Int32_Int32_0 | 100671101 |
| nbGetVirtualAisyo_Public_Static_Int32_Int32_Int32_Int32_0 | 100671115 |

### HP/MP effect magnitudes (“kouka”)

| Native ptr name | Token |
|---|---|
| nbGetKoukaHp_Public_Static_Int32_Int32_Int32_Int32_Int32_Single_Int32_0 | 100671086 |
| nbGetKoukaMp_Public_Static_Int32_Int32_Int32_Int32_Int32_Single_Int32_0 | 100671087 |
| nbGetKoukaBadDamage_Public_Static_UInt32_Int32_Int32_Int32_Single_Int32_0 | 100671091 |
| nbGetKoukaBadKaifuku_Public_Static_UInt32_Int32_Int32_Int32_Single_Int32_0 | 100671092 |

### Badstatus helpers

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

### Hojo (buff/debuff) counters

| Native ptr name | Token |
|---|---|
| nbSetHojoCounter_Public_Static_Void_Int32_Int32_Int32_0 | 100671103 |
| nbGetHojoCounter_Public_Static_Int32_Int32_Int32_0 | 100671104 |
| nbGetHojoCounterMax_Public_Static_Int32_Int32_0 | 100671105 |
| nbGetHojoCounterMin_Public_Static_Int32_Int32_0 | 100671106 |
| nbSetHojoAddCounter_Public_Static_Int32_Int32_Int32_Int32_Int32_0 | 100671107 |
| nbSetHojoKouka_Public_Static_Int32_Int32_UInt32_Int32_Int32_0 | 100671108 |
| nbGetHojoRitu_Public_Static_Single_Int32_Int32_0 | 100671109 |

---

## Skill spec inputs: datNormalSkill_s

Many of the above computations can be tied back to `datNormalSkill_s` fields.

### High-value fields for battle math / targeting

| Field | Field name |
|---|---|
| koukatype | koukatype |
| costtype | costtype |
| cost | cost |
| costbase | costbase |
| targettype | targettype |
| targetarea | targetarea |
| targetrule | targetrule |
| targetrandom | targetrandom |
| hittype | hittype |
| hitlevel | hitlevel |
| hpn | hpn |
| mpn | mpn |
| hpbase | hpbase |
| mpbase | mpbase |
| badtype | badtype |
| badlevel | badlevel |
| criticalpoint | criticalpoint |
| failpoint | failpoint |
| magicbase | magicbase |
| magiclimit | magiclimit |

These fields are exactly the sort of knobs a difficulty mod often wants to reinterpret:
- `costtype/cost/costbase`
- hit and crit/fail (`hittype/hitlevel/criticalpoint/failpoint`)
- effect magnitude (`hpn/mpn/hpbase/mpbase`, `magicbase/magiclimit`)
- badstatus application (`badtype/badlevel`)

