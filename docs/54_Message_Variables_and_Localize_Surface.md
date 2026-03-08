# Variables + Localization Hooks (Doc-only) — `itfMesManager` + `Localize` (v10)

This tranche maps the seams for:
- **variable substitution** (script/engine inserts into message text)
- **language/localization loading** (what gets loaded when, at least at the API surface)

---

## Variable seams in `itfMesManager`

The `itfMesMng*` family exposes several “set variable” calls.

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngGetVarLocalize | 100669378 | itfMesMngGetVarLocalize_Public_Static_String_Int32_Int32_0 |
| itfMesMngSetFloor | 100669380 | itfMesMngSetFloor_Public_Static_String_Int32_0 |
| itfMesMngSetVar | 100669379 | itfMesMngSetVar_Public_Static_Void_Int32_Int32_Int32_Int32_0 |
| itfMesMngSetVarImmediate | 100669381 | itfMesMngSetVarImmediate_Public_Static_Void_Int32_Int32_String_0 |
| itfMesMngSetVarImmediate2 | 100669382 | itfMesMngSetVarImmediate2_Public_Static_Void_Int32_Int32_String_Int32_0 |
| itfMesMngSetVarScr | 100669356 | itfMesMngSetVarScr_Public_Static_Int32_0 |


Interpretation (naming-only; confirm later):
- `itfMesMngSetVar` likely sets an indexed variable (used by inline tags).
- `*Immediate*` variants likely bypass the normal window/script cadence.
- `itfMesMngGetVarLocalize` strongly suggests a “variable resolves to localized string” path (e.g., item names / demon names).
- `itfMesMngSetFloor` suggests a special-case variable for dungeon/field floor string.

### Related string-return seam

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngGetFRQ | 100669385 | itfMesMngGetFRQ_Public_Static__FRQ_Int32_Int32_0 |
| itfMesMngGetStrImmediate | 100669384 | itfMesMngGetStrImmediate_Public_Static_String_Int32_Int32_Int32_0 |


`itfMesMngGetStrImmediate` is the most attractive candidate for **non-invasive string replacement**, because it returns a managed `String` rather than raw message bytes.

---

## Localization surface in `Localize`

Key `Localize` methods (IL2CPP tokens):

| base | il2cpp_token | field |
| --- | --- | --- |
| GetBaseLanguage | 100674505 | GetBaseLanguage_Public_Static_Int32_0 |
| GetData | 100674524 | GetData_Public_Static_EventEMsgData_String_0 |
| GetLangCode | 100674515 | GetLangCode_Public_Static_String_0 |
| GetLanguage | 100674506 | GetLanguage_Public_Static_Int32_0 |
| GetLanguageDefine | 100674507 | GetLanguageDefine_Public_Static_Int32_0 |
| LoadBattleLocalizeText | 100674518 | LoadBattleLocalizeText_Public_Static_Void_0 |
| LoadCommonData | 100674516 | LoadCommonData_Public_Static_Void_0 |
| LoadEventData | 100674521 | LoadEventData_Public_Static_Void_String_String_Int32_Boolean_0 |
| LoadLocalizeText | 100674520 | LoadLocalizeText_Public_Static_Void_String_String_0 |
| ReleaseEventData | 100674522 | ReleaseEventData_Public_Static_Void_Int32_0 |
| RemoveBattleLocalizeText | 100674519 | RemoveBattleLocalizeText_Public_Static_Void_0 |


What this tells us (again: API-shape only):
- there is a base language + language define distinction
- localization appears to have dedicated loaders for:
  - common text (`LoadCommonData`)
  - battle text (`LoadBattleLocalizeText` / `RemoveBattleLocalizeText`)
  - generic localize tables (`LoadLocalizeText`)
  - event-localized data (`LoadEventData` / `ReleaseEventData`)

---

## How this links to messages

A plausible (but unconfirmed) split emerges:

- `Localize` is responsible for **loading language-specific text tables**
- `itfMesManager` is responsible for **binding those tables to message window flows**
- variable seams bridge “data-driven tables” → “final rendered string”

This suggests two high-ROI modding strategies later:
1. Intercept `itfMesMngGetStrImmediate` for a **final-string patch layer**
2. Intercept `Localize.Load*` calls to register/replace **localized table assets** earlier

---

## Open questions

1. Where is the “index → string” lookup code for `Localize.GetData()` used?
2. Where does `itfMesMngGetVarLocalize` dispatch to (names/tables)?
3. What is the inline-tag encoding that triggers variable lookups and populates `message_tag.last_*` state?

To answer these without in-game dumping yet, we’ll do a targeted grep over Assembly-CSharp for:
- callsites of `itfMesMngGetStrImmediate`
- callsites of `LoadLocalizeText` / `LoadEventData`
- the `EvtMsgDat`/event message data types (if present elsewhere in the decompile)
