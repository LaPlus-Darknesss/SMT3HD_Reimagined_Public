# Assembly-CSharp Mapping Notes — v16 (2026-02-11)

This folder is a *living* documentation set derived from the decompiled `Assembly-CSharp` reference sources under:

- `SMT3HD_Reimagined/tools/references/Assembly-CSharp/Assembly-CSharp`


## What’s new in v16 

Focus: **Resource resolution + DLC mounting → byte-level SDF ingest bridge**.

- `FilePathConstans` root path buckets (incl. DLC roots)
- `AssetBundleCtrl` byte/object loaders + key surfaces
- `DlcFileCtr` + `SteamDlcFileUtil` mount surfaces
- Bridging map into `sdfDevFile.sdfDevLoadFileUnity(... bytes ...)`


## What’s new in v14 

Focus: **field resolver runtime pipeline & core consumers** — we map the non-`Set*` resolver surface (`fldLoad*`, `fldAddressResolver*`, `fldFileResolverFL*`, `fldMake*`) and connect it to:
- `sdfModel` (scene graph build/eval/free)
- `dds3ModelBasic` + `dds3ModelRangeBasic`
- `fldEveHit` (event trigger checks)

New docs in this tranche:
- 74–77

Key exports live under `data/part14_*.csv`.

## What’s new in v13 

Focus: **field pack format surfaces** (FL1/FL2 headers, resource tables, and the `fldFileResolver.Setfld*` parsing helper catalog), plus the `pipe_H` and `model_H` format anchors used nearby.

New docs in this tranche:
- 67–73

Key exports live under `data/part13_*.csv`.

## What’s new in v12 

Focus: **resource/file pipeline** (paths, asset bundles, DLC, low-level file handles, and the field resolver).

New docs in this tranche:
- 61–66

Key exports live under `data/part12_*.csv`.

## What’s new in v10 

Focus: **message system deep-dive** (end-to-end flow, BIN headers, variables/localize surface, and rendering-side panel seams).

New docs in this tranche:
- 51–56

Key exports live under `data/part10_*.csv`.

Focus: **negotiation data tables + message/request plumbing**.

New docs in this tranche:
- 47–50

Key exports live under `data/part9_*.csv`.

## Document index

- **00_README.md** — README
- **01_Atlas_Part1.md** — Atlas Part1
- **02_KeySeams_Part1.md** — KeySeams Part1
- **03_DataStruct_datUnitWork.md** — DataStruct datUnitWork
- **04_Skills_datNormalSkill.md** — Skills datNormalSkill
- **05_Demons_datDevilFormat_and_Aisyo.md** — Demons datDevilFormat and Aisyo
- **06_Encounters_Packs_and_Seams.md** — Encounters Packs and Seams
- **07_BattleMath_datCalc.md** — BattleMath datCalc
- **08_BattleRuntime_newbattle_H_Index.md** — BattleRuntime newbattle H Index
- **09_CSV_Indexes_Usage.md** — CSV Indexes Usage
- **10_Event_System_Map.md** — Event System Map
- **11_evtCommand_Catalog.md** — evtCommand Catalog
- **12_evtLoadManager_and_evtStage.md** — evtLoadManager and evtStage
- **13_evtKutiManager_and_evtDebug.md** — evtKutiManager and evtDebug
- **14_evtPolygonMovie_and_PMD.md** — evtPolygonMovie and PMD
- **15_Token_Resolution_Cookbook.md** — Token Resolution Cookbook
- **16_Insaniax_Reference_Surface.md** — Insaniax Reference Surface
- **17_Text_Localize_and_Messages.md** — Text Localize and Messages
- **18_itfMesManager_Method_Catalog.md** — itfMesManager Method Catalog
- **19_MessageBin_Format.md** — MessageBin Format
- **20_MessageFlow_Structs.md** — MessageFlow Structs
- **21_Save_System_Overview.md** — Save System Overview
- **22_Save_Data_Structs.md** — Save Data Structs
- **23_Save_UI_Surface.md** — Save UI Surface
- **24_Save_Mod_Tactics.md** — Save Mod Tactics
- **25_UI_Menu_Flow_Overview.md** — UI Menu Flow Overview
- **26_Camp_Menu.md** — Camp Menu
- **27_Demon_Stock.md** — Demon Stock
- **28_Facility_Terminal.md** — Facility Terminal
- **29_Facility_Shop_and_Tables.md** — Facility Shop and Tables
- **30_Facility_Combine_Fusion.md** — Facility Combine Fusion
- **31_Menu_Enums_and_Work_Structs.md** — Menu Enums and Work Structs
- **32_Fusion_Table_Surfaces.md** — Fusion Table Surfaces
- **33_Fusion_GlobalWork_and_ResultTable.md** — Fusion GlobalWork and ResultTable
- **34_Keisyo_Inheritance_Engine.md** — Keisyo Inheritance Engine
- **35_Fusion_Sequence_Map_CMB_SEQ.md** — Fusion Sequence Map CMB SEQ
- **36_Battle_System_Core_Overview.md** — Battle System Core Overview (newbattle/nb*)
- **37_Action_Packet_Kouka_Pipeline.md** — Action → Packet → Kouka pipeline
- **38_Battle_Formulas_datCalc_nbCalc.md** — Battle formulas surface map (datCalc + nbCalc)
- **39_Aisyo_and_Status_Surfaces.md** — Resist + badstatus surfaces
- **40_Battle_AI_Scripts_and_Tables.md** — AI scripts/conditions and datDevilAI tables
- **41_PressTurn_BossPress_and_UI_Gauge.md** — Press-turn + boss press + gauge surfaces
- **42_nbMainProcess_Phase_Surface.md** — nbMainProcess phase setter surface map
- **43_Battle_CommandSelection_nbCommSelProcess.md** — Command selection (tabs/list/cursor)
- **44_Battle_TargetSelection_nbTarSelProcess.md** — Target selection (ctype/crule/carea)
- **45_Negotiation_Runtime_nbNegoProcess.md** — Negotiation runtime (requests/messages)
- **46_Negotiation_Flow_Catalog.md** — NbNegoFlow flow-id & step catalog

