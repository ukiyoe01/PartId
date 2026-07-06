# PartId Typed Records Format

This document defines the on-disk format used by `PartId.dll`.

Current format ID:

```text
PartId.TypedRecords.v1
```

Runtime capability checks:

```csharp
PartIdApi.RecordFormat
PartIdApi.ApiVersion
PartIdApi.GetSupportedValueTypes()
PartIdApi.SupportsValueType("json")
```

## File Location

Default game install location:

```text
Mods/PartId/pid-records.tsv
```

Fallback location when the game root cannot be found:

```text
Application.persistentDataPath/PartId/pid-records.tsv
```

## File Encoding

- UTF-8 text.
- Line ending is platform default.
- Fields are separated by tabs.
- Lines beginning with `#` are comments.
- Empty lines are ignored.

## Header

The first line should be:

```text
# PartId.TypedRecords.v1 pid owner64 key64 type payload
```

Readers must not require the header to be present. The header is advisory and helps humans identify the format.

## Record Line

Version 1 record:

```text
v1<TAB>pid<TAB>owner64<TAB>key64<TAB>type<TAB>payload
```

Fields:

- `v1`: literal record version.
- `pid`: stable blueprint part id, normalized to `pid_...`.
- `owner64`: base64-encoded UTF-8 owner namespace.
- `key64`: base64-encoded UTF-8 key name.
- `type`: normalized lowercase type name.
- `payload`: type-specific encoded value.

The unique record key is:

```text
pid + owner + key
```

Later duplicate records replace earlier duplicate records when loaded.

## PID Rules

Valid current pid:

```text
pid_<32 lowercase hex chars>
```

PartId currently accepts any non-empty string beginning with `pid_`, but generated ids use the 32-hex form.

Generated ids are **deterministic**: PartId computes a part's pid from its identity (name + rounded position), so the same part resolves to the same pid on any machine. PartId does **not** write pids into blueprint or save files — they are computed on demand and only referenced by records in `pid-records.tsv`.

Legacy pids beginning with `amm_` are normalized to `pid_` by replacing only the prefix.

## Owner And Key Rules

`owner` should be a stable mod identifier.

Good:

```text
ExampleMassMod
PaintMod
MyCompany.MyMod
```

`key` should be stable and specific.

Good:

```text
mass
enabled
paint_color
engine_thrust_multiplier
```

`owner` and `key` are base64 in the file so they can safely contain spaces, dots, Chinese text, or other UTF-8 content. Public APIs accept plain strings.

## Type Payloads

### bool

Payload:

```text
true
false
```

### int

Payload is invariant-culture signed 32-bit integer text.

Example:

```text
-12
```

### long

Payload is invariant-culture signed 64-bit integer text.

Example:

```text
9007199254740991
```

### float

Payload is invariant-culture round-trip single precision text.

Example:

```text
1.25
```

### double

Payload is invariant-culture round-trip double precision text.

Example:

```text
-1
```

### string

Payload is base64-encoded UTF-8 string content.

### json

Payload is base64-encoded UTF-8 JSON text.

PartId stores JSON as text and does not validate its schema.

### vec2

Payload:

```text
x,y
```

Both components are invariant-culture floats.

### vec3

Payload:

```text
x,y,z
```

All components are invariant-culture floats.

### color

Payload:

```text
r,g,b,a
```

All components are invariant-culture floats. Recommended range is 0 to 1, but PartId does not clamp values.

### bytes

Payload is base64-encoded binary data.

## Compatibility

Readers must ignore:

- Empty lines.
- Comment lines.
- Lines with unsupported record versions.
- Lines with unknown value types.
- Lines with invalid base64 owner or key fields.
- Lines with invalid pids.

Future formats should use a new record version, for example `v2`, and keep reading `v1`.

## Legacy Migration

When `Mods/PartId/pid-records.tsv` does not exist, PartId tries to copy legacy records from:

```text
Mods/SfsPidCore/pid-records.tsv
```

The copied file receives the current `PartId.TypedRecords.v1` header.

Do not keep `SfsPidCore.dll` installed together with `PartId.dll`.
