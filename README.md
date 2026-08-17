# MLGWorks RebindX

MLGWorks RebindX is a small runtime and editor integration layer for Unity's Input System. It provides:

- `RebindManager`, which owns an `InputActionAsset` and persists binding overrides.
- `RebindActionUI`, which connects one input binding to a settings screen and starts interactive rebinding.
- Composite binding support, including per-part display and reset/cancel handling.
- Custom inspectors that make selecting actions and bindings easier.

RebindX changes binding overrides at runtime. It does not modify the original `.inputactions` asset or permanently change the default bindings.

## Runtime architecture and extension points

`RebindManager` remains the convenient Unity-facing façade, but its replaceable responsibilities are now separated into small runtime services:

- `IRebindPathProvider` resolves the storage directory and file path. The default implementation is `FileSystemRebindPathProvider`.
- `IBindingOverrideStore` loads and saves overrides. `JsonBindingOverrideStore` is the default persistent implementation, while `InMemoryBindingOverrideStore` is useful for tests, temporary profiles, and custom save flows.
- `IInputActionAssetProvider` owns enabling and disposing an input asset. `InputActionAssetProvider` handles a normal asset and `GeneratedControlsProvider` handles `PlayerInputControls`.
- `RebindSession` owns the enabled-state transition for one interactive rebind and restores the action's original state when the operation ends.

The manager's existing Inspector configuration and methods remain compatible. Advanced integrations can replace the default persistence service:

```csharp
RebindManager.Instance.OverrideStore = new InMemoryBindingOverrideStore();
```

For a production backend such as cloud saves, implement `IBindingOverrideStore` and assign it before calling `SaveRebinds` or `LoadRebinds`. The store receives the active `InputActionAsset`; it does not own the asset or the manager lifetime.

## Requirements

- Unity with the Input System package enabled.
- The Input System package's `InputActionAsset`, `InputActionReference`, and interactive rebinding APIs.
- The included `MLGWorks.Utils` dependency. Keep the `MLGWorks.Utils` submodule checked out when using the source repository.

The runtime assembly is `MLGWorks.RebindX`. The test assembly is `MLGWorks.RebindX.Tests` and is Editor-only.

## Basic setup

### 1. Create or import an Input Action Asset

Create an Input Actions asset through **Assets > Create > Input Actions**, or use an existing asset. Define your maps, actions, and default bindings as usual.

For example:

```text
Gameplay
  Jump     Button   <Keyboard>/space
  Move     Value    2D Vector composite
```

Enable **Generate C# Class** only if your project needs the generated wrapper. RebindX can work directly with any `InputActionAsset`.

### 2. Add a Rebind Manager

Create an active GameObject, add `RebindManager`, and configure it in the Inspector.

Set **Input Action Asset** to the asset that should be used by the game. The manager enables the asset during startup and loads saved overrides. If no asset is assigned, the component uses the generated `PlayerInputControls` wrapper included in the package.

Only one manager should normally exist. `RebindManager` is a singleton and duplicate instances are ignored/destroyed by the singleton base class.

### 3. Configure persistence

The manager exposes three file location modes:

| Location | Result |
| --- | --- |
| `PersistentDataPath` | Stores overrides in `Application.persistentDataPath/<Relative Path>/<File Name>`. Recommended for player settings. |
| `DataPath` | Stores overrides below `Application.dataPath/<Relative Path>/<File Name>`. Usually useful for development tools, not shipped builds. |
| `Custom` | Stores overrides in the exact **Custom Path** plus **File Name**. The path must not be empty. |

The default file is `rebinds.json` under a `Configs` directory in the persistent data path. The manager creates the directory when saving.

The saved file contains Unity Input System binding overrides. Delete the file to restore all bindings to their defaults on the next load.

## Creating a rebind row

Add `RebindActionUI` to a GameObject in your settings UI for each binding the player can change.

In the Inspector:

1. Assign **Action** to an `InputActionReference`.
2. Select the target binding in the **Binding** popup.
3. Optionally assign **Binding Text** and **Action Label** TextMeshPro components.
4. Optionally assign a **Rebind Text** prompt or a **Rebind Overlay** GameObject.
5. Use **Display Options** to control how Unity formats the binding display.

The custom inspector writes the selected binding's stable GUID to `bindingId`. This is preferable to selecting a binding by array index because indices can change when bindings are edited.

At runtime, the row can be controlled with:

```csharp
using MLGWorks.RebindX.Runtime;
using UnityEngine;

public class RebindButton : MonoBehaviour
{
    [SerializeField] private RebindActionUI rebindRow;

    public void Rebind()
    {
        rebindRow.StartInteractiveRebind();
    }

    public void CancelRebind()
    {
        rebindRow.CancelInteractiveRebind();
    }

    public void ResetBinding()
    {
        rebindRow.ResetToDefault();
    }
}
```

