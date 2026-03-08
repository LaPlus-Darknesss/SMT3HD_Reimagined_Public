# 44 — Battle target selection (nbTarSelProcess)

`nbTarSelProcess` is the battle **target selection** process: once a command implies a target rule (single enemy / all enemies / self / ally, etc), this process populates candidate targets, drives cursor navigation, and confirms the target into the active action context (`nbActionProcessData_t`).

---

## Process data (nbTarSelProcessData_t)

The process state is stored in `nbTarSelProcessData_t`.

| Field | Type |
|---|---|
| data | nbMainProcessData_t |
| act | nbActionProcessData_t |
| stat | byte |
| ctype | sbyte |
| crule | byte |
| carea | byte |
| nowcarea | byte |
| nowno | sbyte |
| nowform | sbyte |
| partyindex | short |
| friendcnt | sbyte |
| enemycnt | sbyte |
| friendno | Il2CppStructArray<sbyte> |
| enemyno | Il2CppStructArray<sbyte> |
| jyokyotime | int |
| time | int |
| alpha_a | int |
| alpha_b | int |
| scale_a | float |
| scale_b | float |

Name-based quick read:
- `ctype`, `crule`, `carea` look like the canonical **targeting parameters** for the selected action.
- `friendno[]` / `enemyno[]` plus `friendcnt` / `enemycnt` look like the candidate set.
- `nowform`, `nowno`, `partyindex` suggest “currently highlighted unit (form/slot)”.

Full field list export:
- `data/part8_struct_fields_battle_ui.csv` (filter `struct == nbTarSelProcessData_t`)

---

## Key seams (token surfaces)

| Native ptr name | Token |
|---|---|
| GetTarSelProcessData_Public_Static_nbTarSelProcessData_t_0 | 100672420 |
| nbInitTarSelProcess_Public_Static_Void_nbMainProcessData_t_0 | 100672440 |
| InitTarSelProcessData_Public_Static_nbTarSelProcessData_t_byref_nbMainProcessData_t_0 | 100672439 |
| nbTarSelProcess1_Public_Static_Object_dds3ProcessID_t_0 | 100672437 |
| nbSetTargetProcess_Public_Static_Int32_nbActionProcessData_t_Int32_Int32_Int32_Int32_0 | 100672428 |
| CursorSelect_Public_Static_Void_byref_nbTarSelProcessData_t_0 | 100672435 |
| CursorSelectAuto_Public_Static_Void_byref_nbTarSelProcessData_t_0 | 100672436 |
| SetCursor_Public_Static_Void_byref_nbTarSelProcessData_t_0 | 100672427 |
| CheckFriend_Public_Static_Int32_byref_nbTarSelProcessData_t_Int32_0 | 100672429 |
| CheckEnemy_Public_Static_Int32_byref_nbTarSelProcessData_t_Int32_0 | 100672430 |
| CheckFormCursor_Public_Static_Int32_Int32_Int32_Int32_Int32_Int32_0 | 100672425 |
| nbSetSelectOkMark_Public_Static_Int32_Int32_Int32_Int32_Int32_0 | 100672426 |
| OnOffTargetCursor_Public_Static_Void_byref_nbTarSelProcessData_t_0 | 100672432 |
| DispTargetHelp_Public_Static_Void_byref_nbTarSelProcessData_t_0 | 100672433 |
| MemoryCommand_Public_Static_Void_byref_nbTarSelProcessData_t_0 | 100672434 |
| nbGetTarSelStat_Public_Static_Int32_0 | 100672421 |
| nbSetTarSelStat_Public_Static_Void_Int32_0 | 100672422 |

“Highest ROI” for later investigation:
- `nbSetTargetProcess` is the obvious “enter target selection” seam (it takes `nbActionProcessData_t` + `partyindex` + (ctype, crule, carea)).
- `CursorSelect` / `CursorSelectAuto` likely commit the selection.
- `CheckFriend` / `CheckEnemy` likely decide which indices populate `friendno[]` and `enemyno[]`.

---

## Cross-seam: derive (ctype, crule, carea)

There’s a very relevant helper on `nbMisc`:

| Native ptr name | Token |
|---|---|
| nbGetTargetTypeRuleArea_Public_Static_Void_nbActionProcessData_t_datUnitWork_t_byref_Int32_byref_Int32_byref_Int32_Int32_0 | 100671727 |

Export:
- `data/part8_nbMisc_target_helpers.csv`

Interpretation:
- This looks like the bridge that takes the **action context** (`nbActionProcessData_t`) and the acting **unit work** (`datUnitWork_t`) and derives the target parameters consumed by `nbTarSelProcess`.

---

## Export

Full method catalog:
- `data/part8_nbTarSelProcess_methods.csv`
