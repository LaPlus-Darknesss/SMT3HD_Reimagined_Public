# Part 2 — Cutscenes (evtPolygonMovie) + PMD tables (Il2Cppevent_H)

Raw catalogs:
- `data/part2_evtPolygonMovie_members.csv`
- `data/part2_pmd_structs_and_tables.csv`
- `data/part2_pmd_enums.json`

---

## evtPolygonMovie: start/stop + introspection seams

The names here are unusually direct: this system is a cutscene runner with its own frame loop and a rich internal table format (PMD).

### High-signal public entry points

| name | token_hex | token_dec | decl |
|---|---|---|---|
| SetPMFileName | 0x06001E15 | 100670997 | public unsafe static void SetPMFileName(int eventno, int cutno, out string pm1file, out string pm2file) |
| evtStartPolygonMovie2 | 0x06001E16 | 100670998 | public unsafe static dds3ProcessID_t evtStartPolygonMovie2(int priority, int eventno, int cutno) |
| evtCheckPolygonMovieRun | 0x06001DF9 | 100670969 | public unsafe static int evtCheckPolygonMovieRun(dds3ProcessID_t id) |
| evtPolygonMovieShutdown | 0x06001DFD | 100670973 | public unsafe static Il2CppSystem.Object evtPolygonMovieShutdown(dds3ProcessID_t id) |
| GetNowFrame | 0x06001E10 | 100670992 | public unsafe static int GetNowFrame() |
| GetNowCameraName | 0x06001DDA | 100670938 | public unsafe static string GetNowCameraName() |
| FrameRun | 0x06001DF4 | 100670964 | public unsafe static void FrameRun(pmdProcessWork_t wdata) |
| SwitchAnimation | 0x06001E1E | 100671006 | public unsafe static void SwitchAnimation(pmdProcessWork_t wdata, bool sw) |
| evtSetPolygonMovieFlag | 0x06001E0D | 100670989 | public unsafe static void evtSetPolygonMovieFlag(dds3ProcessID_t id, uint flag) |
| evtResetPolygonMovieFlag | 0x06001E0E | 100670990 | public unsafe static void evtResetPolygonMovieFlag(dds3ProcessID_t id, uint flag) |
| evtCheckPolygonMovieFlag | 0x06001E0F | 100670991 | public unsafe static int evtCheckPolygonMovieFlag(dds3ProcessID_t id, uint flag) |
| CheckLoad | 0x06001DF8 | 100670968 | public unsafe static bool CheckLoad(pmdProcessWork_t wdata) |
| StartAnimation | 0x06001DD6 | 100670934 | public unsafe static void StartAnimation(pmdProcessWork_t wdata) |
| DispAnimation | 0x06001DD7 | 100670935 | public unsafe static void DispAnimation(pmdProcessWork_t wdata) |
| SwitchAnimation | 0x06001E1E | 100671006 | public unsafe static void SwitchAnimation(pmdProcessWork_t wdata, bool sw) |

Practical notes:
- `evtStartPolygonMovie2(priority, eventno, cutno)` is the cleanest “cutscene start” seam.
- `SetPMFileName(eventno, cutno, out pm1file, out pm2file)` is the cleanest “identity resolution” seam (what file(s) will be loaded).
- `GetNowFrame()` + `GetNowCameraName()` give you a cheap way to confirm you are inside a running PMD and what shot you’re on.

---

## PMD format: what the wrapper types tell us

Everything below is “structure-level” information (types and field names), not behavior guesses.

### pmdHeader_t
- `sbyte FileType`
- `sbyte FileFormat`
- `short UserID`
- `int FileSize`
- `Il2CppStructArray<byte> MagicCode`
- `int ExpandSize`
- `int TypeTableCount`
- `int Version`
- `int Reserve2`
- `int Reserve3`

### pmdCutInfo_t
- `int FirstFrame`
- `int LastFrame`
- `int TotalFrame`
- `int Reserve1`

