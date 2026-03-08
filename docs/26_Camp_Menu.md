# Camp Menu (pause/field) — `cmp*`

Camp is the **field pause/menu** surface (items/party/skills/status/config/load/suspend/etc).
It is implemented as a process-driven sequence machine with distinct **Update / Calc / Draw** layers.

Primary wrappers:
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/cmpInit.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/cmpUpdate.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/cmpCalc.cs`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/cmpDraw.cs`

---

## Core entry points (tokens)

### cmpInit (lifecycle + setup)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `GetToRGBA` | `100663760` | `cmpInit.cs` | `GetToRGBA_Public_Static_Color_UInt32_0` |
| `cmpDynamicTexSet` | `100663761` | `cmpInit.cs` | `cmpDynamicTexSet_Public_Static_Void_sdfTexHandle_t_0` |
| `cmpDynamicTexReset` | `100663762` | `cmpInit.cs` | `cmpDynamicTexReset_Public_Static_Void_0` |
| `cmpInitTestGBWKStockList` | `100663763` | `cmpInit.cs` | `cmpInitTestGBWKStockList_Public_Static_Void_0` |
| `cmpInitTestDevilData` | `100663764` | `cmpInit.cs` | `cmpInitTestDevilData_Public_Static_Void_0` |
| `cmpInitLocalStock` | `100663765` | `cmpInit.cs` | `cmpInitLocalStock_Public_Static_Void_0` |
| `cmpInitDrawList` | `100663766` | `cmpInit.cs` | `cmpInitDrawList_Public_Static_Void_0` |
| `cmpInitStockPanelRate` | `100663767` | `cmpInit.cs` | `cmpInitStockPanelRate_Public_Static_Void_0` |
| `cmpinit` | `100663768` | `cmpInit.cs` | `cmpinit_Public_Static_Void_0` |
| `cmpMsgInit` | `100663769` | `cmpInit.cs` | `cmpMsgInit_Public_Static_Void_0` |
| `cmpDestroy` | `100663770` | `cmpInit.cs` | `cmpDestroy_Public_Static_dds3ProcessID_t_dds3ProcessID_t_0` |
| `cmpProcessStart` | `100663771` | `cmpInit.cs` | `cmpProcessStart_Public_Static_Void_0` |
| `cmpProcessEnd` | `100663772` | `cmpInit.cs` | `cmpProcessEnd_Public_Static_Void_0` |
| `cmpChkCampProcess` | `100663773` | `cmpInit.cs` | `cmpChkCampProcess_Public_Static_SByte_0` |
| `cmpSetExitState` | `100663774` | `cmpInit.cs` | `cmpSetExitState_Public_Static_Void_Int32_0` |
| `cmpGetExitState` | `100663775` | `cmpInit.cs` | `cmpGetExitState_Public_Static_Int32_0` |
| `cmpMenuInit` | `100663776` | `cmpInit.cs` | `cmpMenuInit_Public_Static_Void_Int32_0` |
| `cmpMenuTextSet` | `100663777` | `cmpInit.cs` | `cmpMenuTextSet_Public_Static_Void_Int32_String_UInt32_Int32_Boolean_Int32_0` |
| `cmpItemInit` | `100663778` | `cmpInit.cs` | `cmpItemInit_Public_Static_Void_Int32_0` |
| `cmpItemTextSet` | `100663779` | `cmpInit.cs` | `cmpItemTextSet_Public_Static_Void_Int32_String_UInt32_Int32_Int32_Boolean_Int32_Int32_Int32_Int32_0` |
| `OnMenuCollider` | `100663780` | `cmpInit.cs` | `OnMenuCollider_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnItemCollider` | `100663781` | `cmpInit.cs` | `OnItemCollider_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnArrowCollider` | `100663782` | `cmpInit.cs` | `OnArrowCollider_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnPartyCollider` | `100663783` | `cmpInit.cs` | `OnPartyCollider_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnStockCollider` | `100663784` | `cmpInit.cs` | `OnStockCollider_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnMouseMoveCheck` | `100663785` | `cmpInit.cs` | `OnMouseMoveCheck_Private_Static_Boolean_0` |
| `InitMouseIndex` | `100663786` | `cmpInit.cs` | `InitMouseIndex_Public_Static_Void_0` |
| `ClearMouseIndex` | `100663787` | `cmpInit.cs` | `ClearMouseIndex_Public_Static_Void_SByte_0` |
| `ClearMouseIndex2` | `100663788` | `cmpInit.cs` | `ClearMouseIndex2_Public_Static_Void_0` |
| `InitMouseCallback` | `100663789` | `cmpInit.cs` | `InitMouseCallback_Private_Static_Void_0` |
| `RemoveMouseCallback` | `100663790` | `cmpInit.cs` | `RemoveMouseCallback_Private_Static_Void_0` |
| `CheckMouseWheel` | `100663791` | `cmpInit.cs` | `CheckMouseWheel_Public_Static_Int32_Int32_0` |
| `SetMouseCursorIndex` | `100663792` | `cmpInit.cs` | `SetMouseCursorIndex_Public_Static_Boolean_cmpCursorInfo_t_UInt16_Int32_Int32_0` |
| `SetMouseCursorIndex2` | `100663793` | `cmpInit.cs` | `SetMouseCursorIndex2_Public_Static_Boolean_SByte_cmpStockInfo_t_UInt16_Int32_Int32_0` |
| `CheckUnitStockFlag` | `100663794` | `cmpInit.cs` | `CheckUnitStockFlag_Private_Static_Boolean_Int32_Int32_cmpLocalStock_t_0` |
| `GetUnitStockTblIdx` | `100663795` | `cmpInit.cs` | `GetUnitStockTblIdx_Private_Static_Int32_Int32_Int32_Int32_Int32_cmpLocalStock_t_0` |
| `CheckStockList` | `100663796` | `cmpInit.cs` | `CheckStockList_Private_Static_Int32_Int32_Int32_cmpLocalStock_t_0` |
| `IsMouseEnterAction` | `100663797` | `cmpInit.cs` | `IsMouseEnterAction_Public_Static_Boolean_SIPressType_0` |
| `GetGuideCode` | `100663798` | `cmpInit.cs` | `GetGuideCode_Public_Static_Int32_0` |
| `IsValidDecide` | `100663799` | `cmpInit.cs` | `IsValidDecide_Public_Static_Int32_Boolean_0` |
### cmpUpdate (sequence driving)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpDebug` | `100664060` | `cmpUpdate.cs` | `cmpDebug_Public_Static_SByte_SByte_0` |
| `cmpMenuCursor` | `100664061` | `cmpUpdate.cs` | `cmpMenuCursor_Public_Static_Void_Int32_GameObject_Il2CppReferenceArray_1_GameObject_0` |
| `cmpSetupObject` | `100664062` | `cmpUpdate.cs` | `cmpSetupObject_Public_Static_Void_GameObject_Boolean_0` |
| `cmpUpdateRoot` | `100664063` | `cmpUpdate.cs` | `cmpUpdateRoot_Public_Static_Void_0` |
| `cmpUpdateItem` | `100664064` | `cmpUpdate.cs` | `cmpUpdateItem_Public_Static_Void_0` |
| `cmpUpdateParty` | `100664065` | `cmpUpdate.cs` | `cmpUpdateParty_Public_Static_Void_0` |
| `cmpUpdateDevilSelect` | `100664066` | `cmpUpdate.cs` | `cmpUpdateDevilSelect_Public_Static_Void_SByte_0` |
| `cmpUpdateItemSelect` | `100664067` | `cmpUpdate.cs` | `cmpUpdateItemSelect_Public_Static_Void_0` |
| `cmpUpdateSkillSelect` | `100664068` | `cmpUpdate.cs` | `cmpUpdateSkillSelect_Public_Static_Void_0` |
| `cmpUpdateProcessStatus` | `100664069` | `cmpUpdate.cs` | `cmpUpdateProcessStatus_Public_Static_Void_0` |
| `cmpRemoveStockDevil` | `100664070` | `cmpUpdate.cs` | `cmpRemoveStockDevil_Public_Static_Void_0` |
| `cmpUpdateSepConfirm` | `100664071` | `cmpUpdate.cs` | `cmpUpdateSepConfirm_Public_Static_Void_0` |
| `cmpUpdateHearts` | `100664072` | `cmpUpdate.cs` | `cmpUpdateHearts_Public_Static_Void_0` |
| `cmpUpdateDestroyItemConfirm` | `100664073` | `cmpUpdate.cs` | `cmpUpdateDestroyItemConfirm_Public_Static_Void_0` |
| `cmpUpdateConfig` | `100664074` | `cmpUpdate.cs` | `cmpUpdateConfig_Public_Static_Void_0` |
| `cmpUpdateLoad` | `100664075` | `cmpUpdate.cs` | `cmpUpdateLoad_Public_Static_Void_0` |
| `cmpUpdateSuspend` | `100664076` | `cmpUpdate.cs` | `cmpUpdateSuspend_Public_Static_Void_0` |
| `cmpUpdateGoToTitle` | `100664077` | `cmpUpdate.cs` | `cmpUpdateGoToTitle_Public_Static_Void_0` |
| `cmpUpdateGameEnd` | `100664078` | `cmpUpdate.cs` | `cmpUpdateGameEnd_Public_Static_Void_0` |
| `cmpUpdateSubRoot` | `100664079` | `cmpUpdate.cs` | `cmpUpdateSubRoot_Public_Static_Void_0` |
| `cmpUpdateBaseRate` | `100664080` | `cmpUpdate.cs` | `cmpUpdateBaseRate_Public_Static_Void_0` |
| `cmpUpdatePanelRate` | `100664081` | `cmpUpdate.cs` | `cmpUpdatePanelRate_Public_Static_Void_0` |
| `cmpUpdateTimer` | `100664082` | `cmpUpdate.cs` | `cmpUpdateTimer_Public_Static_Void_0` |
| `cmpUpdateSequence` | `100664083` | `cmpUpdate.cs` | `cmpUpdateSequence_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |
| `cmpClearButtonGuide` | `100664084` | `cmpUpdate.cs` | `cmpClearButtonGuide_Public_Static_Void_0` |
| `cmpUpdateButtonGuide` | `100664085` | `cmpUpdate.cs` | `cmpUpdateButtonGuide_Private_Static_Void_0` |
### cmpCalc (logic / effect dispatch)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpChkStockRemoveEff` | `100663588` | `cmpCalc.cs` | `cmpChkStockRemoveEff_Public_Static_SByte_cmpStockInfo_t_0` |
| `cmpSetStockRemoveEff` | `100663589` | `cmpCalc.cs` | `cmpSetStockRemoveEff_Public_Static_Void_cmpStockInfo_t_0` |
| `cmpIniStockRecoverEff` | `100663590` | `cmpCalc.cs` | `cmpIniStockRecoverEff_Public_Static_Void_0` |
| `cmpSetStockRecoverEff` | `100663591` | `cmpCalc.cs` | `cmpSetStockRecoverEff_Public_Static_Void_SByte_UInt16_cmpStockInfo_t_0` |
| `cmpDestroyItem` | `100663592` | `cmpCalc.cs` | `cmpDestroyItem_Public_Static_SByte_SByte_0` |
| `cmpGetSelectSrcStock` | `100663593` | `cmpCalc.cs` | `cmpGetSelectSrcStock_Public_Static_datUnitWork_t_0` |
| `cmpGetSelectDstIndex` | `100663594` | `cmpCalc.cs` | `cmpGetSelectDstIndex_Public_Static_Int32_0` |
| `cmpGetSelectDstStock` | `100663595` | `cmpCalc.cs` | `cmpGetSelectDstStock_Public_Static_datUnitWork_t_0` |
| `cmpGetSelectItemID` | `100663596` | `cmpCalc.cs` | `cmpGetSelectItemID_Public_Static_UInt16_0` |
| `cmpSetItSkMessageVar` | `100663597` | `cmpCalc.cs` | `cmpSetItSkMessageVar_Public_Static_Void_datUnitWork_t_0` |
| `cmpGetItSkErrSelFlag` | `100663598` | `cmpCalc.cs` | `cmpGetItSkErrSelFlag_Public_Static_UInt32_UInt16_0` |
| `cmpGetSkillMsgIndex` | `100663599` | `cmpCalc.cs` | `cmpGetSkillMsgIndex_Public_Static_Int32_Int32_UInt16_0` |
| `cmpGetItemMsgIndex` | `100663600` | `cmpCalc.cs` | `cmpGetItemMsgIndex_Public_Static_Int32_Int32_UInt16_0` |
| `cmpCreateLocalItem` | `100663601` | `cmpCalc.cs` | `cmpCreateLocalItem_Public_Static_Void_Byte_0` |
| `cmpStartSkillTokusyuMsg` | `100663602` | `cmpCalc.cs` | `cmpStartSkillTokusyuMsg_Public_Static_Int32_UInt16_0` |
| `cmpStartItemTokusyuMsg` | `100663603` | `cmpCalc.cs` | `cmpStartItemTokusyuMsg_Public_Static_Int32_UInt16_0` |
| `cmpStartSkillMsg` | `100663604` | `cmpCalc.cs` | `cmpStartSkillMsg_Public_Static_Int32_UInt16_UInt32_0` |
| `cmpGetKugatatiMsg` | `100663605` | `cmpCalc.cs` | `cmpGetKugatatiMsg_Public_Static_Int32_0` |
| `cmpStartItemMsg` | `100663606` | `cmpCalc.cs` | `cmpStartItemMsg_Public_Static_Int32_UInt16_UInt32_0` |
| `cmpUseItem` | `100663607` | `cmpCalc.cs` | `cmpUseItem_Public_Static_SByte_0` |
| `cmpGetSelectSkillID` | `100663608` | `cmpCalc.cs` | `cmpGetSelectSkillID_Public_Static_UInt16_0` |
| `cmpUseSkill` | `100663609` | `cmpCalc.cs` | `cmpUseSkill_Public_Static_SByte_0` |
| `cmpChkDoNotUseSkill` | `100663610` | `cmpCalc.cs` | `cmpChkDoNotUseSkill_Public_Static_SByte_UInt16_0` |
| `cmpChkDoNotUseItemTokusyu` | `100663611` | `cmpCalc.cs` | `cmpChkDoNotUseItemTokusyu_Public_Static_Int32_UInt16_0` |
| `cmpChkDoNotUseItem` | `100663612` | `cmpCalc.cs` | `cmpChkDoNotUseItem_Public_Static_SByte_UInt16_0` |
| `cmpChkDoNotBuyItemTokusyu` | `100663613` | `cmpCalc.cs` | `cmpChkDoNotBuyItemTokusyu_Public_Static_Int32_UInt16_0` |
| `cmpMoveDevilStockToParty` | `100663614` | `cmpCalc.cs` | `cmpMoveDevilStockToParty_Public_Static_Void_0` |
| `cmpMoveDevilPartyToStock` | `100663615` | `cmpCalc.cs` | `cmpMoveDevilPartyToStock_Public_Static_Void_0` |
| `cmpRemoveDevil` | `100663616` | `cmpCalc.cs` | `cmpRemoveDevil_Public_Static_Void_0` |
| `cmpRemoveDevilEx` | `100663617` | `cmpCalc.cs` | `cmpRemoveDevilEx_Public_Static_Int32_UInt16_0` |
| `cmpInitSkillCursor` | `100663618` | `cmpCalc.cs` | `cmpInitSkillCursor_Public_Static_SByte_datUnitWork_t_0` |
| `cmpInitStockCursorPos` | `100663619` | `cmpCalc.cs` | `cmpInitStockCursorPos_Public_Static_Void_0` |
| `cmpReCalcStockCursorPos` | `100663620` | `cmpCalc.cs` | `cmpReCalcStockCursorPos_Public_Static_Void_SByte_0` |
| `cmpInitRootCursorPos` | `100663621` | `cmpCalc.cs` | `cmpInitRootCursorPos_Public_Static_Void_0` |
| `cmpInitSelectCursor` | `100663622` | `cmpCalc.cs` | `cmpInitSelectCursor_Public_Static_Void_SByte_0` |
| `cmpChkPartyDevilNums` | `100663623` | `cmpCalc.cs` | `cmpChkPartyDevilNums_Public_Static_SByte_0` |
| `cmpChkStockDevilNums` | `100663624` | `cmpCalc.cs` | `cmpChkStockDevilNums_Public_Static_SByte_SByte_0` |
| `cmpCalcRoot` | `100663625` | `cmpCalc.cs` | `cmpCalcRoot_Public_Static_Void_0` |
| `cmpSetSkillTSL` | `100663626` | `cmpCalc.cs` | `cmpSetSkillTSL_Public_Static_Void_UInt16_0` |
| `cmpSetItemTSL` | `100663627` | `cmpCalc.cs` | `cmpSetItemTSL_Public_Static_Void_UInt16_0` |
| `cmpCalcSkill` | `100663628` | `cmpCalc.cs` | `cmpCalcSkill_Public_Static_Void_0` |
| `cmpCalcDestroyItem` | `100663629` | `cmpCalc.cs` | `cmpCalcDestroyItem_Public_Static_Int32_0` |
| `cmpCalcItem` | `100663630` | `cmpCalc.cs` | `cmpCalcItem_Public_Static_SByte_0` |
| `cmpSetPartyDSL` | `100663631` | `cmpCalc.cs` | `cmpSetPartyDSL_Public_Static_SByte_0` |
| `cmpCalcStockControl` | `100663632` | `cmpCalc.cs` | `cmpCalcStockControl_Public_Static_SByte_0` |
| `cmpCalcParty` | `100663633` | `cmpCalc.cs` | `cmpCalcParty_Public_Static_SByte_0` |
| `cmpCalcConfig` | `100663634` | `cmpCalc.cs` | `cmpCalcConfig_Public_Static_Void_0` |
| `cmpCalcLoad` | `100663635` | `cmpCalc.cs` | `cmpCalcLoad_Public_Static_Void_0` |
| `cmpUpdateList` | `100663636` | `cmpCalc.cs` | `cmpUpdateList_Public_Static_Void_0` |
| `cmpCalcSubRoot` | `100663637` | `cmpCalc.cs` | `cmpCalcSubRoot_Public_Static_Void_0` |
| `cmpCalcSequence` | `100663638` | `cmpCalc.cs` | `cmpCalcSequence_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |
### cmpDraw (render layers)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `cmpGetInfoStr` | `100663663` | `cmpDraw.cs` | `cmpGetInfoStr_Public_Static_String_Int32_0` |
| `CMP_X` | `100663664` | `cmpDraw.cs` | `CMP_X_Public_Static_Int32_Int32_0` |
| `CMP_Y` | `100663665` | `cmpDraw.cs` | `CMP_Y_Public_Static_Int32_Int32_0` |
| `CMP_XM` | `100663666` | `cmpDraw.cs` | `CMP_XM_Public_Static_Int32_Int32_0` |
| `CMP_YM` | `100663667` | `cmpDraw.cs` | `CMP_YM_Public_Static_Int32_Int32_0` |
| `cmpDrawBgLayer05` | `100663668` | `cmpDraw.cs` | `cmpDrawBgLayer05_Public_Static_Void_Int32_Int32_Il2CppReferenceArray_1_sdfTexHandle_t_Il2CppReferenceArray_1_fclSprTblArry_t_UInt32_Int32_UInt32_0` |
| `cmpCreateWaveOffset` | `100663669` | `cmpDraw.cs` | `cmpCreateWaveOffset_Public_Static_Int32_Int32_Int32_Int32_0` |
| `cmpCreateWaveOffset2` | `100663670` | `cmpDraw.cs` | `cmpCreateWaveOffset2_Public_Static_Int32_Int32_Int32_Int32_0` |
| `cmpDrawBG` | `100663671` | `cmpDraw.cs` | `cmpDrawBG_Public_Static_Void_UInt32_Int32_0` |
| `cmpDrawHelpForUnity` | `100663672` | `cmpDraw.cs` | `cmpDrawHelpForUnity_Public_Static_Void_fclPanelPos_t_UInt16_SByte_SByte_Int32_UInt32_Il2CppReferenceArray_1_TextMeshProUGUI_0` |
| `cmpDisableInfoWindow` | `100663673` | `cmpDraw.cs` | `cmpDisableInfoWindow_Public_Static_Void_0` |
| `cmpDrawInfoWindow` | `100663674` | `cmpDraw.cs` | `cmpDrawInfoWindow_Public_Static_Void_SByte_Il2CppStructArray_1_UInt32_0` |
| `cmpSetSkillTargetDrawMode` | `100663675` | `cmpDraw.cs` | `cmpSetSkillTargetDrawMode_Public_Static_Void_UInt16_SByte_0` |
| `cmpDrawDevilSelect` | `100663676` | `cmpDraw.cs` | `cmpDrawDevilSelect_Public_Static_Void_SByte_0` |
| `cmpDrawSkillSelect` | `100663677` | `cmpDraw.cs` | `cmpDrawSkillSelect_Public_Static_Void_0` |
| `cmpDrawItemSelect` | `100663678` | `cmpDraw.cs` | `cmpDrawItemSelect_Public_Static_Void_0` |
| `cmpDrawMoney` | `100663679` | `cmpDraw.cs` | `cmpDrawMoney_Public_Static_Void_0` |
| `cmpDrawFadeInBG` | `100663680` | `cmpDraw.cs` | `cmpDrawFadeInBG_Public_Static_Void_0` |
| `cmpDrawSequence` | `100663681` | `cmpDraw.cs` | `cmpDrawSequence_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |

