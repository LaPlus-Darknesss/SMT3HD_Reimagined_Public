# Skill Data Tables — `datNormalSkill`, `datSkillName`, and friends

This part focuses on what we can state **directly from the IL2CPP wrappers**: table shapes, key fields, and a couple of string helpers.

## Primary “normal skill” record: `datNormalSkill_s`

Wrapper source: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewdata_H/datNormalSkill_s.cs`

These fields look like the core per-skill “effect spec” used by battle logic:

| Field | C# type | Notes |
|---|---|---|
| `flag` | `byte` |  |
| `use` | `byte` |  |
| `koukatype` | `byte` |  |
| `costtype` | `byte` |  |
| `cost` | `ushort` |  |
| `costbase` | `ushort` |  |
| `targettype` | `byte` |  |
| `targetarea` | `byte` |  |
| `targetrule` | `byte` |  |
| `targetrandom` | `byte` |  |
| `untargetbadstat` | `ushort` |  |
| `targetprog` | `ushort` |  |
| `hittype` | `byte` |  |
| `hitlevel` | `byte` |  |
| `hitprog` | `ushort` |  |
| `targetcntmin` | `byte` |  |
| `targetcntmax` | `byte` |  |
| `hptype` | `ushort` |  |
| `hpn` | `short` |  |
| `mptype` | `ushort` |  |
| `mpn` | `short` |  |
| `hpbase` | `short` |  |
| `mpbase` | `short` |  |
| `minus` | `ushort` |  |
| `badtype` | `byte` |  |
| `badlevel` | `byte` |  |
| `basstatus` | `ushort` |  |
| `hojotype` | `uint` |  |
| `hojopoint` | `sbyte` |  |
| `deadtype` | `byte` |  |
| `program` | `uint` |  |
| `criticalpoint` | `short` |  |
| `failpoint` | `short` |  |
| `magicbase` | `short` |  |
| `magiclimit` | `short` |  |

### Practical observations (inference from names)
- `costtype/cost/costbase` likely drive MP/HP/etc costs.
- `targettype/targetarea/targetrule/targetrandom/targetcnt*` describe selection logic.
- `hittype/hitlevel/hitprog` suggest hit check settings.
- `hptype/hpn/hpbase` and `mptype/mpn/mpbase` look like HP/MP change descriptors.
- `badtype/badlevel/basstatus` likely define status infliction.
- `program` looks like an effect program/dispatch ID.
- `criticalpoint/failpoint` look like crit/fail tuning.
- `magicbase/magiclimit` look like damage/heal scaling caps.

## Table access: `Il2Cpp.datNormalSkill.tbl`

- Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datNormalSkill.cs`
- Exposes static `tbl` field (an `Il2CppReferenceArray<datNormalSkill_t>`).

Even though this wrapper doesn’t expose `Get(int)` helpers, the `tbl` array is enough to:
- index by skill ID (after confirming the ID mapping)
- dump raw fields for balancing

## Name / text helpers

### `Il2Cpp.datSkillName.Get`
- Signature: `string Get(int id, int Devilid = 0)`
- Token: `100672691`
- Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datSkillName.cs`


## Where this ties into gameplay math

- `Il2Cpp.datCalc.datGetSkillCost(datUnitWork_t w, int nskill)` (token `1006726xx`) is a good “sanity seam” to confirm IDs and observe cost computation.
- `Il2Cpp.datCalc.datExecSkill(int nskill, datUnitWork_t s, datUnitWork_t d)` is the higher-impact execution seam.

