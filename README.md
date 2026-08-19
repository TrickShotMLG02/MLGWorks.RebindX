# 🎮 MLGWorks RebindX

> A production-minded rebinding framework for Unity's Input System.

Build a complete controls menu with runtime rebinding, composite bindings, profiles, conflict handling, safe persistence, device-aware prompts, accessibility events, and replaceable integration services.

RebindX changes **binding overrides at runtime**. It does not edit your `.inputactions` asset or permanently change the default bindings.

## ✨ Features

| Feature | Benefit |
| --- | --- |
| 🔁 Interactive rebinding | Keyboard, mouse, gamepad, joystick, touchscreen, XR, and other Input System controls. |
| 🧩 Composite support | Rebind a whole composite or expose each part as its own row. |
| 👥 Binding profiles | Independent layouts for keyboard, controller, players, or accessibility. |
| 🛡️ Safe persistence | Versioned, asset-scoped JSON, atomic writes, corruption quarantine, and explicit results. |
| ⚔️ Conflict handling | Reject, allow, replace, or swap duplicate bindings. |
| 🎯 Rebind policies | Schemes, path filters, type filters, magnitude thresholds, retries, timeout, and device removal. |
| 🖼️ Device-aware UI | Normalized device kinds, glyph keys, prompts, and custom display providers. |
| ♿ Accessibility hooks | Status events for screen readers, audio, haptics, and custom prompts. |
| 🧱 Modular services | Replace persistence, paths, and asset ownership without rewriting the UI. |
| 🧪 Testable | Separate core, UI, EditMode, and PlayMode assemblies. |

## 🚀 Quick start

### Requirements

- Unity 2022.3 or newer.
- Unity Input System.
- TextMeshPro and Unity Localization when using `RebindActionUI`.
- Newtonsoft JSON is installed through UPM by this package.

### Install with UPM

In **Window > Package Manager**, select **+ > Add package from git URL** and enter:

```text
https://github.com/TrickShotMLG02/RebindX.git?path=/Assets/MLGWorks.RebindX
```

For a local checkout, choose **Add package from disk** and select this folder's `package.json`. The package ID is `com.mlgworks.rebindx`; see [package.json](package.json) for dependencies and version metadata.

### Import the demo sample

After installing RebindX:

1. Open **Window > Package Manager**.
2. Select **MLGWorks RebindX**.
3. Open the **Samples** section.
4. Click **Import** next to **RebindX Demo**.

Unity copies the sample into your project's `Assets/Samples/MLGWorks RebindX/` folder, where you can open and modify it without changing the package.

### 1. Create an Input Actions asset

Create or open an Input Actions asset and define maps, actions, bindings, control schemes, and composites as usual:

```text
Gameplay
├── Jump       Button       <Keyboard>/space
└── Move       Value        2D Vector composite
    ├── Up                  <Keyboard>/w
    ├── Down                <Keyboard>/s
    ├── Left                <Keyboard>/a
    └── Right               <Keyboard>/d
```

RebindX works with an ordinary `InputActionAsset` or a generated `PlayerInputControls` wrapper.

### 2. Add a `RebindManager`

Create an active GameObject and add `RebindManager`.

1. Assign the `InputActionAsset` used by the game.
2. Choose a persistence location.
3. Configure the file name and path if needed.
4. Optionally configure profiles.

The manager loads saved overrides during startup. It is a normal component, not a global singleton. Use explicit manager references when working with multiple players, assets, or profiles.

### 3. Add a `RebindActionUI` row

`RebindActionUI` is in the optional `MLGWorks.RebindX.UI` assembly. Add it to a settings-row GameObject and assign:

1. An `InputActionReference`.
2. The intended `RebindManager`.
3. A binding using the custom Inspector popup.
4. Optional TextMeshPro labels and a rebind prompt.
5. Optional overlay and UnityEvent listeners.

The Inspector stores the binding's stable GUID in `bindingId`, which is safer than relying on an array index.

```csharp
using MLGWorks.RebindX.Runtime;
using UnityEngine;

public sealed class RebindButtons : MonoBehaviour
{
    [SerializeField] private RebindActionUI row;

    public void Begin() => row.StartInteractiveRebind();
    public void Cancel() => row.CancelInteractiveRebind();
    public void Reset() => row.ResetToDefault();
}
```

## 🧱 Package architecture

```text
MLGWorks.RebindX/
├── package.json                         UPM metadata
├── MLGWorks.RebindX/Runtime/            Core runtime services
├── MLGWorks.RebindX/Resources/          Optional generated controls wrapper
├── Samples~/                            UPM-importable demo sample
├── MLGWorks.RebindX.UI/                 Optional UI and custom Inspector
├── MLGWorks.RebindX.Tests/              EditMode tests
├── MLGWorks.RebindX.PlayModeTests/      PlayMode tests
└── Documentation~/                      LaTeX source and PDF (UPM documentation)
```

