# CHANGELOG

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
