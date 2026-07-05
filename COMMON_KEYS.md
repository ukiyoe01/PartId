# PartId Common Keys

PartId stores records by:

```text
pid + owner + key
```

The `key` names below are recommended vocabulary for mods that want predictable names. They are not hard restrictions. A mod may define its own keys inside its own `owner` namespace.

Use these common keys when the meaning matches. Use a more specific key when the meaning is different.

The same vocabulary is available at runtime:

```csharp
PartIdApi.GetCommonKeyDefinitions()
PartIdApi.TryGetCommonKeyDefinition("mass", out PartIdKeyDefinition definition)
PartIdApi.GetCommonKeyValueTypes("mass")
```

## Common

| Key | Type | Meaning |
| --- | --- | --- |
| `enabled` | `bool` | Whether this mod's behavior is enabled for the part. |
| `label` | `string` | Human-facing short name. |
| `notes` | `string` | Human-facing longer note. |
| `settings` | `json` | Small structured settings owned by one mod. |
| `version` | `int` | Schema version for one mod's per-part data. |

## Physics

| Key | Type | Meaning |
| --- | --- | --- |
| `mass` | `double` | Override or target mass in the same unit the game mass field uses. |
| `dry_mass` | `double` | Mass without fuel or consumables. |
| `wet_mass` | `double` | Mass with fuel or consumables. |
| `thrust` | `double` | Thrust value in the same unit the game exposes for the target part. |
| `isp` | `double` | Specific impulse or efficiency value. |
| `fuel_capacity` | `double` | Fuel capacity in the same unit the target fuel module uses. |
| `drag_multiplier` | `double` | Multiplier applied to stock drag. `1` means no change. |
| `torque` | `double` | Torque strength in the same unit the target part uses. |
| `collision_enabled` | `bool` | Whether the part should participate in collision. |

## Transform And Geometry

| Key | Type | Meaning |
| --- | --- | --- |
| `offset` | `vec2` or `vec3` | Offset relative to the part's normal local position. |
| `position` | `vec2` or `vec3` | Absolute or replacement local position, if a mod defines one. |
| `scale` | `vec2` or `vec3` | Visual or logical scale. |
| `rotation` | `float` or `vec3` | 2D angle or 3D Euler-style rotation. |
| `size` | `vec2` or `vec3` | Bounds, panel size, or generated geometry size. |
| `radius` | `float` | Radius for circular geometry or influence. |
| `polygon` | `json` | Polygon, collider, or custom point list. |

## Visual

| Key | Type | Meaning |
| --- | --- | --- |
| `visible` | `bool` | Whether this mod should render the part or overlay. |
| `color` | `color` | Primary RGBA color. |
| `secondary_color` | `color` | Secondary RGBA color. |
| `alpha` | `float` | Opacity multiplier. `1` means fully opaque. |
| `texture` | `string` | Texture id, path, or asset key. |
| `sprite` | `string` | Sprite id, path, or asset key. |
| `render_order` | `int` | Ordering hint for visual layering. |

## Build

| Key | Type | Meaning |
| --- | --- | --- |
| `category` | `string` | Build menu or mod category. |
| `variant` | `string` | Variant id selected for this part. |
| `stage` | `int` | Stage, sequence, or ordering value. |
| `group` | `string` | Mod-defined group name. |
| `tags` | `json` | Small list or object of tags. |
| `attach_enabled` | `bool` | Whether custom attachment logic is enabled. |
| `symmetry_group` | `string` | Identifier linking mirrored or repeated parts. |

## Unit Policy

PartId does not convert units. It stores exactly what the owner mod writes.

If a value mirrors a game field, use the same unit as that game field. If a mod invents a new unit, document it in that mod and prefer a specific key name such as `thrust_kn` or `mass_tons`.

## Interop Policy

Most records should use the writer's own `owner`, for example:

```text
pid_... + ExampleMassMod  + mass
pid_... + PaintMod        + color
```

Two mods should share an `owner` namespace only when they intentionally agree on a shared contract.
