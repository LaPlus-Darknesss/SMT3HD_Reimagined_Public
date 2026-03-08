# Concrete Hook/Probe Seams — Part 1 (tokens + signatures)

This document lists a curated set of high-leverage functions where you can **probe**, **log**, or **detour** to understand or alter gameplay.

For each entry you get:
- **Signature** as seen in the IL2CPP wrapper
- **IL2CPP token** used by the wrapper to resolve the method pointer
- **Wrapper source file** location in the repo

## Encounter lifecycle (random encounters)

| Seam | Signature | Token | Wrapper |
|---|---|---:|---|
| `Il2Cpp.nbEncount.nbEncountCalc` | `int nbEncountCalc(int id, float length)` | 100671416 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/nbEncount.cs` |
| `Il2Cpp.nbEncount.NB_GET_PACKNO` | `int NB_GET_PACKNO(uint x)` | 100671423 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/nbEncount.cs` |
| `Il2Cpp.nbEncount.NB_GET_ENCNO` | `int NB_GET_ENCNO(uint x)` | 100671424 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/nbEncount.cs` |
| `Il2Cpp.datEncountPack.Get` | `datEncountPack_t Get(int id)` | 100672657 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datEncountPack.cs` |
| `Il2Cpp.fldEnc.encStart` | `void encStart(bool bRandomEncount = false)` | 100668425 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fldEnc.cs` |

### Notes
- `nbEncount.nbEncountCalc(...)` is a central counter/calc step for encounter progress. Detouring here is a clean way to observe how often/when the game thinks an encounter should trigger.
- `fldEnc.encStart(bool bRandomEncount=false)` appears to be the actual transition into the encounter start lifecycle.

## Scripted battle entry (boss fights, event-driven)

| Seam | Signature | Token | Wrapper |
|---|---|---:|---|
| `Il2Cpp.evtCommand.CallBattleSub` | `void CallBattleSub(int encno, int eventno)` | 100666982 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/evtCommand.cs` |
| `Il2Cpp.evtCommand.CallBattleSub2` | `void CallBattleSub2(int encno)` | 100666984 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/evtCommand.cs` |
| `Il2Cpp.evtCommand.evtCommand_CALL_BATTLE` | `int evtCommand_CALL_BATTLE()` | 100666983 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/evtCommand.cs` |
| `Il2Cpp.evtCommand.evtCommand_CALL_BATTLE2` | `int evtCommand_CALL_BATTLE2()` | 100666985 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/evtCommand.cs` |
| `Il2Cpp.evtCommand.evtCommand_RESTORE_BATTLE` | `int evtCommand_RESTORE_BATTLE()` | 100666986 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/evtCommand.cs` |

### Notes
- `evtCommand.CallBattleSub(int encno, int eventno)` / `CallBattleSub2(int encno)` look like the main event-script entry points for setting up battles.
- `_CALL_BATTLE()` / `_CALL_BATTLE2()` are command-style wrappers used by the event VM (useful for tracing “who called battle”).

## Skill / battle math seam (core gameplay)

| Seam | Signature | Token | Wrapper |
|---|---|---:|---|
| `Il2Cpp.datCalc.datExecSkill` | `int datExecSkill(int nskill, datUnitWork_t s, datUnitWork_t d)` | 100672558 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datCalc.cs` |
| `Il2Cpp.datCalc.datGetSkillCost` | `int datGetSkillCost(datUnitWork_t w, int nskill)` | 100672612 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datCalc.cs` |
| `Il2Cpp.datCalc.datInitBossPress` | `void datInitBossPress()` | 100672614 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datCalc.cs` |

### Notes
- `datCalc.datExecSkill(int nskill, datUnitWork_t s, datUnitWork_t d)` is one of the most valuable single seams: it’s “skill execution” at a point where you still have attacker/defender work structs.
- `datCalc.datGetSkillCost(...)` is a safer place to start if you want to validate skill IDs and see which work structs are “live” during selection/execution.
- `datCalc.datInitBossPress()` suggests a boss-specific press-turn initialization path.

## Location/name helpers (ID → label mapping)

| Seam | Signature | Token | Wrapper |
|---|---|---:|---|
| `Il2Cpp.fldGlobal.fldAreaNameGetIdx` | `int fldAreaNameGetIdx(int fld, int area)` | 100668559 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fldGlobal.cs` |
| `Il2Cpp.fldGlobal.fldBasyoNameGetIdx` | `int fldBasyoNameGetIdx(int fld, int basyo)` | 100668560 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fldGlobal.cs` |
| `Il2Cpp.fldGlobal.fldFloorNameGetIdx` | `int fldFloorNameGetIdx(int fld, int floor)` | 100668561 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fldGlobal.cs` |

## Resistance lookup

- `Il2Cpp.datAisyo.Get` → `uint Get(int id, int attr, uint su_flag = 0U)` (token `100672540`) in `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datAisyo.cs`

This is a strong seam for:
- verifying which resistance table/id is being consulted
- logging attribute IDs and flags
- confirming whether buffs/debuffs/temporary flags are folded in at this stage

