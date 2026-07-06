# Part ID (PID)

**Part ID** — "PID" for short — is a dependency mod for other Spaceflight Simulator DLL mods. Here "PID" always means a stable *part* identifier; it has nothing to do with process IDs or PID controllers.

Code-facing names stay idiomatic C#: the folder and DLL are `PartId`, the namespace is `PartId`, and the public API entry point is `PartIdApi`.

It owns two responsibilities:

- Give every blueprint part a stable `pid`, computed deterministically from the part itself.
- Store typed per-part records (keyed by that pid) that other mods can share.

**PartId never modifies your blueprint or save files — it treats them as strictly read-only.** A part's `pid` is computed on demand from the part's own identity (name + position), so the same craft resolves to the same pids on every machine. Shared blueprints and their per-part records therefore stay consistent across computers, with nothing ever written into your builds.

## How Consumers Use It

`PartId.dll` is the dependency/base mod. A consumer mod references it — directly or by reflection — and stores its own typed per-part records under its own owner namespace (for example, a mass editor storing a mass override).

For deeper integration guidance, see [DEVELOPERS.md](DEVELOPERS.md).

For the exact on-disk data format, see [FORMAT.md](FORMAT.md).

For recommended shared key names, see [COMMON_KEYS.md](COMMON_KEYS.md).

For a small consumer-mod template, see [examples/ConsumerExample.cs](examples/ConsumerExample.cs).

For a reflection-based soft dependency template, see [examples/SoftDependencyBridge.cs](examples/SoftDependencyBridge.cs).

## Install

Install just `PartId.dll`:

```text
Mods/
  PartId/
    PartId.dll
```

The mod creates `pid-records.tsv` in the same folder automatically on first run — you do not add it yourself. (This is PartId's own record file; the game's blueprint and save files are never touched.)

## Release Status

Current release: `v1.1.1`

The record format is stable as `PartId.TypedRecords.v1`. Future Part ID (PID) releases should keep reading this format.

## Record Format

Records are written to:

```text
Mods/PartId/pid-records.tsv
```

Format:

```text
# PartId.TypedRecords.v1 pid owner64 key64 type payload
v1    pid_xxx    owner_base64    key_base64    type    payload
```

The actual separator is a tab.

Example for a mass editor:

```text
v1    pid_4900916eb8ad414385144a55857d4e7c    RXhhbXBsZU1hc3NNb2Q=    bWFzcw==    double    -1
```

This means:

- `pid`: `pid_4900916eb8ad414385144a55857d4e7c`
- `owner`: `ExampleMassMod`
- `key`: `mass`
- `type`: `double`
- `payload`: `-1`

## Supported Value Types

- `bool`: `true` or `false`
- `int`: invariant integer text
- `long`: invariant integer text
- `float`: invariant round-trip float text
- `double`: invariant round-trip double text
- `string`: UTF-8 base64
- `json`: UTF-8 base64
- `vec2`: `x,y`
- `vec3`: `x,y,z`
- `color`: `r,g,b,a`
- `bytes`: base64

Type-specific getters are intentionally strict where it matters:

- `TryGetInt` reads `int`, not a rounded `double`.
- `TryGetString` reads `string`, while `TryGetJson` reads `json`.
- `TryGetDouble` can read any numeric record type: `int`, `long`, `float`, or `double`.

## Public API

```csharp
using PartId;

int apiVersion = PartIdApi.ApiVersion;
string recordFormat = PartIdApi.RecordFormat;
bool supportsJson = PartIdApi.SupportsValueType(PartIdValue.Json);
string[] supportedTypes = PartIdApi.GetSupportedValueTypes();
string[] massTypes = PartIdApi.GetCommonKeyValueTypes(PartIdKeys.Physics.Mass);

PartIdApi.Ensure();

if (PartIdApi.TryGetPid(part, out string pid))
{
    PartIdApi.SetDouble(part, "MyMod", "some_number", 123.0);
}

if (PartIdApi.TryGetDouble(part, "MyMod", "some_number", out double value))
{
    // use value
}

PartIdApi.SetBool(part, "MyMod", "enabled", true);
PartIdApi.SetInt(part, "MyMod", "stage", 2);
PartIdApi.SetFloat(part, "MyMod", "temperature", 273.15f);
PartIdApi.SetVector2(part, "MyMod", "offset", new Vector2(1f, 2f));
PartIdApi.SetVector3(part, "MyMod", "axis", new Vector3(0f, 1f, 0f));
PartIdApi.SetColor(part, "MyMod", "paint", Color.red);
PartIdApi.SetJson(part, "MyMod", "settings", "{\"mode\":\"demo\"}");
PartIdApi.SetBytes(part, "MyMod", "blob", new byte[] { 1, 2, 3 });

PartIdApi.RemoveValue(part, "MyMod", "settings");
```

Common key names are also available as constants:

```csharp
PartIdApi.SetDouble(part, "MyMod", PartIdKeys.Physics.Mass, 1.0);
PartIdApi.SetColor(part, "MyMod", PartIdKeys.Visual.Color, Color.red);
```

Common key definitions can be queried for editor UI or validation:

```csharp
if (PartIdApi.TryGetCommonKeyDefinition(PartIdKeys.Visual.Color, out PartIdKeyDefinition definition))
{
    string primaryType = definition.PrimaryType; // color
}
```

Each setter also has a `pid` overload, for example:

```csharp
PartIdApi.SetDouble("pid_...", "MyMod", "some_number", 123.0);
PartIdApi.TryGetDouble("pid_...", "MyMod", "some_number", out double value);
```

For debugging, migration, or inspector tools, records can also be listed:

```csharp
List<PartIdRecord> myRecords = PartIdApi.GetRecordsForOwner("MyMod");
List<PartIdRecord> partRecords = PartIdApi.GetRecordsForPidList("pid_...");
```

## Naming Rules

- `owner` should be the mod ID, for example `ExampleMassMod`.
- `key` should be stable snake_case or lower-case words, for example `mass`, `paint_color`, `enabled`.
- Mods should not write into another mod's `owner` namespace unless they are intentionally interoperating.

## Verify

From the `PartId` folder:

```bash
./verify.sh
```

This rebuilds `PartId.dll` and checks the public value encoders/readers for the documented types.
