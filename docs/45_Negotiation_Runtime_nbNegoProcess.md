# 45 — Negotiation runtime (nbNegoProcess)

`nbNegoProcess` is the battle **negotiation** process (talking to demons, requests, outcomes, flow control). It is strongly message-driven and appears to keep a significant internal history/flag state in `nbNegoProcessData_t`.

This tranche documents:
- the *runtime surface* (process entry points + message helpers + request getters)
- the *data schema* (the process work struct)
- the *flow catalog* is in doc 46

---

## Process data (nbNegoProcessData_t)

This struct is large; here are the highest-signal fields by name:

| Field | Type |
|---|---|
| data | nbMainProcessData_t |
| actdata | nbActionProcessData_t |
| stat | uint |
| negotype | int |
| waitframe | int |
| nownegoid | uint |
| nownegophase | int |
| nextnegoid | uint |
| nextkouhocnt | int |
| nextkouho | Il2CppStructArray<uint> |
| nowmsgid | int |
| nowmsgno | int |
| nowmes | int |
| msgwndid | int |
| nowselect | int |
| workid | Il2CppStructArray<uint> |
| requesttype | int |
| requestdata | int |
| requesthpmp | int |
| prevreqtype | int |
| cnt6_36 | int |
| cnt6_37 | int |
| cnt6_58 | int |
| cnt6_66 | int |
| mes6_2 | int |
| mes6_25 | int |
| mes6_67 | int |
| flag1_6 | int |
| flag1_39 | int |
| historyindex | int |
| history | Il2CppStructArray<uint> |
| historymes | Il2CppStructArray<int> |
| endid | int |
| endtype | int |
| negoreqtype | int |

Notes:
- `nownegoid` / `nextnegoid` + `GET_NEGO_PROCID` / `GET_NEGO_FLOWID` imply the negotiation state machine is keyed by an opaque **negoid**.
- `requesttype`, `requestdata`, `requesthpmp`, plus many `nbGetNego*` getters suggest requests are computed on-demand from the work struct + tables.

Full field list export:
- `data/part8_struct_fields_battle_ui.csv` (filter `struct == nbNegoProcessData_t`)

---

## Key seams (token surfaces)

| Native ptr name | Token |
|---|---|
| InitNegoProcessData_Private_Static_nbNegoProcessData_t_byref_nbActionProcessData_t_Int32_Int32_Int32_UInt32_0 | 100672267 |
| nbInitNegoProcess_Public_Static_Void_byref_nbActionProcessData_t_Int32_Int32_Int32_Int32_0 | 100672268 |
| nbNegoProcess1_Private_Static_Object_dds3ProcessID_t_0 | 100672224 |
| nbNegoProcessMessageChk_Private_Static_Object_dds3ProcessID_t_0 | 100672263 |
| nbNegoProcessMessage_Private_Static_Object_dds3ProcessID_t_0 | 100672264 |
| nbNegoProcessEnd_Private_Static_Object_dds3ProcessID_t_0 | 100672265 |
| nbNegoShutdown_Private_Static_Object_dds3ProcessID_t_0 | 100672266 |
| GetMsgList_Private_Static_Object_Int32_Int32_0 | 100672223 |
| nbDispNegoMessage_Public_Static_Void_byref_nbNegoProcessData_t_Int32_Int32_Int32_0 | 100672231 |
| nbCheckNegoMessage_Public_Static_Int32_byref_nbNegoProcessData_t_0 | 100672232 |
| nbClsNegoMessage_Public_Static_Void_byref_nbNegoProcessData_t_0 | 100672230 |
| nbGetNegoRequestType_Public_Static_Int32_Int32_0 | 100672246 |
| nbGetNegoTuikaRequestType_Public_Static_Int32_Int32_0 | 100672247 |
| nbAddNegoIdKouho_Public_Static_Void_byref_nbNegoProcessData_t_UInt32_0 | 100672225 |
| nbGetPrevNegoId_Public_Static_UInt32_byref_nbNegoProcessData_t_0 | 100672226 |
| nbGetBackNoNegoId_Public_Static_UInt32_byref_nbNegoProcessData_t_Int32_0 | 100672227 |
| GET_NEGO_PROCID_Private_Static_Int32_UInt32_0 | 100672219 |
| GET_NEGO_FLOWID_Private_Static_Int32_UInt32_0 | 100672220 |
| MAKE_NEGOID_Private_Static_UInt32_Int32_Int32_0 | 100672221 |

Useful clusters:
- **Process lifecycle**: `InitNegoProcessData` → `nbInitNegoProcess` → `nbNegoProcess1` → (MessageChk/Message/End) → `nbNegoShutdown`
- **Message control**: `nbDispNegoMessage`, `nbCheckNegoMessage`, `nbClsNegoMessage`
- **Request computation**: `nbGetNegoRequestType`, `nbGetNegoTuikaRequestType`, + the many `nbGetNego*` getters for Makka/HP/MP/Item/Stone

---

## NegoID utilities

`nbNegoProcess` exposes three “treat the negoid as opaque” helpers:

- `GET_NEGO_PROCID(negoid)` → proc id
- `GET_NEGO_FLOWID(negoid)` → flow id
- `MAKE_NEGOID(proc, flow)` → negoid

We can lean on these instead of assuming any bit-packing.

---

## Export

Full method catalog:
- `data/part8_nbNegoProcess_methods.csv`
