---
status: accepted
date: 2026-11-01
---

#   Keep generic entries while removing game metadata in Save 2.0

The real consumer owns progress schema, revision, UTC timestamps and stage state inside keyed payloads. Root level IDs and play-time bookkeeping duplicate that responsibility. Save 2.0 removes those root fields and clock APIs, rather than deprecating them again in this major release. Unity 6.3 LTS and the 2.0 package dependencies form the new support floor.

We retain `GlobalSaveData`, `SavedDataContainer.Version = 1`, `Entries` and their property names. Flattening or renaming the generic document would force unrelated file and Editor tooling migrations. Old JSON/XML ignores removed root metadata on reading; reading alone does not rewrite files. Subsequent explicit saves omit it. We rejected a legacy metadata bag and synthetic reserved keys because they would perpetuate game-specific ownership in a generic package. Unknown consumer entries remain intact.

Persistence operations are serialized and writes use independent capture snapshots with revision tracking. Atomic replacement belongs to the default Unity composition, while App continues to depend on serialization contracts. `DeleteAsync` replaces synchronous deletion; explicit key deletion requires unregistering first. Whole-document decode precedes callbacks, but arbitrary Restore side effects cannot be rolled back. Normal Windows quit delays until final save succeeds; blocking the Unity thread was rejected because async continuations need that thread.
