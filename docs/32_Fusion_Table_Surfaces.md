# 32 — Fusion Data Tables: `fclCombineTable` Surfaces (v6)

This tranche drills into the **data tables** surfaced by `Il2Cpp/fclCombineTable.cs` and the **typed record schemas** that show up directly in the decompile.  
Anything stored as `Il2CppStructArray<byte>` / `Il2CppReferenceArray<Il2CppStructArray<byte>>` is a **packed binary table**: we can *name and locate it*, but its row layout isn’t expressed in these wrappers.

## 1) Static table inventory (`fclCombineTable`)

These are all `public unsafe static ...` properties on `fclCombineTable`.

| property | property_type | getter | setter |
| --- | --- | --- | --- |
| fclAccidentFailTbl | Il2CppStructArray<ushort> | 0x06001F01 (100671233) | 0x06001F02 (100671234) |
| fclActParamTbl | Il2CppStructArray<fclCombineTable.fclActParamTbl_t> | 0x06001F03 (100671235) | 0x06001F04 (100671236) |
| fclBirthSpiritTbl | Il2CppReferenceArray<fclCombCom_t> | 0x06001F05 (100671237) | 0x06001F06 (100671238) |
| fclCombineCurseTbl | Il2CppReferenceArray<Il2CppStructArray<byte>> | 0x06001F09 (100671241) | 0x06001F0A (100671242) |
| fclCombineManinKagTbl | Il2CppReferenceArray<Il2CppStructArray<byte>> | 0x06001F0B (100671243) | 0x06001F0C (100671244) |
| fclCombineManinRaceTbl | Il2CppStructArray<byte> | 0x06001F0D (100671245) | 0x06001F0E (100671246) |
| fclCombineManinTbl | Il2CppReferenceArray<Il2CppStructArray<ushort>> | 0x06001F0F (100671247) | 0x06001F10 (100671248) |
| fclCombineTbl | Il2CppReferenceArray<Il2CppStructArray<byte>> | 0x06001F07 (100671239) | 0x06001F08 (100671240) |
| fclEventDevilTbl | Il2CppReferenceArray<fclEventDevil_t> | 0x06001F11 (100671249) | 0x06001F12 (100671250) |
| fclExtPatternTbl | Il2CppReferenceArray<fclCombCom_t> | 0x06001F13 (100671251) | 0x06001F14 (100671252) |
| fclIkenieExtPatternDev2Tbl | Il2CppReferenceArray<fclIkenieExtPatternDev2_t> | 0x06001F19 (100671257) | 0x06001F1A (100671258) |
| fclIkenieExtPatternDevRaceTbl | Il2CppReferenceArray<fclIkenieExtPatternDevRace_t> | 0x06001F1B (100671259) | 0x06001F1C (100671260) |
| fclIkenieExtPatternDevTbl | Il2CppReferenceArray<fclIkenieExtPatternDev_t> | 0x06001F17 (100671255) | 0x06001F18 (100671256) |
| fclIkenieExtPatternTbl | Il2CppReferenceArray<fclIkenieExtPattern_t> | 0x06001F15 (100671253) | 0x06001F16 (100671254) |
| fclRankUpDownTbl | Il2CppReferenceArray<fclRankTbl_t> | 0x06001F1D (100671261) | 0x06001F1E (100671262) |
| fclSpiritParamUpTbl | Il2CppReferenceArray<fclCombSpirit_t> | 0x06001F1F (100671263) | 0x06001F20 (100671264) |

### Notes
- Tables with `Il2CppReferenceArray<Il2CppStructArray<byte>>` likely represent **variable-length or multi-table** packed data.
- `fclActParamTbl` is the only table here with an explicitly defined record type (`fclActParamTbl_t`).

## 2) Record schema: `fclActParamTbl_t`

This struct is declared inside `fclCombineTable` and is directly indexable from `fclActParamTbl`.

| struct | offset | field | type |
| --- | --- | --- | --- |
| fclActParamTbl_t | 0 | PosMode | byte |
| fclActParamTbl_t | 1 | MotionNo | byte |
| fclActParamTbl_t | 4 | EffOfs | float |
| fclActParamTbl_t | 8 | EffEraseSpd | float |
| fclActParamTbl_t | 12 | Radius | float |
| fclActParamTbl_t | 16 | Nums | int |

## 3) Typed “combine” record schemas (non-packed tables)

These are separate types from `Il2Cppfacility_H` that appear as typed tables in `fclCombineTable`:

- `fclCombCom_t` — used by `fclBirthSpiritTbl` and `fclExtPatternTbl`
- `fclCombSpirit_t` — used by `fclSpiritParamUpTbl`
- `fclEventDevil_t` — used by `fclEventDevilTbl`
- `fclRankTbl_t` / `fclRankBase_t` — used by `fclRankTbl`

See: `data/part6_*_props.csv` for exact getters/setters and token IDs.

## 4) Practical takeaway for modding (doc-only)

If you want to change fusion behavior without decoding packed binary tables, prefer **native query seams** in `fclCombineCalcCore`:
- Rank logic: `cmbGetRankUpDown*` (uses `fclRankUpDownTbl` indirectly)
- Accident logic: `cmbGetAccidentFailDevil` / `cmbChkAccident*`
- Inheritance logic: `cmbGetKeisyo*`, `cmbCalcTotalKeisyoSkillRate`, `cmbRndGetSkill` (covered in doc 34)

Those seams give you “behavioral” leverage without needing to reverse the raw table formats first.
