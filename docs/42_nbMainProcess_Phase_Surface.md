# 42 — nbMainProcess phase surface (state-machine entry points)

`nbMainProcessData_t.phase` / `nextphase` are `Int32` fields, and `nbMainProcess` exposes a set of *named phase setters*.

This gives us a concrete “phase vocabulary” before doing any runtime probing.

---

## Phase setters and related seams

| Native ptr name | Token |
|---|---|
| nbSetPhase_Private_Static_Void_Int32_0 | 100671572 |
| nbSetNextPhase_Private_Static_Void_Int32_0 | 100671573 |
| nbSetMaePhase_Private_Static_Void_nbMainProcessData_t_0 | 100671589 |
| nbSetEventPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671590 |
| nbSetAtoPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671591 |
| nbSetPressMaePhase_Private_Static_Void_nbMainProcessData_t_0 | 100671592 |
| nbSetPressNiidaPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671593 |
| nbSetActionMaePhase_Private_Static_Void_nbMainProcessData_t_0 | 100671594 |
| nbSetActionPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671595 |
| nbSetChkEndPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671596 |
| nbSetChkTurnPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671597 |
| nbSetResultPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671598 |
| nbSetEndPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671599 |
| nbSetDebugPhase_Private_Static_Void_nbMainProcessData_t_0 | 100671600 |

### Observations (name-based)

The phase names look deliberately grouped:

- `Mae` / `Ato` likely represent “before/after” segments of a larger loop.
- `PressMae` and `PressNiida` strongly suggest press-turn related sub-phases.
- `ActionMae` and `ActionPhase` suggest action execution sub-phases.
- `ChkEnd` and `ChkTurn` suggest end-of-battle and end-of-turn checks.
- `Result` and `End` suggest cleanup / result display / exit.
- `EventPhase` suggests event/cutscene dispatch inside battle.
- `DebugPhase` exists as an explicit state.

These are **names only** — the exact integer values for `phase` are not surfaced in wrappers, so treat this as a label set, not a numeric enum.

---

## Action stack + action-run checks

`nbMainProcess` also exposes the core action stack API and “should we run” style checks:

| Native ptr name | Token |
|---|---|
| CheckActionProcessEnd_Private_Static_Int32_nbMainProcessData_t_0 | 100671580 |
| CheckActionRun_Private_Static_Int32_nbMainProcessData_t_0 | 100671582 |
| nbPushAction_Public_Static_Void_Int32_Int32_Int32_Int32_0 | 100671584 |
| CheckErrorActionStack_Private_Static_Int32_nbMainProcessData_t_Int32_0 | 100671585 |
| nbPopAction_Private_Static_Int32_byref_nbActionStack_t_0 | 100671586 |
| nbCheckActionStack_Private_Static_Int32_0 | 100671587 |
| nbClearActionStack_Private_Static_Void_0 | 100671588 |

This provides a minimal static surface map for later runtime logging:
- when actions are pushed
- when they pop
- how “run/ended” checks gate phase transitions

---

## Export

A CSV of the phase/stack-related token surfaces is included in:

- `data/part7_nbMainProcess_phase_and_stack_methods.csv`

