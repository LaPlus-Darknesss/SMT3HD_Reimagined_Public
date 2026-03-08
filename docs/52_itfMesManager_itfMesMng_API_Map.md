# itfMesManager — `itfMesMng*` Internal API Map (v10)

This document focuses only on the `itfMesMng*` family inside `Il2Cpp/itfMesManager.cs` (the script/engine-facing message manager calls). Tokens below are IL2CPP tokens used by the wrapper (`GetIl2CppMethodByToken`).

> **Important**: the parameter/return types encoded in `field` are mechanically derived from the wrapper name. Meaning/semantics still need runtime confirmation.

## Full surface (all `itfMesMng*`)

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngChangeWindowType | 100669386 | itfMesMngChangeWindowType_Public_Static_Void_Int32_Int32_Int32_0 |
| itfMesMngClearBitStat | 100669377 | itfMesMngClearBitStat_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngCloseSelectWindow | 100669355 | itfMesMngCloseSelectWindow_Public_Static_Int32_0 |
| itfMesMngCloseWindow | 100669352 | itfMesMngCloseWindow_Public_Static_Int32_0 |
| itfMesMngGetFRQ | 100669385 | itfMesMngGetFRQ_Public_Static__FRQ_Int32_Int32_0 |
| itfMesMngGetPhase | 100669343 | itfMesMngGetPhase_Public_Static_Int32_Int32_0 |
| itfMesMngGetSelPhase | 100669346 | itfMesMngGetSelPhase_Public_Static_Int32_Int32_0 |
| itfMesMngGetSelectNo | 100669348 | itfMesMngGetSelectNo_Public_Static_Int32_Int32_0 |
| itfMesMngGetStrImmediate | 100669384 | itfMesMngGetStrImmediate_Public_Static_String_Int32_Int32_Int32_0 |
| itfMesMngGetVarLocalize | 100669378 | itfMesMngGetVarLocalize_Public_Static_String_Int32_Int32_0 |
| itfMesMngIgnoreConfig | 100669357 | itfMesMngIgnoreConfig_Public_Static_Int32_0 |
| itfMesMngInitialize | 100669364 | itfMesMngInitialize_Public_Static_Int32_Object_Il2CppStringArray_String_0 |
| itfMesMngInitializeManager | 100669390 | itfMesMngInitializeManager_Public_Static_Void_0 |
| itfMesMngMarkBitStat | 100669376 | itfMesMngMarkBitStat_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngMaskSelect | 100669349 | itfMesMngMaskSelect_Public_Static_Int32_0 |
| itfMesMngMessage | 100669342 | itfMesMngMessage_Public_Static_Int32_0 |
| itfMesMngMsgWndCls | 100669353 | itfMesMngMsgWndCls_Public_Static_Void_Int32_0 |
| itfMesMngOpenSelectWindow | 100669354 | itfMesMngOpenSelectWindow_Public_Static_Int32_0 |
| itfMesMngOpenWindow | 100669351 | itfMesMngOpenWindow_Public_Static_Int32_0 |
| itfMesMngRelease | 100669365 | itfMesMngRelease_Public_Static_Void_Int32_0 |
| itfMesMngRequestMessage | 100669366 | itfMesMngRequestMessage_Public_Static_Int32_Int32_Int32_Int32_Int32_0 |
| itfMesMngRequestMessageRelease | 100669368 | itfMesMngRequestMessageRelease_Public_Static_Void_Int32_Int32_0 |
| itfMesMngRequestSelect | 100669369 | itfMesMngRequestSelect_Public_Static_Void_Int32_Int32_0 |
| itfMesMngRequestSelectRelease | 100669370 | itfMesMngRequestSelectRelease_Public_Static_Void_Int32_0 |
| itfMesMngSelect | 100669345 | itfMesMngSelect_Public_Static_Int32_0 |
| itfMesMngSelectDefKey | 100669363 | itfMesMngSelectDefKey_Public_Static_Int32_0 |
| itfMesMngSetDefaultSpeed | 100669388 | itfMesMngSetDefaultSpeed_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetFloor | 100669380 | itfMesMngSetFloor_Public_Static_String_Int32_0 |
| itfMesMngSetKutiCallback | 100669389 | itfMesMngSetKutiCallback_Public_Static_Void_Int32_func_kutiDelegate_0 |
| itfMesMngSetMaskSelect | 100669371 | itfMesMngSetMaskSelect_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngSetMesPosDefault | 100669359 | itfMesMngSetMesPosDefault_Public_Static_Int32_0 |
| itfMesMngSetMesPosition | 100669375 | itfMesMngSetMesPosition_Public_Static_Void_Int32_Int32_Int32_0 |
| itfMesMngSetMesPositionScript | 100669358 | itfMesMngSetMesPositionScript_Public_Static_Int32_0 |
| itfMesMngSetPhase | 100669344 | itfMesMngSetPhase_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetResourcePtr | 100669383 | itfMesMngSetResourcePtr_Public_Static_Object_Int32_Object_0 |
| itfMesMngSetSelPhase | 100669347 | itfMesMngSetSelPhase_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetSelect | 100669372 | itfMesMngSetSelect_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetSelectDefKey | 100669387 | itfMesMngSetSelectDefKey_Public_Static_Int32_Int32_Int32_Int32_0 |
| itfMesMngSetVar | 100669379 | itfMesMngSetVar_Public_Static_Void_Int32_Int32_Int32_Int32_0 |
| itfMesMngSetVarImmediate | 100669381 | itfMesMngSetVarImmediate_Public_Static_Void_Int32_Int32_String_0 |
| itfMesMngSetVarImmediate2 | 100669382 | itfMesMngSetVarImmediate2_Public_Static_Void_Int32_Int32_String_Int32_0 |
| itfMesMngSetVarScr | 100669356 | itfMesMngSetVarScr_Public_Static_Int32_0 |
| itfMesMngValidateNext | 100669360 | itfMesMngValidateNext_Public_Static_Int32_0 |
| itfMesMngWindowType | 100669361 | itfMesMngWindowType_Public_Static_Int32_0 |
| itfMesMngWindowTypeAlpha | 100669362 | itfMesMngWindowTypeAlpha_Public_Static_Int32_0 |
| itfMesMng_SEL_SELNO | 100669350 | itfMesMng_SEL_SELNO_Public_Static_Int32_0 |


