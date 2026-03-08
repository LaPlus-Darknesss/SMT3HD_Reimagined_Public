# Battle / Skill Math Core — `Il2Cpp.datCalc`

Wrapper: `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/datCalc.cs`

`datCalc` is a dense, high-value gameplay class: it exposes skill execution, cost calculation, resist lookups, party damage helpers, and multiple stat/HP/MP helpers.

## Notable static field

- `datBossPress` (static field)
  - Resolved via `IL2CPP.GetIl2CppField(..., "datBossPress")`.
  - Likely participates in boss press-turn logic (paired with `datInitBossPress`).

## Curated method list (signature + token)

| Method | Signature | Token |
|---|---|---:|
| `datExecSkill` | `int datExecSkill(int nskill, datUnitWork_t s, datUnitWork_t d)` | 100672558 |
| `datGetSkillCost` | `int datGetSkillCost(datUnitWork_t w, int nskill)` | 100672612 |
| `datInitBossPress` | `void datInitBossPress()` | 100672614 |
| `datGetSkillKouka` | `int datGetSkillKouka(int nskill, int type, datUnitWork_t s, datUnitWork_t d)` | 100672556 |
| `datGetSkillBadKouka` | `int datGetSkillBadKouka(int nskill, datUnitWork_t s, datUnitWork_t d)` | 100672557 |
| `datGetAisyo` | `uint datGetAisyo(datUnitWork_t work, int attr)` | 100672569 |
| `datGetAisyoFlag` | `int datGetAisyoFlag(datUnitWork_t work, int attr)` | 100672570 |
| `GetAisyoRitu` | `float GetAisyoRitu(int nskill, datUnitWork_t s, datUnitWork_t d)` | 100672555 |
| `GetButuriAttack` | `int GetButuriAttack(int nskill, datUnitWork_t s, datUnitWork_t d, int waza)` | 100672550 |
| `GetMagicAttack` | `int GetMagicAttack(int nskill, datUnitWork_t s, datUnitWork_t d, int waza)` | 100672552 |
| `datGetParam` | `int datGetParam(datUnitWork_t work, int paratype)` | 100672567 |
| `datGetBaseParam` | `int datGetBaseParam(datUnitWork_t work, int paratype)` | 100672566 |
| `datGetPlayerParam` | `int datGetPlayerParam(int id)` | 100672584 |
| `datAddPlayerParam` | `void datAddPlayerParam(int id, int add)` | 100672585 |
| `datGetNakamaParam` | `int datGetNakamaParam(int did, int id)` | 100672586 |
| `datSetBadStat` | `void datSetBadStat(datUnitWork_t work, uint badstat)` | 100672560 |
| `datResetBadStat` | `void datResetBadStat(datUnitWork_t work, uint badstat)` | 100672561 |
| `datOverwriteBadStat` | `void datOverwriteBadStat(datUnitWork_t work, uint badstat)` | 100672559 |
| `datGetBadStatusAttr` | `int datGetBadStatusAttr(int bad)` | 100672571 |
| `datPartyDamage` | `void datPartyDamage(int mode)` | 100672589 |
| `datPartyDamageFld` | `void datPartyDamageFld(int mode)` | 100672590 |
| `datPartyDamagePipe` | `void datPartyDamagePipe(int big, int sml)` | 100672591 |
| `datGetMaxHp` | `uint datGetMaxHp(datUnitWork_t work)` | 100672562 |
| `datGetMaxMp` | `uint datGetMaxMp(datUnitWork_t work)` | 100672563 |
| `datAddHp` | `uint datAddHp(datUnitWork_t work, int hp)` | 100672564 |
| `datAddMp` | `uint datAddMp(datUnitWork_t work, int mp)` | 100672565 |
| `datGetBaseMaxHp` | `int datGetBaseMaxHp(datUnitWork_t work)` | 100672544 |
| `datGetBaseMaxMp` | `int datGetBaseMaxMp(datUnitWork_t work)` | 100672545 |
| `datGetMagicHitPow` | `int datGetMagicHitPow(datUnitWork_t work)` | 100672547 |
| `datGetDefPow` | `int datGetDefPow(datUnitWork_t work)` | 100672548 |
| `GetMaxHpWazaPoint` | `int GetMaxHpWazaPoint(int nskill, datUnitWork_t s, datUnitWork_t d, int waza)` | 100672554 |

### Suggested “first detour” order (low risk → high impact)
1) `datGetSkillCost(...)` (observe skill IDs, cost types)
2) `datGetSkillKouka(...)` / `datGetSkillBadKouka(...)` (observe effect program IDs)
3) `datExecSkill(...)` (execution seam; best for full trace + controlled balance tweaks)

### Minimal logging payload that pays off
- `nskill` as integer
- attacker/defender: `id`, `level`, `hp/maxhp`, `mp/maxmp`, `badstatus`
- plus a few entries from `param` 

