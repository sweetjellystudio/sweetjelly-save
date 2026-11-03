#   SWeetJelly Save

Key-based persistence for Unity 6.3 LTS. Capture and restore consumer-owned payloads through `ISaveObject<T>`, with queued load/save/delete operations, dirty tracking, atomic default file replacement and Bootstrapper lifecycle integration.

##  Installation

Use public HTTPS Git URLs pinned to published version tags in the consuming project's `Packages/manifest.json`. Add every custom dependency explicitly; UPM package dependencies contain exact versions, not Git URLs. After all listed tags have been published:

```json
{
  "dependencies": {
    "com.sweetjelly.save": "https://github.com/sweetjellystudio/sweetjelly-save.git#v2.0.0",
    "com.sweetjelly.bootstrapper": "https://github.com/sweetjellystudio/sweetjelly-bootstrapper.git#v2.0.0",
    "com.sweetjelly.serialization": "https://github.com/sweetjellystudio/sweetjelly-serialization.git#v2.0.0",
    "com.sweetjelly.shared": "https://github.com/sweetjellystudio/sweetjelly-shared.git#v2.0.0"
  }
}
```

This is the intended published setup, not a claim that unpublished tags are available. See [migration][migration] before upgrading from 1.x. License: [MIT][license].

##  Usage

Add Serialization and Save bootstrap assets to a Bootstrapper profile, with Serialization ordered first. Resolve `ISaveService` at the consumer boundary and register its objects. For direct composition, inject an `ISerializationService` into `SaveService`; the caller owns its storage policy. The default bootstrap wraps the selected codecs in `AtomicSaveSerializer` and `AtomicFileStorage`.

```csharp
var coins = new SimpleIntSave("player.coins", 0);
var registered = service.Register(coins);
if (registered.IsFailure) throw new InvalidOperationException(registered.Error.ToString());
var loaded = await service.LoadAsync(ct);
if (loaded.IsFailure) return;
coins.Set(coins.Value + 10);
service.MarkDirty();
var saved = await service.SaveAsync(ct);
if (saved.IsFailure) { /* report or retry */ }
service.Unregister(coins.Key);
```

`SimpleIntSave` is in `SWeetJelly.Save.Domain`; `ISaveService` is in `SWeetJelly.Save.Contract`. Use a custom `SaveObject<T>` for structured game data. The consumer owns schemas, revisions, timestamps and game progress. Register/restore/capture and dirty mutations run on one Unity/main context. Callbacks must handle their own exceptions. Every registered payload is decoded before Restore starts; arbitrary callback side effects are not transactional.

`SaveAsync(true, ct)` queues an actual write even while another write runs. Dirty changes during a write remain dirty. Unregistered keys are retained; unregistering does not erase data. For permanent key removal, unregister then await `DeleteKeyAsync(key, ct)`. `DeleteAsync(ct)` removes the whole file and makes the service unloaded until another successful load. Check every Result; cancellation throws `OperationCanceledException`.

The default Windows quit handler waits asynchronously for a successful final save. A failure keeps the Player running. Editor stop, OS termination and mobile suspension have different guarantees; use explicit checkpoints for those cases.

##  Samples, tools and design

Import **Save Sample** from Package Manager. It demonstrates registration, load, dirty changes, save, force save and deletion. Disabling the consumer releases its registrations. See [sample guide][sample].

Open **SWeetJelly > Editor > Save Presets** and explicitly select a bootstrap asset, plus its running driver in Play Mode. Apply and Apply and Play are distinct choices. Legacy migration is explicit. Remove Save targets only the selected asset's current file; PlayerPrefs reset is separate.

See [architecture][architecture], [migration][migration] and [tests][tests] for ownership, failure boundaries and verification. This package does not supply game-specific conflict resolution or cloud storage.

[migration]: Documentation~/migration-2.0.md
[license]: LICENSE.md
[sample]: Samples~/Basic/README.md
[architecture]: ARCHITECTURE.md
[tests]: Tests/README.md
