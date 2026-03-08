# Part 2 — evtCommand opcode catalog (script command handlers)

`evtCommand` is the highest-yield file for “what can the event VM do”.
Every `evtCommand_*` method is an opcode handler; the prefix after `evtCommand_` is a good first-pass category.

Full catalog:
- `data/part2_evtCommand_catalog.csv`

---

## Prefix groups (count)

| Group | Count |
|---|---|
| MODEL | 35 |
| CALL | 15 |
| EFFECT | 14 |
| TEX | 13 |
| CAM | 9 |
| misc | 7 |
| FIELD | 6 |
| PATH | 6 |
| EFFBED | 5 |
| GET | 5 |
| POINT | 5 |
| EFFMG2 | 4 |
| PMV | 4 |
| BGM | 4 |
| EFFMG1 | 3 |
| SET | 3 |
| UNIT | 2 |
| OBJ | 2 |
| MOON | 2 |
| LOAD | 2 |
| LIGHT | 2 |
| BOKASI | 2 |
| SE | 2 |
| DIST | 2 |
| FRAMEMODE | 2 |
| KUTISET | 1 |
| KUTI | 1 |
| SEARCH | 1 |
| RTRN | 1 |
| EXIT | 1 |
| RESTORE | 1 |
| CHG | 1 |
| READ | 1 |
| CHK | 1 |
| DEL | 1 |
| FADEOUT | 1 |
| DRAWSTOP | 1 |
| UNLOCK | 1 |
| MESSAGE | 1 |
| LANGUAGE | 1 |

---

## Facility / battle / flow transitions (CALL)

These are particularly valuable because they likely represent **hard mode switches** (field→battle, shop, save, puzzleboy, etc.).

| name | token_hex | token_dec | decl |
|---|---|---|---|
| evtCommand_CALL_STAFF | 0x06001BE7 | 100670439 | public unsafe static int evtCommand_CALL_STAFF() |
| evtCommand_CALL_ENDSAVE | 0x06001BE8 | 100670440 | public unsafe static int evtCommand_CALL_ENDSAVE() |
| evtCommand_CALL_COMBINE | 0x06001BE9 | 100670441 | public unsafe static int evtCommand_CALL_COMBINE() |
| evtCommand_CALL_RECOVER | 0x06001BEA | 100670442 | public unsafe static int evtCommand_CALL_RECOVER() |
| evtCommand_CALL_SHOP | 0x06001BEB | 100670443 | public unsafe static int evtCommand_CALL_SHOP() |
| evtCommand_CALL_RAG | 0x06001BEC | 100670444 | public unsafe static int evtCommand_CALL_RAG() |
| evtCommand_CALL_SAVE | 0x06001BED | 100670445 | public unsafe static int evtCommand_CALL_SAVE() |
| evtCommand_CALL_PUZZLEBOY | 0x06001BEE | 100670446 | public unsafe static int evtCommand_CALL_PUZZLEBOY() |
| evtCommand_CALL_NEXT | 0x06001BEF | 100670447 | public unsafe static int evtCommand_CALL_NEXT() |
| evtCommand_CALL_EVENT | 0x06001BF0 | 100670448 | public unsafe static int evtCommand_CALL_EVENT() |
| evtCommand_CALL_BATTLE | 0x06001BF3 | 100670451 | public unsafe static int evtCommand_CALL_BATTLE() |
| evtCommand_CALL_BATTLE2 | 0x06001BF5 | 100670453 | public unsafe static int evtCommand_CALL_BATTLE2() |
| evtCommand_CALL_FIELD | 0x06001BF7 | 100670455 | public unsafe static int evtCommand_CALL_FIELD() |
| evtCommand_CALL_FIELD2 | 0x06001BF8 | 100670456 | public unsafe static int evtCommand_CALL_FIELD2() |
| evtCommand_CALL_FIELD3 | 0x06001BF9 | 100670457 | public unsafe static int evtCommand_CALL_FIELD3() |

Related helpers (not `evtCommand_` prefixed, but still in the same file):

| name | token_hex | token_dec | decl |
|---|---|---|---|
| CallBattleSub | 0x06001BF2 | 100670450 | public unsafe static void CallBattleSub(int encno, int eventno) |
| evtCommand_CALL_BATTLE | 0x06001BF3 | 100670451 | public unsafe static int evtCommand_CALL_BATTLE() |
| CallBattleSub2 | 0x06001BF4 | 100670452 | public unsafe static void CallBattleSub2(int encno) |
| evtCommand_CALL_BATTLE2 | 0x06001BF5 | 100670453 | public unsafe static int evtCommand_CALL_BATTLE2() |
| evtCommand_RESTORE_BATTLE | 0x06001BF6 | 100670454 | public unsafe static int evtCommand_RESTORE_BATTLE() |

