# Menu Enums + Work Structs (quick reference)

This file collects the **menu state enums** and the highest-value work structs used by non-battle UI.

---

## Enums

Export: `data/part5_menu_enums.csv`

### `TRM_SEQ` (Terminal sequence)

| Value | Name |
|---:|---|
| `0` | `TRM_SEQ_RO` |
| `1` | `TRM_SEQ_TR` |
| `2` | `TRM_SEQ_SA` |
| `3` | `TRM_SEQ_TL` |
| `4` | `TRM_SEQ_EX` |
| `5` | `TRM_SEQ_CFM` |
| `6` | `TRM_SEQ_ACT` |
| `7` | `TRM_SEQ_SA_IN` |
| `8` | `TRM_SEQ_SA_OUT` |

### `TRM_EVT` (Terminal event)

| Value | Name |
|---:|---|
| `0` | `TRM_EVT_NONE` |
| `1` | `TRM_EVT_WAIT` |
| `2` | `TRM_EVT_START` |
| `3` | `TRM_EVT_BUSY` |
| `4` | `TRM_EVT_TALK_START` |
| `5` | `TRM_EVT_TALK` |
| `6` | `TRM_EVT_TERM` |

### `SHP_SEQ` (Shop sequence)

| Value | Name |
|---:|---|
| `0` | `SHP_SEQ_RO` |
| `1` | `SHP_SEQ_BU` |
| `2` | `SHP_SEQ_SE` |
| `3` | `SHP_SEQ_TL` |
| `4` | `SHP_SEQ_EX` |
| `5` | `SHP_SEQ_BN` |
| `6` | `SHP_SEQ_SN` |
| `7` | `SHP_SEQ_ERR` |
| `8` | `SHP_SEQ_CFM` |
| `9` | `SHP_SEQ_PAY` |
| `10` | `SHP_SEQ_SRV` |
| `11` | `SHP_SEQ_SRV_IT` |

### `CMB_SEQ` (Combine/Fusion sequence)

| Value | Name |
|---:|---|
| `0` | `CMB_SEQ_RO` |
| `1` | `CMB_SEQ_CM` |
| `2` | `CMB_SEQ_ZE` |
| `3` | `CMB_SEQ_TL` |
| `4` | `CMB_SEQ_EX` |
| `5` | `CMB_SEQ_1ST` |
| `6` | `CMB_SEQ_2ND` |
| `7` | `CMB_SEQ_SAC_CFM` |
| `8` | `CMB_SEQ_SAC` |
| `9` | `CMB_SEQ_STA` |
| `10` | `CMB_SEQ_STA2` |
| `11` | `CMB_SEQ_BID` |
| `12` | `CMB_SEQ_DCI` |
| `13` | `CMB_SEQ_ACC` |
| `14` | `CMB_SEQ_ACT_WAIT` |
| `15` | `CMB_SEQ_ACT` |
| `16` | `CMB_SEQ_INT` |
| `17` | `CMB_SEQ_ERR` |
| `18` | `CMB_SEQ_MSG` |
| `19` | `CMB_SEQ_END` |
| `20` | `CMB_SEQ_SELECT_SKILL` |
| `21` | `CMB_SEQ_CONF_SKILL` |
| `22` | `CMB_SEQ_MAX` |

*(CMB_SEQ is long; full listing is in `data/part5_menu_enums.csv`.)*


---

## Work structs

Exports:
- `data/part5_camp_struct_fields.csv`
- `data/part5_facility_struct_fields.csv`

Recommended starting points:
- Camp sequences: `cmpSeqInfo_s`
- Camp stock: `cmpDataStock_t`
- Terminal: `fclDataTerminal_t`
- Shop: `fclDataShop_t`
- Fusion: `cmbGlobalWork_t`, `cmbResultTable_t`

