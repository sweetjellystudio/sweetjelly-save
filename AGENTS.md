#   Save package instructions

Use the installed `unity-package` coordinator for package work. Before changing public APIs, state, dependencies or lifecycle, read [ARCHITECTURE.md][architecture] and the relevant [decision][decision].

*   Keep game progress, clocks, stage IDs and conflict policies in consumer payloads. Preserve the generic entry document and unknown keys.
*   Preserve bootstrapper/driver script GUIDs and serialized `saveFileName`. Unity assets must be changed through the Editor.
*   Treat caller callbacks as user code. Validate and deserialize the complete load before restoring any object. Failed restoration blocks writes until a successful load.
*   Preserve IO sequencing, revision tracking and atomic replacement. Driver shutdown cancels its own work and releases only resources it owns.
*   Package tests belong in `Tests`; import and exercise `Samples~/Basic` in a consumer. Validate on Unity 6.3 LTS, including an independent consumer and the release's supported Players. Record exact versions and results in the development host, not this policy file.
*   Read [migration][migration] before changing compatibility. Keep setup in [README.md][readme]; keep shared host workflow in the host instructions.

[architecture]: ARCHITECTURE.md
[decision]: docs/adr/0001-generic-save-v2.md
[migration]: Documentation~/migration-2.0.md
[readme]: README.md