---

## Camera-related commands (CAM)

| name | token_hex | token_dec | decl |
|---|---|---|---|
| evtCommand_CAM_SEL | 0x06001C02 | 100670466 | public unsafe static int evtCommand_CAM_SEL() |
| evtCommand_CAM_SEL_FOVY | 0x06001C03 | 100670467 | public unsafe static int evtCommand_CAM_SEL_FOVY() |
| evtCommand_CAM_PATH_MOVE | 0x06001C04 | 100670468 | public unsafe static int evtCommand_CAM_PATH_MOVE() |
| evtCommand_CAM_PATH_WAIT | 0x06001C05 | 100670469 | public unsafe static int evtCommand_CAM_PATH_WAIT() |
| evtCommand_CAM_CREATE | 0x06001C06 | 100670470 | public unsafe static int evtCommand_CAM_CREATE() |
| evtCommand_CAM_CLS | 0x06001C07 | 100670471 | public unsafe static int evtCommand_CAM_CLS() |
| evtCommand_CAM_POS_XYZ | 0x06001C08 | 100670472 | public unsafe static int evtCommand_CAM_POS_XYZ() |
| evtCommand_CAM_ROT_XYZ | 0x06001C09 | 100670473 | public unsafe static int evtCommand_CAM_ROT_XYZ() |
| evtCommand_CAM_ROT_Q | 0x06001C0A | 100670474 | public unsafe static int evtCommand_CAM_ROT_Q() |

---

## Texture/UI-ish commands (TEX)

| name | token_hex | token_dec | decl |
|---|---|---|---|
| evtCommand_TEX_BE | 0x06001C22 | 100670498 | public unsafe static int evtCommand_TEX_BE() |
| evtCommand_TEX_LOAD | 0x06001C23 | 100670499 | public unsafe static int evtCommand_TEX_LOAD() |
| evtCommand_TEX_ON | 0x06001C24 | 100670500 | public unsafe static int evtCommand_TEX_ON() |
| evtCommand_TEX_OFF | 0x06001C25 | 100670501 | public unsafe static int evtCommand_TEX_OFF() |
| evtCommand_TEX_CLS | 0x06001C26 | 100670502 | public unsafe static int evtCommand_TEX_CLS() |
| evtCommand_TEX_SWAP | 0x06001C27 | 100670503 | public unsafe static int evtCommand_TEX_SWAP() |
| evtCommand_TEX_OT | 0x06001C28 | 100670504 | public unsafe static int evtCommand_TEX_OT() |
| evtCommand_TEX_PRI | 0x06001C29 | 100670505 | public unsafe static int evtCommand_TEX_PRI() |
| evtCommand_TEX_SIZE | 0x06001C2A | 100670506 | public unsafe static int evtCommand_TEX_SIZE() |
| evtCommand_TEX_POS | 0x06001C2B | 100670507 | public unsafe static int evtCommand_TEX_POS() |
| evtCommand_TEX_ALP | 0x06001C2C | 100670508 | public unsafe static int evtCommand_TEX_ALP() |
| evtCommand_TEX_ZOOM | 0x06001C2D | 100670509 | public unsafe static int evtCommand_TEX_ZOOM() |
| evtCommand_TEX_STAT | 0x06001C2E | 100670510 | public unsafe static int evtCommand_TEX_STAT() |

---

## Music (BGM)

| name | token_hex | token_dec | decl |
|---|---|---|---|
| evtCommand_BGM_PLAY_E | 0x06001C34 | 100670516 | public unsafe static int evtCommand_BGM_PLAY_E() |
| evtCommand_BGM_TRANS_E | 0x06001C35 | 100670517 | public unsafe static int evtCommand_BGM_TRANS_E() |
| evtCommand_BGM_VOL_DOWN | 0x06001C37 | 100670519 | public unsafe static int evtCommand_BGM_VOL_DOWN() |
| evtCommand_BGM_VOL_UP | 0x06001C38 | 100670520 | public unsafe static int evtCommand_BGM_VOL_UP() |

---

## How to use the CSV effectively

Recommended workflow when you want a specific behavior:

1) Filter by `group` and a keyword in `name` (e.g. `CALL`, `BGM`, `PMV`, `MSG`, `CAM`, `MAP`, `MODEL`).
2) Take the `token_dec` and resolve a method pointer with the standard IL2CPP token pattern (see `15_Token_Resolution_Cookbook.md`).
3) Instrument first (log args/returns). Only after you’ve seen it in action should you attempt to override.

