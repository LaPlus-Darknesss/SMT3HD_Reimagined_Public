# 41 — Press-turn + boss press + UI gauge surfaces

Press-turn is the heart of SMT3 battle flow, and it’s exposed through multiple layers:

- runtime action state (`nbActionProcessData_t`)
- packet emission (`nbActionProcess` + `nbMakePacket`)
- boss-specific press behavior (`datCalc.datBossPress` / `datInitBossPress`)
- UI gauge (`nbPanelProcess`)

---

## Press state lives in nbActionProcessData_t

| Field | Field name |
|---|---|
| press | press |
| newpresstype | newpresstype |
| newpress_p | newpress_p |
| newpress_ten | newpress_ten |
| newaddpresstype | newaddpresstype |

This is where “press bookkeeping” appears to be tracked per action.

---

## Press packet emission seams

### In nbActionProcess

| Native ptr name | Token |
|---|---|
| SetPress_Public_Static_Void_byref_nbActionProcessData_t_Single_0 | 100670818 |
| SetNewPress_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670819 |
| SetAddPressPacket_Public_Static_Void_byref_nbActionProcessData_t_Int32_0 | 100670823 |

### In nbMakePacket

| Native ptr name | Token |
|---|---|
| nbAddPressPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Single_0 | 100671669 |
| nbAddNewPressPacket_Public_Static_nbKoukaPacket_t_Int32_Int32_Int32_Int32_0 | 100671670 |
| nbMakePressPacket_Public_Static_Int32_Int32_Int32_Single_0 | 100671702 |
| nbMakeNewPressPacket_Public_Static_Int32_Int32_Int32_Int32_byref_nbFormation_t_0 | 100671703 |

Name-based interpretation:
- `nbAddPressPacket` likely corresponds to fractional press changes (uses `Single`).
- `nbAddNewPressPacket` suggests a structured “new press” update (likely for more complex rules).

---

## Boss press initialization: datCalc

`datCalc` exposes:

| Native ptr name | Token |
|---|---|
| datInitBossPress_Public_Static_Void_0 | 100672614 |

And a global field `datBossPress` is present on `datCalc` (see wrapper static init).

`datBossPress_t` schema:

| Field | Field name |
|---|---|
| flag | flag |
| flag2 | flag2 |
| encid | encid |
| dbest | dbest |

Even without deeper decoding, this strongly suggests **encounter-specific** press rules.

---

## UI gauge surfaces: nbPanelProcess

`nbPanelProcess` contains a dedicated “press gauge” surface:

| Native ptr name | Token |
|---|---|
| nbPanelPressTurnGaugeAnimStart_Private_Static_Void_0 | 100672287 |
| nbPanelPressGaugeInit_Private_Static_Void_0 | 100672288 |
| nbPanelPressGaugeStart_Public_Static_Void_0 | 100672289 |
| nbPanelPressGaugeShow_Public_Static_Void_0 | 100672290 |
| nbPanelPressGaugeHide_Public_Static_Void_0 | 100672291 |
| nbPanelPressGaugeEncountHide_Public_Static_Void_0 | 100672292 |
| nbPanelPressGauge_Public_Static_Void_nbMainProcessData_t_0 | 100672293 |
| nbPanelPressTurnInit_Private_Static_Void_0 | 100672294 |
| nbPanelPressTurnRun_Public_Static_Int32_0 | 100672295 |
| nbPanelPressTurnGaugeRun_Public_Static_Int32_0 | 100672296 |
| nbPanelPressTurnOmit_Public_Static_Void_0 | 100672297 |
| nbPanelPressTurnStart_Public_Static_Void_0 | 100672298 |
| nbPanelPressTurn_Private_Static_Void_nbMainProcessData_t_0 | 100672301 |

This provides a concrete list of UI seams for:
- initializing the gauge
- showing/hiding it (including “encounter hide”)
- per-frame running/updating

---

## Modding implications (doc-only)

If a future mod wants to change press rules, this map already tells you where the evidence will appear:

1) action state changes (`nbActionProcessData_t.press` / `newpress*`)
2) packet emission (`SetPress` / `SetNewPress` / `nbAddPressPacket` / `nbAddNewPressPacket`)
3) boss-specific tables (`datInitBossPress`, `datBossPress_t`)
4) UI feedback (`nbPanelPressGauge*`)