### pmdFrameTable_t
- `ushort ObjectType`
- `ushort Frame`
- `ushort Length`
- `short NameIndex`
- `Il2CppStructArray<pmdDataInfo_t> Data`

### pmdTypeTable_t
- `int Type`
- `int ItemSize`
- `int ItemCount`
- `int ItemAddress`

### pmdNameTable_t
- `string Name`

### pmdUnitTable_t
- `int NameIndex`
- `int FileIndex`
- `int MajorNum`
- `int MinorNum`
- `int DataOffset`
- `int DataSize`
- `int Reserve1`
- `int Reserve2`

### pmdEffectTable_t
- `int NameIndex`
- `int DataOffset`
- `int Type`
- `int Reserve2`

### pmdStageTable_t
- `int NameIndex`
- `int FileIndex`
- `int MajorNum`
- `int MinorNum`

### pmdCameraTable_t
- `int NameIndex`
- `int Reserve1`
- `int Reserve2`
- `int Reserve3`

### pmdProcessWork_t
- `uint flag`
- `int CameraFirst`
- `pmdHeader_t Header1`
- `Il2CppReferenceArray<pmdTypeTable_t> TypeTable1`
- `Il2CppReferenceArray<pmdNameTable_t> NameTable1`
- `int NameCount1`
- `pmdStageTable_t StageTable`
- `int StageCount`
- `Il2CppStructArray<byte> StageF1`
- `Il2CppStructArray<byte> StageF2`
- `Il2CppStructArray<byte> StageTB`
- `Il2CppReferenceArray<pmdUnitTable_t> UnitTable`
- `int UnitCount`
- `Il2CppStructArray<byte> UnitData`
- `Il2CppReferenceArray<pmdEffectTable_t> EffectTable`
- `int EffectCount`
- `Il2CppSystem.Object EffectData`
- `fileHandle_t FileHandle2`
- `sdfMemHandle_t MemHandle2`
- `Il2CppStructArray<byte> pm2`
- `string fkey`
- `pmdHeader_t Header2`
- `Il2CppReferenceArray<pmdTypeTable_t> TypeTable2`
- `Il2CppReferenceArray<pmdNameTable_t> NameTable2`
- `int NameCount2`
- `pmdCutInfo_t CutInfo`
- `Il2CppReferenceArray<pmdFrameTable_t> FrameTable`
- `int FrameCount`
- `Il2CppReferenceArray<pmdCameraTable_t> CameraTable`
- `int CameraCount`
- `Il2CppReferenceArray<pmdSceneLightTable_t> SLightTable`
- `int SLightCount`
- `Il2CppReferenceArray<pmdSceneFogTable_t> SFogTable`
- `int SFogCount`
- `Il2CppReferenceArray<dds3Effect_Blur_t> Blur2Table`
- `int Blur2Count`
- `Il2CppReferenceArray<dds3Effect_PartialMultiBlurParam_t> MBlurTable`
- `int MBlurCount`
- `Il2CppReferenceArray<dds3Effect_DistortionBlurParam_t> DBlurTable`
- `int DBlurCount`
- `Il2CppReferenceArray<dds3Effect_Filter_t> FilterTable`
- `int FilterCount`
- `Il2CppReferenceArray<dds3Effect_PartialMultiFilterParam_t> MFilterTable`
- `int MFilterCount`
- `Il2CppReferenceArray<dds3Effect_RippleBlurParam_t> RBlurTable`
- `int RBlurCount`
- `int MesHandle`
- `dds3ProcessID_t ProcessID`
- `int EventNo`
- `int CutNo`
- `int NowFrame`
- `Il2CppReferenceArray<dds3Basic_t> EffectBasicTable`
- `evtTexManager TexMngr`
- `GameObject ChrObj`
- `GameObject FldObj`
- `List<AnimCheckerPMV> anim_list`


---

## PMD enums (type/category vocab)

