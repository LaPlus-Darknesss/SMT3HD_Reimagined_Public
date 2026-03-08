# UI / Menu Process Framework (Camp + Facility)

This tranche maps the **non-battle UI** systems used by:
- the **Camp menu** (pause/menu in field) — `cmp*`
- the **Facility / Terminal / Shop / Cathedral** menus — `fcl*`

All of these surfaces are implemented as **process-driven state machines** on top of the kernel process graph.

---

## Process backbone: `dds3ProcessID_t` / `dds3ProcessFunc_t`

Most menu “update” entry points take a `dds3ProcessID_t` and return a `dds3ProcessFunc_t`:
- **Input:** `PID` (the process node for this UI)
- **Output:** the function pointer/delegate for the next tick

Key type:
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppkernel_H/dds3ProcessID_t.cs`

Important fields on `dds3ProcessID_t` (high-level; see the wrapper for exact names):
- parent/child links (`pParent`, `pChild`, `pPrev`, `pNext`)
- current function (`pFunc`) and argument (`pArg`)
- flags, priority/order

When you see a method named `*UpdateSequence(dds3ProcessID_t PID) -> dds3ProcessFunc_t`, it is usually the **tick function** for the entire menu surface.

---

## Shared “sequence” state: `cmpSeqInfo_s`

Both Camp and Facility menus use a small sequence controller object:
- `tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cppcamp_H/cmpSeqInfo_s.cs`

Notable fields:
- `Current`, `Next`, `Last`, `Change` (sequence state transitions)
- `Timer` (sequence timing)
- `MesPID` (message process association)
- `pParent` (nested sequences)

This is one of the most useful *read-only* observation points for answering: “what sub-screen/state is the menu currently in?”

---

## Common UI widgets referenced by menus

You will see these types repeatedly in Camp / Facility work structs:

- `cmpCmdSelInfo_t` + `cmpCmdSel` (command selection)
- `lstListWindow_t` (scrolling list window)
- `fclPanel_t`, `fclPanelMover_t` (panel UI and transitions)
- `sdfTexHandle_t` (texture handles; texture load/free in `*TexLoad` / `*TexFree`)
- `dds3ProcessID_t` child processes for scripts and message windows

(We keep the detailed widget / panel deep-dive for a later tranche; this part focuses on the menu state machines and the surfaces that consume them.)
