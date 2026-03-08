# Part 2 — evtLoadManager + evtStage (how events pick & load content)

This part is meant to answer two practical questions:

1) **What event number is being loaded / started right now?**
2) **Which assets/data objects/messages does the event VM pull in to run it?**

Raw catalogs:
- `data/part2_evtLoadManager_members.csv`
- `data/part2_evtStage_members.csv`

---

## evtLoadManager: key state fields (static)

| name | token_hex | token_dec | decl |
|---|---|---|---|
| eventno | 0x06001D5A | 100670810 | public unsafe static int eventno |
| akey | 0x06001D5C | 100670812 | public unsafe static string akey |
| keys | 0x06001D5E | 100670814 | public unsafe static List<string> keys |
| lists | 0x06001D60 | 100670816 | public unsafe static List<string> lists |
| objects | 0x06001D62 | 100670818 | public unsafe static Dictionary<string, evtLoadManager.eLoadData> objects |
| rep_tbl | 0x06001D64 | 100670820 | public unsafe static Dictionary<string, string> rep_tbl |
| event_msg | 0x06001D66 | 100670822 | public unsafe static EventEMsgData event_msg |
| kuti_obj | 0x06001D68 | 100670824 | public unsafe static GameObject kuti_obj |
| kuti_scr | 0x06001D6A | 100670826 | public unsafe static evtKutiManager kuti_scr |
| step_cnt | 0x06001D6C | 100670828 | public unsafe static int step_cnt |
| load_index | 0x06001D6E | 100670830 | public unsafe static int load_index |
| inst_cash_dic | 0x06001D70 | 100670832 | public unsafe static Dictionary<string, GameObject> inst_cash_dic |

Interpretation (based on names):
- `eventno` is the current event id being loaded/active.
- `keys` / `lists` are likely the script’s requested asset “buckets”.
- `objects` is a Dictionary mapping names → loaded objects (prefabs, textures, etc).
- `rep_tbl` suggests string replacement / remap table (key → value).
- `event_msg` is the loaded message data for the current event.
- `kuti_obj` / `kuti_scr` are the mouth-timeline object and its controller.

---

## evtLoadManager: high-signal methods

| name | token_hex | token_dec | decl |
|---|---|---|---|
| InitializeLoadManager | 0x06001D47 | 100670791 | public unsafe static void InitializeLoadManager() |
| StartLoad | 0x06001D48 | 100670792 | public unsafe static void StartLoad() |
| Load | 0x06001D4D | 100670797 | public unsafe static int Load(int eventno) |
| load_init | 0x06001D4F | 100670799 | public unsafe static void load_init() |
| load_wait | 0x06001D50 | 100670800 | public unsafe static void load_wait() |
| GetData | 0x06001D4A | 100670794 | public unsafe static Il2CppSystem.Object GetData(string name) |
| Contains | 0x06001D4C | 100670796 | public unsafe static bool Contains(string name) |
| GetKutiManager | 0x06001D4B | 100670795 | public unsafe static evtKutiManager GetKutiManager() |
| StartFKuti | 0x06001D51 | 100670801 | public unsafe static void StartFKuti(string key, string name, bool append = false) |
| EndFKuti | 0x06001D52 | 100670802 | public unsafe static void EndFKuti() |
| SetEvtInstCash | 0x06001D53 | 100670803 | public unsafe static void SetEvtInstCash(GameObject prefab, int type) |
| GetEvtInstCash | 0x06001D54 | 100670804 | public unsafe static GameObject GetEvtInstCash(GameObject obj) |
| GetTarminalEventNo | 0x06001D58 | 100670808 | public unsafe static void GetTarminalEventNo(ref string _key, ref string _list) |

What these likely represent:
- `InitializeLoadManager` / `StartLoad` set up an event load session.
- `Load(eventno)` is the “do the load” entry point.
- `GetData(name)` and `Contains(name)` let scripts query what’s loaded.
- `StartFKuti` / `EndFKuti` bracket dialogue/mouth work during an event.
- `SetEvtInstCash` / `GetEvtInstCash` look like a prefab/instance cache.

---

## evtStage: key state fields (static)

