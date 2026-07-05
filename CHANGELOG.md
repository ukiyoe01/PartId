# Changelog

## v1.0.0

- First public release of Part ID (PID).
- Keeps code-facing names as `PartId`: `PartId.dll`, `Mods/PartId/`, namespace `PartId`, and API class `PartIdApi`.
- Adds stable blueprint `pid` assignment and cleanup for legacy `amm_id` / `pid_id` fields.
- Adds typed per-part records in `Mods/PartId/pid-records.tsv`.
- Adds value APIs for `bool`, `int`, `long`, `float`, `double`, `string`, `json`, `vec2`, `vec3`, `color`, and `bytes`.
- Adds common key constants and runtime common-key schema metadata.
- Adds list APIs for record inspector/debug tooling.
- Adds direct-reference and soft-dependency consumer examples.

Record format: `PartId.TypedRecords.v1`

Public API version: `2`
