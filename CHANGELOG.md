#   Changelog

##  2.0.0

*   Require Unity 6.3 LTS and Bootstrapper, Serialization and Shared 2.0.0; remove unused Utils dependency.
*   Remove game metadata and clock APIs while preserving the generic version 1 entry document and old JSON/XML reads.
*   Preserve unknown entries, serialize load/save/delete requests, retain dirty revisions during IO and await real force writes.
*   Validate/decode before Restore, block writes after failed loads and keep saved snapshots unchanged on write failure.
*   Add atomic default writes, asynchronous Windows final save, owned bootstrap/driver cleanup and async deletion APIs.
*   Make Editor preset targets and migration explicit; separate save-file deletion and PlayerPrefs reset.
*   Add migration, architecture/decision documentation and reliability tests. See Documentation~/migration-2.0.md for breaking changes.


## v1.0.0

* [Added] Added `SWeetJelly.Save` as an independent Unity package extracted from the previous Core module.
* [Added] Added save service contracts including `ISaveService`, `ISaveObject<T>`, `SaveKey`, and save storage abstractions.
* [Added] Added save data container model for storing keyed save entries.
* [Added] Added global save data support for project-level save state.
* [Added] Added Unity save driver support through `SaveBehaviour`.
* [Added] Added Bootstrapper integration through `SavePackageBootstrapperSo`.
* [Added] Added `SaveErrors` for package-owned save failure descriptions.
* [Changed] `Register`, `LoadAsync`, and `SaveAsync` now return `Result` values.
* [Changed] Moved save logic from the unclassified Core module into dedicated Save assemblies.
* [Changed] Separated Save into `Contract`, `Domain`, `App`, `Framework/Unity`, and `Framework/Bootstrapper` layers.
* [Changed] Delegated serialization and storage concerns to the Serialization package instead of keeping them directly inside Save.
