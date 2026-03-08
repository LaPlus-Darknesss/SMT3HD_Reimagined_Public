# 33 — Fusion Working State: `cmbGlobalWork_t` + `cmbResultTable_t` (v6)

This tranche maps the **working structs** that back the fusion UI + calculations.

## 1) How to get the global fusion work

`Il2Cpp/fclCombineInit.cs` exposes the global work pointer as a static:

- `fclCombineInit.CMB_GBWK : cmbGlobalWork_t`  

The full static list is exported at: `data/part6_fclCombineInit_static_props.csv`.

## 2) `cmbResultTable_t` schema

This is the “results grid” produced by fusion calculations: IDs, unit pointers, and inheritance lists.

| property | property_type | getter | setter |
| --- | --- | --- | --- |
| flag | sbyte | 0x06008395 (100696981) | 0x06008396 (100696982) |
| table | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppStructArray<sbyte>>> | 0x06008397 (100696983) | 0x06008398 (100696984) |
| b_id | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppStructArray<ushort>>> | 0x06008399 (100696985) | 0x0600839A (100696986) |
| devil | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppReferenceArray<datUnitWork_t>>> | 0x0600839B (100696987) | 0x0600839C (100696988) |
| keisyo | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppReferenceArray<fclKeisyoList_t>>> | 0x0600839D (100696989) | 0x0600839E (100696990) |
| keisyoAll | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppReferenceArray<fclKeisyoListAll_t>>> | 0x0600839F (100696991) | 0x060083A0 (100696992) |

### Reading guidance
- `table`: appears to be a signed-byte “cell” grid (likely flags / validity / accident markers).
- `b_id`: `ushort` birth IDs aligned with `table`.
- `devil`: per-cell `datUnitWork_t` references (actual instantiated result units).
- `keisyo` / `keisyoAll`: per-cell inheritance option lists.

Full export: `data/part6_cmbResultTable_props.csv`.

## 3) `cmbSelDev_t` (selection slot)

| owner_type | property | property_type | getter | setter |
| --- | --- | --- | --- | --- |
| cmbSelDev_t | Index | sbyte | 0x060083A4 (100696996) | 0x060083A5 (100696997) |
| cmbSelDev_t | CursorIndex | sbyte | 0x060083A6 (100696998) | 0x060083A7 (100696999) |
| cmbSelDev_t | ID | ushort | 0x060083A8 (100697000) | 0x060083A9 (100697001) |

This is the minimal selection payload (indexes + devil ID). The global work holds multiple of these to represent the currently selected inputs.

## 4) `cmbGlobalWork_t` — what’s in it

`cmbGlobalWork_t` is large. The complete property list is exported at:
- `data/part6_cmbGlobalWork_props.csv`

High-signal clusters you’ll care about long-term:

### Sequence / UI / process
- `SeqInfo` (camp sequence struct)  
- `cmdSelInfo` / cursor and menu navigation  
- many “draw” and “panel mover” helpers (UI presentation)

### Current selection (inputs)
- selected devil arrays + stock indices
- fields that look like “which slot is active” vs “how many are selected”

### Current result / accident
- result/birth devil IDs and/or pointers
- accident flags + accident result pointers

### Inheritance / skill selection UI
- `RandomSkillSw`
- `mSkillSelectCur`, `mSkillSelectPage`, `mSkillSelectCount`, `mSkillSelectMax`
- `mKeisyoList` (points at `fclKeisyoList_t` arrays)
- `mSkillConfCur`

These names line up with the dedicated inheritance methods documented in **doc 34**.

## 5) Practical takeaway for modding (doc-only)

- If you need a “read-only HUD overlay” of fusion state, `CMB_GBWK` and its `Result`/selection fields are the first place to look.
- If you need to alter behavior, use the calculation seams (doc 34) and keep state changes localized (don’t fight the UI loop).
