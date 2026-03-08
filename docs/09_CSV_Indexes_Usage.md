# CSV Indexes — How to use `assembly_csharp_types.csv` and `assembly_csharp_methods.csv`

The repo already contains two high-value indexes under `SMT3HD_Reimagined/docs/`:

## `assembly_csharp_types.csv`

Columns:
- `type_id`: internal ID used by the method index
- `namespace`, `type_name`: where to find the wrapper file (`<namespace>/<type_name>.cs`)
- `method_start`, `method_end_exclusive`, `method_count`: method index span/count for this type

Typical uses:
- find “big” systems by sorting on `method_count`
- locate the wrapper file for a type name quickly

## `assembly_csharp_methods.csv`

Columns:
- `method_idx`: global method index
- `method_name`: method name only (no signature)
- `owner_type_id`, `owner_namespace`, `owner_type_name`: owner lookup

Typical uses:
- grep-like search across the entire Assembly-CSharp surface without opening every wrapper file
- build “call surface inventories” (e.g., all methods named `Init`, `Update`, `Calc*`, `Get*` under a namespace)

## Important limitation

The CSV method list does **not** include IL2CPP tokens or signatures.
To get tokens/signatures you must open the wrapper `.cs` and read the static constructor:
- `IL2CPP.GetIl2CppMethodByToken(..., <token>)`

This pack’s `part1_tokens.csv` is an example of extracting that extra layer for a curated subset.



## This doc pack’s `data/part*_*.csv` exports

Each tranche adds curated token/field exports under `data/`:

- `data/part7_*` — battle runtime core (nbMain/Action/Kouka/Calc/AI/press)
- `data/part8_*` — battle command/target selection + negotiation flows

These are meant to be “grep targets” so you can filter by class/method/flow id without reopening the wrapper sources.


## Tranche exports (`data/part*_*.csv`)

These docs include tranche-specific exports under `data/`. They are meant for quick filtering/sorting.

### Part 12 (resource/file pipeline)
- `part12_resource_pipeline_method_catalog.csv` — tokens + signatures + callerCount for core resource classes (bundles, DLC, file handle, field resolver, model table, Localize).
- `part12_resource_pipeline_fields.csv` — field lists for those same classes (useful for spotting roots and caches).
- `part12_FilePathConstans_fields.csv` — canonical root/path field list.
- `part12_fldFileResolver_method_groups.csv` — grouped summary of fldFileResolver surface.
- `part12_fldFileResolver_methods_grouped.csv` — method list with group labels.
- `part12_candidate_file_asset_types.csv` — filename-based index of likely file/bundle/path/message related wrapper types to investigate next.
