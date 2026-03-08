# 48 — Negotiation Messages: IDs, Slots, and Variable Binding — v9 (2026-02-08)

This tranche focuses on the **message plumbing** inside negotiation:
- where message ids live in `nbNegoProcessData_t`
- which `nbNegoProcess` methods appear to **populate** those ids
- which methods **display / check / clear** the message window

Primary reference sources:
- `Il2Cpp/nbNegoProcess.cs`
- `Il2Cpp/nbNegoProcessData_t.cs`
- `Il2Cppnewdata_H/datInnenNegoTable_s.cs` (message id fields)

## Message-related fields in `nbNegoProcessData_t`

Source: `Il2Cpp/nbNegoProcessData_t.cs`

| Field | Type | Role_hint |
|---|---|---|
| nowmsgid | int | message id/slot |
| nowmsgno | int | message id/slot |
| nowmes | int | message id/slot |
| msgwndid | int | message id/slot |
| mes6_2 | int |  |
| mes6_25 | int |  |
| mes6_67 | int |  |
| historymes | Il2CppStructArray<int> | message id/slot |
| aisyomes | int | message id/slot |
| hojoaisyomes | int | message id/slot |
| getinfomsg | int | message id/slot |
| flowmsg | Il2CppStructArray<int> | message id/slot |
| questionmsg | int | message id/slot |
| aisyomsg | Il2CppStructArray<int> | message id/slot |
| infomsg | Il2CppStructArray<int> | message id/slot |
| helpmsg | int | message id/slot |
| helpmsg2 | int | message id/slot |
| innenmsg | int | message id/slot |
| bossmsg | int | message id/slot |


### Immediate takeaways
- Negotiation stores multiple “channels” of message ids:
  - **current message**: `nowmsgid`, `nowmsgno`, `nowmes`, `msgwndid`
  - **flow scripting**: `flowmsg` (array of ints)
  - **question**: `questionmsg`
  - **aisyo**: `aisyomsg` (array) + `aisyomes` (single)
  - **info/help**: `infomsg` (array), `getinfomsg`, `helpmsg`, `helpmsg2`
  - **special**: `innenmsg`, `bossmsg`

This suggests negotiation is designed as a *state machine* that stages message IDs into these slots and then renders them via a shared message window.

## Key message methods in `nbNegoProcess`

Source: `Il2Cpp/nbNegoProcess.cs`

| Method | Token |
|---|---|
| GetMsgList(int ekucyo, int idx) -> Il2CppSystem.Object | 100672223 |
| nbSetMesMakka(int msgid, int i) -> void | 100672228 |
| nbSetMesItem(int msgid, int it) -> void | 100672229 |
| nbClsNegoMessage(ref nbNegoProcessData_t n) -> void | 100672230 |
| nbDispNegoMessage(ref nbNegoProcessData_t n, int msgid, int msgno, int sel) -> void | 100672231 |
| nbCheckNegoMessage(ref nbNegoProcessData_t n) -> int | 100672232 |
| nbSetNegoMesVar(int msgid, ref datUnitWork_t p, ref datUnitWork_t e) -> void | 100672233 |
| nbGetInfoMsg(int infotype) -> int | 100672255 |
| nbCheckSelectMesJoukenAri(int msg_no, int field_no) -> int | 100672256 |
| nbNegoProcessMessageChk(dds3ProcessID_t id) -> Il2CppSystem.Object | 100672263 |
| nbNegoProcessMessage(dds3ProcessID_t id) -> Il2CppSystem.Object | 100672264 |


### “Bindings” vs “Display”
By name alone (doc-only inference):
- **binding / formatting**: `nbSetNegoMesVar`, `nbSetMesMakka`, `nbSetMesItem`
- **display lifecycle**: `nbDispNegoMessage`, `nbCheckNegoMessage`, `nbClsNegoMessage`
- **message lookup / list generation**: `GetMsgList`, `nbGetInfoMsg`
- **message loop drivers**: `nbNegoProcessMessageChk`, `nbNegoProcessMessage`

## Crosslink: Innen negotiation message ids
`datInnenNegoTable_s` carries these message-like fields:
- `mainmes`, `winmes`, `losemes`, `fullmes` (all `ushort`)
- plus `mescnt` (`ushort`)

That strongly implies `nbGetInnenNegoID(...)` and/or `nbGetInfoMsg(...)` can feed `innenmsg` / `flowmsg` / `questionmsg` from `datNegoTable.InnenNegoTable`.