## Curated groups (easier to reason about)

### Lifecycle / manager init

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngInitialize | 100669364 | itfMesMngInitialize_Public_Static_Int32_Object_Il2CppStringArray_String_0 |
| itfMesMngInitializeManager | 100669390 | itfMesMngInitializeManager_Public_Static_Void_0 |
| itfMesMngRelease | 100669365 | itfMesMngRelease_Public_Static_Void_Int32_0 |
| itfMesMngSetResourcePtr | 100669383 | itfMesMngSetResourcePtr_Public_Static_Object_Int32_Object_0 |


### Message request + window control

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngCloseSelectWindow | 100669355 | itfMesMngCloseSelectWindow_Public_Static_Int32_0 |
| itfMesMngCloseWindow | 100669352 | itfMesMngCloseWindow_Public_Static_Int32_0 |
| itfMesMngMsgWndCls | 100669353 | itfMesMngMsgWndCls_Public_Static_Void_Int32_0 |
| itfMesMngOpenSelectWindow | 100669354 | itfMesMngOpenSelectWindow_Public_Static_Int32_0 |
| itfMesMngOpenWindow | 100669351 | itfMesMngOpenWindow_Public_Static_Int32_0 |
| itfMesMngRequestMessage | 100669366 | itfMesMngRequestMessage_Public_Static_Int32_Int32_Int32_Int32_Int32_0 |
| itfMesMngRequestMessageRelease | 100669368 | itfMesMngRequestMessageRelease_Public_Static_Void_Int32_Int32_0 |


