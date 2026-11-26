# Night Shift - Recovery Notes

Quick reference to the known failure modes and how to fix them without a
support session. All are already handled in code; this is the "what to re-run
/ what to nuke" cheat sheet.

## 1. Assembly copy failure from the Plastic SCM / Package Manager

    "Copying assembly from 'Temp/Unity.PlasticSCM.Editor.dll' to
    'Library/ScriptAssemblies/Unity.PlasticSCM.Editor.dll' failed"

(or any similar assembly-copy error.) Stale script-assembly cache, unrelated to
game code.

1. Quit Unity completely - it holds those files open.
2. Delete `Library\ScriptAssemblies` (whole folder) and `Temp\*.dll`.
3. Reopen the project; Unity rebuilds all assemblies from scratch (first open is
   slower).
4. If scene geometry or art looks wrong afterwards, re-run the builder (3).

## 2. "Scripts look corrupted" (splinter tokens), or odd `?` characters

Two separate false alarms:

- Observability paths sometimes splinter tokens in displayed file contents,
  making clean files look broken (stray `return);`, `perm the`, `0Granted`).
  Verify with a **byte-exact decode** before acting on it. Do not trust prose
  reads.
- This console renders non-ASCII UTF-8 punctuation (em dash, smart quotes) as
  `?`. That is a codepage artifact, **not** corruption. Keep these docs ASCII.

## 3. Wrong or missing geometry, doors or sprites after a change

The scene is generated - **never hand-edit it**.

1. Make sure you are **not in Play mode**.
2. Run: **Tools > Night Shift > Build All Scenes**

   `BuildAll()` deletes and re-derives `Title.unity`, `School.unity` and
   `School2.unity` from the coordinate tables in `SchoolBuilder.cs`, so re-running is safe. Headless
   equivalent is `NS_AUTOBUILD=1` with `NS_STAGE=build`, with Unity closed and
   **no `-quit`** (see `AGENTS.md`).

Useful console lines: `Painted N floor, M wall, K furniture tiles.`, and
`Loaded 63 player and 20 npc slices.` If the slice counts differ, a
hand-authored sheet is no longer sliced correctly.

## 4. Player walks through a closed door, or is stuck at a doorway

`GridMap` bakes walkability from physics colliders via `OverlapBox` against
`blockingMask` - not from tile data. So:

- Anything that opens or closes a collider must call `GridMap.Rebuild()`
  immediately afterwards, or the cell stays stale until the next rebuild.
- Doors belong on layer **8 (Blocking)**. Every other interactable belongs on
  layer **9 (Interactable)**, or it will block the player.
- Confirm those indices still agree between the constants in
  `SchoolBuilder.cs` and `ProjectSettings/TagManager.asset`.

## 5. Missing TMP fonts, or invisible UI text

Run **Tools > Night Shift > Import TMP Essentials** (headless:
`NS_STAGE=tmp`). Rebuilding the scenes rebuilds the canvas but will not
re-import the font asset.

## Archived

The old front-gate recovery path - auto-scanning transparent doorway runs and
force-creating a guaranteed `Door_Entrance` at the `FloorSpawn_Start` marker -
lives in `_Archive/Assets/Scripts/Environment/Editor/DoorPlacer.cs`. Its
`Tools > Night Shift > Auto-Place Doors` menu item is **not** in this build and
cannot be restored from there; that game is superseded by `School.unity`. See
that file only for the technique, not as a repair step.
