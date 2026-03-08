# Part 2 — Kuti (mouth timeline) + Event debug toggles

Raw catalogs:
- `data/part2_evtKutiManager_members.csv`
- `data/part2_evtDebug_members.csv`

---

## evtKutiManager

This looks like the controller for the “Kuti” system (mouth / talk timeline driving).
It’s reachable directly via `evtLoadManager.GetKutiManager()` (**0x06001D4B / 100670795**) and is also stored as `evtLoadManager.kuti_scr`.

### High-signal methods

| name | token_hex | token_dec | decl |
|---|---|---|---|
| Initialize | 0x06001D0B | 100670731 | public unsafe void Initialize(string key, string path, bool evt = true) |
| Append | 0x06001D0C | 100670732 | public unsafe void Append(string key, string path, bool evt = true) |
| Clear | 0x06001D0E | 100670734 | public unsafe void Clear(bool sw = false) |
| SetKutiName | 0x06001D11 | 100670737 | public unsafe void SetKutiName(string name) |
| StopKuti | 0x06001D13 | 100670739 | public unsafe void StopKuti() |
| IsSpeaking | 0x06001D14 | 100670740 | public unsafe void IsSpeaking(evtUnitHandle_t h, float time) |
| SetUnit | 0x06001D15 | 100670741 | public unsafe void SetUnit(evtUnitHandle_t h) |
| DlcTimeLineLoad | 0x06001D08 | 100670728 | public unsafe void DlcTimeLineLoad(Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> _tbl) |
| DlcTimeLineDivisionAdd | 0x06001D09 | 100670729 | public unsafe void DlcTimeLineDivisionAdd(string key) |
| FixedUpdate | 0x06001D10 | 100670736 | public unsafe void FixedUpdate() |

### High-signal fields/properties

| name | token_hex | token_dec | decl |
|---|---|---|---|
| hUnit | 0x06001D18 | 100670744 | public unsafe evtUnitHandle_t hUnit |
| TimeLine | 0x06001D1A | 100670746 | public unsafe Dictionary<string, List<float>> TimeLine |
| KutiName | 0x06001D1C | 100670748 | public unsafe string KutiName |
| KutiSwitch | 0x06001D1E | 100670750 | public unsafe bool KutiSwitch |
| MsgTime | 0x06001D20 | 100670752 | public unsafe float MsgTime |
| UpTime | 0x06001D22 | 100670754 | public unsafe float UpTime |
| eTimeLine710 | 0x06001D24 | 100670756 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine710 |
| eTimeLine711 | 0x06001D26 | 100670758 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine711 |
| eTimeLine712 | 0x06001D28 | 100670760 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine712 |
| eTimeLine713 | 0x06001D2A | 100670762 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine713 |
| eTimeLine714 | 0x06001D2C | 100670764 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine714 |
| eTimeLine715 | 0x06001D2E | 100670766 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine715 |
| eTimeLine716 | 0x06001D30 | 100670768 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine716 |
| eTimeLine717 | 0x06001D32 | 100670770 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine717 |
| eTimeLine718 | 0x06001D34 | 100670772 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine718 |
| eTimeLine719 | 0x06001D36 | 100670774 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine719 |
| eTimeLine720 | 0x06001D38 | 100670776 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine720 |
| eTimeLine723 | 0x06001D3A | 100670778 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine723 |
| eTimeLine726 | 0x06001D3C | 100670780 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine726 |
| eTimeLine728 | 0x06001D3E | 100670782 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine728 |
| eTimeLine730 | 0x06001D40 | 100670784 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine730 |
| eTimeLine731 | 0x06001D42 | 100670786 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> eTimeLine731 |
| fTimeLine015 | 0x06001D44 | 100670788 | public unsafe Il2CppReferenceArray<evtKutiManager.E_TimeLineTbl> fTimeLine015 |
| name | 0x0600B41F | 100709407 | public unsafe string name |
| timeline | 0x0600B421 | 100709409 | public unsafe List<float> timeline |

Interpretation (based on names):
- `KutiName` is likely the current timeline “speaker” name (or key).
- `KutiSwitch` looks like an enable/mute toggle.
- `MsgTime`/`UpTime` suggest timing alignment with message boxes.
- The `eTimeLine710..` arrays look like preloaded DLC timeline tables.
- `TimeLine` is a `Dictionary<string, List<float>>` — extremely likely to be the actual per-key time tracks.

---

## evtDebug

This is a compact but important “hidden diagnostics” surface.

### High-signal methods

| name | token_hex | token_dec | decl |
|---|---|---|---|
| IsPaused | 0x06001C56 | 100670550 | public unsafe static bool IsPaused() |
| draweventdebug | 0x06001C57 | 100670551 | public unsafe static void draweventdebug() |
| EventDebugProcess1 | 0x06001C5B | 100670555 | public unsafe static Il2CppSystem.Object EventDebugProcess1(dds3ProcessID_t id) |
| EventDebugProcess2 | 0x06001C58 | 100670552 | public unsafe static Il2CppSystem.Object EventDebugProcess2(dds3ProcessID_t id) |
| EventDebugShutdown | 0x06001C5D | 100670557 | public unsafe static Il2CppSystem.Object EventDebugShutdown(dds3ProcessID_t id) |
| evtInitEventTest | 0x06001C5E | 100670558 | public unsafe static void evtInitEventTest() |
| evtExitEventTest | 0x06001C5F | 100670559 | public unsafe static void evtExitEventTest() |
| evtChkEndProcess | 0x06001C60 | 100670560 | public unsafe static bool evtChkEndProcess() |
| GetDbgEventName | 0x06001C61 | 100670561 | public unsafe static string GetDbgEventName(int eve_id) |

### Properties/flags exposed in the wrapper

- `Dic_EveName_E`
- `Dic_EveName_J`
- `DlcHead`
- `E646_Text_E`
- `E646_Text_J`
- `E646_Type`
- `E655_Text_E`
- `E655_Text_J`
- `E677_Text_E`
- `E677_Text_J`
- `E677_Type`
- `E681_Text_E`
- `E681_Text_J`
- `E681_Type`
- `E705_Ending_Text_E`
- `E705_Ending_Text_J`
- `E705_Ending_Type`
- `E_BossBtlAfter_Text_E`
- `E_BossBtlAfter_Text_J`
- `E_OldFuku_Text_E`
- `E_OldFuku_Text_J`
- `EventDebugProcess1_e`
- `EvtDante`
- `EvtNo1`
- `EvtNo2`
- `EvtNo3`
- `EvtRaidou`
- `IsDataCheck`
- `TagName`
- `Text_Nodata_E`
- `Text_Nodata_J`
- `VoiceName`
- `bStatus`
- `cam_name`
- `curbase`
- `curpoint`
- `debug_draw`
- `dpy`
- `event_filename`
- `event_mode`
- `fieldObj`
- `free_camera`
- `itfon`
- `str_freecamera`

Notes:
- Even without a visible debug menu, these static properties are a strong hint that the runtime has an event-debug mode (with its own process lifecycle: `EventDebugProcess*`, `EventDebugShutdown`).
- `GetDbgEventName(eve_id)` is useful for translating numeric ids into readable names during instrumentation.

