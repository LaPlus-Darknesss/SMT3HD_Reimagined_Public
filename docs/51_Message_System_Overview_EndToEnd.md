# Message System — End-to-End Overview (v10)

Scope: this tranche drills into the **message pipeline** that drives dialogue / system text / selection prompts, based on the `Assembly-CSharp` decompile wrappers:

- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/itfMesManager.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/message_tag.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/instanceMes_tag.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/mesflow_tag.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppinterface_H/itfMesBin*.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/itfPanel.cs` (window geometry + draw)

This is aimed at **high-ROI seams** for future text mods: message requests, variable substitution, selection prompts, and string-return helpers.

---

## Core objects (what “holds the state”)

### `instanceMes_tag` — per-message-flow runtime state

Key fields (selected):

| name | type |
| --- | --- |
| stat | uint |
| pdata | Il2CppSystem.Object |
| pext | Il2CppSystem.Object |
| z | uint |
| otindex | short |
| type | short |
| speaker | Speaker_tag |
| message | message_tag |
| select | select_tag |
| window | window_tag |
| variable | variable_tag |
| curctr | curctr_tag |
| func_kuti | instanceMes_tag.func_kutiDelegate |
| master_text | Il2CppStringArray |
| key | string |
| id | int |


Notes (observed from field names/types; meaning to be confirmed later):
- `speaker`, `message`, `select`, `window` are sub-structures for the visible window.
- `variable` likely backs variable substitution (script-driven inserts).
- `func_kuti` looks like a callback seam (lip sync / mouth “kuti” in Atlus terminology).

### `message_tag` — message text state + last formatting

Key fields:

| name | type |
| --- | --- |
| pos | sdfIVector2_t |
| pbinheader | itfMesBinMesHeader_t |
| pfont | _FRQ |
| phase | sbyte |
| wait | byte |
| last_style | byte |
| last_col | byte |
| last_tp | byte |
| last_spd | byte |
| lines | short |
| page | short |
| maxpage | short |
| callmode | int |


Notes:
- The `last_*` fields (`last_style/col/tp/spd`) strongly suggest an **inline-tag parser** that mutates formatting while rendering.

### `window_tag` / `select_tag` / `Speaker_tag`

`window_tag`:

| name | type |
| --- | --- |
| spk | itfpanel_t |
| mes | itfpanel_t |
| mes_offset | sdfIVector4_t |
| mes_color | sdfIVector4_t |


`select_tag`:

| name | type |
| --- | --- |
| pos | sdfIVector2_t |
| pfont | _FRQ |
| mask | uint |
| phase | short |
| select | short |
| select_imm | short |
| lines | short |
| alpha | short |
| numdef | short |
| defkey | Il2CppReferenceArray<defkey_tag> |


`Speaker_tag`:

| name | type |
| --- | --- |
| pos | sdfIVector2_t |
| pfont | _FRQ |
| num | ushort |


---

## Where message text lives (BIN headers)

At runtime, `message_tag.pbinheader` points at an `itfMesBinMesHeader_t`:

| name | type |
| --- | --- |
| label | Il2CppStructArray<byte> |
| page | short |
| speaker | ushort |
| addr | Il2CppStructArray<uint> |
| len | Il2CppStructArray<int> |


…and selection prompts use `itfMesBinSelHeader_t`:

| name | type |
| --- | --- |
| label | Il2CppStructArray<byte> |
| ext | short |
| item | short |
| pattern | short |
| reserved | ushort |
| ptr | Il2CppStructArray<uint> |
| len | Il2CppStructArray<int> |


**Key takeaway:** message content is page-based (`addr[]` + `len[]` per page), and each message/selection has a `label` field (byte array), presumably used for lookup/identification.

---

## High-ROI seams in `itfMesManager` (IL2CPP tokens)

Below are the most actionable entry points (from `NativeMethodInfoPtr_* = GetIl2CppMethodByToken(..., <token>)` in the wrapper). This list is meant to guide later hook selection.

| base | il2cpp_token | field |
| --- | --- | --- |
| CalcMesFlow | 100669339 | CalcMesFlow_Internal_Static_Void_instanceMes_tag_0 |
| CalcMessage | 100669341 | CalcMessage_Internal_Static_Void_instanceMes_tag_0 |
| ClearMessage | 100669297 | ClearMessage_Internal_Static_Void_0 |
| ClearSpeaker | 100669298 | ClearSpeaker_Internal_Static_Void_0 |
| DrawMesFlow | 100669402 | DrawMesFlow_Private_Static_Void_instanceMes_tag_0 |
| DrawMessage | 100669404 | DrawMessage_Internal_Static_Int32__FRQ_0 |
| ForceWindowDispOff | 100669300 | ForceWindowDispOff_Public_Static_Void_0 |
| GetMessagePagePtr | 100669293 | GetMessagePagePtr_Internal_Static_Il2CppStructArray_1_Byte_itfMesBinMesHeader_t_Il2CppStructArray_1_Byte_Int32_0 |
| GetSpeakerName | 100669313 | GetSpeakerName_Internal_Static_Il2CppStructArray_1_Byte_instanceMes_tag_0 |
| GetTypeHeader | 100669291 | GetTypeHeader_Internal_Static_itfMesBinTypeHeader_t_instanceMes_tag_Int32_0 |
| GetTypeHeader2 | 100669292 | GetTypeHeader2_Internal_Static_itfMesBinTypeHeader2_t_Il2CppStructArray_1_Byte_0 |
| InitializeWindow | 100669331 | InitializeWindow_Internal_Static_Void_window_tag_0 |
| OpenMessageWindow | 100669326 | OpenMessageWindow_Internal_Static_Void_instanceMes_tag_0 |
| RaiseMessageSpeedMax | 100669324 | RaiseMessageSpeedMax_Internal_Static_Void__FRQ_0 |
| RaiseMessageSpeedMaxAll | 100669325 | RaiseMessageSpeedMaxAll_Internal_Static_Void__FRQ_0 |
| ResolveResourceAddress | 100669290 | ResolveResourceAddress_Internal_Static_Void_Object_0 |
| SwitchSpeaker | 100669309 | SwitchSpeaker_Internal_Static_Void_Boolean_Boolean_0 |
| WindowDispOff | 100669299 | WindowDispOff_Internal_Static_Void_Boolean_0 |
| WindowDispOn | 100669295 | WindowDispOn_Public_Static_Void_Int32_0 |
| WindowDispOn | 100669296 | WindowDispOn_Internal_Static_Void_instanceMes_tag_0 |
| itfMesMngChangeWindowType | 100669386 | itfMesMngChangeWindowType_Public_Static_Void_Int32_Int32_Int32_0 |
| itfMesMngCloseWindow | 100669352 | itfMesMngCloseWindow_Public_Static_Int32_0 |
| itfMesMngGetSelectNo | 100669348 | itfMesMngGetSelectNo_Public_Static_Int32_Int32_0 |
| itfMesMngGetStrImmediate | 100669384 | itfMesMngGetStrImmediate_Public_Static_String_Int32_Int32_Int32_0 |
| itfMesMngGetVarLocalize | 100669378 | itfMesMngGetVarLocalize_Public_Static_String_Int32_Int32_0 |
| itfMesMngInitialize | 100669364 | itfMesMngInitialize_Public_Static_Int32_Object_Il2CppStringArray_String_0 |
| itfMesMngInitializeManager | 100669390 | itfMesMngInitializeManager_Public_Static_Void_0 |
| itfMesMngOpenWindow | 100669351 | itfMesMngOpenWindow_Public_Static_Int32_0 |
| itfMesMngRelease | 100669365 | itfMesMngRelease_Public_Static_Void_Int32_0 |
| itfMesMngRequestMessage | 100669366 | itfMesMngRequestMessage_Public_Static_Int32_Int32_Int32_Int32_Int32_0 |
| itfMesMngRequestMessageRelease | 100669368 | itfMesMngRequestMessageRelease_Public_Static_Void_Int32_Int32_0 |
| itfMesMngRequestSelect | 100669369 | itfMesMngRequestSelect_Public_Static_Void_Int32_Int32_0 |
| itfMesMngRequestSelectRelease | 100669370 | itfMesMngRequestSelectRelease_Public_Static_Void_Int32_0 |
| itfMesMngSelect | 100669345 | itfMesMngSelect_Public_Static_Int32_0 |
| itfMesMngSetDefaultSpeed | 100669388 | itfMesMngSetDefaultSpeed_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetFloor | 100669380 | itfMesMngSetFloor_Public_Static_String_Int32_0 |
| itfMesMngSetKutiCallback | 100669389 | itfMesMngSetKutiCallback_Public_Static_Void_Int32_func_kutiDelegate_0 |
| itfMesMngSetMaskSelect | 100669371 | itfMesMngSetMaskSelect_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngSetResourcePtr | 100669383 | itfMesMngSetResourcePtr_Public_Static_Object_Int32_Object_0 |
| itfMesMngSetSelect | 100669372 | itfMesMngSetSelect_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetVar | 100669379 | itfMesMngSetVar_Public_Static_Void_Int32_Int32_Int32_Int32_0 |
| itfMesMngSetVarImmediate | 100669381 | itfMesMngSetVarImmediate_Public_Static_Void_Int32_Int32_String_0 |
| itfMesMngSetVarImmediate2 | 100669382 | itfMesMngSetVarImmediate2_Public_Static_Void_Int32_Int32_String_Int32_0 |
| itfMesMngWindowType | 100669361 | itfMesMngWindowType_Public_Static_Int32_0 |
| itfMesMngWindowTypeAlpha | 100669362 | itfMesMngWindowTypeAlpha_Public_Static_Int32_0 |


Practical reading:
- `itfMesMngRequestMessage` / `itfMesMngRequestSelect` are the cleanest “**who asked for what text**” seams.
- `itfMesMngGetStrImmediate` looks like a “**return final string**” helper (very high-value for non-invasive string replacement).
- `GetMessagePagePtr` is the lowest-level seam we have in `itfMesManager` for **page extraction** given a mes-header + file data.
- `ResolveResourceAddress` + `itfMesMngSetResourcePtr` indicate a relocation/resolve step for message resources.

---

## Likely execution loop (names-only; confirm later)

This is a **best-effort reconstruction** based on method naming in `itfMesManager`:

1. Message/select request: `itfMesMngRequestMessage` / `itfMesMngRequestSelect`
2. Window open: `itfMesMngOpenWindow` (+ `WindowDispOn`)
3. Per-frame update: `CalcMesFlow` → `CalcMessage` (+ `CalcWindow`)
4. Per-frame render: `DrawMesFlow` → `DrawMessage`/`DrawSelection` → `DrawWindow`
5. Close/release: `itfMesMngCloseWindow` (+ `WindowDispOff`), `itfMesMngRequest*Release`

---

## What this means in practice:

If we can reliably intercept either:
- **the request boundary** (MsgID / label / page), or
- **the “final string” boundary** (`itfMesMngGetStrImmediate`),

…then we can implement text mods without touching binary assets, and without depending on fragile UI dumps.

Next tranche will dig into:
- The full `itfMesMng*` API surface map (grouped by purpose),
- BIN header semantics (what each header field likely means),
- and the window rendering side (`itfPanel` + panel structs) so we can safely reason about UI layout interactions.
