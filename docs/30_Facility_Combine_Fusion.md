# Facility: Cathedral / Fusion (“Combine”) — `fclCombine*`

This is the biggest non-battle UI state machine in the decompile. It covers (at minimum):

- fusion selection flows (1st / 2nd / sacrifice selection)
- result generation (birth devil + accidents)
- inheritance (“Keisyo”) selection
- special sub-modes: **Ikenie**, **Magatama**, **Zensyo**  
  (these names appear in method names; exact meanings should be confirmed later via targeted observation)

Primary wrappers:
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fclCombineInit.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fclCombineUpdate.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fclCombineCalc.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fclCombineDraw.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fclCombineTable.cs` (static tables)

---

## Combine lifecycle + outer process seams

### fclCombineInit (event gating + process start/end)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `fclCombineDisableTalkIkenie` | `100667551` | `fclCombineInit.cs` | `fclCombineDisableTalkIkenie_Public_Static_Void_0` |
| `fclCombineDisableTalkMagatama` | `100667552` | `fclCombineInit.cs` | `fclCombineDisableTalkMagatama_Public_Static_Void_0` |
| `fclCombineDisableCmdZensyo` | `100667553` | `fclCombineInit.cs` | `fclCombineDisableCmdZensyo_Public_Static_Void_0` |
| `fclCombineDisableTalkZensyo` | `100667554` | `fclCombineInit.cs` | `fclCombineDisableTalkZensyo_Public_Static_Void_0` |
| `fclCombineChkEventStat` | `100667555` | `fclCombineInit.cs` | `fclCombineChkEventStat_Public_Static_SByte_0` |
| `fclCombineEventStart` | `100667556` | `fclCombineInit.cs` | `fclCombineEventStart_Public_Static_Void_SByte_0` |
| `fclCombineEventEnd` | `100667557` | `fclCombineInit.cs` | `fclCombineEventEnd_Public_Static_Void_0` |
| `fclCombineTexLoad` | `100667558` | `fclCombineInit.cs` | `fclCombineTexLoad_Public_Static_Void_0` |
| `fclCombineTexFree` | `100667559` | `fclCombineInit.cs` | `fclCombineTexFree_Public_Static_Void_0` |
| `cmbInitEffParam` | `100667560` | `fclCombineInit.cs` | `cmbInitEffParam_Public_Static_Void_0` |
| `cmbChkGeneralStat` | `100667561` | `fclCombineInit.cs` | `cmbChkGeneralStat_Public_Static_Int32_0` |
| `fclCombineTokusyuBGM` | `100667562` | `fclCombineInit.cs` | `fclCombineTokusyuBGM_Public_Static_Int32_0` |
| `fclCombineBGM` | `100667563` | `fclCombineInit.cs` | `fclCombineBGM_Public_Static_Void_SByte_0` |
| `fclCombineInitPanelMover` | `100667564` | `fclCombineInit.cs` | `fclCombineInitPanelMover_Public_Static_Void_cmbGlobalWork_t_0` |
| `fclCombineInitialize` | `100667565` | `fclCombineInit.cs` | `fclCombineInitialize_Public_Static_Void_0` |
| `fclUpdateInit` | `100667566` | `fclCombineInit.cs` | `fclUpdateInit_Internal_Static_Void_0` |
| `OnInstitutionCallback` | `100667567` | `fclCombineInit.cs` | `OnInstitutionCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnCombMenuCallback` | `100667568` | `fclCombineInit.cs` | `OnCombMenuCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnDlistSumCallback` | `100667569` | `fclCombineInit.cs` | `OnDlistSumCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnDlistSumBarCallback` | `100667570` | `fclCombineInit.cs` | `OnDlistSumBarCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnDlistRecCallback` | `100667571` | `fclCombineInit.cs` | `OnDlistRecCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `fclCombineDestroy` | `100667572` | `fclCombineInit.cs` | `fclCombineDestroy_Public_Static_Object_dds3ProcessID_t_0` |
| `_fclCombineCalc` | `100667573` | `fclCombineInit.cs` | `_fclCombineCalc_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |
| `_fclCombineDraw` | `100667574` | `fclCombineInit.cs` | `_fclCombineDraw_Public_Static_Object_dds3ProcessID_t_0` |
| `_fclCombineUpdate` | `100667575` | `fclCombineInit.cs` | `_fclCombineUpdate_Public_Static_Object_dds3ProcessID_t_0` |
| `fclCombineProcessStart` | `100667576` | `fclCombineInit.cs` | `fclCombineProcessStart_Public_Static_Void_0` |
| `fclCombineProcessEnd` | `100667577` | `fclCombineInit.cs` | `fclCombineProcessEnd_Public_Static_Int32_0` |
| `fclChkCombineProcess` | `100667578` | `fclCombineInit.cs` | `fclChkCombineProcess_Public_Static_SByte_0` |
| `CollidersEnabled` | `100667579` | `fclCombineInit.cs` | `CollidersEnabled_Public_Static_Void_Int32_Boolean_0` |
| `GetCurrentCursorInfo` | `100667580` | `fclCombineInit.cs` | `GetCurrentCursorInfo_Public_Static_cmpCursorInfo_t_0` |

### fclCombineUpdate (selected high-signal methods)

This wrapper contains many small update steps. The full list is in `data/part5_fcl_combine_methods.csv`.

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `UpdateCursor1st` | `100667582` | `fclCombineUpdate.cs` | `UpdateCursor1st_Internal_Static_Void_Int32_Int32_0` |
| `cmbExit` | `100667583` | `fclCombineUpdate.cs` | `cmbExit_Public_Static_Void_0` |
| `cmbUpdateRoot` | `100667584` | `fclCombineUpdate.cs` | `cmbUpdateRoot_Public_Static_Void_0` |
| `cmbChkNoDeleteFlag` | `100667585` | `fclCombineUpdate.cs` | `cmbChkNoDeleteFlag_Public_Static_Int32_Int32_0` |
| `cmbUpdateDevSel1st` | `100667586` | `fclCombineUpdate.cs` | `cmbUpdateDevSel1st_Public_Static_Void_0` |
| `cmbUpdateDevSel2nd` | `100667587` | `fclCombineUpdate.cs` | `cmbUpdateDevSel2nd_Public_Static_Void_0` |
| `cmbUpdateStatus` | `100667588` | `fclCombineUpdate.cs` | `cmbUpdateStatus_Public_Static_Void_0` |
| `cmbSetActiveRibbon` | `100667589` | `fclCombineUpdate.cs` | `cmbSetActiveRibbon_Public_Static_Void_0` |
| `cmbChkIkenieEnable` | `100667590` | `fclCombineUpdate.cs` | `cmbChkIkenieEnable_Public_Static_Int32_0` |
| `cmbUpdateBirthDevil` | `100667591` | `fclCombineUpdate.cs` | `cmbUpdateBirthDevil_Public_Static_Void_0` |
| `SkillSelWakuClr` | `100667592` | `fclCombineUpdate.cs` | `SkillSelWakuClr_Private_Static_Void_0` |
| `CalcHpMp` | `100667593` | `fclCombineUpdate.cs` | `CalcHpMp_Private_Static_Void_byref_datUnitWork_t_0` |
| `cmbUpdateBirthDevilSelectSkillInit` | `100667594` | `fclCombineUpdate.cs` | `cmbUpdateBirthDevilSelectSkillInit_Public_Static_Void_0` |
| `OnSkillSelectCallback` | `100667595` | `fclCombineUpdate.cs` | `OnSkillSelectCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnSSkillCallback` | `100667596` | `fclCombineUpdate.cs` | `OnSSkillCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `cmbUpdateBirthDevilSelectSkillReturn` | `100667597` | `fclCombineUpdate.cs` | `cmbUpdateBirthDevilSelectSkillReturn_Public_Static_Void_0` |
| `cmbUpdateBirthDevilSelectSkill` | `100667598` | `fclCombineUpdate.cs` | `cmbUpdateBirthDevilSelectSkill_Public_Static_Void_0` |
| `ChkSkill` | `100667599` | `fclCombineUpdate.cs` | `ChkSkill_Public_Static_Boolean_Int32_0` |
| `cmbUpdateBirthDevilConfSkill` | `100667600` | `fclCombineUpdate.cs` | `cmbUpdateBirthDevilConfSkill_Public_Static_Void_0` |
| `cmbUpdateBirthDevilMouse` | `100667601` | `fclCombineUpdate.cs` | `cmbUpdateBirthDevilMouse_Private_Static_UInt32_Int32_0` |
| `cmbSetAction` | `100667602` | `fclCombineUpdate.cs` | `cmbSetAction_Public_Static_Void_0` |
| `cmbUpdateDecideDevil` | `100667603` | `fclCombineUpdate.cs` | `cmbUpdateDecideDevil_Public_Static_SByte_0` |
| `cmbUpdateSacConfirm` | `100667604` | `fclCombineUpdate.cs` | `cmbUpdateSacConfirm_Public_Static_SByte_0` |
| `cmbUpdateDevSelSac` | `100667605` | `fclCombineUpdate.cs` | `cmbUpdateDevSelSac_Public_Static_Void_0` |
| `cmbUpdateError` | `100667606` | `fclCombineUpdate.cs` | `cmbUpdateError_Public_Static_Void_0` |
| `cmbUpdateSeqTalk` | `100667607` | `fclCombineUpdate.cs` | `cmbUpdateSeqTalk_Public_Static_Void_0` |
| `cmbUpdateSeqMsg` | `100667608` | `fclCombineUpdate.cs` | `cmbUpdateSeqMsg_Public_Static_Void_0` |
| `cmbDestroyActModel` | `100667609` | `fclCombineUpdate.cs` | `cmbDestroyActModel_Public_Static_Void_0` |
| `cmbDestroyActEffect` | `100667610` | `fclCombineUpdate.cs` | `cmbDestroyActEffect_Public_Static_Void_0` |
| `cmbUpdateIntroduce` | `100667611` | `fclCombineUpdate.cs` | `cmbUpdateIntroduce_Public_Static_Void_0` |
| `cmbCallEncyc` | `100667612` | `fclCombineUpdate.cs` | `cmbCallEncyc_Public_Static_Void_Int32_0` |
| `cmbUpdateZensyo` | `100667613` | `fclCombineUpdate.cs` | `cmbUpdateZensyo_Public_Static_Void_0` |
| `cmbUpdateDebugActCombine` | `100667614` | `fclCombineUpdate.cs` | `cmbUpdateDebugActCombine_Public_Static_SByte_0` |
| `cmbUpdatePanelMover` | `100667615` | `fclCombineUpdate.cs` | `cmbUpdatePanelMover_Public_Static_Void_0` |
| `cmbUpdateModelLoad` | `100667616` | `fclCombineUpdate.cs` | `cmbUpdateModelLoad_Public_Static_Void_0` |

