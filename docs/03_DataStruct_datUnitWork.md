# Core Runtime Struct: `datUnitWork_s` / `datUnitWork_t`

Location:
- Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewdata_H/datUnitWork_s.cs`
- Derived type: `.../datUnitWork_t.cs`

This is one of the most important “live state” structs in combat-related code. Many core calc functions (e.g. `datCalc.datExecSkill`) take `datUnitWork_t` for attacker/defender.

## Fields / properties exposed by the wrapper

| Field | C# type | Notes |
|---|---|---|
| `flag` | `uint` |  |
| `id` | `ushort` |  |
| `hp` | `ushort` |  |
| `maxhp` | `ushort` |  |
| `mp` | `ushort` |  |
| `maxmp` | `ushort` |  |
| `badstatus` | `ushort` |  |
| `exp` | `uint` |  |
| `level` | `ushort` |  |
| `param` | `Il2CppStructArray<sbyte>` |  |
| `skillparam` | `Il2CppStructArray<sbyte>` |  |
| `uniqueid` | `ushort` |  |
| `reserve1` | `ushort` |  |
| `reserve2` | `ushort` |  |
| `namecode` | `Il2CppStructArray<char>` |  |
| `skillcnt` | `int` |  |
| `skill` | `Il2CppStructArray<int>` |  |
| `nowcommand` | `short` |  |
| `nowindex` | `ushort` |  |
| `nowtarea` | `short` |  |
| `nowtform` | `short` |  |
| `prevcommand` | `short` |  |
| `previndex` | `ushort` |  |
| `prevtarea` | `short` |  |
| `prevtform` | `short` |  |
| `mitamaparam` | `Il2CppStructArray<sbyte>` |  |
| `levelupparam` | `Il2CppStructArray<sbyte>` |  |
| `keisyoskill` | `Il2CppStructArray<ushort>` |  |
| `keiattr` | `Il2CppStructArray<byte>` |  |
| `hensinmae` | `ushort` |  |
| `getdevilhearts` | `Il2CppStructArray<ushort>` |  |
| `hensinmaeexp` | `uint` |  |
| `fullnamecode` | `Il2CppStructArray<char>` |  |

## Field grouping (practical)
- Identity / progression: `id`, `uniqueid`, `level`, `exp`
- HP/MP: `hp`, `maxhp`, `mp`, `maxmp`
- Status: `badstatus`
- Stat-ish arrays: `param`, `skillparam`, `mitamaparam`, `levelupparam`
- Skills: `skillcnt`, `skill`, `keisyoskill`, `keiattr`, `getdevilhearts`
- Action history: `nowcommand`, `nowindex`, `nowtarea`, `nowtform`, and `prev*` equivalents
- Name-ish: `namecode`, `fullnamecode`

## Immediate research pivots
If you want to annotate what each field *means* empirically, the most efficient approach is:
1) Detour a high-signal seam like `datCalc.datExecSkill(...)`.
2) Dump selected fields from `s` and `d` (attacker/defender) for a few controlled actions.
3) Correlate changes across turns (e.g. `badstatus`, `param` deltas, skill usage fields).
