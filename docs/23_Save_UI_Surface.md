# Save UI Surface (slot list + per-slot panel)

This section documents the **UI-side** classes that show save slots and read per-slot summary information.

---

## `saveUI` (slot list controller)

Fields (Unity object graph):

- `_announceList` : `Il2CppReferenceArray<TextMeshProUGUI>`
- `_announceObj` : `GameObject`
- `_choiceBaseObj` : `GameObject`
- `_choiceCursor` : `GameObject`
- `_choiceList` : `Il2CppReferenceArray<TextMeshProUGUI>`
- `_choiceObj` : `Il2CppReferenceArray<GameObject>`
- `_fileList` : `Il2CppReferenceArray<saveFileUI>`
- `_saveTop` : `GameObject`
- `_scrollRect` : `GameObject`
- `_scrollcontent` : `GameObject`
- `_titleMsg` : `TextMeshProUGUI`

Notes:
- This surface is primarily UI wiring: scroll rect, content, file list, choice cursor.
- This class is a good observation seam if we later want to log which slot indices are visible/selected.

---

## `saveFileUI` (single slot panel)

Fields (slot metadata + display elements):

- `_anime` : `Animator`
- `_brokenNum` : `CounterCtr`
- `_cursor` : `GameObject`
- `_difficulty` : `TextMeshProUGUI`
- `_endingBase` : `Animator`
- `_endingList` : `Il2CppReferenceArray<Animator>`
- `_endingOption` : `Il2CppReferenceArray<GameObject>`
- `_endinglDel` : `GameObject`
- `_fileNum` : `CounterCtr`
- `_lapNum` : `CounterCtr`
- `_lapObj` : `Il2CppReferenceArray<GameObject>`
- `_loopStartNon` : `Il2CppReferenceArray<GameObject>`
- `_lvObj` : `GameObject`
- `_noDataNum` : `CounterCtr`
- `_noDataText` : `TextMeshProUGUI`
- `_playMode` : `Animator`
- `_playTime` : `Il2CppReferenceArray<CounterCtr>`
- `_playerLV` : `CounterCtr`
- `_playerName` : `TextMeshProUGUI`
- `_terminalName` : `TextMeshProUGUI`

Interpretation hints (name-only):
- `_playerLV`, `_playTime`, `_playerName`, `_terminalName`, `_difficulty` align with `GameFileInfo_t` fields and `FsSaveData.loadHeaderGameFileInfo(...)`.
- `_endingOption` / `_endingList` suggest “lap/ending” history display (ties to `GameFileInfo_t.end_list` / `endcnt`).

---

## Slot summary loader seam

The likely direct producer of `saveFileUI` content is:

- `FsSaveData.loadHeaderGameFileInfo(slot, out GameFileInfo_t)` — token `100674366`

This gives a clean long-term direction:
- If/when we add logging later, one useful workflow is “slot panel builds -> call into `loadHeaderGameFileInfo` -> fill UI”.