### PMD_DATATYPE
- `PMD_DATATYPE_CUTINFO`
- `PMD_DATATYPE_NAME`
- `PMD_DATATYPE_STAGE`
- `PMD_DATATYPE_UNIT`
- `PMD_DATATYPE_FRAME`
- `PMD_DATATYPE_CAMERA`
- `PMD_DATATYPE_MESSAGE`
- `PMD_DATATYPE_EFFECT`
- `PMD_DATATYPE_EFFECTDATA`
- `PMD_DATATYPE_UNITDATA`
- `PMD_DATATYPE_F1`
- `PMD_DATATYPE_F2`
- `PMD_DATATYPE_FTB`
- `PMD_DATATYPE_SLIGHT`
- `PMD_DATATYPE_SFOG`
- `PMD_DATATYPE_BLUR2`
- `PMD_DATATYPE_MULTBLUR`
- `PMD_DATATYPE_DISTBLUR`
- `PMD_DATATYPE_FILTER`
- `PMD_DATATYPE_MULTFILTER`
- `PMD_DATATYPE_RIPBLUR`
- `PMD_DATATYPE_MAX`

### PMD_EFFTYPE
- `PMD_EFFTYPE_D3P`
- `PMD_EFFTYPE_BED`
- `PMD_EFFTYPE_MG1`
- `PMD_EFFTYPE_MG2`

### PMD_FADE
- `PMD_FADE_WHITE_IN`
- `PMD_FADE_WHITE_OUT`
- `PMD_FADE_BLACK_IN`
- `PMD_FADE_BLACK_OUT`

### PMD_OBJTYPE
- `PMD_OBJTYPE_STAGE`
- `PMD_OBJTYPE_UNIT`
- `PMD_OBJTYPE_CAMERA`
- `PMD_OBJTYPE_EFFECT`
- `PMD_OBJTYPE_MESSAGE`
- `PMD_OBJTYPE_SE`
- `PMD_OBJTYPE_FADE`
- `PMD_OBJTYPE_QUAKE`
- `PMD_OBJTYPE_BLUR`
- `PMD_OBJTYPE_LIGHT`
- `PMD_OBJTYPE_SLIGHT`
- `PMD_OBJTYPE_SFOG`
- `PMD_OBJTYPE_SKY`
- `PMD_OBJTYPE_BLUR2`
- `PMD_OBJTYPE_MBLUR`
- `PMD_OBJTYPE_DBLUR`
- `PMD_OBJTYPE_FILTER`
- `PMD_OBJTYPE_MFILTER`
- `PMD_OBJTYPE_BED`
- `PMD_OBJTYPE_BGM`
- `PMD_OBJTYPE_MG1`
- `PMD_OBJTYPE_MG2`
- `PMD_OBJTYPE_FB`
- `PMD_OBJTYPE_RBLUR`
- `PMD_OBJTYPE_MAX`

### PMD_UNIT
- `PMD_UNIT_DISP_ON`
- `PMD_UNIT_DISP_OFF`
- `PMD_UNIT_SHADOW_OFF`
- `PMD_UNIT_MSHADOW_ON`
- `PMD_UNIT_VSHADOW_ON`

### PMD_BGM
- `PMD_BGM_TYPE_PLAY`
- `PMD_BGM_TYPE_FADEIN`
- `PMD_BGM_TYPE_FADEOUT`
- `PMD_BGM_TYPE_TRANS`
- `PMD_BGM_TYPE_VOLDOWN`
- `PMD_BGM_TYPE_VOLUP`
- `PMD_BGM_TYPE_ALLSTOP`


How to read this:
- `PMD_DATATYPE` suggests which **table blocks** can appear (CUTINFO, UNIT, EFFECT, CAMERA, LIGHT, FOG, BGM, …).
- Other enums provide per-table classification (`PMD_OBJTYPE`, `PMD_EFFTYPE`, etc.).


