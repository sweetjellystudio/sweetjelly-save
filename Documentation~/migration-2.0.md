#   Migrating to Save 2.0

Requires Unity 6.3 LTS (`6000.3`) and Bootstrapper, Serialization and Shared 2.0.0. This is a breaking major release; the old root metadata API is removed.

##  API changes

*   Remove `GlobalSaveData.BuildId`, `LevelId`, `SavedGameTime`, `GameTime`, `LastExitTime`, `Initialise`, `SetTime` and `Flush` usage. Remove `SaveService.LevelId`, `GameTime`, `LastExitTime`, `UpdateTime` and constructor `nowProvider`. Put required game state inside your own `ISaveObject<T>` payload.
*   Replace `service.Delete()` with `await service.DeleteAsync(ct)` and check Result. Load successfully again before saving.
*   `Unregister(key)` only disconnects an object. To remove its persisted data, unregister then `await service.DeleteKeyAsync(key, ct)`; check Result. Other entries remain. Interface test doubles must implement both async deletion methods.
*   `SaveBehaviour.ForceSaveFromDriverAsync` returns `Task<Result>`. Await it and check failure before copying a preset or reporting success. Busy force requests queue a real write.
*   App callbacks and service use belong on one caller/Unity context. MarkDirty is explicit. Notification exceptions propagate after the operation commits; callbacks must handle their own failures. Cancellation remains an exception, while IO/capture/restore failures return Result.
*   `SaveObject<T>` accepts null for reference or nullable payloads. Value-type mismatches still throw.

##  Existing files and assets

Keep the generic JSON/XML `SavedData` wrapper, container version 1, `Entries`, `Key`, `TypeName` and `SerializedValue`. Removed root game metadata is ignored, not migrated to reserved keys. Reading does not rewrite the source. A later explicit save drops obsolete root fields and preserves unregistered entries and their payloads. Back up old files before deliberate migration if you still need discarded metadata. Unknown TypeName values are preserved without loading arbitrary types.

Bootstrapper and driver GUIDs and `saveFileName` remain stable. Newly created drivers use bootstrap settings; a borrowed unconstructed driver keeps Inspector settings. Duplicated active composition is rejected. Shutdown releases only owned resources and registration. Custom serializer injection remains possible; it owns durability unless wrapped in `AtomicSaveSerializer` with an appropriate `IPathProvider` and storage.

##  Editor tools and quitting

Select a Save bootstrap asset in Save Presets. In Play Mode also select its running driver. Preset creation/update awaits the actual save Result. Apply overwrites only the selected save. Enable Apply and Play only with an explicit valid start scene; modified scenes are handled before copying/opening. Opening the window does not migrate files. Legacy preset/save migration is explicit; legacy save copy retains its source and refuses an existing destination.

Remove Save operates on the selected bootstrap asset and only its current file. Reset All PlayerPrefs is a separate confirmed action. No first-found bootstrap asset or project-specific scene is chosen.

Windows normal quit awaits asynchronous final save and remains open on failure. Editor stop, OS kill and mobile suspension are outside that guarantee. Use explicit saves/checkpoints when such interruption matters.
