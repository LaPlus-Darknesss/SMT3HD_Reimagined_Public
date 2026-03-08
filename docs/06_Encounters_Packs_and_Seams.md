# Encounters — Packs, Tables, and Battle Entry IDs

This part gathers the “encounter database” shapes and the most useful runtime seams for observing/randomizing encounters.

## Data shapes (from `newdata_H`)

### `datEncountPack_s`
Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewdata_H/datEncountPack_s.cs`

| Field | C# type | Notes |
|---|---|---|
| `stageid` | `ushort` |  |
| `areaid` | `ushort` |  |
| `btlsound` | `ushort` |  |
| `moonchk` | `ushort` |  |
| `table2` | `ushort` |  |
| `eventbit1` | `ushort` |  |
| `eventbit2` | `ushort` |  |
| `table1` | `Il2CppStructArray<byte>` |  |
| `enctable` | `Il2CppReferenceArray<datEncountTbl_s>` |  |

### `datEncountTbl_s`
Wrapper: `.../Il2Cppnewdata_H/datEncountTbl_s.cs`

| Field | C# type | Notes |
|---|---|---|
| `normalritu` | `int` |  |
| `ritu` | `Il2CppReferenceArray<datEncountRitu_s>` |  |

### `datEncountRitu_s`
Wrapper: `.../Il2Cppnewdata_H/datEncountRitu_s.cs`

| Field | C# type | Notes |
|---|---|---|
| `encountid` | `ushort` |  |
| `ritu` | `ushort` |  |
| `sensei` | `byte` |  |
| `renzokuritu` | `byte` |  |
| `renzokuindex` | `ushort` |  |

### `datEncount_s`
Wrapper: `.../Il2Cppnewdata_H/datEncount_s.cs`

| Field | C# type | Notes |
|---|---|---|
| `backattack` | `sbyte` |  |
| `esc` | `sbyte` |  |
| `item` | `byte` |  |
| `itemcnt` | `byte` |  |
| `formationtype` | `byte` |  |
| `devil` | `Il2CppStructArray<ushort>` |  |
| `stageid` | `ushort` |  |
| `areaid` | `ushort` |  |
| `flag` | `ushort` |  |
| `maxparty` | `byte` |  |
| `maxcall` | `byte` |  |
| `btlsound` | `ushort` |  |


## Access helpers (from `Il2Cpp`)

| Helper | Signature | Token | Wrapper |
|---|---|---:|---|
| `datEncountPack.Get` | `datEncountPack_t Get(int id)` | 100672657 | `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datEncountPack.cs` |
| `datEncount.Get` | `datEncount_t Get(int id)` | 100672660 | `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datEncount.cs` |

## Runtime seams

| Seam | Signature | Token | Why it’s useful |
|---|---|---:|---|
| `Il2Cpp.nbEncount.nbEncountCalc` | `int nbEncountCalc(int id, float length)` | 100671416 | Observe encounter accumulation and thresholds; good for logging ‘when would a battle happen?’ |
| `Il2Cpp.fldEnc.encStart` | `void encStart(bool bRandomEncount = false)` | 100668425 | Watch/modify the actual field→battle transition (random encounter path). |
| `Il2Cpp.evtCommand.CallBattleSub` | `void CallBattleSub(int encno, int eventno)` | 100666982 | Trace scripted battle setup (boss fights, event battles). |

## Bit packing helpers

- `NB_GET_PACKNO`: `int NB_GET_PACKNO(uint x)` (token `100671423`)
- `NB_GET_ENCNO`: `int NB_GET_ENCNO(uint x)` (token `100671424`)

These look like utilities for extracting pack/encounter numbers from a packed integer.