## Data exports

- `data/part1_*` … `data/part8_*` are machine-generated CSV inventories (methods, properties, enum values, etc.).
- Treat “meaning” inferences as provisional until confirmed via runtime observation/logging.

- **47_Negotiation_DataSurfaces_and_Tables.md** — Negotiation data surfaces (tables + packed blobs)
- **48_Negotiation_Messages_IDs_and_Binding.md** — Negotiation message IDs, slots, and binding methods
- **49_Negotiation_Requests_Pipeline.md** — Request type + amount + item selection pipeline
- **50_Negotiation_Modding_Playbook.md** — Hook-first playbook (doc-only)
- **51_Message_System_Overview_EndToEnd.md** — Message System End-to-End Overview
- **52_itfMesManager_itfMesMng_API_Map.md** — itfMesMng* Internal API Map
- **53_Message_BIN_Layout_Headers.md** — Message BIN Layout (Headers)
- **54_Message_Variables_and_Localize_Surface.md** — Variables + Localize Surface
- **55_Message_Window_Rendering_itfPanel.md** — Message Window Rendering (itfPanel)
- **56_Text_Modding_Playbook_DocOnly.md** — Text Modding Playbook (Doc-only)


### v12 additions

- **61_Resource_System_Overview.md** — Resource System Overview (Files, Paths, Bundles, DLC)
- **62_FilePathConstans_Canonical_Roots.md** — FilePathConstans (Canonical Roots)
- **63_AssetBundles_and_DLC.md** — AssetBundles & AssetBundleCtrl (Bundle Loading + DLC)
- **64_file_Manager_LowLevel.md** — file_Manager (Low-level “file → pointer → size”)
- **65_fldFileResolver_Field_Resource_Pipeline.md** — fldFileResolver (Field Resource Resolver + SDF defs)
- **66_ModelTables_and_Localize_Bridge.md** — Models + Messages bridge to resource pipeline
- **67_FieldPack_FL1_FL2_Header_and_AreaTables.md** — Field pack headers (FL1/FL2) + area tables
- **68_Field_ResourceTables_and_Heads.md** — Resource type tables + resource head records (incl. put/collision payload surfaces)
- **69_fldFileResolver_ParseCatalog_Setfld.md** — Catalog of `fldFileResolver.Setfld*` native parsing helpers (format surface inventory)
- **70_Field_ResourcePathTable_and_KeySeams.md** — Resource path table (`fldResourcePath*`) surface + typed decode variants
- **71_Pipe_Pack_Schemas.md** — `pipe_H` table/header schemas
- **72_Model_PIB_and_FileTables.md** — `model_H` file tables + PIB chunk headers
- **73_Type_Consumer_Crosswalk_Field_SDF.md** — Type → consumer crosswalk for field/SDF core
### v16 additions
- Docs 85–90
- New catalogs under `data/part16_*`


### v17 additions
- Docs 91–95 (key/akey economy + table-driven keys)
  - **91_Key_and_Akey_Economy.md** — Key/akey surface catalog and safe seam guidance
  - **92_mdlFileDefTable_KeyedModelTables.md** — Model table that returns `fname` / `pbname` / `akey`
  - **93_ScrScriptProcess_ScriptLoad_and_DLC.md** — Script start surfaces, DLC tags, akey threading
  - **94_Localize_and_Snd_Key_Pipeline.md** — Localize + audio asset bundle key routing
  - **95_Platform_AB_Chk_Path_Search.md** — Platform/language path search helper inventory
- New catalogs under `data/part17_*`
