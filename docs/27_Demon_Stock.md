# Demon Stock (Camp) — `cmp*Stock`

This section maps the **Camp demon stock** menu (the list, selection, and per-devil panels) and its lifecycle.

Primary wrappers:
- `Il2Cpp/cmpInitStock.cs`
- `Il2Cpp/cmpUpdateStock.cs`
- `Il2Cpp/cmpCalcStock.cs`
- `Il2Cpp/cmpDrawStock.cs`


## Stock lifecycle / process control

### cmpInitStock (create/start/terminate/destroy)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpGetStockSelNums` | `100663826` | `cmpInitStock.cs` | `cmpGetStockSelNums_Public_Static_SByte_cmpStockInfo_t_0` |
| `cmpinitStock` | `100663827` | `cmpInitStock.cs` | `cmpinitStock_Public_Static_cmpDataStock_t_cmpStockInfo_t_cmpSeqInfo_t_0` |
| `cmpDestroyStock` | `100663828` | `cmpInitStock.cs` | `cmpDestroyStock_Public_Static_Object_dds3ProcessID_t_0` |
| `cmpStartProcessStock` | `100663829` | `cmpInitStock.cs` | `cmpStartProcessStock_Public_Static_dds3ProcessID_t_cmpStockInfo_t_cmpSeqInfo_t_0` |
| `cmpTerminateStock` | `100663830` | `cmpInitStock.cs` | `cmpTerminateStock_Public_Static_Void_0` |
| `cmpSetStockVisible` | `100663831` | `cmpInitStock.cs` | `cmpSetStockVisible_Public_Static_Void_SByte_0` |
| `cmpChkStockVisible` | `100663832` | `cmpInitStock.cs` | `cmpChkStockVisible_Public_Static_SByte_0` |

### cmpUpdateStock (sequence + callbacks)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpupdateStock` | `100664109` | `cmpUpdateStock.cs` | `cmpupdateStock_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |

### cmpCalcStock (stock calc)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpcalcStock` | `100663643` | `cmpCalcStock.cs` | `cmpcalcStock_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |

### cmpDrawStock (stock draw)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpDrawStockInfo` | `100663757` | `cmpDrawStock.cs` | `cmpDrawStockInfo_Public_Static_Void_cmpStockInfo_t_SByte_0` |
| `cmpdrawStock` | `100663758` | `cmpDrawStock.cs` | `cmpdrawStock_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |

## Key work structs

Camp stock has a dedicated work struct:
- `Il2Cppcamp_H/cmpDataStock_t.cs`

Fields (high-signal):
- `SeqInfo` : `cmpSeqInfo_t` (sequence controller)
- `Term` : `cmpStockInfo_t` (stock list info)
- `CursorInfo` : `cmpCursorInfo_t` (cursor state)
- `WorkCI` : array of cursor infos (multi-cursor / multiple panels)
- `BackColor` : `Color32` (UI styling)

See `data/part5_camp_struct_fields.csv` for a field list.


## Notes for future hook planning

- `cmpUpdateSequenceStock(PID)` is the stock menu tick function.
- `cmpStockAdd` / `cmpStockRemove` indicate explicit list mutation helpers.
- `cmpStockCursorActive` / `cmpStockCursorStop` suggests the stock cursor can be “locked” during transitions.
