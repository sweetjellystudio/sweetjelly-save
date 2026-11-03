#   Save tests

Enable `com.sweetjelly.save` in the consumer project's `testables` and install Unity Test Framework. Run EditMode and PlayMode separately, filtered by `SWeetJelly.Save.Tests`.

EditMode covers generic keys, values, capture/restore, queued persistence, revisions, unknown entries, explicit deletion, malformed documents, decode-before-restore, failure/retry, legacy JSON/XML and atomic filesystem replacement. Temporary files use unique directories; bootstrap fixtures use unique names to avoid reading user saves. PlayMode covers the actual driver lifecycle and asynchronous force-save path.

Release verification also requires a consumer with only declared dependencies, actual sample/Editor interaction and the targeted Players. Record exact Editor patch, executed/passed/failed/skipped counts and fresh reports in the development host work item. A successful compile or zero tests is not a test pass.
