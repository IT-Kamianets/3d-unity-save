# 3d-unity-save

Local save files for Unity games on the IT Kamianets 3D Engine: one versioned JSON file per slot, written safely, with a clear answer when a save is missing, damaged or from a newer version.

### Logic

`SaveStore` belongs to one game (`new SaveStore("hippodrome")`) and keeps its slots as `<persistent data path>/<gameId>/<slot>.json`.

- The file is an **envelope** (`schemaVersion`, `gameId`, `slotId`, `savedAt`, `payloadVersion`, `payload`), described by [`3d-save-schema`](../3d-save-schema). The **payload** is whatever the game wants to store; this package never looks inside it.
- `Save(slot, payload, payloadVersion)` writes a temporary file and then swaps it in, so a crash cannot leave half a save.
- `Load<T>(slot, currentPayloadVersion, migrate)` returns a `SaveResult<T>` with a status:
  - `Ok`: the value is there (an older payload goes through your `migrate` function first).
  - `NotFound`: nothing saved yet, start fresh.
  - `Corrupt`: the file could not be parsed. It is **renamed aside** (`<slot>.corrupt-<time>.json`), never deleted, and the game starts fresh.
  - `UnsupportedVersion`: saved by a newer game. The file is left alone.
  - `IoError`: disk or permissions.
- `new SaveStore(gameId, root, keepBackup: true)` keeps the previous good save of a slot beside it as `<slot>.backup.json`; `LoadBackup<T>` reads it, so a game can offer "try the backup" when the main file is damaged. `Delete` removes both.
- Per-device **settings** (volumes, key bindings) stay in `EngineStorage` from `3d-unity-core`; this package is for game progress.

No game rules, no cloud, no accounts (`3d-unity-firebase` is the online route).

### Status

v0.1.1: `SaveStore` (`Save`, `Load`, `LoadBackup`, `Exists`, `BackupExists`, `Delete`, `PathFor`, `BackupPathFor`), `SaveResult<T>`, `SaveStatus`. Used by Temnorid Hippodrome, Escape, Detective and Master.

### Roadmap

- Edit-mode tests for the write, read and corrupt cases.
- A cloud adapter that sends the same envelope to `3d-unity-firebase`.
