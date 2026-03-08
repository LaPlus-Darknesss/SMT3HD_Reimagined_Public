# Save System Overview (Assembly-CSharp surface)

This section documents what we can prove from the **Assembly-CSharp IL2CPP wrappers** about save/load, suspend saves, and local config persistence.

> Notes on confidence:
> - Anything listed as a **type/member/token** is concrete.
> - Anything described as a **call flow** is a best-effort inference based on which class owns which method surface; it should be confirmed later via runtime logging if/when we decide to do so.

---

## Primary types (roles)

### `FsSaveData` (MonoBehaviour)
Save/load orchestrator and buffer owner.

**Key fields (high signal):**
- `filePath` : `string`
- `fileName` : `string`
- `mountName` : `string`
- `saveDataVersion` : `int`
- `saveDataSize` : `int`
- `saveData` : `Il2CppStructArray<byte>`
- `local_config_data` : `Il2CppReferenceArray<Il2CppStructArray<int>>`
- `configver` : `int`

**Key methods (tokens):**
- `FsSaveData.Save(slot, globalWork, isSuspend?) -> bool` — token `100674359`
- `FsSaveData.Load(slot, out globalWork, out additionWork) -> int` — token `100674365`
- `FsSaveData.CreateSaveBuffer() -> byte[]` — token `100674360`
- `FsSaveData.loadHeaderGameFileInfo(slot, out GameFileInfo_t)` — token `100674366`
- `FsSaveData.GetSuspendFieldArea(out fieldID, out areaID)` — token `100674367`
- `FsSaveData.GetSuspendDispName(out nameU, out nameD)` — token `100674368`

**Version correction hooks (internal):**
- `LoadCorrectionVer6(...)` — token `100674363`
- `LoadCorrectionVer7(...)` — token `100674364`

**Local config (Steam) surface:**
- `SteamConfigLocalLoad()` — token `100674373`
- `SteamConfigLocalSave()` — token `100674374`
- `GetConfigLocal(idx) -> int[]` — token `100674375`
- `SetConfigLocal(idx, int[])` — token `100674376`

---

### `SAVEDATA`
Serializer/transform layer for the actual payload structs. It exposes a backup/restore vocabulary, including a “Ver7” set of routines.

**Key methods (tokens):**
- `SAVEDATA.backupData(slot, globalWork, isSuspend?)` — token `100674395`
- `SAVEDATA.restoreData(slot, out globalWork, out additionWork)` — token `100674390`
- `SAVEDATA.setHeaderGameFileInfo(slot, ref GameFileInfo_t)` — token `100674387`
- `SAVEDATA.backupSystemVer7()` — token `100674380`
- `SAVEDATA.restoreSystemVer7()` — token `100674379`
- `SAVEDATA.initialize()` — token `100674382`

**Observed internal responsibilities (from method names/signatures):**
- Push-block state backup/restore (`backupPushBlock`, `restorePushBlock`) — likely maps to `fldSave_t.PushBlock`.
- “Buff” copy/restore between *tag* and *t* structs (`backupBuff`, `restoreBuff`).
- `restoreAddition(ref dds3AdditionWork_t)` suggests suspend-position/state reconstruction.

> Wrapper gap: `dds3WorkVer7_tag` appears in signatures but does not have a standalone wrapper file in the reference set (only appears inside `FsSaveData` / `SAVEDATA`).

---

### `SteamSaveData`
Storage + encryption boundary.

**Key fields:**
- `key_code`
- `iv_code`

**Key methods (tokens):**
- `SteamSaveData.SaveFile(bytes, filename)` — token `100674187`
- `SteamSaveData.LoadFile(filename) -> byte[]` — token `100674188`
- `SteamSaveData.GetFileSize(filename) -> int` — token `100674189`
- `SteamSaveData.GetFreeSpace(refresh?) -> ulong` — token `100674190`
- `SteamSaveData.GetFilePath() -> string` — token `100674191`

**Crypto helpers (private static):**
- `Encrypt(byte[])->byte[]` — token `100674192`
- `Decrypt(byte[])->byte[]` — token `100674193`

---

### Runtime root: `dds3GlobalWork`
Provides a strongly-named global pointer:

- `dds3GlobalWork.DDS3_GBWK : dds3GlobalWork_t` (static property)

This is the **in-memory** global work object that `FsSaveData` / `SAVEDATA` likely serialize.

---

## Payload structs that actually get persisted

### `dds3GlobalWork_t` (main save payload)
Includes:
- `GameFileInfo` header block
- runtime variables (`IntVariable`, `FloatVariable`)
- event bits, moon, currency, stats, party/unitwork, stocklist
- item inventory
- field save (`fldSave`)
- config-ish array (`config_data`)
- automap variants (`amap31`, `amapEX`, `amap_Ver7`, ...)

(Full field map is in `22_Save_Data_Structs.md`.)

### `dds3AdditionWork_t` (suspend/field context payload)
Stores:
- `fieldID`, `areaID`
- `playerPos`, `playerRot`
- camera indices/modes
- audio ids (BGM/SE)
- warp index
- additional display names

### `fldSave_t` (field save sub-struct)
Stores:
- push-block buffers
- automap open flags
- treasure buffers (`takara`, `takara2`)
- terminal info (`tmnltype`, `tmnlid`)
- misc counters / arrays (`AnahoriCnt`, `pub`)

---

## Probable high-level flow (needs runtime confirmation later)

### Save
1. UI/menu invokes `FsSaveData.Save(slot, globalWork, ...)`.
2. `FsSaveData` uses `SAVEDATA.backupData(...)` to serialize/prepare payload.
3. Data is encrypted via `SteamSaveData.Encrypt(...)`.
4. Data is written by `SteamSaveData.SaveFile(...)` to a path under `SteamSaveData.GetFilePath()`.

### Load
1. UI/menu invokes `FsSaveData.Load(slot, out globalWork, out additionWork)`.
2. File bytes are read via `SteamSaveData.LoadFile(...)`.
3. Data is decrypted via `SteamSaveData.Decrypt(...)`.
4. `SAVEDATA.restoreData(...)` reconstructs the `dds3GlobalWork_t` + `dds3AdditionWork_t`.
5. Version correction may occur via `FsSaveData.LoadCorrectionVer6/Ver7`.

---

## Quick reference
- Full token lists: `data/part4_save_methods.csv`
- Curated seam shortlist: `data/part4_save_seams_curated.csv`
