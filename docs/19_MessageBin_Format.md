# 19 — Message BIN Format (itfMesBin* headers)

The references include several `Il2Cppinterface_H/itfMesBin*` types which strongly suggest a **binary message container format**.

This doc is intentionally conservative:
- It lists **fields and sizes** that are directly present in the headers
- It avoids guessing about magic values or full layout beyond what the headers reveal

Raw field list: `data/part3_itfMesBin_headers.csv`

---

## 1) File header: `itfMesBinFileHeader_t`

**File:** `Il2Cppinterface_H/itfMesBinFileHeader_t.cs`  
**Layout:** `[StructLayout(LayoutKind.Explicit)]` with field offsets.

Fields (offset → name:type):
- `0`  → `filetype : byte`
- `1`  → `format : byte`
- `2`  → `user_id : short`
- `4`  → `filesize : uint`
- `8`  → `mgc : uint`
- `12` → `extsize : uint`
- `16` → `ofs_addrinfo : uint`
- `20` → `size_addrinfo : uint`
- `24` → `numtypeheader : uint`
- `28` → `resolveaddress : byte`
- `29` → `reserved0 : byte`
- `30` → `version : ushort`

Observation:
- This strongly looks like a 32-byte header (last field at offset 30, 2 bytes).

---

## 2) Type headers: `itfMesBinTypeHeader_t` and `itfMesBinTypeHeader2_t`

**Files:**
- `Il2Cppinterface_H/itfMesBinTypeHeader_t.cs`
- `Il2Cppinterface_H/itfMesBinTypeHeader2_t.cs`

Public properties observed:
- `itfMesBinTypeHeader_t`
  - `type : uint`
  - `ptr : int`
- `itfMesBinTypeHeader2_t`
  - `ppspkhead : int`
  - `spknum : int`
  - `pextdata : int`
  - `reserved : uint`

These are accessed by:
- `itfMesManager.GetTypeHeader(instanceMes_tag, int)` (token **100669291**)
- `itfMesManager.GetTypeHeader2(Il2CppStructArray<byte>)` (token **100669292**)

---

## 3) Message headers: `itfMesBinMesHeader_t`

**File:** `Il2Cppinterface_H/itfMesBinMesHeader_t.cs`

Public properties:
- `label : Il2CppStructArray<byte>`
- `page : short`
- `speaker : ushort`
- `addr : Il2CppStructArray<uint>`
- `len : Il2CppStructArray<int>`

And the accessor that ties it to bytes:
- `itfMesManager.GetMessagePagePtr( ..., itfMesBinMesHeader_t, ..., int)` (token **100669293**)

Interpretation (grounded, minimal):
- a message has a label, a page count/index, a speaker id, and a table of per-page offsets (`addr`) + lengths (`len`).

---

## 4) Selection headers: `itfMesBinSelHeader_t`

**File:** `Il2Cppinterface_H/itfMesBinSelHeader_t.cs`

Public properties:
- `label : Il2CppStructArray<byte>`
- `ext : short`
- `item : short`
- `pattern : short`
- `reserved : ushort`
- `ptr : Il2CppStructArray<uint>`
- `len : Il2CppStructArray<int>`

Interpretation (minimal):
- a selection block has a label and metadata (`ext`, `item`, `pattern`) plus a table of option offsets/lengths.

---

## 5) Why this matters for modding

Even without runtime dumping yet, this is already useful because:

- It tells us `itfMesManager` is not just “UI sugar” — it knows about a structured **binary message resource**.
- If later you want to:
  - locate *exact* message pages,
  - change pagination behavior,
  - or translate a message into a longer string while preserving page breaks,
  We will likely need to understand and/or validate these header-based lookups.

