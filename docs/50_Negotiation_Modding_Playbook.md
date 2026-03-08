# 50 — Negotiation Modding Playbook (doc-only) — v9 (2026-02-08)

This doc summarizes *where the leverage is* for negotiation mods **without** requiring binary table format RE up-front.

It’s intentionally “observe-first”: prioritize **hooking/overriding consumers** over editing packed blobs.

## The safest high-ROI seams (by wrapper token)

Source: `Il2Cpp/nbNegoProcess.cs`

| Seam | Token | Why it matters |
|---|---|---|
| nbClsNegoMessage(ref nbNegoProcessData_t n) -> void | 100672230 |  |
| nbDispNegoMessage(ref nbNegoProcessData_t n, int msgid, int msgno, int sel) -> void | 100672231 |  |
| nbCheckNegoMessage(ref nbNegoProcessData_t n) -> int | 100672232 |  |
| nbSetNegoMesVar(int msgid, ref datUnitWork_t p, ref datUnitWork_t e) -> void | 100672233 |  |
| nbGetNegoRequestType(int reqtype) -> int | 100672246 | request selection / message binding / process stage |
| nbGetNegoTuikaRequestType(int tuikareqtype) -> int | 100672247 | request selection / message binding / process stage |
| nbGetQuestionType(ref datUnitWork_t w) -> int | 100672251 | request selection / message binding / process stage |
| nbGetInfoMsg(int infotype) -> int | 100672255 | request selection / message binding / process stage |
| nbGetInnenNegoID(ref nbNegoProcessData_t n) -> int | 100672259 | request selection / message binding / process stage |
| nbNegoProcessMessageChk(dds3ProcessID_t id) -> Il2CppSystem.Object | 100672263 |  |
| nbNegoProcessMessage(dds3ProcessID_t id) -> Il2CppSystem.Object | 100672264 |  |
| InitNegoProcessData(ref nbActionProcessData_t actdata, int pformindex, int eformindex, int negotype, uint skillid) -> nbNegoProcessData_t | 100672267 |  |
| nbInitNegoProcess(ref nbActionProcessData_t actdata, int pformindex, int eformindex, int negotype, int skillid) -> void | 100672268 |  |


## Recommended strategy (long-term maintainable)

### 1) Override “request selection” at the helper level
If you want to change:
- what kind of demand happens (Makka vs item vs HP/MP vs stone)
- how big the numbers are
- how item candidates are chosen

Prefer targeting:
- `nbGetNegoRequestType`
- `nbGetNegoTuikaRequestType`
- `nbGetNegoKure*` / `nbGetNegoYaru*`
- `nbGetKureItemA/B` + `nbSelectKureItemA/B`

Reason: the upstream sources include **packed blob tables** (`datNegoReqTable`, `datNegoReqItemListA/B`, etc.). Helper overrides let you change behavior *immediately* without having to decode those formats.

### 2) Treat message ids as a “render contract”
Negotiation staging uses `nbNegoProcessData_t` fields like:
- `questionmsg`, `flowmsg`, `helpmsg`, `innenmsg`, etc.

If you want to:
- swap lines, suppress certain lines, inject new placeholders
- alter how request amounts are inserted into text

Prefer targeting:
- `nbSetNegoMesVar`
- `nbSetMesMakka`, `nbSetMesItem`
- `nbDispNegoMessage` / `nbCheckNegoMessage` / `nbClsNegoMessage`

### 3) Gate deeper edits behind confirmed table formats
Typed tables are already friendly:
- `datDevilNegoFormat` (typed)
- `datNegoSkill` (typed)
- `datInnenNegoTable` (typed)
- `datInfoSelectTable` (typed)

Packed blob surfaces should be treated as “future RE work” unless:
- you have a very specific knob that *must* be data-driven
- you can confirm a layout via runtime inspection / dumps later

## Concrete “next RE targets” (when we decide to decode blobs)
If/when we want record layouts, prioritize these blobs first (highest behavioral impact):
- `datNegoReqTable`
- `datNegoReqItemListA/B`
- `datNegoTuikaReqtype`
- `datNegoPrivateQues`
- `datNegoFightTaido`
- `datNegoMukanTypeList`

The typed table `datNegoNormalQuesInfo_s {type, ritu1, ritu2}` can be used as a “ground truth anchor” while decoding question selection logic.
