# Save/Load Mod Tactics (doc-only, wrapper-grounded)

This is a “what can we hook?” brainstorm that stays strictly within what the wrappers prove exists.
No runtime behavior is assumed beyond method naming + ownership.

---

## Lowest-risk seams (observation-first)

### Slot summary
- `FsSaveData.loadHeaderGameFileInfo(slot, out GameFileInfo_t)` — token `100674366`

This is useful for:
- Lets you observe which slot is being queried and what header data is presented to the user.
- Works even if you don’t want to touch full save buffers yet.

### Suspend display + resume context
- `FsSaveData.GetSuspendFieldArea(out fieldID, out areaID)` — token `100674367`
- `FsSaveData.GetSuspendDispName(out nameU, out nameD)` — token `100674368`

This is useful for:
- Cleanly isolates suspend-only metadata, separate from full game save.

---

## High-impact seams (requires more care)

### Full save serialization boundary
- `SAVEDATA.backupData(slot, globalWork, isSuspend?)` — token `100674395`

Potential uses:
- Pre-serialize patching: modify `dds3GlobalWork.DDS3_GBWK` fields before serialization.
- Consistency rules: enforce invariants (e.g., clamp invalid IDs) before data becomes persistent.

### Full load reconstruction boundary
- `SAVEDATA.restoreData(slot, out globalWork, out additionWork)` — token `100674390`

Potential uses:
- Post-load patching: adjust global work after reconstructing.
- Version-fix experimentation: compare with `FsSaveData.LoadCorrectionVer6/Ver7`.

---

## Crypto / storage boundary (Steam)

### Encrypt/decrypt entry points
- `SteamSaveData.SaveFile(bytes, filename)` — token `100674187`
- `SteamSaveData.LoadFile(filename) -> byte[]` — token `100674188`
- `SteamSaveData.GetFilePath() -> string` — token `100674191`
- `SteamSaveData.Encrypt(byte[]) -> byte[]` — token `100674192` (private)
- `SteamSaveData.Decrypt(byte[]) -> byte[]` — token `100674193` (private)

Potential uses:
- Observability: log file names, sizes, paths, and timing.
- Tooling: use the same encrypt/decrypt routines to build external save tooling later (without re-implementing the algorithm).

---

## Local config persistence

- `FsSaveData.SteamConfigLocalLoad()` — token `100674373`
- `FsSaveData.SteamConfigLocalSave()` — token `100674374`
- `FsSaveData.GetConfigLocal(idx)` — token `100674375`
- `FsSaveData.SetConfigLocal(idx, int[])` — token `100674376`

Potential uses:
- Persisting “mod settings” in an existing config-like area, if it’s safe and doesn’t conflict with game usage.
- (Needs later confirmation) whether indices are reserved/validated.

---

## Wrapper gaps worth tracking
- `dds3WorkVer7_tag` appears in signatures but is not present as a standalone wrapper file in the reference set.
  - If we later need that struct, we’ll likely have to discover it via token → metadata → field walk at runtime.