### Selection control

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngGetSelPhase | 100669346 | itfMesMngGetSelPhase_Public_Static_Int32_Int32_0 |
| itfMesMngGetSelectNo | 100669348 | itfMesMngGetSelectNo_Public_Static_Int32_Int32_0 |
| itfMesMngMaskSelect | 100669349 | itfMesMngMaskSelect_Public_Static_Int32_0 |
| itfMesMngSelect | 100669345 | itfMesMngSelect_Public_Static_Int32_0 |
| itfMesMngSelectDefKey | 100669363 | itfMesMngSelectDefKey_Public_Static_Int32_0 |
| itfMesMngSetMaskSelect | 100669371 | itfMesMngSetMaskSelect_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngSetSelPhase | 100669347 | itfMesMngSetSelPhase_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetSelect | 100669372 | itfMesMngSetSelect_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetSelectDefKey | 100669387 | itfMesMngSetSelectDefKey_Public_Static_Int32_Int32_Int32_Int32_0 |
| itfMesMng_SEL_SELNO | 100669350 | itfMesMng_SEL_SELNO_Public_Static_Int32_0 |


### Message phase/state

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngClearBitStat | 100669377 | itfMesMngClearBitStat_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngGetPhase | 100669343 | itfMesMngGetPhase_Public_Static_Int32_Int32_0 |
| itfMesMngMarkBitStat | 100669376 | itfMesMngMarkBitStat_Public_Static_Void_Int32_UInt32_0 |
| itfMesMngMessage | 100669342 | itfMesMngMessage_Public_Static_Int32_0 |
| itfMesMngSetPhase | 100669344 | itfMesMngSetPhase_Public_Static_Void_Int32_Int32_0 |
| itfMesMngValidateNext | 100669360 | itfMesMngValidateNext_Public_Static_Int32_0 |


### Variables / substitution

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngGetVarLocalize | 100669378 | itfMesMngGetVarLocalize_Public_Static_String_Int32_Int32_0 |
| itfMesMngSetFloor | 100669380 | itfMesMngSetFloor_Public_Static_String_Int32_0 |
| itfMesMngSetVar | 100669379 | itfMesMngSetVar_Public_Static_Void_Int32_Int32_Int32_Int32_0 |
| itfMesMngSetVarImmediate | 100669381 | itfMesMngSetVarImmediate_Public_Static_Void_Int32_Int32_String_0 |
| itfMesMngSetVarImmediate2 | 100669382 | itfMesMngSetVarImmediate2_Public_Static_Void_Int32_Int32_String_Int32_0 |
| itfMesMngSetVarScr | 100669356 | itfMesMngSetVarScr_Public_Static_Int32_0 |


### Window presentation

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngChangeWindowType | 100669386 | itfMesMngChangeWindowType_Public_Static_Void_Int32_Int32_Int32_0 |
| itfMesMngIgnoreConfig | 100669357 | itfMesMngIgnoreConfig_Public_Static_Int32_0 |
| itfMesMngSetDefaultSpeed | 100669388 | itfMesMngSetDefaultSpeed_Public_Static_Void_Int32_Int32_0 |
| itfMesMngSetMesPosDefault | 100669359 | itfMesMngSetMesPosDefault_Public_Static_Int32_0 |
| itfMesMngSetMesPosition | 100669375 | itfMesMngSetMesPosition_Public_Static_Void_Int32_Int32_Int32_0 |
| itfMesMngSetMesPositionScript | 100669358 | itfMesMngSetMesPositionScript_Public_Static_Int32_0 |
| itfMesMngWindowType | 100669361 | itfMesMngWindowType_Public_Static_Int32_0 |
| itfMesMngWindowTypeAlpha | 100669362 | itfMesMngWindowTypeAlpha_Public_Static_Int32_0 |


### Text getters

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngGetFRQ | 100669385 | itfMesMngGetFRQ_Public_Static__FRQ_Int32_Int32_0 |
| itfMesMngGetStrImmediate | 100669384 | itfMesMngGetStrImmediate_Public_Static_String_Int32_Int32_Int32_0 |


### Callbacks

| base | il2cpp_token | field |
| --- | --- | --- |
| itfMesMngSetKutiCallback | 100669389 | itfMesMngSetKutiCallback_Public_Static_Void_Int32_func_kutiDelegate_0 |

