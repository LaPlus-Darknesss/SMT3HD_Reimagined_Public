# Part 2 — Insaniax (reference-only) surface map from a DLL string scan

This is intentionally **non-authoritative**: it is derived from scanning printable strings in `NocturneInsaniax.dll`,
so it tells us what names are *present*, not exactly what gets patched.

---

## Referenced IL2CPP types (by prefix)

### NB
- `Il2Cpp.nbActionProcess`
- `Il2Cpp.nbAi`
- `Il2Cpp.nbCalc`
- `Il2Cpp.nbCommSelProcess`
- `Il2Cpp.nbE901`
- `Il2Cpp.nbEncount`
- `Il2Cpp.nbEventProcess`
- `Il2Cpp.nbHelpProcess`
- `Il2Cpp.nbInit`
- `Il2Cpp.nbKoukaProcess`
- `Il2Cpp.nbMainProcess`
- `Il2Cpp.nbMakePacket`
- `Il2Cpp.nbMisc`
- `Il2Cpp.nbNegoProcess`
- `Il2Cpp.nbPanelProcess`
- `Il2Cpp.nbResultProcess`
- `Il2Cpp.nbSkillError`
- `Il2Cpp.nbTarSelProcess`
- `Il2Cpp.nbUnitProcess`

### DAT
- `Il2Cpp.datAisyoName`
- `Il2Cpp.datAnalyzeOff`
- `Il2Cpp.datCalc`
- `Il2Cpp.datHeartsHelp_msg`
- `Il2Cpp.datHeartsName`
- `Il2Cpp.datItemHelp_msg`
- `Il2Cpp.datItemName`
- `Il2Cpp.datRaceName`
- `Il2Cpp.datSkillHelp_msg`
- `Il2Cpp.datSkillName`

### FLD
- `Il2Cpp.fldFileResolver`
- `Il2Cpp.fldGlobal`
- `Il2Cpp.fldMain`
- `Il2Cpp.fld_Npc`

### ITF
- `Il2Cpp.itfMesManager`

### CMP
- `Il2Cpp.cmpCalc`
- `Il2Cpp.cmpDrawDH`
- `Il2Cpp.cmpDrawSkill`
- `Il2Cpp.cmpDrawStatus`
- `Il2Cpp.cmpInit`
- `Il2Cpp.cmpMisc`
- `Il2Cpp.cmpPanel`
- `Il2Cpp.cmpUpdate`

### SND
- `Il2Cpp.SndAssetBundleManager`

### SMG
- `Il2Cpp.Smg`

### OTHER
- `Il2Cpp.CounterCtr`
- `Il2Cpp.Localize`
- `Il2Cpp.SoundManager`
- `Il2Cpp.effModelTrackPoly`
- `Il2Cpp.fclCombineCalc`
- `Il2Cpp.fclCombineCalcCore`
- `Il2Cpp.fclCombineDraw`
- `Il2Cpp.fclCombineUpdate`
- `Il2Cpp.fclEncyc`
- `Il2Cpp.fclMisc`
- `Il2Cpp.fclRagDraw`
- `Il2Cpp.fclRagInit`
- `Il2Cpp.fclRagUpdate`
- `Il2Cpp.fclRecoverDraw`
- `Il2Cpp.fclShopCalc`
- `Il2Cpp.fclShopDraw`
- `Il2Cpp.frFont`
- `Il2Cpp.lstListWindow`
- `Il2Cpp.rstCalcCore`
- `Il2Cpp.rstcalc`
- `Il2Cpp.rstinit`
- `Il2Cpp.rstupdate`
- `Il2Cpp.scrParameterCommand`


Quick read:
- `NB*` looks battle/turn processing.
- `DAT*` looks data tables.
- `FLD*` looks field.
- `ITF*` is interface/UI (notably `itfMesManager`).
- `CMP*` appears to be UI panels/draw/update components.

---

## Patch-class name inventory (heuristic grouping)

These are class-like names found in the DLL that *look* like Harmony patch containers.

