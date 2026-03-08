# 17 — Text, Localization, and the Message System (Localize + itfMesManager)

This part maps the **text pipeline** you can reach from decompiled `Assembly-CSharp`:

- **Asset-backed text** (ScriptableObjects / tables) via `EventEMsgData` + `MsgData`
- **Localized lookups** via `Localize`
- **Runtime message window system** via `itfMesManager` (plus `mesflow_tag` / `instanceMes_tag` and related tags)

> Scope note: this is focused on *how text is retrieved and displayed* (high ROI for dialogue edits, name overrides, debug overlays).  
> Deeper UI/menu graphs (inventory/shop etc) are a later part.

---

## 1) “Where does this string come from?” — fast mental model

### A) “General localization” (UI/system strings): `Localize`
**Type:** `Il2Cpp/Localize.cs`

Core API (token list in `data/part3_Localize_methods.csv`):
- `Localize.LoadDefault(int)` (token **100674513**)
- `Localize.LoadCommonData()` (**100674516**)
- `Localize.LoadLocalizeText(string, string)` (**100674520**)
- `Localize.GetLocalizeText(string)` (**100674526**)
- `Localize.GetLocalizeText(string, string)` (**100674527**)

Practical implication:
- If you need a **global string override** (menu labels, system prompts), `GetLocalizeText` is an obvious “single choke-point” candidate.

### B) “Event/cutscene dialog” data: `EventEMsgData` + `MsgData`
**Types:**
- `Il2Cpp/EventEMsgData.cs` (ScriptableObject)
- `Il2CppMsgDataTbl/MsgData.cs` (record)

Observed structure:
- `EventEMsgData._eventEMsgData : List<MsgData>`
- `MsgData.msgID : string`
- `MsgData.msgStr : string`
- `EventEMsgData.GetMsgText(string id)` (token **100674595**) does a lookup by `msgID`.

Practical implication:
- You can intercept at `EventEMsgData.GetMsgText` to override *event message IDs* without touching other localization keys.

### C) “Classic message window / selection window” (flowed text): `itfMesManager`
**Type:** `Il2Cpp/itfMesManager.cs`

This is a large API surface controlling:
- message window visibility, fading, selecting, speaker display
- variable substitution / “FRQ” string rendering
- message binary headers & page pointers (see Part 3 format doc)

This is also where “name tables” are exposed:
- `DAT_STR_REF_*NAME(int idx)` methods return **names** for key systems.

---

## 2) High-value seams (no guessing; grounded in names + types)

### Seam 1: “Give me a name string for an ID”
`itfMesManager` exposes public wrappers for the internal `DAT_STR_REF_*` lookups:

- `DAT_STR_REF_RCNAME(int idx)` — token **100669281**
- `DAT_STR_REF_DVNAME(int idx)` — token **100669282**
- `DAT_STR_REF_SKNAME(int idx)` — token **100669283**
- `DAT_STR_REF_ITNAME(int idx)` — token **100669284**
- `DAT_STR_REF_DHNAME(int idx)` — token **100669285**

(Exact signatures/tokens are in `data/part3_itfMesManager_methods.csv`.)

- These are *stable, high-level* lookups for **race**, **devil/demon**, **skill**, **item**, **demon “H”** name tables (naming based on method names).

### Seam 2: “Replace what the message window prints”
Candidate message-window-centric methods in `itfMesManager`:
- `SetMessage(GameObject, int, string)` — token **100669310**  
- `DrawMessage(_FRQ)` — token **100669404**
- `GetFRQString(_FRQ)` — token **100669287**

You can treat these as tiers:
- **Tier A (safest):** return-value string seams (`GetFRQString`, `GetSpeakerName`, `GetLocalizeText`)
- **Tier B (stateful UI):** setters like `SetMessage`, window activation, var tables

### Seam 3: “Speaker / voice coordination”
`itfMesManager` carries explicit fields for voice playback state:
- `bVoicePlaySw`, `bWaitVoiceStop`, `bNowSpeaking`, `bDanteEvent`
…and APIs:
- `CallEventVoice(instanceMes_tag)` — token **100669411**
- `IsSpeaking()` — token **100669413**
- `IsSpeakStart()` — token **100669414**
- `InitDanteEventMsg()` / `FinalDanteEventMsg()` — tokens **100669415/416**

If you ever want to:
- inject subtitles,
- gate message advance until a voice finishes,
- or debug “stuck” speech states,
this is the likely hub.

---

## 3) How the message system represents “what’s on screen”

The message system uses a few “tag” objects (see `data/part3_message_flow_types.csv` for fields):

- `mesflow_tag` (holds link header + instance pointer + memory handle)
- `instanceMes_tag` (a big state bag: speaker/message/select/window/variable/etc)
- `message_tag` (per-message state: page, maxpage, style state, pointer to `itfMesBinMesHeader_t`)
- `select_tag`, `window_tag`, `Speaker_tag`, `variable_tag`, `curctr_tag`

This is important because many `itfMesManager` methods accept `instanceMes_tag` and/or return pointers/arrays that relate to `itfMesBin*` headers.

---

## 4) Where to look next in this pack

- **Method catalogs:**
  - `data/part3_itfMesManager_methods.csv`
  - `data/part3_Localize_methods.csv`

- **Binary message format:**
  - `19_MessageBin_Format.md` (+ `data/part3_itfMesBin_headers.csv`)

- **Flow struct field list (for deep debugging later):**
  - `20_MessageFlow_Structs.md` (+ `data/part3_message_flow_types.csv`)

