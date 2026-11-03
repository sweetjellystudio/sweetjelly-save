#   Save architecture

Save owns generic key-based persistence. The consumer owns payload schemas, game progress, play time, revisions and merge policy. DDD responsibility boundaries and Clean Architecture dependency direction guide this separation; the package's folders are not separate bounded contexts.

##  Responsibilities and dependencies

*   `Contract` owns `ISaveService`, typed capture/restore ports, keys and save errors. It references Shared.Result, without Unity.
*   `Domain` owns the document DTOs and reusable save objects, referencing Contract. `GlobalSaveData.SavedData`, version 1 containers and entry names remain stable for existing tools and files.
*   `App` coordinates registration, full-document validation, capture/restore, dirty revisions and a single asynchronous IO gate. It references Domain, Contract, Serialization.Contract and Shared.Result, without Unity or concrete codecs.
*   `Framework.Unity` owns atomic file storage, the codec-preserving `AtomicSaveSerializer` adapter and `SaveBehaviour` lifecycle. It references App and the relevant contracts; file policy stays outside App.
*   `Framework.Bootstrapper` is the composition root. It resolves serialization codecs, selects the atomic storage adapter and registers one service. It also references Serialization.Framework.Unity for the persistent-data path provider. A newly created driver receives bootstrap settings; an unconstructed borrowed driver keeps its own settings. An already constructed driver or existing registration is rejected. Shutdown unregisters only the same service and destroys only an owned component.
*   `Editor` owns explicit target selection and preset tools; the sample owns its screen and registration lifetime.

These dependencies are declared in the corresponding asmdefs. Bootstrapper is an explicit integration dependency, not a lookup used by App. No Utils dependency remains.

##  State and failure boundaries

Call from one Unity/main context, including registration, callbacks and dirty changes. IO requests are serialized; this is not a general multithreaded object store. Mark changes dirty explicitly. A write clears dirty only for the revision it captured. A forced request awaits its own write. Unknown entries survive capture; unregistering disconnects an object. `DeleteKeyAsync` requires unregistering first and persists removal while retaining other keys; `DeleteAsync` removes the whole document and requires loading again.

Load validates shape, supported container version and unique nonempty keys, then decodes every registered payload before calling Restore. TypeName is informational and never used to resolve or instantiate arbitrary saved types. A restore callback can have irreversible external side effects, so callback failure marks the service unloaded but cannot roll back a consumer's arbitrary changes. Cancellation propagates as `OperationCanceledException`; operational failures use Result. Notifications run after the IO gate releases. A notification exception propagates after state is committed; subscribers must handle their own exceptions.

The default composition serializes a snapshot, writes and flushes a unique sibling temporary file, then replaces the destination. Failure/cancellation before replacement leaves the old file. Custom `SaveService` serializer injections own their storage guarantees; use `AtomicSaveSerializer` to retain this policy with custom text codecs. JSON and XML are supported by that adapter. Atomic replacement is not a power-loss durability guarantee on every filesystem.

##  Unity lifetime

BeginLoad is idempotent per construction. Shutdown cancels driver-owned work, stops autosave and removes the quit subscription. Normal Windows Player quit is delayed until a successful asynchronous force save, without blocking the main thread. Failure keeps the Player open for retry. OS termination, mobile suspension and Editor Play Mode stopping cannot offer the same guarantee. Pause requests are best effort. Shutdown itself cancels, rather than promising a final write.

See [migration][migration] for consumer changes and [ADR 0001][decision] for the approved compatibility trade-off.

[migration]: Documentation~/migration-2.0.md
[decision]: docs/adr/0001-generic-save-v2.md
