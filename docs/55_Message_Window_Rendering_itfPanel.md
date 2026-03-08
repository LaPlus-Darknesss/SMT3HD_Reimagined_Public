# Message Window Rendering — `itfPanel` + `window_tag` (v10)

This document maps the **rendering-side surfaces** for message windows, based on:

- `Il2Cpp/itfPanel.cs`
- `Il2Cpp/window_tag.cs`
- `Il2Cppinterface_H/itfpanel_t.cs`

The goal is to understand which seams affect:
- geometry generation (vertex/color builders)
- draw calls for message + speaker windows
- panel placement + color (UI layout constraints)

---

## `window_tag` holds panel handles

| name | type |
| --- | --- |
| spk | itfpanel_t |
| mes | itfpanel_t |
| mes_offset | sdfIVector4_t |
| mes_color | sdfIVector4_t |


Takeaway:
- `window_tag.spk` and `window_tag.mes` are both `itfpanel_t` (panel objects).
- The message window has `mes_offset` and `mes_color` (likely for text inset + tint).

## `itfpanel_t` (panel object)

| name | type |
| --- | --- |
| pwork | itfPanelTypeWork_t |
| type | int |
| z | uint |
| pos | sdfIVector4_t |
| color | sdfIVector4_t |


The panel has:
- `type`
- `pos` and `color`
- a `pwork` pointer (type-specific work struct)

---

## High-ROI `itfPanel` seams (IL2CPP tokens)

These are the most relevant `itfPanel` entry points for message windows:

| base | il2cpp_token | field |
| --- | --- | --- |
| CreateColorMesWin | 100669446 | CreateColorMesWin_Internal_Static_Void_itfPanelTypeWork_t_Int32_Int32_Int32_Int32_0 |
| CreateColorSelWin | 100669447 | CreateColorSelWin_Internal_Static_Void_itfPanelTypeWork_t_Int32_Int32_Int32_Int32_0 |
| CreateVertexMesWin | 100669440 | CreateVertexMesWin_Internal_Static_Void_itfPanelTypeWork_t_Int32_Int32_Int32_Int32_0 |
| DrawMesWin | 100669453 | DrawMesWin_Internal_Static_Void_itfpanel_t_sdfDrawTag_t_0 |
| DrawMesWin2 | 100669458 | DrawMesWin2_Internal_Static_Void_itfpanel_t_sdfDrawTag_t_0 |
| DrawSelWin | 100669454 | DrawSelWin_Internal_Static_Void_itfpanel_t_sdfDrawTag_t_0 |
| DrawSpkWin2 | 100669459 | DrawSpkWin2_Internal_Static_Void_itfpanel_t_sdfDrawTag_t_0 |
| itfPnlDraw | 100669467 | itfPnlDraw_Public_Static_Void_itfpanel_t_sdfGrOt_t_0 |
| itfPnlRegist2 | 100669462 | itfPnlRegist2_Public_Static_itfpanel_t_Int32_sdfTexHandle_t_0 |
| itfPnlRelease | 100669463 | itfPnlRelease_Public_Static_Void_itfpanel_t_0 |
| itfPnlSetColor | 100669466 | itfPnlSetColor_Public_Static_Void_itfpanel_t_Int32_Int32_Int32_Int32_0 |
| itfPnlSetPosition | 100669464 | itfPnlSetPosition_Public_Static_Void_itfpanel_t_Int32_Int32_Int32_Int32_UInt32_0 |


Interpretation (name-driven):
- `CreateVertexMesWin` / `CreateColorMesWin` are likely the geometry/color builders for the **message window panel**.
- `DrawMesWin` / `DrawMesWin2` and `DrawSpkWin2` suggest alternate draw paths for message/speaker.
- `itfPnlRegist2` likely registers a panel with a given type/position.
- `itfPnlSetPosition`, `itfPnlSetColor`, `itfPnlDraw` are generic panel controls.

---

## How `itfMesManager` likely uses this

In `instanceMes_tag`, the `window_tag` is present as `instanceMes_tag.window`.

That implies a flow:
- message system updates `window_tag` (positions/colors, active state)
- panel system draws those `itfpanel_t` objects

This division is helpful for modding:
- if you want *only* to change text, stay on the `itfMesManager` side
- if you want to alter window visuals, `itfPanel` seams are the right layer

---

## Caution (UI stability)

Message windows are tightly coupled to:
- selection windows (`select_tag`)
- speaker rendering (`Speaker_tag`)
- likely screen scaling / UI camera setup

So a future visual mod should:
- prefer adjusting `mes_offset` / `mes_color` via higher-level calls (if any)
- avoid rewriting raw vertex builders until we’ve mapped the exact vertex format + prim types.
