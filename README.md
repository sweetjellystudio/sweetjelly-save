# SWeetJelly Save

`SWeetJelly Save` is a Unity save package for capturing registered save objects, serializing them through `SWeetJelly.Serialization`, and restoring them on load.

The package separates save state, save orchestration, Unity lifecycle handling, and Initialiser integration.

## Purpose

Use this package when project systems need a shared save flow with:

- typed save keys
- registered save objects
- load/save/delete lifecycle
- dirty-state tracking
- global save metadata such as level id, game time, and last exit time
- automatic Unity lifecycle save support through `SaveBehaviour`
- Initialiser integration through `SaveInitModule`

The package does not implement low-level file serialization itself. It depends on `SWeetJelly.Serialization` through `ISerializationService`.

## Package Structure

```text
Runtime/
  Contract/
    ISaveService.cs
    ISaveObject.cs
    SaveKey.cs
    SaveErrors.cs

  Domain/
    GlobalSaveData.cs
    SavedDataContainer.cs
    SavedDataEntry.cs
    SaveObject.cs
    PrimitiveSaveObject.cs
    SimpleBoolSave.cs
    SimpleFloatSave.cs
    SimpleIntSave.cs
    SimpleStringSave.cs

  App/
    SaveService.cs

  Framework/
    SaveBehaviour.cs

  InitialiserIntegration/
    SaveInitModule.cs

Editor/
  SaveActionsMenu.cs
  SaveEditorUtility.cs
  SavePresetsWindow.cs
```

## Assemblies

### `SWeetJelly.Save.Contract`

Defines the public save API.

- `ISaveService`
- `ISaveObject`
- `ISaveObject<T>`
- `SaveKey`
- `SaveErrors`

Project gameplay code should usually depend on this assembly only.

### `SWeetJelly.Save.Domain`

Contains serializable save data and reusable save object implementations.

- `GlobalSaveData`
- `SavedDataContainer`
- `SavedDataEntry`
- `SaveObject<T>`
- `PrimitiveSaveObject<T>`
- `SimpleBoolSave`
- `SimpleFloatSave`
- `SimpleIntSave`
- `SimpleStringSave`

### `SWeetJelly.Save.App`

Contains the main save flow implementation.

- `SaveService`

### `SWeetJelly.Save.Framework`

Contains Unity lifecycle integration.

- `SaveBehaviour`

### `SWeetJelly.Save.InitialiserIntegration`

Connects Save to the Initialiser package.

- `SaveInitModule`

Note: the current assembly name is `SWeetJelly.Save.InitialiserIntegraion`.

## Core Concepts

### `SaveKey`

`SaveKey` identifies a save object.

```csharp
SaveKey key = "player.coins";
```

Keys are compared with ordinal string equality. Empty or whitespace keys are rejected by save object implementations.

### `ISaveObject<T>`

Save objects expose how to capture and restore one piece of state.

```csharp
public interface ISaveObject<T> : ISaveObject
{
    T Capture();
    void Restore(T value);
}
```

The save service stores values by `SaveKey`.

### Primitive Save Objects

The package includes simple save object implementations for primitive values.

```csharp
var coins = new SimpleIntSave("player.coins", defaultValue: 0);

coins.Set(100);
int currentCoins = coins.Value;
```

Available primitive objects:

- `SimpleBoolSave`
- `SimpleFloatSave`
- `SimpleIntSave`
- `SimpleStringSave`

### `GlobalSaveData`

`GlobalSaveData` is the root save model.

It stores:

- `BuildId`
- `LevelId`
- `SavedData`
- `SavedGameTime`
- `LastExitTime`

`SaveService` captures registered save objects into `SavedData`.

## Basic Usage

Most projects should resolve `ISaveService` from Initialiser.

```csharp
using SWeetJelly.Initialiser.Framework;
using SWeetJelly.Save.Contract;

var saveService = InitialiserBehaviour.Services.Resolve<ISaveService>();
```

Then register save objects before or after load.

```csharp
var coins = new SimpleIntSave("player.coins", 0);

var registerResult = saveService.Register(coins);

if (registerResult.IsFailure)
    return;
```

Load save data:

```csharp
var loadResult = await saveService.LoadAsync();

if (loadResult.IsFailure)
    return;
```

Change data and mark dirty:

```csharp
coins.Set(250);
saveService.MarkDirty();
```

Save:

```csharp
var saveResult = await saveService.SaveAsync();

if (saveResult.IsFailure)
    return;
```

Force save even when not dirty:

```csharp
var forceSaveResult = await saveService.SaveAsync(forceSave: true);

if (forceSaveResult.IsFailure)
    return;
```

Delete:

```csharp
saveService.Delete();
```

## Save Flow

```text
Register save objects
  -> LoadAsync()
     -> load GlobalSaveData from persistent data path if it exists
     -> create a new GlobalSaveData if no save exists
     -> cache saved entries
     -> restore registered save objects
     -> raise Loaded

MarkDirty()
  -> SaveAsync()
     -> skip if not dirty and not forced
     -> capture registered save objects
     -> serialize entries
     -> flush game time and exit time
     -> write GlobalSaveData through ISerializationService
     -> raise Saved
```

If a save object is registered after `LoadAsync()`, `SaveService` immediately restores it when a matching saved entry exists.

`Register`, `LoadAsync`, and `SaveAsync` return `Result` values. Concrete save failures are described through `SaveErrors`.

## Events

`ISaveService` exposes lifecycle events.

```csharp
saveService.Loaded += OnLoaded;
saveService.Saved += OnSaved;
saveService.Deleted += OnDeleted;
```

## Game Time

`SaveService.UpdateTime(float time)` updates the current runtime time used by `GlobalSaveData`.

`SaveBehaviour` calls this with `Time.time` before load/save operations.

When saving, `GlobalSaveData.Flush(...)` accumulates elapsed game time and stores `LastExitTime`.

## Unity Lifecycle Driver

`SaveBehaviour` is a Unity `MonoBehaviour` driver for save lifecycle behavior.

It can:

- begin loading with `BeginLoad()`
- force save from driver code
- run periodic auto-save
- save on application pause
- save on application quit
- cancel lifetime work on destroy

`SaveBehaviour` must be constructed with a `SaveService`.

```csharp
var behaviour = gameObject.AddComponent<SaveBehaviour>();
behaviour.Construct(saveService);
behaviour.Configure(
    useAutoSave: true,
    autoSaveInterval: 30f,
    saveOnApplicationPause: true,
    saveOnApplicationQuit: true);
behaviour.BeginLoad();
```

## Initialiser Integration

`SaveInitModule` wires Save into the Initialiser system.

On install it:

1. resolves `ISerializationService`
2. creates `SaveService`
3. creates or finds `SaveBehaviour` on the holder object
4. configures and constructs the behaviour
5. registers `ISaveService`
6. begins loading

Typical module order:

```text
Serialization
Save
Project modules that register save objects
```

Serialization should be installed before Save because Save resolves `ISerializationService`.

## Custom Save Object

Use `SaveObject<T>` when a value needs custom capture/restore logic.

```csharp
public sealed class PlayerPositionSave : SaveObject<Vector3>
{
    private readonly Transform target;

    public PlayerPositionSave(SaveKey key, Transform target)
        : base(key)
    {
        this.target = target;
    }

    public override Vector3 Capture()
    {
        return target.position;
    }

    public override void Restore(Vector3 value)
    {
        target.position = value;
    }
}
```

Register it with the save service:

```csharp
var result = saveService.Register(new PlayerPositionSave("player.position", playerTransform));
```

## Design Notes

- `SaveService` owns save orchestration, dirty state, and registered save objects.
- `ISerializationService` owns serialization and file persistence.
- `SaveBehaviour` owns Unity lifecycle triggers.
- Save operations return `Result` and use `SaveErrors` for package-owned failure descriptions.
- Gameplay code should depend on `ISaveService` and `ISaveObject<T>`, not concrete framework types.
- Use stable, namespaced save keys such as `player.coins`, `settings.audio.volume`, or `level.progress`.

## Current Status

This package currently provides:

- save service contract
- save object contracts
- primitive save objects
- global save data model
- save/load/delete service flow
- Unity lifecycle save behaviour
- Initialiser integration module