`StartInteractiveRebind()` disables the target action while Unity waits for input, then restores its previous enabled state when the operation completes or is cancelled. Calling `CancelInteractiveRebind()` while idle is safe.

## Display and UI events

`RebindActionUI` supports optional UnityEvents for UI systems that do not use TextMeshPro directly:

- `updateBindingUIEvent(RebindActionUI, string displayString, string deviceLayoutName, string controlPath)` is invoked whenever the displayed binding changes.
- `startRebindEvent(RebindActionUI, RebindingOperation)` fires when waiting for input begins.
- `stopRebindEvent(RebindActionUI, RebindingOperation)` fires when the operation completes or is cancelled.

Example listener:

```csharp
private void OnBindingUpdated(RebindActionUI row, string display, string device, string controlPath)
{
    // Replace the text with an icon, localized label, or custom device glyph.
    Debug.Log($"Binding: {display} ({controlPath})");
}
```

When a composite is being rebound, RebindX clears the old composite overrides first. The display then contains only parts that have been freshly rebound, such as `Up: W`, instead of showing default paths that have not been selected in the current operation. Cancelling restores the previous overrides.

## Composite bindings

Create composites normally in the Input Actions editor. A `RebindActionUI` row should reference the composite header, not one of its individual parts, when the whole composite should be rebound:

```text
Move
  2D Vector
    Up       W
    Down     S
    Left     A
    Right    D
```

RebindX walks the composite parts in order. After a part is completed, the next part becomes active. Duplicate paths within the same composite and conflicts with bindings belonging to other actions are rejected and the operation is restarted for that part.

To expose individual composite parts as separate rows, reference each part binding instead. This is useful when the settings screen has separate controls for Up, Down, Left, and Right.

## Resetting and loading settings

Reset one row with:

```csharp
rebindRow.ResetToDefault();
```

For a composite header, this removes overrides from every part. To reset the entire asset, remove all binding overrides and save, or delete the persisted JSON file before the next load.

The manager also exposes:

```csharp
var manager = RebindManager.Instance;
manager.SaveRebinds();
manager.LoadRebinds();
```

Saving is normally performed automatically after a successful `RebindActionUI` operation. Explicit calls are useful for a dedicated **Apply** button or when binding overrides are changed directly through the Input System API.

## Using a different action asset at runtime

If the game loads its input asset dynamically, assign it after the manager exists:

```csharp
using UnityEngine.InputSystem;
using MLGWorks.RebindX.Runtime;

public void UseLoadedAsset(InputActionAsset loadedAsset)
{
    RebindManager.Instance.SetActionAsset(loadedAsset);
}
```

`SetActionAsset` disables the previously managed asset, enables the new one, and loads overrides from the configured file. Passing `null` is rejected.

If using the generated wrapper instead:

```csharp
var controls = new PlayerInputControls();
RebindManager.Instance.SetControls(controls);
```

The manager takes ownership of the wrapper and disposes the previously managed wrapper when replaced.

## Common pitfalls

- Do not assign a binding array index manually when the editor popup is available. Use the binding GUID generated by the inspector.
- Do not start a rebind on an enabled action yourself. RebindX handles the required disable/restore sequence.
- Do not use an empty Custom Path; the manager throws an `InvalidOperationException` when resolving it.
- A `RebindActionUI` must reference an action and a valid binding GUID. Invalid or missing references are ignored and logged rather than rebinding an unintended binding.
- Keep the manager alive while settings rows are active. `RebindActionUI` uses the manager's live action asset when one is available.
- Do not store player-specific override files in source control. Use `PersistentDataPath` for player settings.
- If a binding appears unchanged after editing the Input Actions asset, remove the saved override JSON while testing. Saved overrides intentionally take precedence over defaults.

## Testing

The package includes an Editor-only assembly named `MLGWorks.RebindX.Tests`. It tests runtime behavior without testing `MLGWorks.Utils` itself.

Run it from Unity's Test Runner with **EditMode** selected, or from a command line build with the test filter:

```text
MLGWorks.RebindX.Tests
```

The suite covers persistence, invalid configuration, binding resolution, normal and composite rebinding, cancellation, duplicate detection, display refreshes, and action lifecycle edge cases.

## Package layout

```text
MLGWorks.RebindX/
├── Runtime/       RebindManager and RebindActionUI
├── Editor/        Custom inspectors
├── Resources/     Optional generated PlayerInputControls asset/wrapper
├── Demo/          Sample scene
└── ../MLGWorks.RebindX.Tests/
                   RebindX-only EditMode tests
```
