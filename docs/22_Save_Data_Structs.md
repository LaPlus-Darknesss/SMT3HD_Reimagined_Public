# Save Data Structs (field maps)

This section is a **field/type map** of the core payload structs that appear directly in save/load signatures.

---

## `GameFileInfo_t` (header / slot summary)

Fields:

- `Lv` : `char`
- `PlayTime` : `int`
- `TmnlId` : `char`
- `TmnlMode` : `char`
- `autoMapOpen` : `Il2CppStructArray<sbyte>`
- `autoMapOpenEX` : `Il2CppStructArray<sbyte>`
- `cname_code` : `string`
- `cname_nums` : `char`
- `dante_mode` : `char`
- `end_list` : `Il2CppStructArray<char>`
- `endcnt` : `short`
- `mode` : `char`
- `nameLang` : `int`
- `newfile` : `short`
- `pad3` : `string`
- `saveTime` : `long`
- `ver` : `char`
- `verstr` : `Il2CppStructArray<char>`

Notable interpretation hints (name-only):
- `Lv`, `PlayTime`, `mode`, `saveTime` are consistent with slot summary screens.
- `TmnlMode` / `TmnlId` match terminal-related fields in `fldSave_t`.
- `autoMapOpen` / `autoMapOpenEX` look like per-slot “map discovered” toggles.
- `nameLang` likely selects which localized name table to use for the player/demon name display.

---

## `dds3AdditionWork_t` (suspend / field resume state)

Fields:

- `AmFloor` : `int`
- `BgmId` : `int`
- `CamMode` : `byte`
- `CamTableIdx` : `int`
- `SeId` : `int`
- `WarpIndex` : `byte`
- `areaID` : `int`
- `dummy` : `Il2CppStructArray<sbyte>`
- `fieldID` : `int`
- `gFldNowDZone` : `byte`
- `nameD` : `int`
- `nameU` : `int`
- `playerPos` : `Vector4`
- `playerRot` : `Vector4`

Notes:
- `playerPos` and `playerRot` are persisted as vectors (Unity types).
- `CamTableIdx`, `CamMode` look like camera mode/slot selection.
- `nameU` / `nameD` correlate with `FsSaveData.GetSuspendDispName(...)`.

---

## `fldSave_t` (field save block)

Fields:

- `AnahoriCnt` : `int`
- `PushBlock` : `Il2CppReferenceArray<Il2CppReferenceArray<fldBlockBuff_t>>`
- `amap` : `fldAutoMapBuff_t`
- `pub` : `Il2CppStructArray<int>`
- `takara` : `Il2CppStructArray<byte>`
- `takara2` : `Il2CppStructArray<byte>`
- `tmnlid` : `short`
- `tmnltype` : `short`

Sub-structs referenced here:

### `fldBlockBuff_t`
- `EanimeCngID` : `Il2CppStructArray<byte>`
- `EanimeOffFlg` : `ushort`
- `EviewOffFlg` : `ushort`
- `ManimeOffFlg` : `ushort`
- `MviewOffFlg` : `ushort`
- `gimicLastFlg` : `ushort`
- `hitOffFlg` : `ushort`
- `npcOffFlg` : `ushort`

### `fldAutoMapBuff_t`
- `blkOpenFlg` : `Il2CppReferenceArray<Il2CppStructArray<uint>>`

Also present in the reference set:

### `fldSaveNml_t`
- `_fld` : `short`
- `_flg` : `short`

### `fldSaveSml_t`
- `_fld` : `short`
- `_jmp` : `short`

---

## `dds3GlobalWork_t` (main save payload)

Fields (top-level):

- `EventBit` : `Il2CppStructArray<int>`
- `FloatVariable` : `Il2CppStructArray<float>`
- `GameFileInfo` : `GameFileInfo_t`
- `IntVariable` : `Il2CppStructArray<int>`
- `Moon` : `evtMoon_t`
- `amap31` : `fldAutoMapBuff31_t`
- `amap31_Ver7` : `fldAutoMapBuff31_t`
- `amapEX` : `fldAutoMapBuffEX_t`
- `amapEX_Ver7` : `fldAutoMapBuffEX_t`
- `amap_Ver7` : `fldAutoMapBuff_t`
- `boss_press` : `Il2CppReferenceArray<Il2CppStructArray<int>>`
- `bouga_cnt` : `ushort`
- `cname_code` : `Il2CppReferenceArray<Il2CppStructArray<byte>>`
- `cname_nums` : `Il2CppStructArray<byte>`
- `config_data` : `Il2CppReferenceArray<Il2CppStructArray<int>>`
- `dmy` : `int`
- `encountcnt` : `ushort`
- `encountkyori` : `float`
- `encyc_record` : `fclEncycRecord_t`
- `fldSave` : `fldSave_t`
- `hearts` : `Il2CppStructArray<byte>`
- `hearts_sk` : `Il2CppReferenceArray<Il2CppStructArray<ushort>>`
- `hearts_up_param` : `Il2CppReferenceArray<Il2CppStructArray<sbyte>>`
- `heartscnt` : `byte`
- `heartsequip` : `byte`
- `heartsskcnt` : `Il2CppStructArray<byte>`
- `item` : `Il2CppStructArray<byte>`
- `kotowari` : `Il2CppStructArray<byte>`
- `maka` : `int`
- `maxstock` : `int`
- `mh` : `sdfMemHandle_t`
- `nameLanguage` : `int`
- `name_code` : `Il2CppReferenceArray<Il2CppStructArray<byte>>`
- `name_nums` : `Il2CppReferenceArray<Il2CppStructArray<byte>>`
- `negoques` : `Il2CppStructArray<byte>`
- `playtime` : `int`
- `runtime` : `int`
- `stat` : `uint`
- `stockcnt` : `int`
- `stocklist` : `Il2CppStructArray<int>`
- `syakkincnt` : `Il2CppStructArray<byte>`
- `unitwork` : `Il2CppReferenceArray<datUnitWork_t>`
- `wakimi_id` : `ushort`

High-signal clusters:
- **Slot header:** `GameFileInfo`
- **Runtime variable banks:** `IntVariable`, `FloatVariable`
- **Progress flags:** `EventBit`, `Moon`
- **Party state:** `unitwork`, `stocklist`, `stockcnt`, `maxstock`
- **Inventory:** `item`
- **Field persistence:** `fldSave`
- **Config-ish:** `config_data`, `nameLanguage`
- **Automap / exploration:** `amap31`, `amapEX`, plus Ver7 variants

