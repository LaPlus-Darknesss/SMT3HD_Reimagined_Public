# Facility: Shop + Rag/Junk Tables — `fclShop*` + tables

This section maps the standard **shop UI** plus the associated static tables for shop inventories.

Primary wrappers:
- `Il2Cpp/fclShopInit.cs`
- `Il2Cpp/fclShopUpdate.cs`
- `Il2Cpp/fclShopCalc.cs`
- `Il2Cpp/fclShopDraw.cs`
- `Il2Cpp/fclRagShopTable.cs` (static tables)
- `Il2Cpp/fclJunkShopTable.cs` (static tables)


## Shop lifecycle + key actions

### fclShopInit (setup + process start/end)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `fclInitShop` | `100668041` | `fclShopInit.cs` | `fclInitShop_Public_Static_fclDataShop_t_SByte_cmpSeqInfo_t_0` |
| `fclShopUpdateInit` | `100668042` | `fclShopInit.cs` | `fclShopUpdateInit_Public_Static_Void_fclDataShop_t_0` |
| `OnInstitutionCallback` | `100668043` | `fclShopInit.cs` | `OnInstitutionCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnListCallback` | `100668044` | `fclShopInit.cs` | `OnListCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnNumUDCallback` | `100668045` | `fclShopInit.cs` | `OnNumUDCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `fclDestroyShop` | `100668046` | `fclShopInit.cs` | `fclDestroyShop_Public_Static_Object_dds3ProcessID_t_0` |
| `fclShopEventStart` | `100668047` | `fclShopInit.cs` | `fclShopEventStart_Public_Static_Void_SByte_0` |
| `fclShopEventEnd` | `100668048` | `fclShopInit.cs` | `fclShopEventEnd_Public_Static_Void_0` |
| `shpInitItemWindow` | `100668049` | `fclShopInit.cs` | `shpInitItemWindow_Public_Static_Void_lstListWindow_t_0` |
| `fclShopTexLoad` | `100668050` | `fclShopInit.cs` | `fclShopTexLoad_Public_Static_Void_fclDataShop_t_0` |
| `fclShopTexFree` | `100668051` | `fclShopInit.cs` | `fclShopTexFree_Public_Static_Void_fclDataShop_t_0` |
| `fclShopInitPanelMover` | `100668052` | `fclShopInit.cs` | `fclShopInitPanelMover_Public_Static_Void_fclDataShop_t_0` |
| `fclShopLoadMesScript` | `100668053` | `fclShopInit.cs` | `fclShopLoadMesScript_Public_Static_Void_fclDataShop_t_Int32_0` |
| `fclShopProcessStart` | `100668054` | `fclShopInit.cs` | `fclShopProcessStart_Public_Static_Void_Object_0` |
| `fclShopProcessEnd` | `100668055` | `fclShopInit.cs` | `fclShopProcessEnd_Public_Static_Int32_0` |
| `fclChkShopProcess` | `100668056` | `fclShopInit.cs` | `fclChkShopProcess_Public_Static_SByte_0` |

### fclShopUpdate (SHP_SEQ driving)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `shpAddServiceTicket` | `100668058` | `fclShopUpdate.cs` | `shpAddServiceTicket_Private_Static_Int32_Int32_0` |
| `shpGetItemFromItemBox` | `100668059` | `fclShopUpdate.cs` | `shpGetItemFromItemBox_Private_Static_UInt16_Int32_0` |
| `shpExit` | `100668060` | `fclShopUpdate.cs` | `shpExit_Private_Static_Void_0` |
| `shpUpdateSeqBuy` | `100668061` | `fclShopUpdate.cs` | `shpUpdateSeqBuy_Private_Static_Void_0` |
| `shpUpdateSeqSell` | `100668062` | `fclShopUpdate.cs` | `shpUpdateSeqSell_Private_Static_Void_0` |
| `shpUpdateSeqBuyNums` | `100668063` | `fclShopUpdate.cs` | `shpUpdateSeqBuyNums_Private_Static_Void_0` |
| `shpUpdateSeqSellNums` | `100668064` | `fclShopUpdate.cs` | `shpUpdateSeqSellNums_Private_Static_Void_0` |
| `shpUpdateSeqConfirm` | `100668065` | `fclShopUpdate.cs` | `shpUpdateSeqConfirm_Private_Static_SByte_SByte_0` |
| `shpUpdateSeqPay` | `100668066` | `fclShopUpdate.cs` | `shpUpdateSeqPay_Private_Static_Void_0` |
| `shpServiceTicketItem` | `100668067` | `fclShopUpdate.cs` | `shpServiceTicketItem_Private_Static_Void_0` |
| `shpServiceTicket` | `100668068` | `fclShopUpdate.cs` | `shpServiceTicket_Private_Static_Void_0` |
| `shpUpdateServiceTicket` | `100668069` | `fclShopUpdate.cs` | `shpUpdateServiceTicket_Private_Static_Void_0` |
| `shpUpdateServiceTicketItem` | `100668070` | `fclShopUpdate.cs` | `shpUpdateServiceTicketItem_Private_Static_Void_0` |
| `shpUpdateSeqRoot` | `100668071` | `fclShopUpdate.cs` | `shpUpdateSeqRoot_Private_Static_Void_0` |
| `shpUpdateSeqTalk` | `100668072` | `fclShopUpdate.cs` | `shpUpdateSeqTalk_Private_Static_Void_0` |
| `shpUpdateSeqErr` | `100668073` | `fclShopUpdate.cs` | `shpUpdateSeqErr_Private_Static_Void_0` |
| `shpUpdatePanelMover` | `100668074` | `fclShopUpdate.cs` | `shpUpdatePanelMover_Private_Static_Void_0` |
| `shpUpdate` | `100668075` | `fclShopUpdate.cs` | `shpUpdate_Public_Static_Object_dds3ProcessID_t_0` |

### fclShopCalc (price + list creation)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `shpGetItemID` | `100668011` | `fclShopCalc.cs` | `shpGetItemID_Public_Static_UInt16_Int32_SByte_0` |
| `shpGetPackID` | `100668012` | `fclShopCalc.cs` | `shpGetPackID_Public_Static_Int32_Int32_0` |
| `shpGetShopItemNums` | `100668013` | `fclShopCalc.cs` | `shpGetShopItemNums_Public_Static_Int32_Int32_0` |
| `shpCreateItemList` | `100668014` | `fclShopCalc.cs` | `shpCreateItemList_Public_Static_Void_fclDataShop_t_0` |
| `shpCalcItemPrice` | `100668015` | `fclShopCalc.cs` | `shpCalcItemPrice_Public_Static_Int32_Int32_SByte_0` |
| `shpCalcPayOfMoney` | `100668016` | `fclShopCalc.cs` | `shpCalcPayOfMoney_Public_Static_SByte_Int32_SByte_0` |
| `shpChkChoiceMax` | `100668017` | `fclShopCalc.cs` | `shpChkChoiceMax_Public_Static_SByte_Int32_SByte_SByte_0` |
| `shpCalc` | `100668018` | `fclShopCalc.cs` | `shpCalc_Public_Static_Object_dds3ProcessID_t_0` |

### fclShopDraw (render layer)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `InitializeGameObject` | `100668019` | `fclShopDraw.cs` | `InitializeGameObject_Public_Static_Void_0` |
| `FinalizeGameObject` | `100668020` | `fclShopDraw.cs` | `FinalizeGameObject_Public_Static_Void_0` |
| `DispInsfee` | `100668021` | `fclShopDraw.cs` | `DispInsfee_Public_Static_Void_Int32_0` |
| `shpDrawPayMoney` | `100668022` | `fclShopDraw.cs` | `shpDrawPayMoney_Internal_Static_Void_UInt32_Int32_0` |
| `DispItemList` | `100668023` | `fclShopDraw.cs` | `DispItemList_Internal_Static_Void_GameObject_Boolean_0` |
| `DispItemUD` | `100668024` | `fclShopDraw.cs` | `DispItemUD_Internal_Static_Void_GameObject_Int32_Int32_0` |
| `shopClearNumControl` | `100668025` | `fclShopDraw.cs` | `shopClearNumControl_Internal_Static_Void_0` |
| `shpDrawNumsControl` | `100668026` | `fclShopDraw.cs` | `shpDrawNumsControl_Internal_Static_Void_cmpCursorInfo_t_Int32_SByte_0` |
| `shpDrawPanelParts` | `100668027` | `fclShopDraw.cs` | `shpDrawPanelParts_Internal_Static_Void_Int32_0` |
| `shpDrawItem` | `100668028` | `fclShopDraw.cs` | `shpDrawItem_Internal_Static_Void_String_UInt32_UInt16_Int32_0` |
| `shpDrawBuyItem` | `100668029` | `fclShopDraw.cs` | `shpDrawBuyItem_Internal_Static_Void_SByte_lstListWindow_t_SByte_0` |
| `shpDrawSellItem` | `100668030` | `fclShopDraw.cs` | `shpDrawSellItem_Internal_Static_Void_SByte_lstListWindow_t_SByte_0` |
| `SetCursorEffect` | `100668031` | `fclShopDraw.cs` | `SetCursorEffect_Internal_Static_Void_cmpCursorInfo_t_Dictionary_2_String_GameObject_String_Int32_Il2CppStructArray_1_Int32_0` |
| `shpDrawSeqRoot` | `100668032` | `fclShopDraw.cs` | `shpDrawSeqRoot_Internal_Static_Void_Int32_0` |
| `shpDrawSeqBuy` | `100668033` | `fclShopDraw.cs` | `shpDrawSeqBuy_Internal_Static_Void_SByte_0` |
| `shpDrawSeqSell` | `100668034` | `fclShopDraw.cs` | `shpDrawSeqSell_Internal_Static_Void_SByte_0` |
| `shpDrawSeqSelect` | `100668035` | `fclShopDraw.cs` | `shpDrawSeqSelect_Internal_Static_Void_SByte_0` |
| `shpDrawSeqConfirm` | `100668036` | `fclShopDraw.cs` | `shpDrawSeqConfirm_Public_Static_Void_SByte_0` |
| `shpDrawSeqErr` | `100668037` | `fclShopDraw.cs` | `shpDrawSeqErr_Public_Static_Void_0` |
| `shpDrawServiceTicket` | `100668038` | `fclShopDraw.cs` | `shpDrawServiceTicket_Public_Static_Void_0` |
| `shpDraw` | `100668039` | `fclShopDraw.cs` | `shpDraw_Public_Static_Object_dds3ProcessID_t_0` |

## Shop state machine enum

- `Il2Cppfacility_H/SHP_SEQ.cs`

Exported to `data/part5_menu_enums.csv`.


## Shop work struct

- `Il2Cppfacility_H/fclDataShop_t.cs`

High-signal fields:
- `SeqInfo` : `cmpSeqInfo_t`
- list window: `ListWindow : lstListWindow_t`
- buy list: `BuyItemList` (u16), `BuyItemCnt`
- sell list: `SellItemList : fclItemList_t`
- money: `PayMoney`, `PayTotal`
- `TexHandle`, `HelpPanel`, `BackPanel`, `PanelMover`

See `data/part5_facility_struct_fields.csv`.


## Static table surfaces (rag/junk)

The following wrappers expose global/static inventory tables via `NativeFieldInfoPtr_*`:

- `Il2Cpp/fclRagShopTable.cs` fields:

- `fclRagTbl`
- `fclRagDefaultMitamaTbl`
- `fclRagDefaultSeireiTbl`
- `fclRagItemTbl`
- `fclRagItemPackTbl`
- `fclRagMitamaTbl`
- `fclRagMitamaPackTbl`
- `fclRagSeireiTbl`
- `fclRagSeireiPackTbl`

- `Il2Cpp/fclJunkShopTable.cs` fields:

- `fclShopTbl`
- `fclShopItemBoxTbl`
- `fclShopItemPackTbl`

These are exported to `data/part5_table_field_surfaces.csv`.


## Notes for future hook planning

- `shpCreateItemList`, `shpCalcItemPrice`, `shpCalcPayOfMoney` are the “math seams” for shop behavior.
- `fclShopProcessStart` is the cleanest outer seam.
- Rag/Junk tables are likely *data-driven* inventories; if these tables are mutated at runtime (or copied into work structs), a mod can either patch the source tables or patch the created item list.