### Battle Math / Damage / Hit (18)
- `AntiDamagePatch`
- `CriticalPowPatch`
- `DefPowPatch`
- `GetHitTypePatch`
- `GetKoukaTypePatch`
- `GetKoukaTypePatch2`
- `HealEveryonePatch`
- `HealEveryonePatch2`
- `HealEveryonePatch3`
- `HealEveryonePatch5`
- `MagicAttackPatch`
- `MagicHitPowPatch`
- `MagicKaifukuPatch`
- `MaxHpAttackPatch`
- `NormalAtkPowPatch`
- `PatchMitamaPowerUp`
- `PoisonDamagePatch`
- `SakePowPatch`

### Press Turns / Turn System (17)
- `ActionProcessDataPatch`
- `BeastEyePatch1`
- `BeastEyePatch2`
- `CheckAction_COMMPatch`
- `CheckAction_NONPatch`
- `FocusPatch`
- `FocusPatch2`
- `FocusPatch3`
- `FocusPatch4`
- `InitBattlePatch`
- `PartyExpPatch`
- `PartyExpPatch2`
- `PressMaePhasePatch`
- `PressTurnPatch2`
- `PressTurnPatch22`
- `PressTurnPatch3`
- `PressTurnPatch4`

### Buffs / Debuffs (14)
- `DDS2BuffLimitPatch`
- `DDS2BuffMaxPatch`
- `DDS2BuffMinPatch`
- `DDS2BuffRatioPatch`
- `DisplayBuffsPatch`
- `DisplayBuffsPatch2`
- `GetNakamaMaxPatch`
- `MaxHpAttackPatch`
- `PatchChkParamLimitAll`
- `PatchGetBaseMaxHP`
- `PatchGetBaseMaxMP`
- `PatchGetMaxHP`
- `PatchGetMaxMP`
- `PatchSetMaxHpMp`

### UI / Text / Localization (24)
- `BattleMessagePatch`
- `DispComm1CommColourPatch`
- `DispComm1ItemColourPatch`
- `DispComm1TalkColourPatch`
- `DispInnateAttackPassiveNamePatch`
- `DispSkillNameColourPatch`
- `DispTextPatch`
- `DisplayBuffsPatch`
- `DisplayBuffsPatch2`
- `HeartsNamePatch`
- `ItemNamePatch`
- `LocalizeNamesPatch`
- `MagatamaGetMessagePatch`
- `PatchCompendiumConfirmText`
- `PatchGetParamName`
- `RaceNamePatch`
- `ReadytoMessagePatch`
- `ShopMessagePatch`
- `SkillNamesPatch`
- `cmpItemTextColourPatch`

### Shop / Facilities (15)
- `CompendiumProfilePatch`
- `ItemBoxAddPatch`
- `ItemBoxOpenPatch`
- `PatchCombineCalcSequence`
- `PatchCompendiumConfirmText`
- `PatchCompendiumPrice`
- `PatchGetCompendiumDemonParam`
- `PatchRecoverReviveDraw`
- `RagStockPatch`
- `ShopItemsPatch`
- `ShopMessagePatch`
- `ShopPricesPatch`
- `ragChkItemErrPatch`
- `ragDrawDevilWindowColourPatch`
- `ragDrawItemColourPatch`

### Negotiation (3)
- `CheckMukanNegoPatch`
- `CheckSenseiNegoPatch`
- `CheckSenseiPatch`

### Encounters / Field (7)
- `CheckBackAttackPatch`
- `CheckEscapePatch`
- `NpcAddPatch`
- `fldFirstInitPatch`
- `fldLoadFilePatch`
- `nbCheckRenzokuEncountPatch`
- `nbSetRenzokuEncountPatch`

### Skills / Innates (50)
- `AnalyzeSkillColourPatch`
- `DispInnateAttackPassiveNamePatch`
- `DispSkillNameColourPatch`
- `InfiniteMagatamaSkillsPatch`
- `InfiniteMagatamaSkillsPatch2`
- `InfiniteMagatamaSkillsPatch3`
- `InfiniteMagatamaSkillsPatch4`
- `InnateSkillPatch1`
- `InnateSkillPatch2`
- `InnateSkillPatch3`
- `InnateSkillPatch4`
- `InnateSkillPatch5`
- `InnateSkillPatch6`
- `MagatamaGetMessagePatch`
- `MitamaFusionSkillOverwritePatch`
- `OutOfBattleSkillPatch`
- `PatchDrawMagatamaInfo`
- `PatchMagatamaAddPoint`
- `PatchMitamaPowerUp`
- `PatchStandbyMagatamaEvent`


---

