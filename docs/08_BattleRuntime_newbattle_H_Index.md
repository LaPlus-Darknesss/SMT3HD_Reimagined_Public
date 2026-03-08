# Battle Runtime Type Index — `Il2Cppnewbattle_H` (Part 1)

This is a quick index of battle-runtime types exposed under the `Il2Cppnewbattle_H` namespace.

## Top types by method count

| Type | Method count | Wrapper file |
|---|---:|---|
| `nbMainProcessData_t` | 182 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbMainProcessData_t.cs` |
| `nbNegoProcessData_t` | 149 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbNegoProcessData_t.cs` |
| `nbFormation_t` | 107 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbFormation_t.cs` |
| `nbActionProcessData_t` | 102 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbActionProcessData_t.cs` |
| `nbCommSelProcessData_t` | 54 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbCommSelProcessData_t.cs` |
| `nbTarSelProcessData_t` | 44 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbTarSelProcessData_t.cs` |
| `nbHelpProcessData_t` | 40 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbHelpProcessData_t.cs` |
| `nbKoukaPacket_t` | 39 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbKoukaPacket_t.cs` |
| `nbPanelFlashData_t` | 33 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbPanelFlashData_t.cs` |
| `nbDebugProcessData_t` | 27 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbDebugProcessData_t.cs` |
| `nbEventProcessData_t` | 26 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbEventProcessData_t.cs` |
| `nbMakaProcessData_t` | 26 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbMakaProcessData_t.cs` |
| `nbEventFuncList_t` | 21 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbEventFuncList_t.cs` |
| `nbParty_t` | 17 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbParty_t.cs` |
| `nbE` | 17 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbE.cs` |
| `nbStageProcessData_t` | 15 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbStageProcessData_t.cs` |
| `nbFormData_t` | 13 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbFormData_t.cs` |
| `nbKoukaData_t` | 13 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbKoukaData_t.cs` |
| `nbActionStack_t` | 12 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbActionStack_t.cs` |
| `nbKoukaProcessData_t` | 11 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbKoukaProcessData_t.cs` |
| `nbSound_t` | 11 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbSound_t.cs` |
| `nbTimeList_t` | 9 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbTimeList_t.cs` |
| `bDrawText` | 7 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/bDrawText.cs` |
| `SOBED_PB_LIST` | 7 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/SOBED_PB_LIST.cs` |
| `nbNewBattleSeqData_t` | 2 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbNewBattleSeqData_t.cs` |

## A few “anchor” types worth opening early

- `nbMainProcessData_t` (182 methods) — wrapper `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbMainProcessData_t.cs`
- `nbActionProcessData_t` (102 methods) — wrapper `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbActionProcessData_t.cs`
- `nbParty_t` (17 methods) — wrapper `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbParty_t.cs`
- `nbFormation_t` (107 methods) — wrapper `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewbattle_H/nbFormation_t.cs`

## Next research step for battle runtime

A high-ROI approach is:
1) Use `datCalc.datExecSkill(...)` to capture the skill ID and attacker/defender `datUnitWork_t`.
2) Correlate that with `newbattle_H` state by locating where `datUnitWork_t` is converted into battle-side unit work (likely `nbUnitWork_t`).
3) Once we find the bridge, we can safely map “selection phase” vs “execution phase” data ownership.

