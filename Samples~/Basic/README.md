# Save Dev Sample

This sample shows the consumer-side flow for `com.sweetjelly.save`.

Save is different from Serialization:

- Serialization converts objects to JSON/XML and reads/writes bytes.
- Save registers save objects, captures/restores their values by `SaveKey`, tracks dirty state, and manages load/save/delete flow.
- Save depends on `ISerializationService`, so the sample installs Serialization before Save.

## Setup

Open `Sample Scene` and enter Play Mode.

Use the on-screen buttons to change registered save objects, mark the save dirty, save, load, and delete save data.
