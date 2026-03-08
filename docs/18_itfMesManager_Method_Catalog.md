# 18 — itfMesManager Method Catalog (curated + how to use it)

This file explains how to *use the catalog* we generated from the wrapper:

- `data/part3_itfMesManager_methods.csv`

That CSV is the “index of everything” with:
- token (the stable IL2CPP metadata token used in wrappers)
- native pointer name (includes return/arg types in the wrapper generator’s convention)
- a parsed best-effort breakdown into method name / visibility / return / args
- category labels (DAT_STR, itfMesMng, Window/UI, etc.)

---

## 1) Reading the native pointer names

Example row:

`itfMesMngSetVarImmediate2_Public_Static_Void_Int32_Int32_String_Int32_0`

Interpretation:
- Method: `itfMesMngSetVarImmediate2`
- Access: Public
- Static
- Return: Void
- Args: `Int32, Int32, String, Int32`
- Overload index: `0`

Caveat:
- Types like `Il2CppStructArray_1_Byte` appear in this generated naming scheme.  
  Treat them as “`Il2CppStructArray<byte>`”.

---

## 2) The small set of “always useful” clusters

### A) Name lookup cluster (`DAT_STR_REF_*`)
See Part 17 for why this is high ROI.

### B) Message window lifecycle (open/close/fade)
Look for:
- `WindowDispOn`, `WindowDispOff`
- `FadeOutWindow`, `FadeInWindow`, `IsFadeOutWindow`
- `MsgWndSetActive`, `MsgWndActiveSelf`, `MsgWndOwnerShowUI`
- `ClearMessage`, `ClearSpeaker`, `ForceWindowDispOff`

### C) Selection window and choice handling
Look for:
- `SelectWindowDispOn/Off`
- `itfMesMngRequestSelect`, `itfMesMngSetSelect`, `itfMesMngSetMaskSelect`
- `GetNumSelectItem`, `GetMaskedIndex`, `itfMesMngGetSelectNo`
- `SelectItem*` helpers

### D) Variable substitution + immediate string generation
Look for:
- `itfMesMngSetVar*`
- `itfMesMngGetVarLocalize`
- `itfMesMngGetStrImmediate`

These are prime “debug overlay” seams because they provide a mapping from (varId, value) to the final rendered string.

### E) Message binary navigation (headers / page pointers)
Look for:
- `GetTypeHeader`, `GetTypeHeader2`
- `GetMessagePagePtr`
- `GetDataBytes`

These relate directly to the `itfMesBin*` header structs described in Part 19.