| name | token_hex | token_dec | decl |
|---|---|---|---|
| UnitInitPosRot | 0x06001E8F | 100671119 | public unsafe static Vector4 UnitInitPosRot |
| fld_default_light | 0x06001E91 | 100671121 | public unsafe static Il2CppReferenceArray<sdf3DLight_t> fld_default_light |
| fld_default_fog | 0x06001E93 | 100671123 | public unsafe static sdf3DFogParam_t fld_default_fog |
| fld_default_ambient | 0x06001E95 | 100671125 | public unsafe static Vector4 fld_default_ambient |
| fld_light_dst | 0x06001E97 | 100671127 | public unsafe static Il2CppReferenceArray<sdf3DLight_t> fld_light_dst |
| fld_fog_dst | 0x06001E99 | 100671129 | public unsafe static sdf3DFogParam_t fld_fog_dst |
| fld_ambient_dst | 0x06001E9B | 100671131 | public unsafe static Vector4 fld_ambient_dst |
| evtStartEvent_path | 0x06001E9D | 100671133 | public unsafe static string evtStartEvent_path |
| evtStartEvent_no | 0x06001E9F | 100671135 | public unsafe static int evtStartEvent_no |
| process | 0x06001EA1 | 100671137 | public unsafe static int process |
| m_head | 0x06001EA3 | 100671139 | public unsafe static StringBuilder m_head |
| m_akey | 0x06001EA5 | 100671141 | public unsafe static StringBuilder m_akey |
| m_aname | 0x06001EA7 | 100671143 | public unsafe static StringBuilder m_aname |
| m_tkey | 0x06001EA9 | 100671145 | public unsafe static StringBuilder m_tkey |
| m_tname | 0x06001EAB | 100671147 | public unsafe static StringBuilder m_tname |

Notes:
- There are explicit “default” lighting/fog/ambient parameters and “dst” (destination) parameters, strongly suggesting the stage system interpolates environment settings during events.
- `evtStartEvent_*` fields imply the stage system stores the current event start request (path/no).
- `process` and `m_*` StringBuilders look like debug/log formatting buffers.

---

## evtStage: high-signal methods

| name | token_hex | token_dec | decl |
|---|---|---|---|
| evtStartEvent | 0x06001E7E | 100671102 | public unsafe static void evtStartEvent(int no) |
| LoadEventData | 0x06001E83 | 100671107 | public unsafe static void LoadEventData(string key, int e, bool ecmnf = false) |
| evtLoadArea | 0x06001E7C | 100671100 | public unsafe static int evtLoadArea(int fld, int area) |
| evtLoadArea_FromMemory | 0x06001E7D | 100671101 | public unsafe static int evtLoadArea_FromMemory(int fld, int area, Il2CppStructArray<byte> pF1, Il2CppStructArray<byte> pF2, Il2CppStructArray<byte> pTB, Il2CppStructArray<byte> pBF) |
| evtKillFieldScript | 0x06001E84 | 100671108 | public unsafe static void evtKillFieldScript() |
| evtSetRegistUnit | 0x06001E77 | 100671095 | public unsafe static int evtSetRegistUnit(int major, int minor) |
| evtLoadUnit | 0x06001E78 | 100671096 | public unsafe static int evtLoadUnit(int major, int minor) |
| evtFreeStage | 0x06001E7A | 100671098 | public unsafe static void evtFreeStage() |
| evtFreeAllUnit | 0x06001E7B | 100671099 | public unsafe static void evtFreeAllUnit() |
| evtSetTestCamera | 0x06001E76 | 100671094 | public unsafe static void evtSetTestCamera() |
| evtPathStart | 0x06001E86 | 100671110 | public unsafe static void evtPathStart(dds3Basic_t basic) |
| evtPathStop | 0x06001E85 | 100671109 | public unsafe static void evtPathStop(dds3Basic_t basic) |
| evtEffectLoopControl | 0x06001E8C | 100671116 | public unsafe static void evtEffectLoopControl(dds3Basic_t eff, int sw) |
| evtStageAnimeControl | 0x06001E8D | 100671117 | public unsafe static int evtStageAnimeControl(int type, int mode) |

Suggested reading order when tracing “field → event → scene”:
1) `evtLoadArea` / `evtLoadArea_FromMemory`
2) `LoadEventData`
3) `evtStartEvent`
4) `evtKillFieldScript` (used when switching scripts/states)


