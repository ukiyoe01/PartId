# Changelog

## v1.1.1

- Fixed per-part data (such as a saved mass) appearing to reset on Windows. PartId reads
  the current blueprint to resolve deterministic pids; the read used a share mode that
  clashed with the game's own open handle right after it saved (e.g. on a scene change),
  throwing "Sharing violation". The failed read left the pid cache empty, so consumer
  mods could not resolve parts and their stored data looked lost. The blueprint is now
  opened with `FileShare.ReadWrite`, so it reads fine even while the game holds it.
- The mod now logs its version on load (`[PartId] v1.1.1 loaded ...`) to make diagnosing
  installs from a log easier.

Record format: `PartId.TypedRecords.v1` (unchanged).

Public API version: `2`

## v1.1.0

- **Blueprint and save files are now strictly read-only.** PartId no longer writes `pid` fields into blueprints — pids are computed deterministically on demand, so your builds are never modified or at risk of corruption.
- **Deterministic pids.** A part's pid is derived from its identity (name + position), so the same craft resolves to identical pids on every machine; shared blueprints and their records stay consistent across computers.
- Existing records are migrated automatically from old random pids to the new deterministic pids (no data loss).
- Fixed a startup hang for players with many saved blueprints (no longer scans and rewrites every saved blueprint on load).
- Fixed multi-second stalls on large craft (removed O(N²) pid lookups during bulk restore by consumer mods).
- Removed the `System.Security.Cryptography` dependency (it can be stripped from the game's managed build on some platforms, e.g. Windows); uses a self-contained hash instead.
- Trimmed noisy diagnostic logging.

Record format: `PartId.TypedRecords.v1` (unchanged — v1.0.0 records are still read and migrated)

Public API version: `2`

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
