# 46 — Negotiation flow catalog (NbNegoFlow*)

Negotiation appears to be implemented as a **flow state machine** keyed by `flow id` and `step`, with per-flow handler classes named `NbNegoFlow*`.

Each flow class:
- has a public entry function (e.g. `nbNegoFlowOk`, `nbNegoFlowNg`, …)
- contains many `FlowX_Y` functions (`X = flow id`, `Y = step`)
- operates on `ref nbNegoProcessData_t` and returns an `Int32` (likely next state / return code)

This doc is a static surface catalog derived from wrapper method names + tokens (no runtime assumptions).

---

## Flow ID ↔ class map

| Flow ID | Class |
|---|---|
| 1 | NbNegoFlowOne |
| 2 | NbNegoFlowFullMoon |
| 3 | NbNegoFlowStock |
| 4 | NbNegoFlowDouchara |
| 5 | NbNegoFlowInnen |
| 6 | NbNegoFlowTwo |
| 7 | NbNegoFlowQuestion |
| 8 | NbNegoFlowFight |
| 9 | NbNegoFlowOk |
| 10 | NbNegoFlowNg |
| 11 | NbNegoFlowDrain |
| 12 | NbNegoFlowMukan |
| 13 | NbNegoFlowSensei |
| 14 | NbNegoFlowBinjo |
| 15 | NbNegoFlowInoti |
| 16 | NbNegoFlowKoketa |

Export:
- `data/part8_NegoFlow_flowid_map.csv`

---

## Flow step density (how big each flow is)

| Flow ID | Class | Steps | Step range |
|---|---|---|---|
| 1 | NbNegoFlowOne | 37 | 3–39 |
| 2 | NbNegoFlowFullMoon | 16 | 1–16 |
| 3 | NbNegoFlowStock | 2 | 1–2 |
| 4 | NbNegoFlowDouchara | 13 | 1–13 |
| 5 | NbNegoFlowInnen | 21 | 1–21 |
| 6 | NbNegoFlowTwo | 75 | 1–81 |
| 7 | NbNegoFlowQuestion | 10 | 1–26 |
| 8 | NbNegoFlowFight | 19 | 1–19 |
| 9 | NbNegoFlowOk | 20 | 1–22 |
| 10 | NbNegoFlowNg | 32 | 1–33 |
| 11 | NbNegoFlowDrain | 9 | 1–9 |
| 12 | NbNegoFlowMukan | 35 | 1–49 |
| 13 | NbNegoFlowSensei | 12 | 1–12 |
| 14 | NbNegoFlowBinjo | 2 | 1–2 |
| 15 | NbNegoFlowInoti | 16 | 1–23 |
| 16 | NbNegoFlowKoketa | 3 | 1–3 |

Interpretation tips:
- Some flows are compact endpoints (`Stock`, `Binjo`) with only a couple of steps.
- Others are deep (`Two`, `Mukan`, `Ok`, `Ng`) and likely represent major negotiation branches.

Exports:
- `data/part8_NegoFlow_steps_summary.csv`
- `data/part8_NegoFlow_steps_full.csv` (includes token + ptr for each step)

---

## Method catalogs

- Full method catalog across all `NbNegoFlow*`:
  - `data/part8_NegoFlow_methods_full.csv`

This is intentionally “raw” so you can grep/filter for:
- specific steps (`Flow6_24`, `Flow10_33`, …)
- or specific flow entry points (`nbNegoFlowOk`, `nbNegoFlowTwo`, …)