---

## Practical reading notes

- `cmpUpdateSequence(PID)` is the best single entry point to track **state transitions**.
- `cmpUpdateItemSelect`, `cmpUpdateDevilSelect`, `cmpUpdateSkillSelect` are the key sub-surfaces that gate most menu interactions.
- `cmpUseItem` / `cmpUseSkill` look like the “dispatch” points that apply the actual effect after selection.
- `cmpUpdateButtonGuide` suggests Camp owns its own button prompt UI (likely the bottom-of-screen guides).

## Shared state objects used by Camp

Camp drives sub-states through `cmpSeqInfo_s`:
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppcamp_H/cmpSeqInfo_s.cs`

Camp also uses shared widgets and lists:
- `cmpCmdSelInfo_t` / `cmpCmdSel` (command selection)
- `lstListWindow_t` (list UIs)
- `cmpCursorInfo_t` (cursor state)
- `fclPanelMover_t` (panel transitions are used heavily by Facility; Camp has some panel-rate helpers too)

## Where item/inventory lists show up

Camp itself does not expose an obvious `cmpDataItem_t` work struct in `camp_H` in this decompile snapshot.
Instead, item list handling appears to be delegated to:
- Camp “item select” update/draw/calc functions (see `cmpUpdateItemSelect`, `cmpDrawItemSel`, `cmpItemListCreate`, etc.)
- shared list window types (`lstListWindow_t`)
- Facility item list container `fclItemList_t` (used by shops; see `Il2Cppfacility_H/fclItemList_t.cs`)

This means “inventory UI” tends to be a **composition** of shared list widgets, not a single big dedicated item-work struct.


---

## Cursor arrays: RootCI / WorkCI (runtime-observed)

The vanilla camp menu stores **cursor state** in `cmpInit.CMP_GBWK` (global work object).
Via reflection-dumps and seqTrace probing, we can observe two high-ROI arrays:

- `CMP_GBWK.RootCI` : an array of `cmpCursorInfo_t` cursors used for **hierarchical root/sub-root menus**
- `CMP_GBWK.WorkCI` : an array of `cmpCursorInfo_t` cursors used for **active list windows** (items, etc)

### Effective selection formula

`cmpCursorInfo_t` exposes (at minimum) these integer fields:

- `Index` : cursor index within the visible window (0..N-1)
- `Shift` : scroll offset (how far the window has been scrolled)
- `ListNums` : total list length (items in list)

We treat the effective selection as:

`Sel = Index + Shift`

### Practical mapping we’ve observed so far

- `RootCI[0]` corresponds to the **top-level command menu** selection.
  - It lines up with `cmpInit.gCmpRootMenuStr` (string keys), and we can localize keys via `Localize.GetLocalizeText`.
- `RootCI[1]` corresponds to the **Items sub-root** selector.
  - It lines up with `cmpInit.gCmpItemRootStr` (usually: Use / Discard / Gems / Key Items).
- `WorkCI[0]` corresponds to the **active list cursor** (e.g. the item list inside Items→Use).
  - When an item list is active, `CMP_GBWK.ItemIdx[WorkSel]` yields the selected item-id, which can be resolved through `datItemName.Get(...)`.

### Devtools hooks that observe these

- `CTRL+ALT+F10` (seqTrace) prints the live camp/menu state and includes:
  - `rootSel` (RootCI[0] derived selection + label/localized)
  - `subSel`  (RootCI[1] derived selection + label/localized, best-effort)
  - `workSel` (WorkCI[0] derived selection) + `itemSel` resolution when applicable
- `CTRL+ALT+F11` writes a `camp_reflection_surface_*.txt` snapshot with:
  - RootCI / WorkCI dumps
  - menu label arrays (`gCmpRootMenuStr`, `gCmpItemRootStr`, etc) with localized values
  - the decoded `ItemIdx` table (id → name) for correlation/diffing


## Stock / Party selection internals (CMP_GBWK.StockInfo)

When you’re on screens that involve **choosing a demon from Party / Stock** (e.g. Items→Use target selection, Party→Summon, Party→Change, etc.), the camp system exposes a second selection surface via `CMP_GBWK.StockInfo`.

Key pieces:

- `cmpStockInfo_t.DrawMode`:
  - A small mode integer that correlates with *which* stock/party UI variant is active.
  - Treat this as a “what kind of list is currently on screen” hint.

- `cmpStockInfo_t.SelPos` is **not** a cursor-position struct.
  - It’s an `Il2CppReferenceArray<cmpCursorInfo_t>`.
  - Each `cmpCursorInfo_t` contains a `CursorPos` (`cmpCursorPos_t`) with:
    - `Index`, `Shift`, `ListNums`, `ShiftMax`, `Sel`
    - and `Sel = Index + Shift` (the “overall slot” into the currently displayed list)
  - The most useful entry is usually `SelPos[CursorCurSel]` (fallback: `CursorSel`).

- `cmpStockInfo_t.LocalStock` is an `Il2CppReferenceArray<cmpLocalStock_t>`.
  - Each `cmpLocalStock_t` contains:
    - `PartyCnt`, `StockCnt`
    - `ListIdx[]`, `StockIdx[]` (sbyte arrays used as mapping tables)

- `cmpGlobalWork_t.DrawList` (on `CMP_GBWK`) is often the **active** `cmpLocalStock_t` for the list currently being shown.
  - When present, it’s the best “single object to consult” for `PartyCnt/StockCnt` + mapping arrays.

### What devtools should dump for these screens

- `StockInfo` selectors (`ListSel/ListCurSel`, `StockSel/StockCurSel`, `CursorSel/CursorCurSel`)
- `SelPos[]` entries (at least the active one), including `CursorPos` and “overall slot”
- `DrawList` + `ListIdx[]` / `StockIdx[]` previews

This gives us enough signal to:

1. Confirm which cursor is active.
2. Map “overall slot” → party vs stock partition (`overall < PartyCnt` ⇒ party).
3. Start correlating mapping indices with higher-level devil/unit work structures.

