# 20 — Message flow structs (mesflow_tag / instanceMes_tag / message_tag …)

This doc is a “field map” for the core message-flow objects.

Source of truth:
- We extracted their public properties into `data/part3_message_flow_types.csv`.

---

## 1) Core objects and their role

### `mesflow_tag`
Fields:
- `linkheader : itfmisclinkheader_t`
- `pinst : instanceMes_tag`
- `memhandle : sdfMemHandle_t`

Why it matters:
- looks like the root “linked-list node” for an active message-flow instance (based on naming + presence of `linkheader`).

### `instanceMes_tag`
This is the big state bag for a single message session.

Notable fields:
- `speaker : Speaker_tag`
- `message : message_tag`
- `select : select_tag`
- `window : window_tag`
- `variable : variable_tag`
- `curctr : curctr_tag`
- plus a set of scalar state fields: `stat`, `z`, `otindex`, `type`, etc.

### `message_tag`
Fields include:
- `pbinheader : itfMesBinMesHeader_t` (links directly to the message BIN headers)
- `page`, `maxpage`, `phase`, `wait`
- last-style fields: `last_style`, `last_col`, `last_tp`, `last_spd`
- layout-ish: `pos : sdfIVector2_t`, `lines : short`, `callmode : int`

### `select_tag`, `window_tag`, `Speaker_tag`, `variable_tag`, `curctr_tag`
These are the component objects hung off `instanceMes_tag`.

---

## 2) How to use this as a research tool

When you encounter an `itfMesManager` method that accepts `instanceMes_tag`:

1) Open this doc (or the CSV) and identify which sub-object is likely relevant.
2) Jump to the corresponding wrapper (e.g. `message_tag.cs`) and inspect any additional internal method pointers.
3) Record tokens for any method that:
   - returns a string,
   - returns a header pointer,
   - or mutates page/selection state.

This is a good way to “walk down” the runtime graph without doing runtime dumping yet.



