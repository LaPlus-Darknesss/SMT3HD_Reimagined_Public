# Part 2 — Event system map (evt*)

This part focuses on the **event/script runtime** and the systems it drives (stage loads, cutscenes, Kuti/mouth timelines, and debug toggles).
All signatures/tokens below come from the local IL2CPP wrapper sources under:

- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/`
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppevent_H/`

The raw catalogs for this part live in `data/`:
- `data/part2_evtCommand_catalog.csv` (full opcode catalog)
- `data/part2_evtLoadManager_members.csv`
- `data/part2_evtStage_members.csv`
- `data/part2_evtPolygonMovie_members.csv`
- `data/part2_evtKutiManager_members.csv`
- `data/part2_evtDebug_members.csv`
- `data/part2_pmd_structs_and_tables.csv` + `data/part2_pmd_enums.json`

---

## Mental model 

### 1) Script “opcodes” / command handlers: `evtCommand`
`evtCommand_*` functions are the **handlers** invoked by the event VM when the script hits a command.
They almost all return `int` (likely a status/continue code), and are grouped by prefix (MODEL, CALL, TEX, CAM, …).

High-value seam: **CALL\_*** (facility/battle/field transitions) and **RESTORE\_BATTLE**.

### 2) Event asset/data loading: `evtLoadManager`
Owns the **event number** being loaded, the **key/list** selection, a **Dictionary of loaded objects**, and the loaded **event message data** (`EventEMsgData`).
It also owns the “Kuti” (mouth timeline) object/script used by event scenes.

### 3) Stage/area orchestration: `evtStage`
Owns stage-wide operations like:
- loading areas (field/area),
- loading event data,
- starting an event by number,
- killing the current field script,
- path/movement helpers,
- default lighting/fog/ambient values.

### 4) Cutscenes: `evtPolygonMovie` + PMD tables (`Il2Cppevent_H` pmd* types)
PolygonMovie seems to be the **cutscene runner**.
It exposes:
- `evtStartPolygonMovie2(priority, eventno, cutno)` to start,
- `SetPMFileName(eventno, cutno, out pm1file, out pm2file)` to resolve files,
- `GetNowFrame()` / `GetNowCameraName()` for introspection,
- flag control (set/reset/check) via `evtSetPolygonMovieFlag`…,
- a big internal frame dispatch table of `*FrameFunc` handlers.

---

## Concrete “first hooks to observe” 

These are good “log only” seams we should add next (before any behavior changes):

### Event load + selection
- `evtLoadManager.Load(int eventno)` (token **0x06001D4D / 100670797**)
- `evtStage.evtStartEvent(int no)` (token **0x06001E7E / 100671102**)
- `evtStage.LoadEventData(string key, int e, bool ecmnf=false)` (token **0x06001E83 / 100671107**)

We are looking to learn about: event numbers, keys, data packs, and when the VM transitions between “loaded” and “running”.

### Cutscene start + identity
- `evtPolygonMovie.evtStartPolygonMovie2(int priority, int eventno, int cutno)` (**0x06001E16 / 100670998**)
- `evtPolygonMovie.SetPMFileName(int eventno, int cutno, out string pm1file, out string pm2file)` (**0x06001E15 / 100670997**)

We are looking to learn about: which cutscene is requested, and which PMD files it maps to.

### Kuti (mouth) timeline activation
- `evtLoadManager.StartFKuti(string key, string name, bool append=false)` (**0x06001D51 / 100670801**)
- `evtLoadManager.EndFKuti()` (**0x06001D52 / 100670802**)
- `evtKutiManager.Initialize(string key, string path, bool evt=true)` (**0x06001D0B / 100670731**)

We are looking to learn about: when event dialogue/mouth animation is live, and which timelines are used.

---

## Token formats (quick note)

Wrappers express method tokens in **hex** (`0x0600....`) and often resolve them in **decimal** (e.g. `100670797`).
Conversion is simply: `token_dec = int(token_hex, 16)`.

See `15_Token_Resolution_Cookbook.md` for the exact IL2CPP resolution pattern used by the wrappers.

