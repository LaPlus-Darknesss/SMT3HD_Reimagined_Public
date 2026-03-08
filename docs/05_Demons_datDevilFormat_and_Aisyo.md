# Demon Base Data + Resistances — `datDevilFormat`, `datAisyo`

This part extracts concrete, wrapper-backed info about demon base records and resistance lookup.

## Base demon record: `datDevilFormat_s`

Wrapper source: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewdata_H/datDevilFormat_s.cs`

| Field | C# type | Notes |
|---|---|---|
| `flag` | `uint` |  |
| `race` | `byte` |  |
| `level` | `byte` |  |
| `hp` | `ushort` |  |
| `maxhp` | `ushort` |  |
| `mp` | `ushort` |  |
| `maxmp` | `ushort` |  |
| `aisyoid` | `short` |  |
| `param` | `Il2CppStructArray<sbyte>` |  |
| `skill` | `Il2CppStructArray<ushort>` |  |
| `keisyotype` | `short` |  |
| `keisyoform` | `ushort` |  |
| `dropmakka` | `ushort` |  |
| `dropexp` | `ushort` |  |
| `dropitem` | `Il2CppStructArray<byte>` |  |
| `droppoint` | `Il2CppStructArray<byte>` |  |
| `specialbit` | `ushort` |  |
| `specialitem` | `byte` |  |
| `specialpoint` | `byte` |  |
| `hougyokupoint` | `byte` |  |
| `masekipoint` | `byte` |  |
| `attackattr` | `sbyte` |  |
| `attackcnt` | `byte` |  |
| `attackinterval` | `byte` |  |
| `reserve1` | `byte` |  |

### Practical grouping
- Identity/classification: `race`, `level`, `flag`, `aisyoid`
- HP/MP: `hp/maxhp`, `mp/maxmp`
- Stat array: `param`
- Skill list: `skill`
- Drops/rewards: `dropmakka`, `dropexp`, `dropitem`, `droppoint`
- Attack defaults: `attackattr`, `attackcnt`, `attackinterval`

## Access helpers: `Il2Cpp.datDevilFormat`

| Method | Signature | Token | Wrapper |
|---|---|---:|---|
| `Get` | `datDevilFormat_t Get(int id, bool dlc = true)` | 100672630 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datDevilFormat.cs` |
| `Analyze` | `datDevilFormat_t Analyze(int id)` | 100672631 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datDevilFormat.cs` |

These helpers are excellent for building reliable “ID → record” dumps without manually indexing `tbl`.

## Name helpers: `Il2Cpp.datDevilName`

| Method | Signature | Token |
|---|---|---:|
| `Get` | `string Get(int id)` | 100672676 |
| `GetTag` | `string GetTag(int id)` | 100672677 |
| `GetEncyc` | `string GetEncyc(int id)` | 100672678 |

Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datDevilName.cs`

## Resistance lookup: `Il2Cpp.datAisyo.Get`

- Signature: `uint Get(int id, int attr, uint su_flag = 0U)`
- Token: `100672540`
- Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datAisyo.cs`

This is a strong probing seam for verifying the exact resistance table + attribute IDs in play.

