# Part ID (PID) Developer Guide

**Part ID** — "PID" for short — is a small shared foundation for SFS DLL mods. Here "PID" always means a stable *part* identifier, not a process ID or PID controller.

The code-facing name is `PartId`: `PartId.dll`, namespace `PartId`, and API class `PartIdApi`.

Use it when a mod needs to attach data to a specific blueprint part and read that data again after the game rebuilds runtime part objects.

## Concepts

- `pid`: A stable identifier for a part, computed deterministically from the part itself (name + position). PartId does not write it into the blueprint — it is recomputed on demand, so builds are never modified and the same part resolves to the same pid on every machine.
- `owner`: The mod namespace that owns a record, usually your `ModNameID`.
- `key`: The field name inside your namespace.
- `type`: The storage type of the payload.
- `payload`: The encoded value.

A record is uniquely identified by:

```text
pid + owner + key
```

That means two mods can store data on the same part without stepping on each other:

```text
pid_abc + ExampleMassMod  + mass
pid_abc + PaintMod        + color
pid_abc + MyLogicMod      + enabled
```

## Record File

PartId writes shared records to:

```text
Mods/PartId/pid-records.tsv
```

Version 1 format:

```text
# PartId.TypedRecords.v1 pid owner64 key64 type payload
v1<TAB>pid_xxx<TAB>owner_base64<TAB>key_base64<TAB>type<TAB>payload
```

`owner` and `key` are base64-encoded UTF-8 so mod IDs, Chinese text, spaces, and punctuation remain safe in a TSV file.

The full file contract is documented in `FORMAT.md`.

## Choosing Types

Prefer the smallest clear type:

- Use `bool` for toggles.
- Use `int` for counters, choices, indexes, stages, and enum-like values.
- Use `long` only when `int` may overflow.
- Use `float` for Unity-style visual or physics values when single precision is enough.
- Use `double` for precise numeric gameplay values or values already represented as double.
- Use `string` for plain text.
- Use `json` for structured settings that belong to one mod.
- Use `vec2` for 2D offsets or directions.
- Use `vec3` for 3D offsets, axes, or rotations when you do not need quaternions.
- Use `color` for RGBA values from 0 to 1.
- Use `bytes` only for compact binary data that cannot reasonably be represented as text or JSON.

Avoid storing large blobs in PartId records. Use a separate file if the payload is bigger than a small settings object.

## Direct Reference

If your mod can reference `PartId.dll` at compile time:

```csharp
using PartId;
using UnityEngine;

if (PartIdApi.ApiVersion < 2 || !PartIdApi.SupportsValueType(PartIdValue.Json))
{
    // Show a helpful message or disable only the feature that needs JSON records.
}

PartIdApi.Ensure();

if (PartIdApi.TryGetPid(part, out string pid))
{
    PartIdApi.SetDouble(part, "MyMod", "mass", 2.5);
    PartIdApi.SetColor(part, "MyMod", "paint", Color.red);
}

if (PartIdApi.TryGetDouble(part, "MyMod", "mass", out double mass))
{
    // Apply mass.
}

PartIdApi.RemoveValue(part, "MyMod", "paint");
```

You can use `PartIdKeys` when a common key matches your meaning:

```csharp
PartIdApi.SetDouble(part, "MyMod", PartIdKeys.Physics.Mass, 2.5);
PartIdApi.SetColor(part, "MyMod", PartIdKeys.Visual.Color, Color.red);
```

For tooling, migration, or debug panels, use the list APIs:

```csharp
List<PartIdRecord> modRecords = PartIdApi.GetRecordsForOwner("MyMod");
List<PartIdRecord> partRecords = PartIdApi.GetRecordsForPidList("pid_...");
List<PartIdRecord> allRecords = PartIdApi.GetAllRecords();
```

`GetRecordsForPid` remains available for older callers that prefer a `Dictionary<string, PartIdValue>`.

For editor UI or validation tools, query the common key schema:

```csharp
PartIdKeyDefinition[] commonKeys = PartIdApi.GetCommonKeyDefinitions();
string[] massTypes = PartIdApi.GetCommonKeyValueTypes(PartIdKeys.Physics.Mass);
```

## Runtime Soft Dependency

If you do not want a hard assembly reference, load `PartId.dll` at runtime and call `PartId.PartIdApi` by reflection.

For a direct-reference consumer template, see `examples/ConsumerExample.cs`.

For a small copyable reflection bridge, see `examples/SoftDependencyBridge.cs`.

Use a soft dependency when:

- You do not control mod loading order.
- You want your mod to show a helpful message instead of failing assembly load.
- You want to keep your DLL build independent from PartId.

Use a direct reference when:

- You distribute both DLLs together.
- You want compile-time type checking.
- Your mod loader guarantees dependency load order.

## Naming Rules

Use stable names. Do not change `owner` or `key` casually, because that creates a new storage slot.

For recommended shared key names and their value types, see `COMMON_KEYS.md`.

Recommended:

```text
owner = "MyMod"
key   = "enabled"
key   = "paint_color"
key   = "engine_thrust_multiplier"
```

Avoid:

```text
owner = "my mod v2"
key   = "value"
key   = "new setting !!!"
```

If a key must be renamed, read the old key once, write the new key, then remove the old key.

## Compatibility Policy

The current record format is:

```text
PartId.TypedRecords.v1
```

The current API version is:

```text
PartIdApi.ApiVersion = 2
```

The current common key vocabulary version is:

```text
PartIdApi.CommonKeysVersion = 1
```

Future versions should keep reading v1 records. If the on-disk format changes, add a new version tag rather than silently changing the meaning of existing lines.

Increase `ApiVersion` only when public API behavior changes in a way that consumers may need to detect. Increase `CommonKeysVersion` when `COMMON_KEYS.md` changes in a way that affects shared vocabulary.

PartId can migrate legacy records from `Mods/SfsPidCore/pid-records.tsv` into `Mods/PartId/pid-records.tsv` on first record-file creation. Do not keep both mods installed at the same time.

Mods should ignore records they do not own.

Mods should not write into another mod's `owner` namespace unless both mods intentionally agree on shared keys.

## Common Key Vocabulary

`COMMON_KEYS.md` defines the recommended key vocabulary for common mod data: physical values, transform/geometry values, visual values, and build metadata.

`PartIdKeys` exposes those names as constants for direct-reference mods. Soft-dependency mods can still use the plain string values from `COMMON_KEYS.md`.

## Verification

Run this before distributing a changed `PartId.dll`:

```bash
./verify.sh
```

It checks that the documented value types still round-trip with the public `PartIdValue` API.
