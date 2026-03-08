# Assembly-CSharp Reference Atlas — Part 1
Generated from `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp` and the CSV indexes in `SMT3HD_Reimagined/docs/`.
Date generated: 2026-02-09 04:51:41
## What this part covers
- How the IL2CPP wrapper sources are structured (tokens, fields, method pointers)
- A high-level map of namespaces and type clusters
- Curated, concrete “seams” for encounter flow + scripted battle entry + skill/battle math
- Concrete field layouts for a few core data structures (unit work, skill spec, demon base, encounter packs)

## Namespace overview (top 20 by type count)
| Namespace | Types | Notable high-method-count types |
|---|---:|---|
| `Il2Cpp` | 854 | `_PrivateImplementationDetails_` (5293), `dds3KernelDraw` (558), `Nme_Draw` (516), `effPCPMisc` (472), `Nme_Draw_TCHINESE` (466) |
| `(root)` | 509 | `dds3GlobalWork_tag` (309), `typeSKYTBL` (116), `fldSave_tag` (97), `modelViewerWork_t` (85), `datUnitWork_tag` (69) |
| `Il2Cppeffect_H` | 224 | `dds3Particle_Basic_t` (77), `effPCPCrackParam_t` (71), `effPCPWhirlwind3PolyParam_t` (69), `effPCPWhirlwind2PolyParam_t` (65), `effPCPWhirlwindPolyParam_t` (63) |
| `Il2Cpplibsdf_H` | 86 | `SDF_VU0` (86), `sdfTexHandle_t` (51), `sdfModelRootNode_t` (50), `sdfMatPacket_t` (49), `sdfModelNode_t` (47) |
| `Il2Cppfield_H` | 80 | `fldGlobalWork_t` (143), `fldDebug_t` (57), `fldSceneParam_t` (42), `GameFileInfo_t` (39), `collision_s` (39) |
| `Il2Cppfacility_H` | 60 | `cmbGlobalWork_t` (135), `fclDataRag_t` (57), `fclDataShop_t` (47), `fclDataRecover_t` (47), `fclDataTerminal_t` (45) |
| `Il2Cppnewdata_H` | 50 | `datNormalSkill_s` (73), `datUnitWork_s` (69), `datDevilFormat_s` (53), `datDevilNegoFormat_s` (37), `datUnitVisual_s` (31) |
| `Il2Cppevent_H` | 32 | `evtUnitHandle_t` (140), `pmdProcessWork_t` (121), `evtPictureProcessData_t` (59), `evtPicturePriTbl_t` (33), `evtPackProcessData_t` (33) |
| `Il2Cppcamp_H` | 29 | `cmpGlobalWork_t` (43), `cmpDataDH_t` (37), `cmpHeartsIcon_t` (33), `cmpPanelEff_s` (31), `cmpCursorPos_t` (27) |
| `Il2Cppnewbattle_H` | 25 | `nbMainProcessData_t` (182), `nbNegoProcessData_t` (149), `nbFormation_t` (107), `nbActionProcessData_t` (102), `nbCommSelProcessData_t` (54) |
| `Il2Cppbasic_H` | 22 | `dds3Basic_t` (35), `dds3EffectObjectBasicWorkData_t` (34), `dds3ObjectBaseControlWorkData_t` (30), `dds3PoseSet_t` (29), `dds3LightObjectBasicWorkData_t` (29) |
| `Il2Cppmodel_H` | 20 | `dds3ModelResrc_t` (35), `dds3ModelHandle_t` (34), `pibChunkItem_TRACK_t` (22), `dds3ModelBlurCb_t` (21), `pibHeaderChunk_t` (19) |
| `Il2Cppinterface_H` | 15 | `_TEXT` (41), `_FRQ` (33), `itfMesBinSelHeader_t` (17), `itfMesBinMesHeader_t` (13), `itfpanel_t` (13) |
| `Il2Cpppipe_H` | 14 | `PIPE_EFF_t` (25), `tagPipeObject` (24), `tagpipeBasic` (17), `tagPipeFileHeader` (17), `tagMakaObject` (13) |
| `Il2Cppresult2_H` | 12 | `rstData_t` (111), `fclHearts_t` (21), `fclGiftItem_t` (11), `fclSkillParam_t` (9), `rstSkillInfo_t` (9) |
| `Il2Cppscr_H` | 10 | `scrProcessWork_s` (53), `scrHeader_t` (26), `scrProcTable_t` (10), `scrLabelTable_t` (10), `scrCommandTable_t` (7) |
| `Il2Cpppb_H` | 9 | `pbWork_t` (25), `pbGroupData_t` (21), `pb_General` (16), `pbBackup_t` (14), `pbBlockData_t` (13) |
| `Il2Cppdds3ConfigMain_H` | 8 | `dds3ConfigGBWK_t` (23), `CFG_TAB` (0), `CFG_TYPE_AUDIO` (0), `CFG_TYPE_GAME` (0), `CFG_TYPE_GRAPHICS` (0) |
| `Il2CppXRD773Unity` | 7 | `CommonMesh` (81), `BoxG` (31), `GraphicManager` (30), `CommonSprite` (17), `BoxObjectPool`1` (15) |
| `Il2CppfileManager_H` | 7 | `fileCb_t` (35), `pacFileCb_t` (12), `fileEndCallback_t` (9), `pacFileEndCallback_t` (9), `fileHandle_t` (3) |

## Prefix / suffix glossary (quick orientation)
- `dat*`: data-table accessors and gameplay calculation helpers (stats, skills, drops, etc.)
- `fld*`: field / exploration (map, encounters, area naming, movement, triggers)
- `evt*`: event scripting / cutscenes / scripted battle entry
- `nb*` / `newbattle_H`: battle-system runtime/process structures
- `eff*`: visual effects systems (usually not where gameplay math lives)
- `fcl*` / `facility_H`: facilities (shops, cathedral, menus that are “places”)
- Struct-ish suffixes:
  - `*_s`: “shape/base” record (fields/properties)
  - `*_t`: derived concrete type (often just adds constructor)

## How to pivot from name → file
1) Look up the type in `docs/assembly_csharp_types.csv`.
2) Open the corresponding wrapper source in `tools/references/Assembly-CSharp/Assembly-CSharp/<namespace>/TypeName.cs`.
3) For methods, the wrapper’s static constructor usually resolves pointers via `IL2CPP.GetIl2CppMethodByToken(..., <token>)`.
4) For fields, the wrapper resolves field handles via `IL2CPP.GetIl2CppField(..., "fieldName")`.

## Token notes
- The wrapper sources use **IL2CPP method tokens** (integers like `100672544`) to resolve a method at runtime.
- These tokens are stable within the same build of the target `Assembly-CSharp.dll` and are extremely handy for locating the native method pointer without string-based reflection.
- The wrapper method bodies call `IL2CPP.il2cpp_runtime_invoke(...)`. For performance-critical hooks, you usually want a detour at the resolved native pointer rather than patching the wrapper itself.


## v11 additions
- 57: CallerCount-based prioritization for text pipeline
- 58: Tiered hook priority matrix for text + negotiation
- 59: Expanded itfMesManager hot-method list (window/msgwnd/vars)
- 60: Expanded nbNegoProcess hot-method list (negotiation text seams)
