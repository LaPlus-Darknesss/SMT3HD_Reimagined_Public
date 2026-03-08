# Part 2 — Token → MethodInfo resolution cookbook (IL2CPP)

This is the mechanical bridge between “a name in the decompile” and “a callable/patchable runtime method”.

The wrapper sources show a consistent pattern:

- Find the IL2CPP class pointer for a type in `Assembly-CSharp.dll`
- Resolve a **method** by metadata token (decimal)
- Use the resulting `NativeMethodInfoPtr_*` for `il2cpp_runtime_invoke(...)` (or as a hook seam)

---

## What the wrappers do (canonical pattern)

Example (from `Il2Cpp/Achievement.cs`), simplified:

```csharp
// 1) class pointer
var klass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "Achievement");

// 2) method by token (decimal)
var mi = IL2CPP.GetIl2CppMethodByToken(klass, 100673732);
```

That `100673732` corresponds to a `0x0600....` method token (same value, just different base).

---

## Token formats

You will see both formats across tools:

- Hex method token (common in comments): `0x06001D4D`
- Decimal token (common in wrapper initializers): `100670797`

Conversion:
- `token_dec = int(token_hex, 16)`

---

## Practical workflow (recommended)

1) Start from the wrapper file you care about (e.g. `evtLoadManager.cs`).
2) Take the method’s token (hex in comment) or the decimal token if present elsewhere.
3) Resolve the MethodInfo pointer at runtime with `GetIl2CppMethodByToken`.
4) Validate with **log-only** instrumentation first:
   - log the args
   - log return values
   - confirm call frequency (per frame vs per transition)

Only then decide whether to:
- call the method yourself (`il2cpp_runtime_invoke`), or
- patch it (Harmony/interop approach), or
- detour it (native hook approach)

---

## Where to get tokens quickly

For event work:
- `data/part2_evtCommand_catalog.csv`
- `data/part2_evtLoadManager_members.csv`
- `data/part2_evtStage_members.csv`
- `data/part2_evtPolygonMovie_members.csv`

Each row includes both `token_hex` and `token_dec`.