Use `MLGWorks.RebindX` for the manager and services. Use `MLGWorks.RebindX.UI` for `RebindActionUI`, the custom binding selector, device display support, and UI events. The UI assembly requires TextMeshPro and Unity Localization; the core assembly does not require those UI packages.

## 💾 Persistence

The default JSON store writes a versioned envelope containing the format version, asset/profile identity, and Unity Input System overrides.

| Location | Recommended use |
| --- | --- |
| `PersistentDataPath` | Player settings in a shipped game. |
| `DataPath` | Development tools; may not be writable in a player build. |
| `Custom` | An explicit directory; it must not be empty. |

The default file is `rebinds.json` below a `Configs` directory in `Application.persistentDataPath`.

Writes use a temporary file before replacement. Malformed or legacy files are moved to timestamped `.corrupt-*.json` files. Saves belonging to another asset/profile are rejected instead of being silently applied.

```csharp
BindingOverrideResult result = manager.SaveRebinds();
if (!result.Succeeded)
    Debug.LogWarning($"Controls were not saved: {result.Code} - {result.Message}");
```

Result codes include `Success`, `NoData`, `InvalidAsset`, `InvalidPath`, `AssetMismatch`, `UnsupportedVersion`, `CorruptData`, and `IoFailure`. `NoData` is successful when no previous save exists.

```csharp
manager.SaveRebinds();
manager.LoadRebinds();
manager.ResetRebinds();
```

Successful UI rebinds save automatically. Use explicit calls for Apply/Discard workflows or direct Input System override changes.

## 👥 Binding profiles

Profiles provide stable IDs, display names, and independent override files:

```csharp
manager.CreateProfile("keyboard", "Keyboard and Mouse");
manager.CreateProfile("gamepad", "Controller");
manager.SwitchProfile("gamepad");
manager.RenameProfile("gamepad", "Xbox Controller");
```

Profile IDs are file-safe, metadata is persisted beside the binding file, switching saves the current profile before loading the next one, and the active profile cannot be deleted. Use one manager per independent asset/profile context.

## ⚔️ Duplicate bindings

Duplicates are rejected by default. Configure the behavior with `RebindOptions`:

| Resolution | Behavior |
| --- | --- |
| `Reject` | Keep the previous binding, report the conflict, and optionally retry. |
| `Allow` | Permit the duplicate control. |
| `Replace` | Keep the new control and remove the conflicting override. |
| `Swap` | Exchange the new control with the target's previous effective control. |

```csharp
row.rebindOptions = new RebindOptions
{
    bindingGroup = "Gamepad",
    expectedControlType = "Button",
    duplicateBindingPolicy = DuplicateBindingPolicy.Reject,
    duplicateBindingResolution = DuplicateBindingResolution.Replace,
    duplicateBindingScope = DuplicateBindingScope.EntireAsset,
    maximumDuplicateRetries = 2
};
```

Use `duplicateBindingEvent` for a user-facing conflict message and `duplicateResolutionEvent` for Replace/Swap feedback. Exclusive control-scheme groups are not treated as conflicts; ungrouped bindings are global. The legacy `duplicateBindingPolicy = Allow` setting takes precedence for compatibility.

## 🧩 Composite bindings

Reference a composite header when one row should rebind all parts:

```text
Move
└── 2D Vector
    ├── Up       W
    ├── Down     S
    ├── Left     A
    └── Right    D
```

RebindX clears stale composite overrides while the operation is in progress, so the display contains only freshly bound parts. Cancelling restores the previous overrides. Reference individual part bindings instead when you want separate rows for Up, Down, Left, and Right.

## 🎯 Rebind policy and lifecycle

`RebindOptions` supports:

- `bindingGroup` for control-scheme filtering.
- `controlPathsToMatch` and `controlPathsToExclude`.
- `cancelControlPath`, defaulting to `<Keyboard>/escape`.
- `expectedControlType`, such as `Button` or `Stick`.
- `minimumMagnitude` for ignoring weak input.
- `timeoutSeconds` for automatic cancellation.
- `cancelWhenDeviceIsRemoved`.
- Duplicate scope, policy, resolution, and retry count.

RebindX captures and restores the enabled state of the target action, action map, and asset. Cleanup also occurs on cancellation, timeout, device removal, disable, and destruction.

## 🖼️ Device-aware displays

The default `IDeviceBindingDisplayProvider` normalizes controls to `Unknown`, `Keyboard`, `Mouse`, `Gamepad`, `Joystick`, `Touchscreen`, `XR`, or `Pen`. It produces glyph keys such as:

```text
keyboard.enter
mouse.left_button
gamepad.button_south
```

Connect it to your icon or glyph system:

```csharp
row.deviceBindingDisplayEvent.AddListener((source, device, glyph, prompt) =>
{
    glyphView.SetGlyph(glyph);
    promptLabel.text = prompt;
});
```

Implement `IDeviceBindingDisplayProvider` and assign it to `row.bindingDisplayProvider` for a custom sprite atlas or platform naming scheme.

## ♿ Accessibility and UI events

Useful `RebindActionUI` events include:

