# Overview

NightShiftGame - a small Unity 2D horror game. Unity **2020.3.49f1**. Three
scenes: `Title.unity`, `School.unity`, then the harder `School2.unity`.

The game is **grid-based, not free-movement**. The player steps cell to cell on
a 1-unit grid, and walkability is baked from physics colliders rather than read
out of a tile map.

IMPORTANT: the old continuous-movement floor game (`NightShiftFloor1-3`,
`Scripts/Player/`, `Scripts/Environment/`, `DoorPlacer`, `SnapToSpawn` /
`UnstickFromOverlap`) is **not part of this build**. It is archived under
`_Archive/` for reference only. Do not go looking for those paths in `Assets/`.

# Project layout

    Assets/
      Scripts/                     flat, no subfolders, 28 classes
        GridMap.cs                 physics-baked walkability + BFS DistanceField
        PlayerController.cs        cell stepping, SnapTo, Animator feed
        GameManager.cs             flags, clock, checkpoints, spawns, HUD text
        Flags.cs                   has/set/save/load flag store
        Interactable.cs            abstract base for anything E activates
        LockedDoor.cs              + KeypadDoor.cs; door opens with a shrink/fade
        ItemPickup.cs  NoteInteractable.cs  Locker.cs  ExitDoor.cs
        FuseBox.cs  MonsterAI.cs  LightingController.cs
        WorldTint.cs  CameraFollow.cs  AudioManager.cs  AudioSynth.cs
        UIManager.cs  DialogueManager.cs  KeypadUI.cs  TitleScreen.cs
        PauseMenu.cs               Esc pause screen (freezes Time.timeScale)
        TitleMenu.cs  MenuMusic.cs  AutoDoor.cs  settings/volume, title loop, sensor door
        Janitor.cs                 hallway NPC, gives the curse story (NPCS_5-9)
        Jumpscare.cs               curse-student scare on the generator (NPCS_15-19)
      Editor/
        SchoolBuilder.cs           builds the whole game from coordinate tables
        NightShiftAutoBuild.cs     env-var hook that drives the builder headlessly
      Scenes/
        Title.unity  School.unity  School2.unity (harder remix)
      Art/
        Generated/                 procedural sprites + tiles the builder writes
        Funiture.png               30-slice furniture sheet          (UNUSED)
        STINIGHT_Palette.prefab    empty Palette, no tiles          (UNUSED)
        WallMaps/WallMap1F|2F.png  wall-layout reference             (UNUSED)
      IdleChar/
        NewCharacterNightShift 1.png  63 slices, player walk cycle (USED)
        NPCS.png                      20 slices, ghost + janitor + curse (USED)
      Animations/                  player + ghost clips, all referenced
      Prefabs/                     Locker.prefab, Monster.prefab (both generated)
      Shaders/AdditiveSprite.shader  "NightShift/AdditiveSprite"

Also unused: `Assets/Scenes/MapsNightShift.png` (15 slices; `_0`/`_1`/`_2` are
the archived floor-plan seeds per `_Archive/.../WallMapBuilder.cs`).

# Gameplay shape

Blackout at `y >= -36`, enter the lobby at `y >= -31` (front doors slam shut),
then lab key -> flash drive -> fuse + electrical key -> power -> escape.
A 15-minute clock runs from the moment you enter; retrying from a checkpoint
does **not** rewind it.

The hallway `Janitor` tells the curse story then vanishes (`janitorStory` flag),
revealing the cursed student by the electrical-room generator; approaching it
triggers a `NPCS_15-19` jumpscare.

Escaping School shows a Level Complete panel with Next level -> `School2.unity`.
That remix uses the same map with a 7-minute clock, faster monster, fewer
lockers and a stronger curse. The final win, any loss, and the pause panel all
offer Main Menu -> `Title.unity`.

# Invariants - breaking these breaks the game

- Layers **8/9/10/11 = Blocking / Interactable / Player / Monster**. These must
  agree between the constants in `SchoolBuilder.cs` and `ProjectSettings/TagManager.asset`.
- **Doors sit on Blocking** so a closed door stops the player. Every other
  interactable sits on **Interactable** so it never blocks walking.
- Anything that toggles a collider **must** call `GridMap.Rebuild()` after.
- `Horizontal` / `Vertical` drive movement; `E` / `Space` / `Return` interact.
- Player art: the ~200 px character frames are reimported at 200 PPU so one
  figure fills one cell; `PlayerPpu` (`148`) is the hero knob that matches the
  player to the curse student's ~1.37-unit silhouette (the janitor sheet's
  `cstudent_0` is ~137 px at its native 100 PPU). `NpcSheet` stays at `CharacterPpu`
  (`200`). Both live in `SchoolBuilder.cs`.
- `animCycleSeconds / (stepsPerCycle * stepTime)` sets the walk Animator speed
  so one footfall lands per cell (one 1.5 s clip spans `stepsPerCycle` cells,
  each 0.18 s). Idle runs at its own `idleAnimSpeed`. Forcing one full cycle
  per cell was ~8x too fast and read as a judder. Step motion is a plain linear
  lerp: easing each cell to a stop makes a run of steps lurch.

# Art status

Still procedural placeholders: floor, wall, furniture, locker, door, head,
sparkle, cone, glow, eye, vignette, vent.

Audio is procedural too: no clip assets ship, so `AudioSynth.cs` builds every
SFX and the ambience/chase loops in code and `AudioManager.Sync()` fills any null
clip at `Start`. Rebuilds sound identical (all synthesis is deterministic).

Real sheets **not yet wired in**: `Funiture.png`, `MapsNightShift.png`,
`WallMap1F|2F.png`, `STINIGHT_Palette.prefab`. `NPCS_5-9` (janitor loop) drive the
hallway `Janitor`, and `NPCS_15-19` (cursed student) drive the generator
`Jumpscare`; only `NPCS_10-14` stay unreferenced.

`Assets/Art/Generated/Player.png`, `Monster.png` and `Head.png` are now orphans:
no longer generated and no longer referenced. Harmless, delete if tidying.

# Build/verify commands

- Faithful reads: single bash call, `Get-Content <path> -Raw` or minimal short
  loops. **The `read` tool must NOT be trusted this session.**
- **No Unity CLI while the Editor holds the project open** (single-instance
  lock). Quit Unity first.
- Rebuild scenes headlessly - note **no `-quit`**, the `[InitializeOnLoad]` hook
  needs the update loop to tick:

      $env:NS_AUTOBUILD = "1"; $env:NS_STAGE = "build"

  `NS_STAGE`: `tmp` -> `ImportTmpEssentials`, `validate` -> `Validate()`,
  anything else -> `BuildAll`.
- **Always edit `SchoolBuilder.cs`, never the scene.** `BuildAll()` deletes and
  re-derives `School.unity`, so hand-painted scene edits are destroyed on the
  next rebuild.

# Conventions

- Keep the edits minimal; one token family per change. Sweep for stray tokens
  (`return\);`, `0Granted`, `perm the`, splintered identifiers) after every edit.
- bash has been faithful; short single-purpose commands only.
- Write ASCII in these docs. This console renders UTF-8 punctuation (em dash)
  as `?`, which looks like corruption but is not. See `RECOVERY.md` section 2.