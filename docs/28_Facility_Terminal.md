# Facility: Terminal (save/warp/etc) — `fclTerminal*`

The Terminal UI (save terminal, terminal travel, and related actions) is implemented as a Facility process surface.

Primary wrappers:
- `Il2Cpp/fclTerminalInit.cs`
- `Il2Cpp/fclTerminalUpdate.cs`
- `Il2Cpp/fclTerminalCalc.cs`
- `Il2Cpp/fclTerminalDraw.cs`


## Terminal lifecycle + process start/end

### fclTerminalInit (event/BGM/menu-mask + process control)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `trmGetCallMode` | `100668097` | `fclTerminalInit.cs` | `trmGetCallMode_Public_Static_SByte_0` |
| `trmGetEventStat` | `100668098` | `fclTerminalInit.cs` | `trmGetEventStat_Internal_Static_TRM_EVT_0` |
| `trmSetEventStat` | `100668099` | `fclTerminalInit.cs` | `trmSetEventStat_Public_Static_Void_TRM_EVT_0` |
| `trmGetEventNo` | `100668100` | `fclTerminalInit.cs` | `trmGetEventNo_Public_Static_Int32_Int32_fclDataTerminal_t_0` |
| `trmCreateEventProcessName` | `100668101` | `fclTerminalInit.cs` | `trmCreateEventProcessName_Public_Static_SByte_Int32_byref_String_0` |
| `trmStartBGM` | `100668102` | `fclTerminalInit.cs` | `trmStartBGM_Public_Static_Void_Int32_Int32_Boolean_0` |
| `trmEndBGM` | `100668103` | `fclTerminalInit.cs` | `trmEndBGM_Public_Static_Void_0` |
| `trmStartEvent` | `100668104` | `fclTerminalInit.cs` | `trmStartEvent_Public_Static_SByte_0` |
| `trmEndEvent` | `100668105` | `fclTerminalInit.cs` | `trmEndEvent_Public_Static_SByte_Int32_0` |
| `trmChkEventWait` | `100668106` | `fclTerminalInit.cs` | `trmChkEventWait_Public_Static_SByte_0` |
| `trmSetProcessTerm` | `100668107` | `fclTerminalInit.cs` | `trmSetProcessTerm_Public_Static_Void_0` |
| `trmCreateTerminalList` | `100668108` | `fclTerminalInit.cs` | `trmCreateTerminalList_Public_Static_Void_fclDataTerminal_t_0` |
| `trmInitTransWindow` | `100668109` | `fclTerminalInit.cs` | `trmInitTransWindow_Public_Static_Void_lstListWindow_t_SByte_sdfTexHandle_t_0` |
| `trmSetNmlRootMenu` | `100668110` | `fclTerminalInit.cs` | `trmSetNmlRootMenu_Public_Static_Void_UInt16_cmpCmdSelInfo_t_fclDataTerminal_t_0` |
| `trmChkJumpSmallToLearge` | `100668111` | `fclTerminalInit.cs` | `trmChkJumpSmallToLearge_Public_Static_Int32_fclDataTerminal_t_0` |
| `trmInitRootMenu` | `100668112` | `fclTerminalInit.cs` | `trmInitRootMenu_Public_Static_Void_SByte_UInt16_cmpCmdSelInfo_t_fclDataTerminal_t_0` |
| `fclTerminalTexLoad` | `100668113` | `fclTerminalInit.cs` | `fclTerminalTexLoad_Public_Static_Void_fclDataTerminal_t_0` |
| `fclTerminalTexFree` | `100668114` | `fclTerminalInit.cs` | `fclTerminalTexFree_Public_Static_Void_fclDataTerminal_t_0` |
| `fclInitTerminalCore` | `100668115` | `fclTerminalInit.cs` | `fclInitTerminalCore_Public_Static_Void_Int32_Int32_fclDataTerminal_t_Boolean_0` |
| `fclInitTerminal` | `100668116` | `fclTerminalInit.cs` | `fclInitTerminal_Public_Static_fclDataTerminal_t_Int32_Int32_cmpSeqInfo_t_0` |
| `fclUpdateTerminalInit` | `100668117` | `fclTerminalInit.cs` | `fclUpdateTerminalInit_Public_Static_Void_fclDataTerminal_t_0` |
| `OnInstitutionCallback` | `100668118` | `fclTerminalInit.cs` | `OnInstitutionCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `OnMenuTmnlCallback` | `100668119` | `fclTerminalInit.cs` | `OnMenuTmnlCallback_Private_Static_Void_Int32_SteamColliders_SteamCollider_Int32_0` |
| `fclDestroyTerminal` | `100668120` | `fclTerminalInit.cs` | `fclDestroyTerminal_Public_Static_Object_dds3ProcessID_t_0` |
| `fclTerminalProcessStart` | `100668121` | `fclTerminalInit.cs` | `fclTerminalProcessStart_Public_Static_Void_Int32_Int32_0` |
| `fclTerminalProcessEnd` | `100668122` | `fclTerminalInit.cs` | `fclTerminalProcessEnd_Public_Static_Int32_0` |
| `fclChkTerminalProcess` | `100668123` | `fclTerminalInit.cs` | `fclChkTerminalProcess_Public_Static_SByte_0` |
| `fclTerminal` | `100668124` | `fclTerminalInit.cs` | `fclTerminal_Public_Static_Int32_0` |
| `fclTerminalMenuMask` | `100668125` | `fclTerminalInit.cs` | `fclTerminalMenuMask_Public_Static_Int32_0` |
| `fclTerminalMenuMaskDef` | `100668126` | `fclTerminalInit.cs` | `fclTerminalMenuMaskDef_Public_Static_Int32_0` |
| `fclTerminalEnableTalk` | `100668127` | `fclTerminalInit.cs` | `fclTerminalEnableTalk_Public_Static_Int32_0` |
| `fclTerminalDisableTalk` | `100668128` | `fclTerminalInit.cs` | `fclTerminalDisableTalk_Public_Static_Int32_0` |

### fclTerminalUpdate (TRM_SEQ driving)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `trmStartSaveAct` | `100668130` | `fclTerminalUpdate.cs` | `trmStartSaveAct_Public_Static_Void_Int32_0` |
| `trmCheckSaveAct` | `100668131` | `fclTerminalUpdate.cs` | `trmCheckSaveAct_Public_Static_Int32_0` |
| `trmGetJumpTerminalNo` | `100668132` | `fclTerminalUpdate.cs` | `trmGetJumpTerminalNo_Public_Static_Int32_0` |
| `trmUpdateSeqTrans` | `100668133` | `fclTerminalUpdate.cs` | `trmUpdateSeqTrans_Public_Static_Void_0` |
| `trmActInitMisc` | `100668134` | `fclTerminalUpdate.cs` | `trmActInitMisc_Public_Static_Void_0` |
| `trmUpdateSeqConfirm` | `100668135` | `fclTerminalUpdate.cs` | `trmUpdateSeqConfirm_Public_Static_SByte_0` |
| `trmUpdateSeqRoot` | `100668136` | `fclTerminalUpdate.cs` | `trmUpdateSeqRoot_Public_Static_Void_0` |
| `trmUpdateActLearge` | `100668137` | `fclTerminalUpdate.cs` | `trmUpdateActLearge_Public_Static_Void_0` |
| `trmUpdateActSmall` | `100668138` | `fclTerminalUpdate.cs` | `trmUpdateActSmall_Public_Static_Void_0` |
| `trmUpdateAct` | `100668139` | `fclTerminalUpdate.cs` | `trmUpdateAct_Public_Static_Void_0` |
| `trmUpdateSave` | `100668140` | `fclTerminalUpdate.cs` | `trmUpdateSave_Public_Static_Void_0` |
| `trmUpdateSaveActIn` | `100668141` | `fclTerminalUpdate.cs` | `trmUpdateSaveActIn_Public_Static_Void_0` |
| `trmUpdateSaveActOut` | `100668142` | `fclTerminalUpdate.cs` | `trmUpdateSaveActOut_Public_Static_Void_0` |
| `trmUpdate` | `100668143` | `fclTerminalUpdate.cs` | `trmUpdate_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |
| `trmUpdateDebug` | `100668144` | `fclTerminalUpdate.cs` | `trmUpdateDebug_Public_Static_SByte_0` |
| `CheckSave` | `100668145` | `fclTerminalUpdate.cs` | `CheckSave_Public_Static_Void_0` |
| `CheckTransportCount` | `100668146` | `fclTerminalUpdate.cs` | `CheckTransportCount_Public_Static_Void_0` |

