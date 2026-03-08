# 47 — Negotiation Data Surfaces (tables + packed blobs) — v9 (2026-02-08)

This tranche maps the **data surfaces** that negotiation pulls from, using the `Assembly-CSharp` Il2Cpp wrapper declarations.

Primary reference roots:
- `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/*`
- `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppnewdata_H/*`

## Surface index

| Surface | Type | Kind | Token | Wrapper |
|---|---|---|---|---|
| datNegoTable.datNegoReqTable | Il2CppReferenceArray<Il2CppStructArray<byte>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoReqItemListA | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppStructArray<byte>>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoTuikaReqtype | Il2CppReferenceArray<Il2CppStructArray<byte>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoReqItemListB | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppStructArray<byte>>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoMukanTypeList | Il2CppReferenceArray<Il2CppStructArray<byte>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoFightTaido | Il2CppReferenceArray<Il2CppStructArray<byte>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoPrivateQues | Il2CppReferenceArray<Il2CppStructArray<byte>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoNormalQuesInfo | Il2CppReferenceArray<Il2CppReferenceArray<datNegoNormalQuesInfo_t>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.datNegoKotowariQuesInfo | Il2CppReferenceArray<Il2CppReferenceArray<Il2CppReferenceArray<Il2CppStructArray<byte>>>> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.InnenNegoTable | Il2CppReferenceArray<datInnenNegoTable_t> | table |  | Il2Cpp/datNegoTable.cs |
| datNegoTable.InfoSelectTable | Il2CppReferenceArray<datInfoSelectTable_t> | table |  | Il2Cpp/datNegoTable.cs |
| datDevilNegoFormat.tbl | Il2CppReferenceArray<datDevilNegoFormat_t> | table |  | Il2Cpp/datDevilNegoFormat.cs |
| datDevilNegoFormat.Get(id) | datDevilNegoFormat_t | method | 100672633 | Il2Cpp/datDevilNegoFormat.cs |
| datNegoSkill.tbl | Il2CppReferenceArray<datNegoSkill_t> | table |  | Il2Cpp/datNegoSkill.cs |
| nego_help.tbl | Il2CppStructArray<byte> | blob |  | Il2Cpp/nego_help.cs |
| nego_help_UP.tbl | Il2CppStructArray<byte> | blob |  | Il2Cpp/nego_help_UP.cs |


### Notes on “packed blob” tables
Several negotiation tables are exposed as `Il2CppStructArray<byte>` or nested arrays of byte arrays. These are **serialized record blobs** whose internal layout is not visible from the wrappers alone.

For long-term work, treat these as:
- **opaque sources** (observe/override the *consumer* methods), until we have a confirmed record layout
- candidates for a future **format RE** pass once we decide the exact knob we want to tune (request amounts, question pools, etc.)

## Typed struct schemas (direct field layouts)

### `datDevilNegoFormat_s` (per-demon negotiation profile)
Source: `Il2Cppnewdata_H/datDevilNegoFormat_s.cs`

| name | type |
|---|---|
| kucyo | uint |
| tokucyo | uint |
| tokuboutype | byte |
| honboutype | byte |
| fighttype | byte |
| reqtype | byte |
| tuikareqtype | byte |
| privatesel | byte |
| privateselindex | byte |
| senseitype | byte |
| inotitype | byte |
| binjotype | byte |
| mukantype | ushort |
| mukanindex | ushort |
| itema | Il2CppReferenceArray<datNegoPresent_s> |
| itemb | Il2CppReferenceArray<datNegoPresent_s> |
| stone | Il2CppReferenceArray<datNegoPresent_s> |


Practical interpretation from field names:
- `kucyo`, `tokucyo`: likely “speech style / tone” selectors (uint IDs)
- `reqtype` / `tuikareqtype`: primary/secondary demand kind
- `itema`, `itemb`, `stone`: per-demand “present” lists, each entry is `datNegoPresent_s`

Access patterns:
- table is `datDevilNegoFormat.tbl`
- direct getter exists: `datDevilNegoFormat.Get(int id)` (token `100672633`)

### `datNegoPresent_s` (a single “present” entry)
Source: `Il2Cppnewdata_H/datNegoPresent_s.cs`

| name | type |
|---|---|
| ritu | byte |
| item | byte |


### `datNegoSkill_s` (negotiation skill behavior spec)
Source: `Il2Cppnewdata_H/datNegoSkill_s.cs`

| name | type |
|---|---|
| type | uint |
| rate | Il2CppStructArray<byte> |
| killeraisyo | Il2CppReferenceArray<datNegoAisyo_s> |
| ngaisyo | Il2CppReferenceArray<datNegoAisyo_s> |


Notes:
- `rate` is a raw byte array, so it is likely a packed mini-table (e.g., per-step chances or per-level rates).
- `killeraisyo` / `ngaisyo` are typed arrays of `datNegoAisyo_s`.

### `datNegoAisyo_s` (aisyo transition + message id)
Source: `Il2Cppnewdata_H/datNegoAisyo_s.cs`

| name | type |
|---|---|
| from | uint |
| to | uint |
| mes | int |


The `mes` field is an `int` and is very likely a **message id** used during negotiation (“aisyo change” lines).

### `datInnenNegoTable_s` (Innen negotiation table)
Source: `Il2Cppnewdata_H/datInnenNegoTable_s.cs`

| name | type |
|---|---|
| check | short |
| id1 | ushort |
| id2 | ushort |
| result | ushort |
| item | Il2CppReferenceArray<Il2CppStructArray<ushort>> |
| mainmes | ushort |
| mescnt | ushort |
| winmes | ushort |
| losemes | ushort |
| fullmes | ushort |
| innen_motion | Il2CppReferenceArray<InnenMotionInfo_s> |


The following fields are strong “message id” candidates by name:
- `mainmes`, `winmes`, `losemes`, `fullmes`
…and `mescnt` looks like the “message count” for a run.

### `datInfoSelectTable_s` (info selection gating)
Source: `Il2Cppnewdata_H/datInfoSelectTable_s.cs`

| name | type |
|---|---|
| flagcheck | Il2CppStructArray<short> |
| fieldno | Il2CppStructArray<short> |


This is a compact gating table:
- `flagcheck`: a list of small condition codes
- `fieldno`: likely maps to a “field id / map id / scenario id” surface used by `nbGetInfoMsg` or related selection logic.
