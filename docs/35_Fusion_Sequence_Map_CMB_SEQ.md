# 35 — Fusion sequence map (`CMB_SEQ`) and dispatchers (v6)

This tranche ties the `CMB_SEQ` enum to the visible calc/update entrypoints.

## 1) `CMB_SEQ` values and likely handlers

| value | CMB_SEQ | calc | update | notes |
| --- | --- | --- | --- | --- |
| 0 | CMB_SEQ_RO | cmbCalcRoot | cmbUpdateRoot | Root / Combine entry |
| 1 | CMB_SEQ_CM |  |  | Combine menu (likely dispatched inside Root/Sequence) |
| 2 | CMB_SEQ_ZE | cmbCalcZensyo | cmbUpdateZensyo | Zensyo menu (?) |
| 3 | CMB_SEQ_TL |  | cmbUpdateSeqTalk | Talk menu / dialogue selection |
| 4 | CMB_SEQ_EX |  |  | Extra / extension sequence (naming unclear) |
| 5 | CMB_SEQ_1ST | cmbCalcDevSel1st | cmbUpdateDevSel1st | Select 1st devil (fusion input) |
| 6 | CMB_SEQ_2ND | cmbCalcDevSel2nd | cmbUpdateDevSel2nd | Select 2nd devil (fusion input) |
| 7 | CMB_SEQ_SAC_CFM | cmbCalcDevSelSac | cmbUpdateSacConfirm | Confirm sacrifice selection (Ikenie) |
| 8 | CMB_SEQ_SAC | cmbCalcDevSelSac | cmbUpdateDevSelSac | Select sacrifice devil (Ikenie) |
| 9 | CMB_SEQ_STA | cmbCalcStatus | cmbUpdateStatus | Status view (pre-result) |
| 10 | CMB_SEQ_STA2 | cmbCalcStatus | cmbUpdateStatus | Status view (variant) |
| 11 | CMB_SEQ_BID | cmbCalcBirthDevil | cmbUpdateBirthDevil | Birth devil (result) |
| 12 | CMB_SEQ_DCI | cmbCalcBirthDevil2 | cmbUpdateDecideDevil | Decide/confirm birth devil |
| 13 | CMB_SEQ_ACC |  | cmbUpdateBirthDevil | Accident branch (likely uses same update w/ different flags) |
| 14 | CMB_SEQ_ACT_WAIT |  |  | Wait for birth action/animation |
| 15 | CMB_SEQ_ACT |  |  | Execute birth action/animation |
| 16 | CMB_SEQ_INT | cmbCalcIntroduce | cmbUpdateIntroduce | Introduction / cut-in |
| 17 | CMB_SEQ_ERR | cmbCalcError | cmbUpdateError | Error state |
| 18 | CMB_SEQ_MSG |  | cmbUpdateSeqMsg | Message state |
| 19 | CMB_SEQ_END |  |  | End / return |
| 20 | CMB_SEQ_SELECT_SKILL |  | cmbUpdateBirthDevilSelectSkill | Skill selection (inherit) |
| 21 | CMB_SEQ_CONF_SKILL |  | cmbUpdateBirthDevilConfSkill | Confirm inherited skills |
| 22 | CMB_SEQ_MAX |  |  | Sentinel / size |

Exports:
- `data/part6_CMB_SEQ_values.csv`

## 2) The two big dispatchers

These two are the “hub” seams that appear to dispatch by `CMB_SEQ`:

- `fclCombineCalc.cmbCalcSequence()`  
- `fclCombineUpdate.cmbUpdateSequence()`

If you ever need to:
- log state transitions
- detect “when the game enters skill selection”
- gate modifications to a specific part of the fusion flow

…these dispatchers are high-leverage places to observe.

## 3) Practical takeaway for modding (doc-only)

- Prefer *observation hooks* first on `cmbCalcSequence` / `cmbUpdateSequence` to build confidence in real state flow.
- Only after that, target leaf nodes (`cmbCalcBirthDevil`, `cmbUpdateBirthDevilSelectSkill`, etc.) for behavior changes.
