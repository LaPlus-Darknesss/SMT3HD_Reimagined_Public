# Message BIN Layout (Headers) — `itfMesBin*` (v10)

This is a header-level map of the message binary (“MesBin”) structures visible in `Il2Cppinterface_H/itfMesBin*.cs`.

The wrappers don’t expose full parsing code, but we *do* have:
- the header field layouts, and
- low-level helpers in `itfMesManager` (`GetTypeHeader`, `GetTypeHeader2`, `GetMessagePagePtr`).

---

## `itfMesBinFileHeader_t` (file header)

| name | type |
| --- | --- |
| filetype | byte |
| format | byte |
| user_id | short |
| filesize | uint |
| mgc | uint |
| extsize | uint |
| ofs_addrinfo | uint |
| size_addrinfo | uint |
| numtypeheader | uint |
| resolveaddress | byte |
| reserved0 | byte |
| version | ushort |


Working interpretation (field names only; verify later):
- `mgc` is a **magic** value.
- `filesize` is total file size.
- `extsize`, `ofs_addrinfo`, `size_addrinfo` suggest an address/relocation block.
- `numtypeheader` indicates an array of `itfMesBinTypeHeader_t`.
- `resolveaddress` implies the file supports **relocation**, consistent with `itfMesManager.ResolveResourceAddress(...)`.

## Type headers

### `itfMesBinTypeHeader_t`

| name | type |
| --- | --- |
| type | uint |
| ptr | int |


### `itfMesBinTypeHeader2_t`

| name | type |
| --- | --- |
| ppspkhead | int |
| spknum | int |
| pextdata | int |
| reserved | uint |


These appear to be “type table” nodes:
- `type` + `ptr` in `TypeHeader_t`
- `spknum` + pointers/offsets in `TypeHeader2_t`

The exact meaning is not derivable from fields alone, but the naming aligns with **speaker tables** and **extended data**.

---

## Message header (`itfMesBinMesHeader_t`)

| name | type |
| --- | --- |
| label | Il2CppStructArray<byte> |
| page | short |
| speaker | ushort |
| addr | Il2CppStructArray<uint> |
| len | Il2CppStructArray<int> |


Key points:
- `label` is a byte array (likely fixed-size C-string) used as an identifier.
- `page` is a page count.
- `speaker` is a speaker id (ushort).
- `addr[]` + `len[]` are per-page location/lengths.

### Page extraction seam

`itfMesManager.GetMessagePagePtr(itfMesBinMesHeader_t pmesheader, Il2CppStructArray<byte> data, int page)`  
IL2CPP token: **100669293**

This helper is the cleanest bridge from:
- a message header (`pmesheader`) and raw data bytes (`data`)
to:
- a page byte-array suitable for decoding/parsing.

---

## Selection header (`itfMesBinSelHeader_t`)

| name | type |
| --- | --- |
| label | Il2CppStructArray<byte> |
| ext | short |
| item | short |
| pattern | short |
| reserved | ushort |
| ptr | Il2CppStructArray<uint> |
| len | Il2CppStructArray<int> |


Notes:
- `label` likely parallels message labels (lookups).
- `item`, `pattern`, `ext` suggest selection UI variants.
- `ptr[]` + `len[]` look like per-choice offsets/lengths.

---

## Related `itfMesManager` type helpers

- `GetTypeHeader(...)` (IL2CPP token **100669291**)
- `GetTypeHeader2(...)` (IL2CPP token **100669292**)

These are likely used after the resource bytes are loaded and “resolved”.

---

## What we still need 

Without doing any runtime dumping, the next “pure research” step is:
- find the **producer/loader** side that constructs `message_tag.pbinheader` and `select_tag` from the BIN,
- identify the **encoding** (UTF-8 vs Shift-JIS vs custom tag encoding),
- map the inline tag bytecodes that drive `message_tag.last_*` fields.