---

### fclCombineCalc (result table + selection calc)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmbModelLoad` | `100667437` | `fclCombineCalc.cs` | `cmbModelLoad_Internal_Static_Void_0` |
| `cmbChkCurse` | `100667438` | `fclCombineCalc.cs` | `cmbChkCurse_Internal_Static_SByte_0` |
| `cmbInitCombineResultTable` | `100667439` | `fclCombineCalc.cs` | `cmbInitCombineResultTable_Internal_Static_Void_Int32_0` |
| `cmbCreateCombineResultTable` | `100667440` | `fclCombineCalc.cs` | `cmbCreateCombineResultTable_Internal_Static_Void_SByte_0` |
| `cmbCreateLocalStock` | `100667441` | `fclCombineCalc.cs` | `cmbCreateLocalStock_Internal_Static_Void_0` |
| `cmbCalcRoot` | `100667442` | `fclCombineCalc.cs` | `cmbCalcRoot_Internal_Static_SByte_0` |
| `cmbCalcDevSel1st` | `100667443` | `fclCombineCalc.cs` | `cmbCalcDevSel1st_Public_Static_Void_0` |
| `cmbCalcDevSel2nd` | `100667444` | `fclCombineCalc.cs` | `cmbCalcDevSel2nd_Public_Static_Void_0` |
| `cmbCalcDevSelSac` | `100667445` | `fclCombineCalc.cs` | `cmbCalcDevSelSac_Public_Static_Void_0` |
| `cmbInitRibbon` | `100667446` | `fclCombineCalc.cs` | `cmbInitRibbon_Internal_Static_Void_Int32_0` |
| `cmbCalcBirthDevil` | `100667447` | `fclCombineCalc.cs` | `cmbCalcBirthDevil_Public_Static_SByte_0` |
| `cmbCalcBirthDevil2` | `100667448` | `fclCombineCalc.cs` | `cmbCalcBirthDevil2_Public_Static_Void_0` |
| `cmbAddBirthDevilToStock` | `100667449` | `fclCombineCalc.cs` | `cmbAddBirthDevilToStock_Public_Static_Void_Il2CppReferenceArray_1_datUnitWork_t_Int32_Byte_0` |
| `cmbCalcError` | `100667450` | `fclCombineCalc.cs` | `cmbCalcError_Internal_Static_Void_0` |
| `cmbCalcIntroduce` | `100667451` | `fclCombineCalc.cs` | `cmbCalcIntroduce_Internal_Static_SByte_0` |
| `cmbCalcZensyo` | `100667452` | `fclCombineCalc.cs` | `cmbCalcZensyo_Internal_Static_Int32_0` |
| `cmbCalcStatus` | `100667453` | `fclCombineCalc.cs` | `cmbCalcStatus_Internal_Static_Void_0` |
| `cmbCalcModelLoad` | `100667454` | `fclCombineCalc.cs` | `cmbCalcModelLoad_Internal_Static_Void_0` |
| `cmbCalcSequence` | `100667455` | `fclCombineCalc.cs` | `cmbCalcSequence_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |
| `_ctor` | `100667456` | `fclCombineCalc.cs` | `_ctor_Public_Void_0` |

### fclCombineDraw (selected high-signal methods)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `InitializeGameObject` | `100667518` | `fclCombineDraw.cs` | `InitializeGameObject_Public_Static_Void_0` |
| `FinalizeGameObject` | `100667519` | `fclCombineDraw.cs` | `FinalizeGameObject_Public_Static_Void_0` |
| `cmbDrawDevilInfo` | `100667520` | `fclCombineDraw.cs` | `cmbDrawDevilInfo_Internal_Static_Void_String_UInt32_UInt32_datUnitWork_t_Boolean_0` |
| `cmbDrawDevilInfo` | `100667521` | `fclCombineDraw.cs` | `cmbDrawDevilInfo_Internal_Static_Void_String_Int32_UInt32_UInt32_datUnitWork_t_Int32_0` |
| `cmbDrawDevilIcon` | `100667522` | `fclCombineDraw.cs` | `cmbDrawDevilIcon_Internal_Static_Void_String_Int32_0` |
| `cmbDrawCombineResultIcon` | `100667523` | `fclCombineDraw.cs` | `cmbDrawCombineResultIcon_Internal_Static_Void_GameObject_Int32_SByte_0` |
| `cmbDrawStockDevilInfo` | `100667524` | `fclCombineDraw.cs` | `cmbDrawStockDevilInfo_Internal_Static_Void_SByte_UInt32_cmpCursorInfo_t_Byte_0` |
| `cmbDrawSelDevInfo` | `100667525` | `fclCombineDraw.cs` | `cmbDrawSelDevInfo_Internal_Static_Void_String_Int32_datUnitWork_t_SByte_Int32_Int32_0` |
| `cmbDrawPlayerInfo` | `100667526` | `fclCombineDraw.cs` | `cmbDrawPlayerInfo_Internal_Static_Void_datUnitWork_t_Int32_0` |
| `cmbDrawRootMenu` | `100667527` | `fclCombineDraw.cs` | `cmbDrawRootMenu_Private_Static_Void_Int32_0` |
| `cmbDrawHelp` | `100667528` | `fclCombineDraw.cs` | `cmbDrawHelp_Internal_Static_Void_Int32_0` |
| `cmbDrawInfoWindow` | `100667529` | `fclCombineDraw.cs` | `cmbDrawInfoWindow_Internal_Static_Void_SByte_0` |
| `cmbDrawSelectCursor` | `100667530` | `fclCombineDraw.cs` | `cmbDrawSelectCursor_Internal_Static_Void_Byte_UInt32_cmpCursorInfo_t_0` |
| `cmbDrawSelDevilInfo` | `100667531` | `fclCombineDraw.cs` | `cmbDrawSelDevilInfo_Internal_Static_Void_Int32_Int32_0` |
| `cmbDraw1stDevilSelect` | `100667532` | `fclCombineDraw.cs` | `cmbDraw1stDevilSelect_Internal_Static_Void_0` |
| `cmbDraw2ndDevilSelect` | `100667533` | `fclCombineDraw.cs` | `cmbDraw2ndDevilSelect_Internal_Static_Void_0` |
| `cmbDrawSacDevilSelect` | `100667534` | `fclCombineDraw.cs` | `cmbDrawSacDevilSelect_Internal_Static_Void_0` |
| `cmbDrawRibbon` | `100667535` | `fclCombineDraw.cs` | `cmbDrawRibbon_Public_Static_Void_Int32_0` |
| `cmbDrawStatus` | `100667536` | `fclCombineDraw.cs` | `cmbDrawStatus_Internal_Static_Void_0` |
| `cmbDrawStatusRibbon` | `100667537` | `fclCombineDraw.cs` | `cmbDrawStatusRibbon_Internal_Static_Void_0` |
| `cmbDrawStatusRibbon` | `100667538` | `fclCombineDraw.cs` | `cmbDrawStatusRibbon_Internal_Static_Void_Il2CppStringArray_Int32_0` |
| `cmbDrawBirthDevilMaskBG` | `100667539` | `fclCombineDraw.cs` | `cmbDrawBirthDevilMaskBG_Private_Static_Void_0` |
| `cmbDrawBirthDevil` | `100667540` | `fclCombineDraw.cs` | `cmbDrawBirthDevil_Private_Static_Void_Int32_0` |
| `cmbDrawBirthDevilSelectSkillInit` | `100667541` | `fclCombineDraw.cs` | `cmbDrawBirthDevilSelectSkillInit_Public_Static_Void_0` |
| `cmbDrawBirthDevilSelectSkillRet` | `100667542` | `fclCombineDraw.cs` | `cmbDrawBirthDevilSelectSkillRet_Public_Static_Void_0` |
| `cmbDrawBirthDevilSelectSkillEnd` | `100667543` | `fclCombineDraw.cs` | `cmbDrawBirthDevilSelectSkillEnd_Public_Static_Void_0` |
| `cmbDrawBirthDevilSelectSkill` | `100667544` | `fclCombineDraw.cs` | `cmbDrawBirthDevilSelectSkill_Private_Static_Void_0` |
| `cmbDrawZensyoMenu` | `100667545` | `fclCombineDraw.cs` | `cmbDrawZensyoMenu_Private_Static_Void_Int32_0` |
| `cmbDrawTalkMenu` | `100667546` | `fclCombineDraw.cs` | `cmbDrawTalkMenu_Private_Static_Void_Int32_0` |
| `cmbDrawSeqMessage` | `100667547` | `fclCombineDraw.cs` | `cmbDrawSeqMessage_Private_Static_Void_0` |
| `cmbDrawSequence` | `100667548` | `fclCombineDraw.cs` | `cmbDrawSequence_Public_Static_Object_dds3ProcessID_t_0` |
| `UpdateDevilList` | `100667549` | `fclCombineDraw.cs` | `UpdateDevilList_Public_Static_Void_Int32_0` |

*(Full draw list is in `data/part5_fcl_combine_methods.csv`.)*

---

## Combine state machine enum: `CMB_SEQ`

- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppfacility_H/CMB_SEQ.cs`

