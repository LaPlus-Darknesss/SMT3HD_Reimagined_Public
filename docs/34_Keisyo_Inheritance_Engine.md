# 34 — Inheritance Engine (“Keisyo”) seams (v6)

This tranche focuses on **skill inheritance** (“Keisyo”) as exposed by `fclCombineCalcCore`.

## 1) Core data containers

`cmbResultTable_t` stores inheritance lists per result cell:

- `keisyo : ... fclKeisyoList_t`
- `keisyoAll : ... fclKeisyoListAll_t`

The list element schemas:

| owner_type | property | property_type | getter | setter |
| --- | --- | --- | --- | --- |
| fclKeisyoList_t | SkillID | Il2CppStructArray<ushort> | 0x06008385 (100696965) | 0x06008386 (100696966) |
| fclKeisyoList_t | SkillCnt | sbyte | 0x06008387 (100696967) | 0x06008388 (100696968) |
| fclKeisyoListAll_t | SkillID | Il2CppStructArray<ushort> | 0x0600838E (100696974) | 0x0600838F (100696975) |
| fclKeisyoListAll_t | SkillCnt | sbyte | 0x06008390 (100696976) | 0x06008391 (100696977) |
| cmbKeisyoSort_t | KeisyoSkill | ushort | 0x0600861C (100697628) | 0x0600861D (100697629) |
| cmbKeisyoSort_t | KeiAttr | byte | 0x0600861E (100697630) | 0x0600861F (100697631) |

(Full export: `data/part6_keisyo_types_props.csv`)

## 2) High-signal inheritance methods in `fclCombineCalcCore`

These are the best “behavior seams” we can see *without* decoding packed tables.

| token | token_dec | method | signature |
| --- | --- | --- | --- |
| 0x06001FB2 | 100671410 | cmbGetKeisyoSkillEndIndex | public unsafe static sbyte cmbGetKeisyoSkillEndIndex(datUnitWork_t pStock) |
| 0x06001FC5 | 100671429 | cmbAddKeisyoSkillFromSkill | public unsafe static void cmbAddKeisyoSkillFromSkill(datUnitWork_t pDstStock, datUnitWork_t pSrcStock) |
| 0x06001FC6 | 100671430 | cmbDeleteKeisyoSkill | public unsafe static void cmbDeleteKeisyoSkill(datUnitWork_t pStock, byte Index) |
| 0x06001FC7 | 100671431 | cmbMoveKeisyoSkillToSkill | public unsafe static sbyte cmbMoveKeisyoSkillToSkill(ushort KeisyoSkillID, datUnitWork_t pStock) |
| 0x06001FCA | 100671434 | cmbChkKeisyoSkillOwner | public unsafe static sbyte cmbChkKeisyoSkillOwner(ushort SkillID, datUnitWork_t pStock) |
| 0x06001FCB | 100671435 | cmbChkKeisyoSkillNums | public unsafe static sbyte cmbChkKeisyoSkillNums(datUnitWork_t pStock) |
| 0x06001FD0 | 100671440 | cmbClearKeisyoSkill | public unsafe static void cmbClearKeisyoSkill(datUnitWork_t pStock) |
| 0x06001FD6 | 100671446 | cmbCalcTotalKeisyoSkillRate | public unsafe static int cmbCalcTotalKeisyoSkillRate(datUnitWork_t pStock) |
| 0x06001FD7 | 100671447 | cmbRndGetKeisyoSkill | public unsafe static ushort cmbRndGetKeisyoSkill(datUnitWork_t pStock) |
| 0x06001FD8 | 100671448 | cmbRndAddKeisyoSkillToSkill | public unsafe static void cmbRndAddKeisyoSkillToSkill(datUnitWork_t pDevil1, datUnitWork_t pDevil2, datUnitWork_t pSacrifice, datUnitWork_t pStock, fclKeisyoList_t pKeisyoBuf, ref fclKeisyoListAll_t pKeisyoAllBuf) |
| 0x06001FD9 | 100671449 | cmbSetKeisyoListBuf | public unsafe static void cmbSetKeisyoListBuf(fclKeisyoList_t pKeisyoList) |
| 0x06001FDA | 100671450 | GetcmbSetKeisyoListBuf | public unsafe static fclKeisyoList_t GetcmbSetKeisyoListBuf() |
| 0x06001FDB | 100671451 | cmbSetKeisyoListAllBuf | public unsafe static void cmbSetKeisyoListAllBuf(fclKeisyoListAll_t pKeisyoListAll) |
| 0x06001FDC | 100671452 | GetcmbSetKeisyoListAllBuf | public unsafe static fclKeisyoListAll_t GetcmbSetKeisyoListAllBuf() |
| 0x06001FDD | 100671453 | cmbChkLastKeisyoSkill | public unsafe static sbyte cmbChkLastKeisyoSkill(ushort SkillID) |

Also relevant (skill list helpers, non-`Keisyo`-named):

- `cmbAddSkill(ushort SkillID, datUnitWork_t pStock)`
- `cmbChkSkillOwner(ushort SkillID, datUnitWork_t pStock)`
- `cmbClearSkill(datUnitWork_t pStock)`

## 3) What these seams likely correspond to

Based on naming + parameter types (treat as **inference** until validated by runtime behavior):

- `cmbGetKeisyoSkill*` / `cmbAddKeisyoSkillFromSkill`  
  Build the candidate inheritance pool from parents and attach it to the “birth” unit’s working list.

- `cmbCalcTotalKeisyoSkillRate` / `cmbRndGetSkill`  
  Compute weighted totals and randomly select inheritable skills.

- `cmbChkKeisyoSkillOwner` / `cmbChkKeisyoSkillNums` / `cmbChkKeisyoFull`  
  Ownership rules + max-count enforcement.

- `cmbDeleteKeisyoSkill` / `cmbMoveKeisyoSkillToSkill`  
  UI-driven selection: delete/move from candidate list into final skill set.

## 4) Practical takeaway for modding (doc-only)

If your long-term goal is to rework inheritance:
- Start with these methods: they’re already the “interfaces” the game uses.
- Keep structural outputs consistent (don’t break list counts / expected ordering), and prefer small deltas:
  - adjust rates
  - filter candidates
  - add “bonus” candidates
  - override RNG selection
