# 49 — Negotiation Requests (Makka/Items/HP/MP/Stone): Pipeline Map — v9 (2026-02-08)

This tranche documents the **request subsystem** inside negotiation: how request type/amounts/items are staged and which tables likely feed them.

Primary reference sources:
- `Il2Cpp/nbNegoProcess.cs`
- `Il2Cpp/nbNegoProcessData_t.cs`
- `Il2Cppnewdata_H/datDevilNegoFormat_s.cs`
- `Il2Cpp/datNegoTable.cs`

## Request-related state in `nbNegoProcessData_t`

| Field | Type | Notes |
|---|---|---|
| requesttype | int |  |
| requestdata | int |  |
| requesthpmp | int |  |
| prevreqtype | int |  |
| negoreqtype | int |  |
| negoskillid | uint |  |
| getmakka | int |  |
| getitem | int |  |


Important fields by name:
- `requesttype` / `requestdata` / `requesthpmp`: current demand (kind + payload)
- `prevreqtype`: previous demand kind (likely to prevent repeats)
- `negoreqtype`: negotiation “effective request type” (can differ from `requesttype`)
- `getmakka` / `getitem`: payout amounts after a successful deal

## Request and “present” helpers in `nbNegoProcess`

| Method | Token |
|---|---|
| nbSetMesMakka(int msgid, int i) -> void | 100672228 |
| nbSetMesItem(int msgid, int it) -> void | 100672229 |
| nbGetNegoYaruMakkaA(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672234 |
| nbGetNegoYaruMakkaB(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672235 |
| nbGetNegoKureMakkaA(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672236 |
| nbGetNegoKureMakkaB(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672237 |
| nbGetNegoKureHpA(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672238 |
| nbGetNegoKureHpB(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672239 |
| nbGetNegoKureMpA(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672240 |
| nbGetNegoKureMpB(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672241 |
| nbGetKureItemA(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672242 |
| nbSelectKureItemA(int lv_idx) -> int | 100672243 |
| nbGetKureItemB(ref nbNegoProcessData_t n, ref datUnitWork_t a, ref datUnitWork_t b) -> int | 100672244 |
| nbSelectKureItemB(int lv_idx) -> int | 100672245 |
| nbGetNegoRequestType(int reqtype) -> int | 100672246 |
| nbGetNegoTuikaRequestType(int tuikareqtype) -> int | 100672247 |
| nbGetQuestionType(ref datUnitWork_t w) -> int | 100672251 |
| nbGetNegoYaruItemA(ref datUnitWork_t w) -> int | 100672252 |
| nbGetNegoYaruItemB(ref datUnitWork_t w) -> int | 100672253 |
| nbGetNegoYaruStone(ref datUnitWork_t w) -> int | 100672254 |


### Interpretation (doc-only, based on names)
The method set splits into 3 layers:

**A) “What does the demon ask for?”**
- `nbGetNegoRequestType(ref nbNegoProcessData_t)`
- `nbGetNegoTuikaRequestType(ref nbNegoProcessData_t)`
- `nbGetQuestionType(ref nbNegoProcessData_t)` (ties into question selection + follow-up demands)

**B) “How much / which item?”**
- Makka: `nbGetNegoYaruMakkaA/B`, `nbGetNegoKureMakkaA/B`
- HP/MP: `nbGetNegoKureHpA/B`, `nbGetNegoKureMpA/B`
- Items: `nbGetKureItemA/B`, `nbSelectKureItemA/B`
- Presents / stone lists: `nbGetNegoYaruItemA/B`, `nbGetNegoYaruStone`

**C) “How does it get printed to the message window?”**
- `nbSetMesMakka(msgid, i)`
- `nbSetMesItem(msgid, i)`
- plus the general binder `nbSetNegoMesVar(ref nbNegoProcessData_t)` (see doc 48)

## Data sources for request behavior

### Per-demon: `datDevilNegoFormat_s`
Fields that clearly map to request selection:
- `reqtype`, `tuikareqtype`
- `itema`, `itemb`, `stone` (each is `Il2CppReferenceArray<datNegoPresent_s>`)

Access:
- `datDevilNegoFormat.tbl` (typed table)
- `datDevilNegoFormat.Get(id)` (token `100672633`)

### Global negotiation tables: `datNegoTable`
Fields that look like they back request type and item lists:
- `datNegoReqTable`: `ReferenceArray<StructArray<byte>>` (packed blob records)
- `datNegoReqItemListA/B`: nested arrays of packed blobs
- `datNegoTuikaReqtype`: packed blob list
- `datNegoMukanTypeList`, `datNegoFightTaido`: packed blobs likely controlling behavior branches
- `datNegoNormalQuesInfo`: typed table (question type + ritu weights)

Because many of these are packed blobs, **method-level overrides** (hook the nbGet* helpers) are the safest high-ROI knob until we RE the binary layouts.