This enum has explicit names for many internal steps (root menus, selection phases, inheritance phases, etc).
It is exported to `data/part5_menu_enums.csv`.

---

## Combine work structs (high value)

Key structs:
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppfacility_H/cmbGlobalWork_t.cs` (global combine work)
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppfacility_H/cmbSelDev_t.cs` (selected devil entries)
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppfacility_H/cmbResultTable_t.cs` (result list and derived data)
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppfacility_H/fclKeisyoList_t.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppfacility_H/fclKeisyoListAll_t.cs`

These are mapped in `data/part5_facility_struct_fields.csv`.

High-signal fields on `cmbGlobalWork_t` (by name):
- `SeqInfo` (sequence controller)
- selection arrays: `SelDev`
- result data: `Result`
- birth/accident units: `BirthDevil`, `BirthDevilOrg`, `AccidentDevil`
- script/process ties: `ScriptPID`, `PMPID`
- effect/animation handles: `EffHandle`, `ActFlag`, `ActCnt`, etc.

---

## Static combine tables (`fclCombineTable`)

`fclCombineTable` exposes a number of global tables (field-style access). Field names include:
- `fclAccidentFailTbl`
- `fclActParamTbl`
- `fclBirthSpiritTbl`
- `fclCombineTbl`
- `fclCombineCurseTbl`
- `fclCombineManinKagTbl`
- `fclCombineManinRaceTbl`
- `fclCombineManinTbl`
- `fclEventDevilTbl`
- `fclExtPatternTbl`
- `fclIkenieExtPatternTbl`
- `fclIkenieExtPatternDevTbl`
- `fclIkenieExtPatternDev2Tbl`
- `fclIkenieExtPatternDevRaceTbl`
- `fclRankUpDownTbl`
- `fclSpiritParamUpTbl`
- `PosMode`
- `MotionNo`
- `EffOfs`
- `EffEraseSpd`
- `Radius`
- `Nums`

These are exported to `data/part5_table_field_surfaces.csv`.

---

## Sorting helper: `fclStockSort`

`tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/fclStockSort.cs` contains `cmbSortKeisyoSkill` and related helpers, implying that skill inheritance selection can be sorted/reordered.

---

## Notes for future hook planning

- `cmbCreateCombineResultTable` / `cmbInitCombineResultTable` are the core “generate candidate results” seams.
- `cmbChkCurse`, `cmbChkNoDeleteFlag`, `cmbChkIkenieEnable` suggest explicit gating logic.
- `cmbUpdateKeisyoSkill` is the inheritance UI step.
- Table wrappers suggest the fusion system is at least partially **table-driven** (race tables, curse tables, event devils, etc).
