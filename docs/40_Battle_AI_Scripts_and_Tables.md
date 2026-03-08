# 40 — Battle AI: scripts, conditions, and tables

This doc maps the **AI selection surface** in battle:

- `datDevilAI*` tables (per-demon AI records)
- `nbAi` condition checks and target selection helpers
- `nbAiScript` command interpreter (script opcodes)

---

## datDevilAI: record access

Wrapper exposes a per-id getter:

| Native ptr name | Token |
|---|---|
| tbl_Private_Static_datDevilAI_t_Int32_0 | 100672627 |
| Get_Public_Static_datDevilAI_t_Int32_0 | 100672628 |

### datDevilAI record fields

`datDevilAI_s` + `datDevilAI_t2` expose these fields:

| Field | Field name |
|---|---|
| scriptid | scriptid |
| ailevel | ailevel |
| aichk | aichk |
| aitable | aitable |
| deadscriptid | deadscriptid |
| deadeventno | deadeventno |
| aitable1 | aitable1 |
| aitable2 | aitable2 |
| aitable3 | aitable3 |

Name-based notes:
- `scriptid` / `deadscriptid` imply “normal AI script” vs “on-death script.”
- `aitable*` suggests multiple AI tables/variants per record.

---

## nbAi: condition checks + selection

`nbAi` looks like the runtime helper set used by AI scripts.

### Core dispatcher seams

| Native ptr name | Token |
|---|---|
| nbAiChkJoken_Public_Static_Int32_byref_nbActionProcessData_t_UInt32_0 | 100670951 |
| AiChk_Public_Static_Int32_byref_nbActionProcessData_t_Int32_0 | 100670952 |
| GetKoudou_Public_Static_Int32_byref_nbActionProcessData_t_Int32_0 | 100670953 |
| SetKoudou_Public_Static_Void_byref_nbActionProcessData_t_Int32_Int32_0 | 100670954 |
| Sel_AI_Public_Static_Int32_byref_nbActionProcessData_t_Int32_Int32_0 | 100670955 |

### Condition helpers

Many `Chk_*` functions exist (HP/MP thresholds, status checks, counts, etc).
Rather than paste them all here, the full catalog is exported under:

- `data/part7_nbAi_methods.csv`

---

## nbAiScript: command interpreter (op surface)

`nbAiScript` exposes a very “opcode-like” family of `nbCommand_AI_*` methods.

### Action opcodes

| Native ptr name | Token |
|---|---|
| nbCommand_AI_ACT_ATTACK_Public_Static_Int32_0 | 100670979 |
| nbCommand_AI_ACT_ESCAPE_Public_Static_Int32_0 | 100670980 |
| nbCommand_AI_ACT_WAIT_Public_Static_Int32_0 | 100670981 |
| nbCommand_AI_ACT_SKILL_Public_Static_Int32_0 | 100670982 |
| nbCommand_AI_ACT_SUMMON_Public_Static_Int32_0 | 100670983 |

### Target selection opcodes

| Native ptr name | Token |
|---|---|
| nbCommand_AI_TAR_AI_Public_Static_Int32_0 | 100670985 |
| nbCommand_AI_TAR_LIFEMIN_Public_Static_Int32_0 | 100670986 |
| nbCommand_AI_TAR_BAARLV_Public_Static_Int32_0 | 100670987 |
| nbCommand_AI_TAR_LVMIN_Public_Static_Int32_0 | 100670988 |
| nbCommand_AI_TAR_BAD_Public_Static_Int32_0 | 100670989 |
| nbCommand_AI_TAR_ID_Public_Static_Int32_0 | 100670990 |
| nbCommand_AI_TAR_LIGHT_Public_Static_Int32_0 | 100670991 |
| nbCommand_AI_TAR_DARK_Public_Static_Int32_0 | 100670992 |
| nbCommand_AI_TAR_PLAYER_Public_Static_Int32_0 | 100670993 |
| nbCommand_AI_TAR_RND_Public_Static_Int32_0 | 100670994 |
| nbCommand_AI_TAR_JOKEN_Public_Static_Int32_0 | 100670995 |

### Condition opcodes (selection)

| Native ptr name | Token |
|---|---|
| nbCommand_AI_CHK_MYHP_Public_Static_Int32_0 | 100670997 |
| nbCommand_AI_CHK_MYMP_Public_Static_Int32_0 | 100670998 |
| nbCommand_AI_CHK_FRHP_Public_Static_Int32_0 | 100670999 |
| nbCommand_AI_CHK_DMGCNT_Public_Static_Int32_0 | 100671000 |
| nbCommand_AI_CHK_FRCNT_Public_Static_Int32_0 | 100671001 |
| nbCommand_AI_CHK_ENCNT_Public_Static_Int32_0 | 100671002 |
| nbCommand_AI_CHK_MYBAD_Public_Static_Int32_0 | 100671003 |
| nbCommand_AI_CHK_FRBAD_Public_Static_Int32_0 | 100671004 |
| nbCommand_AI_CHK_ENBAD_Public_Static_Int32_0 | 100671005 |
| nbCommand_AI_CHK_ENALLBAD_Public_Static_Int32_0 | 100671006 |
| nbCommand_AI_CHK_ENID_Public_Static_Int32_0 | 100671007 |
| nbCommand_AI_CHK_FRID_Public_Static_Int32_0 | 100671008 |
| nbCommand_AI_CHK_ENHOJO_Public_Static_Int32_0 | 100671009 |
| nbCommand_AI_CHK_FRHOJO_Public_Static_Int32_0 | 100671010 |
| nbCommand_AI_CHK_ENHOJOMAX_Public_Static_Int32_0 | 100671011 |

Full list is exported under:
- `data/part7_nbAiScript_commands.csv`

---

## Practical uses

If you want “bosses behave differently” without rewriting the world:

- `datDevilAI` is the **data surface** (which script/table to use)
- `nbAiScript` is the **control surface** (opcodes)
- `nbAi` is the **predicate/evaluator surface** (the checks the script can perform)

This is exactly the layer you’d later hook/log to understand how an encounter decides:
- when to heal
- when to buff/debuff
- when to target weak points or spam certain skills

