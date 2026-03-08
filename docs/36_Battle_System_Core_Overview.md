# 36 — Battle System Core Overview (newbattle / nb*)

This tranche maps the **battle runtime core** at a level that’s immediately useful for modding:

- Where “battle state” lives (primary structs)
- How the battle loop progresses (phases + action stack)
- Which seams exist for observation/logging vs intervention

Everything here is derived from `Assembly-CSharp` Il2Cpp wrapper sources (token → method pointer names, and field name layouts).

---

## The big picture

Battle runtime is organized around `nb*` process classes under `Il2Cpp/` and data structs under `newbattle_H/`.

At the center:
- `nbMainProcessData_t` (battle-wide state, phase/turn, parties, action stack)
- `nbActionProcessData_t` (per-action execution state; press-turn bookkeeping lives here)
- `nbKoukaProcessData_t` + `nbKoukaPacket_t` (effect packets, animation/effect execution)

---

## Highest-signal battle types (by method count)

These are the “gravity wells” in the decompile — if you’re looking for where things happen, start here.

### Process classes (Il2Cpp)

| Type (Il2Cpp) | Method count |
|---|---|
| nbPanelProcess | 300 |
| nbCameraBoss | 203 |
| nbMainProcess | 132 |
| nbActionProcess | 122 |
| nbMisc | 91 |
| nbCameraSkill | 85 |
| nbTest | 83 |
| nbAiScript | 76 |
| nbMakePacket | 75 |
| nbResultProcess | 69 |
| nbCalc | 68 |
| nbKoukaProcess | 63 |

### Data structs (newbattle_H)

| Type (newbattle_H) | Method count |
|---|---|
| nbMainProcessData_t | 182 |
| nbNegoProcessData_t | 149 |
| nbFormation_t | 107 |
| nbActionProcessData_t | 102 |
| nbCommSelProcessData_t | 54 |
| nbTarSelProcessData_t | 44 |
| nbHelpProcessData_t | 40 |
| nbKoukaPacket_t | 39 |
| nbPanelFlashData_t | 33 |
| nbDebugProcessData_t | 27 |
| nbEventProcessData_t | 26 |
| nbMakaProcessData_t | 26 |

---

## nbMainProcess: phase + battle root accessors

Key entry seams on `nbMainProcess` (tokens are stable identifiers in the wrapper layer):

| Native ptr name | Token |
|---|---|
| nbSetPhase_Private_Static_Void_Int32_0 | 100671572 |
| nbSetNextPhase_Private_Static_Void_Int32_0 | 100671573 |
| nbGetMainProcessData_Public_Static_nbMainProcessData_t_0 | 100671574 |
| nbGetUnitWorkPointer_Public_Static_datUnitWork_t_nbMainProcessData_t_nbParty_t_0 | 100671575 |
| nbGetPartyFromFormindex_Public_Static_nbParty_t_Int32_0 | 100671576 |
| nbGetUnitWorkFromFormindex_Public_Static_datUnitWork_t_Int32_0 | 100671577 |
| nbPushAction_Public_Static_Void_Int32_Int32_Int32_Int32_0 | 100671584 |

### nbMainProcessData_t: core fields you’ll almost always care about

| Field | Field name |
|---|---|
| form | form |
| party | party |
| activeunit | activeunit |
| encpackno | encpackno |
| encno | encno |
| formationtype | formationtype |
| actionstack | actionstack |
| actionstackp | actionstackp |
| endtype | endtype |
| order | order |
| orderindex | orderindex |
| debug | debug |
| stat | stat |
| flag | flag |
| turn | turn |
| phase | phase |
| nextphase | nextphase |

**Interpretation notes (name-based, verify via runtime logging later):**
- `phase` / `nextphase` look like the primary state machine driver for the battle loop.
- `actionstack` + `actionstackp` are the action queue/stack backing store.
- `party` and `form` appear to be the live formation/party arrays driving unit lookup and targeting.

---

## Action stack record

`nbActionStack_t` is a compact record (fields only):

| Field | Field name |
|---|---|
| type | type |
| from | from |
| to | to |
| data | data |

This is the minimum schema needed to start meaningfully logging “what action got queued” during battle.

---

## Practical “doc-only” next steps (no runtime needed yet)

If we want to keep learning from decompile only, the best scans to perform next are:

1) `nbActionProcess` + `nbMakePacket` + `nbKoukaProcess` to understand the **packet pipeline**.
2) `nbCalc` + `datCalc` for **formula and resist/status logic**.
3) `nbAi` + `nbAiScript` + `datDevilAI*` for **boss AI behavior**.

Those are covered in docs 37–41 in this tranche.