### fclTerminalCalc (logic layer)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `trmCalcAct` | `100668084` | `fclTerminalCalc.cs` | `trmCalcAct_Private_Static_Void_0` |
| `trmCalc` | `100668085` | `fclTerminalCalc.cs` | `trmCalc_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |

### fclTerminalDraw (render layer)

| Method | Token (dec) | Wrapper file | Native signature key |
|---|---:|---|---|
| `InitializeGameObject` | `100668086` | `fclTerminalDraw.cs` | `InitializeGameObject_Public_Static_Void_0` |
| `FinalizeGameObject` | `100668087` | `fclTerminalDraw.cs` | `FinalizeGameObject_Public_Static_Void_0` |
| `trmGetTerminalName` | `100668088` | `fclTerminalDraw.cs` | `trmGetTerminalName_Public_Static_String_Int32_Int32_0` |
| `trmDrawTerminalNamePanel` | `100668089` | `fclTerminalDraw.cs` | `trmDrawTerminalNamePanel_Internal_Static_Void_0` |
| `trmDrawTerminalName` | `100668090` | `fclTerminalDraw.cs` | `trmDrawTerminalName_Internal_Static_Void_GameObject_TextMeshProUGUI_String_UInt32_0` |
| `trmDrawSeqTrans` | `100668091` | `fclTerminalDraw.cs` | `trmDrawSeqTrans_Internal_Static_Void_0` |
| `SetCursorEffect` | `100668092` | `fclTerminalDraw.cs` | `SetCursorEffect_Internal_Static_Void_cmpCursorInfo_t_Dictionary_2_String_GameObject_String_Int32_Il2CppStructArray_1_Int32_0` |
| `trmDrawSeqConfirm` | `100668093` | `fclTerminalDraw.cs` | `trmDrawSeqConfirm_Internal_Static_Void_0` |
| `trmDrawSeqRoot` | `100668094` | `fclTerminalDraw.cs` | `trmDrawSeqRoot_Internal_Static_Void_SByte_0` |
| `trmDraw` | `100668095` | `fclTerminalDraw.cs` | `trmDraw_Public_Static_dds3ProcessFunc_t_dds3ProcessID_t_0` |

## Terminal state machine enums

- `Il2Cppfacility_H/TRM_SEQ.cs` (sequence states)
- `Il2Cppfacility_H/TRM_EVT.cs` (terminal events)

Both are exported to `data/part5_menu_enums.csv`.


## Terminal work struct

- `Il2Cppfacility_H/fclDataTerminal_t.cs`

High-signal fields:
- `SeqInfo` : `cmpSeqInfo_t`
- `CmdSelInfo` : `cmpCmdSelInfo_t`
- `TransWindow` : `lstListWindow_t` (terminal list)
- `TerminalType`, `TerminalNo`, `TerminalList`, `TerminalCnt`
- script/data child processes: `ScriptPID`, `EvtScrPID`, `PMPID`
- `TexHandle` array (texture management)

See `data/part5_facility_struct_fields.csv`.


## Notes for future hook planning

- `fclTerminalProcessStart` / `fclTerminalProcessEnd` are the obvious “outer” seams.
- `trmStartEvent` / `trmEndEvent` and `trmStartBGM` / `trmEndBGM` show that Terminal is tightly coupled to scripted events and music changes.
- `trmSetMenuMask` and `trmClearMenuMask` look like high-level gating for what terminal options are available.
