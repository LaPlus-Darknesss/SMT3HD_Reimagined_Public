# 43 — Battle command selection (nbCommSelProcess)

`nbCommSelProcess` is the battle **command list / tab selection** process: it owns the logic that decides what command entries exist (Attack / Skill / Item / Talk / Summon, etc), what is selectable, and how the command UI is drawn + navigated (cursor, tabs, mouse support).

This is the “front door” into the action pipeline: a selected entry eventually feeds `nbActionProcessData_t` and pushes an action onto `nbMainProcess`’s action stack (see docs 36–42 for the execution side).

---

## Process data (nbCommSelProcessData_t)

The process state is stored in `nbCommSelProcessData_t` (newbattle struct wrapper).

Notable fields (name-based):

| Field | Type |
|---|---|
| mem | sdfMemHandle_t |
| data | nbMainProcessData_t |
| act | nbActionProcessData_t |
| nowstat | sbyte |
| nowtype | byte |
| drawtype | int |
| drawcursor | int |
| drawlisty | int |
| nowcursor | Il2CppStructArray<int> |
| nowlisty | Il2CppStructArray<int> |
| my | nbParty_t |
| partytarget | nbParty_t |
| commlist | Il2CppReferenceArray<Il2CppStructArray<ushort>> |
| commcnt | Il2CppStructArray<int> |
| commdisable | Il2CppReferenceArray<Il2CppStructArray<sbyte>> |
| cnt | int |
| smritu | float |
| animeflag | uint |
| ox | int |
| oy | int |
| oz | int |
| alpha | int |
| helpa | int |
| ritu | float |
| openwait | int |

Name-based quick read:
- `nowstat` / `nowtype` / `drawtype` look like **UI mode** and **current tab/type**.
- `nowcursor[]` and `nowlisty[]` look like per-tab cursor & scroll memory.
- `commlist` + `commcnt` + `commdisable` look like the **command entries** and **disable flags** per tab/type.

Full field list export:
- `data/part8_struct_fields_battle_ui.csv` (filter `struct == nbCommSelProcessData_t`)

---

## Key seams (token surfaces)

These are the methods that look most useful for hooking/logging later (ordered roughly “lifecycle → selection → memory”).

| Native ptr name | Token |
|---|---|
| GetCommSelProcessData_Public_Static_nbCommSelProcessData_t_0 | 100671151 |
| nbInitCommSelProcess_Public_Static_Void_nbMainProcessData_t_nbParty_t_nbActionProcessData_t_0 | 100671195 |
| InitCommSelProcessData_Public_Static_nbCommSelProcessData_t_nbMainProcessData_t_nbParty_t_nbActionProcessData_t_0 | 100671194 |
| nbCommSelProcess1_Public_Static_Object_dds3ProcessID_t_0 | 100671188 |
| SelectCommandList_Public_Static_Void_byref_nbCommSelProcessData_t_0 | 100671180 |
| SelectCommandListAuto_Public_Static_Void_byref_nbCommSelProcessData_t_0 | 100671181 |
| CheckSelectError_Public_Static_Int32_byref_nbCommSelProcessData_t_0 | 100671177 |
| SetCommList_Public_Static_Void_byref_nbCommSelProcessData_t_nbParty_t_0 | 100671190 |
| GetCommListCnt_Public_Static_Int32_byref_nbCommSelProcessData_t_Int32_0 | 100671150 |
| MemoryCommand_Public_Static_Void_byref_nbCommSelProcessData_t_0 | 100671178 |
| SetPrevCursor_Public_Static_Void_byref_nbCommSelProcessData_t_Int32_Int32_0 | 100671191 |
| SetCommCursor2_Public_Static_Void_byref_nbCommSelProcessData_t_0 | 100671192 |
| nbGetCommSelStat_Public_Static_Int32_0 | 100671182 |
| nbSetCommSelStat_Public_Static_Void_Int32_0 | 100671183 |
| nbCloseCommSel_Public_Static_Void_0 | 100671184 |

Notes:
- `nbInitCommSelProcess` takes `nbMainProcessData_t`, `nbParty_t`, and `nbActionProcessData_t` — it is likely the handoff point where the current unit/party/action context is bound into the command selector.
- `SetCommList` + `GetCommListCnt` appear to be the highest-ROI surface for understanding what entries exist.
- `CheckSelectError` is the obvious gate for “why can’t I select this?”

---

## UI / rendering surfaces

These are likely “draw the panel” helpers (useful when you want to *change* what’s drawn, not just what’s selectable):

- `DispCommandPanel` (100671165)
- `DispCommandList2` (100671176)
- `DispHelpPanel` (100671164)
- `DispPanelCursor` / `DispPanelLabel` / `DispPanelSitaji` (100671152–100671154)
- `DispComm1Panel*` (Comm/Talk/Item/Summon) (100671155–100671158)
- Cursor drawing: `DispCommCursor`, `DispSummonCursor`, `DispComm1PanelCursor` (100671159–100671162)

Mouse surfaces are explicitly present:
- `UpdateBcommMouseIndex`, `BcommSelMouseEnter`, `MouseWheelScrol` (100671145–100671149)

---

## Practical mod targets (doc-only hypotheses)

These are “best bets” from naming alone; treat as provisional until we attach logging:

1. **Disable or force-enable commands**
   - likely via `commdisable` + `CheckSelectError`
2. **Remember cursor/tab behavior**
   - `MemoryCommand`, `SetPrevCursor`, `SetCommCursor2`
3. **Auto-select behaviors**
   - `SelectCommandListAuto`, `nbSetAutoCommSel`

---

## Export

Full method catalog:
- `data/part8_nbCommSelProcess_methods.csv`