- `updateBindingUIEvent` — display string, device layout, and control path.
- `startRebindEvent` / `stopRebindEvent` — operation lifecycle.
- `duplicateBindingEvent` — conflicting action and control path.
- `duplicateResolutionEvent` — Replace or Swap result.
- `timeoutRebindEvent` — timeout occurred.
- `rebindAccessibilityEvent` — status messages for screen readers/audio/haptics.
- `deviceBindingDisplayEvent` — device kind, glyph key, and prompt.

Localization is intentionally event-driven so you can connect your own localization tables and language workflow. The optional overlay is active only during an operation.

## 🔌 Integration points

### Cloud or custom persistence

Implement `IBindingOverrideStore` for cloud saves, encrypted files, platform profiles, or databases:

```csharp
public sealed class CloudOverrideStore : IBindingOverrideStore
{
    private string json;

    public BindingOverrideResult Save(InputActionAsset asset)
    {
        json = asset.SaveBindingOverridesAsJson();
        // Queue json for your authenticated backend.
        return BindingOverrideResult.Success("Queued for upload.");
    }

    public BindingOverrideResult Load(InputActionAsset asset)
    {
        if (string.IsNullOrEmpty(json))
            return BindingOverrideResult.NoData("No cloud data exists.");
        asset.LoadBindingOverridesFromJson(json);
        return BindingOverrideResult.Success("Loaded from cloud.");
    }

    public BindingOverrideResult Delete()
    {
        json = null;
        return BindingOverrideResult.Success("Cloud overrides deleted.");
    }
}
```

Assign a custom store through `RebindManager.OverrideStore` or `RebindActionUI.bindingOverrideService`. Keep network work asynchronous in your own layer and apply Input System changes on Unity's main thread.

### Custom paths and assets

Implement `IRebindPathProvider` for platform-specific paths. Always return stable, writable paths and validate any user-derived identifiers. Use `SetActionAsset(loadedAsset)` for dynamic assets; the previous asset is disabled, the new asset is enabled, and configured overrides are loaded. Use `SetControls(new PlayerInputControls())` for generated wrappers.

### Custom UI

Use only the core assembly if you do not want TextMeshPro/Localization. Build around `InputActionReference`, `RebindOptions`, `IBindingOverrideService`, `IBindingOverrideStore`, and Unity's interactive rebinding API.

## 🧪 Testing

The repository contains `MLGWorks.RebindX.Tests` for EditMode tests and `MLGWorks.RebindX.PlayModeTests` for PlayMode tests. Run them from **Window > General > Test Runner**.

Coverage includes persistence, profiles, corrupt data, asset mismatches, composites, conflict resolution, retries, cancellation, timeout, device lifecycle, display providers, and state restoration. Add project-specific PlayMode tests for actual UI navigation and target hardware because synthetic input does not reproduce every platform's native event path.

## 🧯 Troubleshooting

| Problem | Check |
| --- | --- |
| Package does not compile | Confirm Input System, Newtonsoft JSON, TextMeshPro, and Localization are installed as appropriate. |
| Saved binding does not appear | Check profile, path, binding GUID, and `BindingOverrideResult`; old overrides intentionally beat edited defaults. |
| Same control appears on several rows | Check duplicate policy, resolution, scope, and control-scheme groups. |
| Composite shows stale/default controls | Reference the composite header for a whole-composite rebind and start a fresh operation. |
| Rebind never completes | Check binding group, path filters, expected type, magnitude, device connection, and timeout. |
| Settings are lost after asset changes | RebindX rejects asset mismatches by design; provide Reset Controls or implement migration in a custom store. |

## 📚 API cheat sheet

| API | Purpose |
| --- | --- |
| `RebindManager` | Coordinates the configured asset, persistence, and profiles. |
| `RebindActionUI` | Interactive row for one binding or composite. |
| `RebindOptions` | Input filters and conflict/lifecycle policy. |
| `RebindProfile` | Stable profile ID and display name. |
| `IBindingOverrideService` | Save/load/reset service. |
| `IBindingOverrideStore` | Replaceable persistence backend. |
| `JsonBindingOverrideStore` | Versioned local JSON backend. |
| `InMemoryBindingOverrideStore` | File-free backend for tests and temporary contexts. |
| `IRebindPathProvider` | Replaceable path resolution. |
| `IInputActionAssetProvider` | Replaceable asset/wrapper ownership. |
| `IDeviceBindingDisplayProvider` | Device, glyph, and prompt mapping. |
| `BindingOverrideResult` | Explicit outcome with code and message. |

## 📄 Documentation and metadata

The `Documentation~/` folder contains the full LaTeX integration guide and generated PDF. It follows Unity's UPM documentation convention and is not imported as runtime project content. Package ID, version, supported Unity version, dependencies, and author information are in [package.json](package.json). See [CHANGELOG.md](CHANGELOG.md) for release history.

## 📜 License

RebindX is licensed under the [MIT License](LICENSE.md). Copyright © 2026 TrickShotMLG02.
