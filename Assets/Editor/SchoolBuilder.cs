using System;
using System.Collections.Generic;
using System.IO;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;

// Builds the whole school map and scene from the coordinate tables in the
// UNSAVED guide. Everything here is derived, so the builder is re-runnable:
// if a coordinate is wrong, fix the table and run it again.
//
// Menu: Tools > Night Shift > Import TMP Essentials
//        Tools > Night Shift > Build All Scenes
// Batch: -executeMethod NightShiftBuilder.BuildAll
//
// EditorUtility.DisplayDialog blocks forever under -batchmode, so every
// user-facing dialog is gated on Application.isBatchMode.
public static class SchoolBuilder
{
    // ---------------- Layout tables (from the guide) ----------------

    public struct Rect
    {
        public int x0, x1, y0, y1;     // inclusive, always normalised lo..hi
        public Rect(int x0, int x1, int y0, int y1)
        {
            // The tables are written top-down (y0 = -2, y1 = -11), so flip
            // them here instead of trusting every row to be ascending.
            this.x0 = Mathf.Min(x0, x1);
            this.x1 = Mathf.Max(x0, x1);
            this.y0 = Mathf.Min(y0, y1);
            this.y1 = Mathf.Max(y0, y1);
        }
        public bool Contains(int x, int y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
    }

    // The rooms. Floor = these rects + the doorway cells below.
    static readonly Rect[] Rooms =
    {
        new Rect( 2, 13,  -2, -11),   // Room 4-A
        new Rect(16, 27,  -2, -11),   // Faculty room
        new Rect(30, 49,  -2, -11),   // Library
        new Rect( 2, 49, -13, -15),   // Hallway
        new Rect( 2, 12, -17, -24),   // Guard office
        new Rect(14, 29, -17, -31),   // Main lobby
        new Rect(32, 38, -17, -23),   // Electrical room
        new Rect(31, 38, -25, -30),   // Storage
        new Rect(41, 49, -17, -30),   // Capstone lab
        new Rect(14, 29, -33, -40),   // Campus grounds
        new Rect( 2, 12, -26, -31),   // Comfort room (below the guard office)
    };

    // Open doorways: floor, no wall, no door object.
    static readonly Vector2Int[] OpenDoorways =
    {
        new Vector2Int( 7, -12),   // Room 4-A
        new Vector2Int(38, -12),   // Library
        new Vector2Int( 6, -16),   // Guard office (up)
        new Vector2Int(13, -20),   // Guard office (side)
        new Vector2Int(13, -23),   // Guard office (second lobby link)
        new Vector2Int(30, -27),   // Storage
    };

    // Wide lobby opening, (19,-16) to (24,-16).
    static readonly Rect LobbyOpening = new Rect(19, 24, -16, -16);

    // Cells that must be floor because a door object sits on them.
    static readonly Vector2Int[] LockedDoorwayCells =
    {
        new Vector2Int(21, -12),   // Faculty door
        new Vector2Int(45, -16),   // Lab door
        new Vector2Int(35, -16),   // Electrical door
        new Vector2Int(13, -27),   // Comfort room door
    };

    // The main lobby doors span (20,-32)..(23,-32) as one 4-wide object.
    const int MainDoorX0 = 20, MainDoorX1 = 23, MainDoorY = -32;

    // Locker cells (guide Step 7).
    static readonly Vector2Int[] Lockers =
    {
        // Room 4-A
        new Vector2Int(1,-8), new Vector2Int(1,-9),
        // Faculty room
        new Vector2Int(28,-9),
        // Library
        new Vector2Int(50,-6), new Vector2Int(50,-7),
        // Hallway
        new Vector2Int(14,-12), new Vector2Int(15,-12),
        new Vector2Int(33,-12), new Vector2Int(34,-12),
        new Vector2Int(43,-12), new Vector2Int(44,-12),
        new Vector2Int(9,-16),
        // Guard office
        new Vector2Int(1,-22), new Vector2Int(1,-23),
        // Main lobby (both lanes of the reception island)
        new Vector2Int(13,-24), new Vector2Int(13,-28), new Vector2Int(13,-29),
        new Vector2Int(30,-20), new Vector2Int(30,-21),
        // Storage
        new Vector2Int(39,-26), new Vector2Int(39,-27),
        // Capstone lab
        new Vector2Int(50,-20), new Vector2Int(50,-21), new Vector2Int(50,-27),
    };

    // Fewer hiding spots on the second, harder level.
    static readonly Vector2Int[] HardLockers =
    {
        new Vector2Int(1,-8), new Vector2Int(28,-9),
        new Vector2Int(14,-12), new Vector2Int(33,-12),
        new Vector2Int(9,-16), new Vector2Int(1,-22),
        new Vector2Int(13,-24), new Vector2Int(30,-20), new Vector2Int(39,-26),
        new Vector2Int(50,-27),
    };

    struct PropPlacement
    {
        public string sprite;
        public int x0, x1, y0, y1, w, h, step;
        public bool solid;
        public PropPlacement(string sprite, int x0, int x1, int y0, int y1,
                             int w, int h, bool solid, int step = 1)
        {
            this.sprite = sprite;
            this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1;
            this.w = w; this.h = h; this.solid = solid; this.step = step;
        }
    }

    // Solid props sit on IsFurniture cells, which already block through the
    // furniture tilemap. Everything else is decoration with no collider.
    static readonly PropPlacement[] Props =
    {
        // Library bookshelves
        new PropPlacement("Librarty room books shelves", 32, 32, -8, -3, 1, 1, true),
        new PropPlacement("Librarty room books shelves", 35, 35, -8, -3, 1, 1, true),
        new PropPlacement("Librarty room books shelves", 38, 38, -8, -3, 1, 1, true),
        new PropPlacement("Librarty room books shelves", 41, 41, -8, -3, 1, 1, true),
        new PropPlacement("Librarty room books shelves", 44, 44, -8, -3, 1, 1, true),
        new PropPlacement("Librarty room books shelves", 47, 47, -8, -3, 1, 1, true),
        // Classroom desks and chairs
        new PropPlacement("Table", 3, 12, -5, -5, 1, 1, true, 3),
        new PropPlacement("Table", 3, 12, -8, -8, 1, 1, true, 3),
        new PropPlacement("StudentChair", 3, 12, -8, -8, 1, 1, true, 3),
        new PropPlacement("Studentchair2", 17, 26, -4, -4, 1, 1, true, 3),
        new PropPlacement("StudentChair3", 17, 26, -8, -8, 1, 1, true, 3),
        new PropPlacement("Student chair4", 12, 12, -5, -5, 1, 1, true),
        new PropPlacement("Student chair4", 23, 23, -8, -8, 1, 1, true),
        new PropPlacement("OfficeChair", 26, 26, -4, -4, 1, 1, true),
        new PropPlacement("OfficeChair", 5, 5, -21, -21, 1, 1, true),
        new PropPlacement("OfficeDrawer", 26, 26, -8, -8, 1, 1, true),
        new PropPlacement("chair", 7, 9, -21, -21, 1, 1, true, 2),
        new PropPlacement("Hallwaydrawer", 10, 10, -14, -14, 1, 1, true),
        new PropPlacement("labatory", 42, 42, -27, -27, 1, 1, true),
        new PropPlacement("labatory", 45, 45, -19, -19, 1, 1, true),
        new PropPlacement("crlabatory", 4, 4, -29, -29, 1, 1, true),
        new PropPlacement("crlabatory", 10, 10, -29, -29, 1, 1, true),
        new PropPlacement("bush", 14, 18, -36, -35, 1, 1, true),
        new PropPlacement("bush", 26, 29, -36, -35, 1, 1, true),
        // Wall signs and fixtures
        new PropPlacement("Blackboard", 22, 22, -1, -1, 4, 2, false),
        new PropPlacement("Bulettinboard", 16, 16, -12, -12, 2, 2, false),
        new PropPlacement("Window", 4, 4, -12, -12, 2, 2, false),
        new PropPlacement("Miniwindow", 24, 24, -12, -12, 2, 1, false),
        new PropPlacement("Miniwindow2", 40, 40, -12, -12, 1, 1, false),
        new PropPlacement("Clock", 29, 29, -12, -12, 1, 1, false),
        new PropPlacement("Exitsign", 19, 19, -32, -32, 1, 1, false),
        new PropPlacement("crsign", 7, 7, -25, -25, 1, 1, false),
        new PropPlacement("sign", 30, 30, -23, -23, 1, 1, false),
        new PropPlacement("wetfloor", 45, 45, -31, -31, 1, 1, false),
        new PropPlacement("fireextuguis", 40, 40, -20, -20, 1, 1, false),
        new PropPlacement("vent", 39, 39, -22, -22, 1, 1, false),
        new PropPlacement("vent2", 13, 13, -25, -25, 1, 1, false),
        new PropPlacement("camera", 16, 16, -16, -16, 1, 1, false),
        new PropPlacement("curtain", 50, 50, -10, -10, 2, 2, false),
        new PropPlacement("Dispenser", 48, 48, -12, -12, 1, 1, false),
        // Small floor props
        new PropPlacement("Flowerpot", 17, 17, -30, -30, 1, 1, false),
        new PropPlacement("Flowerpot", 28, 28, -18, -18, 1, 1, false),
        new PropPlacement("Flowerpo", 24, 24, -34, -34, 1, 1, false),
        new PropPlacement("Flowerpo", 26, 26, -39, -39, 1, 1, false),
        new PropPlacement("sealdbin", 49, 49, -29, -29, 1, 1, false),
        new PropPlacement("sealdbin", 11, 11, -30, -30, 1, 1, false),
        new PropPlacement("trashbin", 5, 5, -14, -14, 1, 1, false),
        new PropPlacement("trashcan", 48, 48, -10, -10, 1, 1, false),
        new PropPlacement("bench", 16, 16, -38, -38, 2, 1, false),
    };

    // All 45 authored slices of PropSheet: 9 baked surfaces/doors, the
    // whiteboard on the interactable, and the 35 names in Props above.
    static readonly string[] PropNames =
    {
        "Librarty room books shelves", "Blackboard", "WhiteBoard", "OfficeChair",
        "Bulettinboard", "OfficeDrawer", "StudentChair", "Studentchair2",
        "StudentChair3", "Student chair4", "Flowerpot", "Steeldoor", "Window",
        "Table", "Hallwaydrawer", "Miniwindow", "Miniwindow2", "Dispenser",
        "Flowerpo", "crlabatory", "labatory", "sealdbin", "trashbin",
        "fireextuguis", "trashcan", "camera", "vent", "Clock", "Exitsign",
        "bench", "chair", "curtain", "crsign", "sign", "wetfloor", "crfloor",
        "floor2", "floor3", "floor4", "floor5", "wall", "vent2", "grass",
        "bush", "floor6"
    };

    // ---------------- Paths and colours ----------------

    const string GenDir = "Assets/Art/Generated";
    const string TileDir = "Assets/Art/Generated/Tiles";
    const string SchoolScene = "Assets/Scenes/School.unity";
    const string HardScene = "Assets/Scenes/School2.unity";
    const string TitleScene = "Assets/Scenes/Title.unity";
    const string MonsterPrefabPath = "Assets/Prefabs/Monster.prefab";
    const string LockerPrefabPath = "Assets/Prefabs/Locker.prefab";
    const string AdditiveShader = "NightShift/AdditiveSprite";

    // Hand-authored character art. These are the real sheets in the project,
    // not generated: the builder only loads and rescales them.
    const string PlayerSheet = "Assets/IdleChar/NewCharacterNightShift 1.png";
    const string NpcSheet = "Assets/IdleChar/NPCS.png";
    // The janitor and the curse student come from a second, named-slice sheet.
    // It is left at its native 100 PPU (no Reimport) so the figures keep the
    // size the artist drew them at.
    const string JanitorSheet = "Assets/IdleChar/Pixel Horror Janitor Sprite Sheet.png";
    const string PropSheet = "Assets/Art/applinces.png";
    const string PlayerAnimPath = "Assets/Animations/_Playeranimato.controller";
    const string GhostAnimPath = "Assets/Animations/GhostAnimator.controller";

    // Environment tiles. The floor/door art is a hand-authored sliced
    // screenshot sheet; each selected slice is baked into one square cell tile.
    // Walls are procedural instead (see WallBand below).
    const string WallFloorSheet = "Assets/Scenes/Screenshot 2026-08-26 195726.png";
    const int TilePx = 64;

    // The character frames are ~200px tall and a grid cell is one world unit,
    // so 200 PPU lands a full figure inside a single cell.
    // The player is separately reimported at PlayerPpu so the hero matches the
    // curse student's ~1.37-unit silhouette (the sheet's ~203px figures, the
    // curse student's ~137px figures at the janitor sheet's native 100 PPU).
    const float CharacterPpu = 200f;
    const float PlayerPpu = 148f;

    static readonly Color FurnitureColor = new Color(0.42f, 0.33f, 0.26f);
    static readonly Color LockerColor = new Color(0.30f, 0.42f, 0.60f);
    static readonly Color SparkleColor = new Color(1f, 0.95f, 0.55f, 0.85f);
    // Thin top-down pixel walls: a beveled beige band on the room-facing
    // edges of a blocking cell, void behind. Band is 0.25 of a cell.
    static readonly Color WallOutline = new Color(0.12f, 0.11f, 0.10f);
    static readonly Color WallBody = new Color(0.55f, 0.52f, 0.47f);
    static readonly Color WallLip = new Color(0.80f, 0.78f, 0.73f);
    const int WallBandPx = 16;
    // Muted grass green for the campus grounds, with a deterministic speckle.
    static readonly Color FieldGreen = new Color(0.30f, 0.44f, 0.26f);
    const float FieldJitter = 0.06f;
    const int Blocking = 8, InteractableLayer = 9, PlayerLayer = 10, MonsterLayer = 11;
    const int WorldRootOrder = 0, WallOrder = 1, ObjectOrder = 2, LightOrder = 3;

    static bool batch => Application.isBatchMode;

    // ================= Entry points =================

    [MenuItem("Tools/Night Shift/Import TMP Essentials")]
    public static void ImportTmpEssentials()
    {
        if (TmpReady()) { Log("TMP essentials already present."); return; }
        string pkg = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";
        if (!File.Exists(pkg))
        {
            pkg = Path.Combine("Library/PackageCache/com.unity.textmeshpro@3.0.6",
                               "Package Resources/TMP Essential Resources.unitypackage");
        }
        if (!File.Exists(pkg))
        {
            Log("FATAL: cannot find TMP Essential Resources .unitypackage.");
            Fail();
            return;
        }
        Log("Importing TMP essentials from " + pkg);
        AssetDatabase.ImportPackage(pkg, false);
        AssetDatabase.Refresh();
        Log("Import requested. Re-run this or Build All Scenes in a new process to confirm.");
    }

    [MenuItem("Tools/Night Shift/Validate Build")]
    public static void Validate()
    {
        int pass = 0, fail = 0;
        Action<string, bool> check = (label, ok) =>
        {
            if (ok) { pass++; Log("PASS  " + label); }
            else { fail++; Log("FAIL  " + label); }
        };

        // ---- build settings ----
        var scenes = EditorBuildSettings.scenes;
        check("build settings has 3 scenes", scenes.Length == 3);
        check("Title is scene 0", scenes.Length > 0 && scenes[0].path == TitleScene);
        check("School is scene 1", scenes.Length > 1 && scenes[1].path == SchoolScene);
        check("School2 is scene 2", scenes.Length > 2 && scenes[2].path == HardScene);
        check("all scenes enabled", scenes.Length == 3 && scenes[0].enabled &&
              scenes[1].enabled && scenes[2].enabled);

        // ---- school scene ----
        var scene = EditorSceneManager.OpenScene(SchoolScene, OpenSceneMode.Single);

        var gm = Object.FindObjectOfType<GameManager>();
        check("GameManager present", gm != null);
        if (gm != null)
        {
            check("GameManager.player wired", gm.player != null);
            check("GameManager.monsterPrefab wired", gm.monsterPrefab != null);
        }

        var pc = Object.FindObjectOfType<PlayerController>();
        check("PlayerController present", pc != null);
        if (pc != null)
        {
            check("player body renderer", pc.body != null);
            check("player PPU matches curse student scale",
                  pc.body != null && pc.body.sprite != null &&
                  Mathf.Approximately(pc.body.sprite.pixelsPerUnit, PlayerPpu));
            var curseForScale = Object.FindObjectOfType<Jumpscare>();
            check("player height matches curse student",
                  pc.body != null && pc.body.sprite != null &&
                  curseForScale != null && curseForScale.body != null &&
                  curseForScale.body.sprite != null &&
                  Mathf.Abs(pc.body.sprite.bounds.size.y -
                            curseForScale.body.sprite.bounds.size.y) < 0.05f);
            check("player flashlight", pc.flashlight != null);
            check("player animator", pc.animator != null);
            check("player animator controller",
                  pc.animator != null && pc.animator.runtimeAnimatorController != null);
            var cone = pc.flashlight.GetComponent<SpriteRenderer>();
            check("flashlight is additive",
                  cone != null && cone.sharedMaterial != null &&
                  cone.sharedMaterial.shader != null &&
                  cone.sharedMaterial.shader.name == AdditiveShader);
        }

        var grid = Object.FindObjectOfType<GridMap>();
        check("GridMap present", grid != null);
        check("GridMap blocks layer 8", grid != null && (grid.blockingMask.value & (1 << Blocking)) != 0);

        var lightCtl = Object.FindObjectOfType<LightingController>();
        check("LightingController present", lightCtl != null);
        check("LightingController.flashlight wired", lightCtl != null && lightCtl.flashlight != null);
        check("LightingController.glow wired", lightCtl != null && lightCtl.glow != null);

        var audio = Object.FindObjectOfType<AudioManager>();
        check("AudioManager present", audio != null);
        check("AudioManager has 3 sources",
              audio != null && audio.ambienceSource != null && audio.chaseSource != null &&
              audio.sfxSource != null);
        var audioProbe = AudioSynth.Step();
        check("audio synth builds clips", audioProbe != null && audioProbe.samples > 0);
        if (audioProbe != null) Object.DestroyImmediate(audioProbe);

        var dialogue = Object.FindObjectOfType<DialogueManager>();
        check("DialogueManager panel + text",
              dialogue != null && dialogue.panel != null && dialogue.text != null);

        var keypad = Object.FindObjectOfType<KeypadUI>();
        check("KeypadUI panel + input + status",
              keypad != null && keypad.panel != null && keypad.input != null && keypad.status != null);

        var ui = Object.FindObjectOfType<UIManager>();
        check("UIManager fully wired",
              ui != null && ui.clockText != null && ui.objectiveText != null && ui.toastText != null &&
              ui.healthText != null && ui.keysText != null &&
              ui.redVignette != null && ui.hidingOverlay != null && ui.endPanel != null &&
              ui.endTitle != null && ui.endBody != null && ui.retryButton != null &&
              ui.continueButton != null && ui.menuButton != null);

        check("WorldTint present", Object.FindObjectOfType<WorldTint>() != null);
        check("additive shader found", Shader.Find(AdditiveShader) != null);

        var jumpscare = Object.FindObjectOfType<Jumpscare>();
        check("curse student present", jumpscare != null);
        check("curse student body + frames",
              jumpscare != null && jumpscare.body != null && jumpscare.body.sprite != null &&
              jumpscare.idleFrames != null && jumpscare.idleFrames.Length == 9 &&
              jumpscare.scareFrames != null && jumpscare.scareFrames.Length == 1);
        check("curse student gated on story + open door",
              jumpscare != null && jumpscare.requiresFlag == "janitorStory" &&
              jumpscare.alsoRequiresFlag == "elecOpen");
        check("curse student cell beside the fuse box", IsFloor(38, -19));

        var janitor = Object.FindObjectOfType<Janitor>();
        check("janitor present", janitor != null);
        check("janitor body + frames",
              janitor != null && janitor.body != null && janitor.body.sprite != null &&
              janitor.idleFrames != null && janitor.idleFrames.Length == 4 &&
              janitor.cleanFrames != null && janitor.cleanFrames.Length == 8 &&
              janitor.dissolveFrames != null && janitor.dissolveFrames.Length == 4 &&
              janitor.mopFrames != null && janitor.mopFrames.Length == 10);
        check("janitor cell is floor", IsFloor(7, -28));
        check("scare overlay wired", ui != null && ui.scareOverlay != null && ui.scareImage != null);

        var pauseMenu = Object.FindObjectOfType<PauseMenu>();
        check("pause menu present", pauseMenu != null);
        check("pause panel + buttons wired",
              pauseMenu != null && pauseMenu.panel != null &&
              pauseMenu.panel.transform.Find("ResumeButton") != null &&
              pauseMenu.panel.transform.Find("RestartButton") != null &&
              pauseMenu.panel.transform.Find("QuitButton") != null &&
              pauseMenu.panel.transform.Find("MenuButton") != null);
        check("end panel continue + menu buttons wired",
              ui != null && ui.endPanel != null &&
              ui.endPanel.transform.Find("ContinueButton") != null &&
              ui.endPanel.transform.Find("MenuButton") != null);

        // interactables: 4 notes + 4 pickups + 3 key/door interactables + fuse
        // box + main doors + the janitor NPC. The comfort-room door is an
        // AutoDoor (not an Interactable), and Locker also derives from
        // Interactable, so both are counted separately.
        int lockerCount = Object.FindObjectsOfType<Locker>().Length;
        var allInteractables = Object.FindObjectsOfType<Interactable>();
        int storyCount = 0;
        foreach (var it in allInteractables) if (!(it is Locker)) storyCount++;
        check("14 story interactables (" + storyCount + ")", storyCount == 14);
        int withoutCollider = 0;
        foreach (var it in allInteractables)
            if (it.GetComponent<Collider2D>() == null) withoutCollider++;
        check("every interactable has a collider", withoutCollider == 0);

        check("4 note interactables", Object.FindObjectsOfType<NoteInteractable>().Length == 4);
        check("4 item pickups", Object.FindObjectsOfType<ItemPickup>().Length == 4);
        check("faculty keypad code 0417",
              Object.FindObjectOfType<KeypadDoor>() != null &&
              Object.FindObjectOfType<KeypadDoor>().code == "0417");
        check("fuse box wired", Object.FindObjectOfType<FuseBox>() != null);
        check("main exit door wired", Object.FindObjectOfType<ExitDoor>() != null);
        var hud = Object.FindObjectOfType<UIManager>();
        check("interaction prompt wired", hud != null && hud.promptText != null);
        check("keys HUD wired", hud != null && hud.keysText != null);

        // lockers
        check("24 lockers (" + lockerCount + ")", lockerCount == Lockers.Length);
        check("no stray locker at the origin", GameObject.Find("Locker") == null);

        int sheetSprites = 0;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(PropSheet))
            if (obj is Sprite) sheetSprites++;
        check("prop sheet loaded (" + sheetSprites + " sprites)", sheetSprites >= 45);

        int sceneProps = 0, solidPropsOffFurniture = 0, misalignedProps = 0;
        foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>())
        {
            if (!sr.name.StartsWith("Prop_")) continue;
            sceneProps++;
            if (sr.sprite == null) { misalignedProps++; continue; }
            // Compare the visible centre, not the (0,0) pivot position.
            Vector3 visual = sr.transform.position +
                (Vector3)(sr.sprite.bounds.center * sr.transform.lossyScale.x);
            string nm = sr.name.Substring(5);
            int iy = nm.LastIndexOf('_');
            int ix = nm.LastIndexOf('_', iy - 1);
            string sprite = nm.Substring(0, ix);
            int px = int.Parse(nm.Substring(ix + 1, iy - ix - 1));
            int py = int.Parse(nm.Substring(iy + 1));
            Vector3 expected = GridMap.CellToWorld(new Vector2Int(px, py));
            bool found = false;
            foreach (var p in Props)
            {
                if (p.sprite != sprite || px < p.x0 || px > p.x1 || py < p.y0 || py > p.y1 ||
                    (px - p.x0) % p.step != 0) continue;
                expected += new Vector3((p.w - 1) * 0.5f, (p.h - 1) * 0.5f, 0f);
                found = true;
                break;
            }
            if (!found || Vector3.Distance(visual, expected) > 0.01f) misalignedProps++;
            if (sr.gameObject.layer == Blocking && !IsFurniture(px, py))
                solidPropsOffFurniture++;
        }
        check("prop sprites placed (" + sceneProps + ")", sceneProps == 111);
        check("prop sprites centred on cells", misalignedProps == 0);
        check("solid props sit on furniture cells", solidPropsOffFurniture == 0);

        int deskBases = 0, deskWithCollider = 0, misalignedDesks = 0;
        foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>())
        {
            if (!sr.name.StartsWith("Desk_")) continue;
            deskBases++;
            if (sr.GetComponent<Collider2D>() != null) deskWithCollider++;
            if (sr.sprite == null) { misalignedDesks++; continue; }
            Vector3 visual = sr.transform.position +
                (Vector3)(sr.sprite.bounds.center * sr.transform.lossyScale.x);
            string nm = sr.name.Substring(5);
            int iy = nm.LastIndexOf('_');
            int ix = nm.LastIndexOf('_', iy - 1);
            int px = int.Parse(nm.Substring(ix + 1, iy - ix - 1));
            int py = int.Parse(nm.Substring(iy + 1));
            if (Vector3.Distance(visual, GridMap.CellToWorld(new Vector2Int(px, py))) > 0.01f)
                misalignedDesks++;
        }
        check("desk bases placed (" + deskBases + ")", deskBases == 7);
        check("desk bases are decoration only", deskWithCollider == 0);
        check("desk bases centred on cells", misalignedDesks == 0);

        // Blocking wiring: closed doors must stop the player, open ones must not.
        // GridMap.Rebuild is a plain physics bake, so it can be run in the editor.
        int doorsWrongLayer = 0;
        foreach (var d in Object.FindObjectsOfType<LockedDoor>())
            if (d.gameObject.layer != Blocking) doorsWrongLayer++;
        foreach (var d in Object.FindObjectsOfType<AutoDoor>())
            if (d.gameObject.layer != Blocking) doorsWrongLayer++;
        foreach (var d in Object.FindObjectsOfType<ExitDoor>())
            if (d.gameObject.layer != Blocking) doorsWrongLayer++;
        check("all doors are on the Blocking layer", doorsWrongLayer == 0);

        int labKeyDoors = 0;
        foreach (var d in Object.FindObjectsOfType<LockedDoor>())
            if (d.requiredFlag == "labKey") labKeyDoors++;
        check("lab door needs the lab key", labKeyDoors == 1);
        int elecKeyDoors = 0;
        foreach (var d in Object.FindObjectsOfType<LockedDoor>())
            if (d.requiredFlag == "elecKey") elecKeyDoors++;
        check("electrical door needs the electrical key", elecKeyDoors == 1);

        int lockersWrongLayer = 0;
        foreach (var l in Object.FindObjectsOfType<Locker>())
            if (l.gameObject.layer != Blocking) lockersWrongLayer++;
        check("all lockers are on the Blocking layer", lockersWrongLayer == 0);

        int looseWrongLayer = 0;
        foreach (var it in allInteractables)
        {
            if (it is Locker || it is LockedDoor || it is ExitDoor) continue;
            if (it.gameObject.layer != InteractableLayer) looseWrongLayer++;
        }
        check("notes and pickups are on the Interactable layer", looseWrongLayer == 0);

        if (grid != null)
        {
            grid.Rebuild();
            check("closed faculty door blocks (21,-12)", !grid.IsWalkable(new Vector2Int(21, -12)));
            check("closed lab door blocks (45,-16)", !grid.IsWalkable(new Vector2Int(45, -16)));
            check("closed electrical door blocks (35,-16)", !grid.IsWalkable(new Vector2Int(35, -16)));
            bool mainShut = true;
            for (int x = MainDoorX0; x <= MainDoorX1; x++)
                if (!grid.IsWalkable(new Vector2Int(x, MainDoorY))) mainShut = false;
            check("main lobby doors start open", !mainShut);
            check("player start is walkable", grid.IsWalkable(new Vector2Int(21, -38)));
            check("open doorway (7,-12) is walkable", grid.IsWalkable(new Vector2Int(7, -12)));
            check("room 4-A interior is walkable", grid.IsWalkable(new Vector2Int(6, -6)));
            // Regression guard: a plain wall/furniture painted cell must never be
            // walkable. GridMap now ORs the painted tile data into its bake (it
            // is always readable, even in headless batch), so this assert holds
            // everywhere the tilemaps do.
            int wallLeaks = 0;
            for (int x = GridX0; x <= GridX1; x++)
                for (int y = GridY0; y <= GridY1; y++)
                    if (IsWall(x, y) && grid.IsWalkable(new Vector2Int(x, y))) wallLeaks++;
            if (wallLeaks > 0)
            {
                for (int x = GridX0; x <= GridX1; x++)
                    for (int y = GridY0; y <= GridY1; y++)
                        if (IsWall(x, y) && grid.IsWalkable(new Vector2Int(x, y)))
                            Log("WALL LEAK at " + x + "," + y);
            }
            check("no painted wall cell is walkable", wallLeaks == 0);
        }
        // Tilemaps: GetUsedTilesCount() is not populated in batch mode, so the
        // painted cell count is read back out of the saved scene file instead.
        int expectedFloor = 0, expectedWall = 0, expectedFurniture = 0;
        for (int x = GridX0; x <= GridX1; x++)
        {
            for (int y = GridY0; y <= GridY1; y++)
            {
                if (!IsWall(x, y)) expectedFloor++;
                else expectedWall++;
                if (IsFurniture(x, y)) expectedFurniture++;
            }
        }
        string yaml = File.ReadAllText(SchoolScene);
        int painted = CountOccurrences(yaml, "first: {x: ");
        check("painted cells match the tables (" + painted + ")",
              painted == expectedFloor + expectedWall + expectedFurniture);
        check("no locker or door cell is painted over",
              expectedFurniture == 107 && expectedFloor == 1349);

        // spawn cells must be walkable (floor, no furniture, no locker)
        Vector2Int start = new Vector2Int(21, -38);
        check("player start is floor", IsFloor(start.x, start.y));
        check("player start is clear of furniture", !IsFurniture(start.x, start.y));
        check("player start has no locker", Array.IndexOf(Lockers, start) < 0);

        // every locker cell must sit inside the painted grid
        int lockersOutside = 0;
        foreach (var cell in Lockers)
            if (cell.x < GridX0 || cell.x > GridX1 || cell.y < GridY0 || cell.y > GridY1)
                lockersOutside++;
        check("all locker cells inside the grid", lockersOutside == 0);

        // ---- harder second level ----
        EditorSceneManager.OpenScene(HardScene, OpenSceneMode.Single);
        var hardGm = Object.FindObjectOfType<GameManager>();
        check("School2 has GameManager", hardGm != null);
        if (hardGm != null)
        {
            check("School2 clock is shorter", hardGm.timeLimit < 900f);
            check("School2 spawns faster", hardGm.randomSpawnGap.x < 40f);
            check("School2 monster is faster", hardGm.monsterChaseStepTime < 0.215f);
            check("School2 is the final level", string.IsNullOrEmpty(hardGm.nextScene));
        }
        check("School2 has fewer lockers",
              Object.FindObjectsOfType<Locker>().Length == HardLockers.Length);
        check("School2 has an exit", Object.FindObjectOfType<ExitDoor>() != null);
        var hardUi = Object.FindObjectOfType<UIManager>();
        check("School2 end panel wired", hardUi != null &&
              hardUi.continueButton != null && hardUi.menuButton != null);

        // ---- title scene ----
        var title = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Single);
        check("title canvas", Object.FindObjectOfType<Canvas>() != null);
        var startButton = GameObject.Find("StartButton");
        check("title start button exists", startButton != null);
        if (startButton != null)
        {
            var button = startButton.GetComponent<Button>();
            check("start button has a persistent listener",
                  button != null && button.onClick.GetPersistentEventCount() > 0);
        }
        check("title has an EventSystem",
              Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null);

        foreach (var name in new[] { "SettingsButton", "ExitButton" })
        {
            var go = GameObject.Find(name);
            check("title " + name + " exists", go != null);
            if (go != null)
            {
                var button = go.GetComponent<Button>();
                check(name + " has a persistent listener",
                      button != null && button.onClick.GetPersistentEventCount() > 0);
            }
        }
        var titleMenu = Object.FindObjectOfType<TitleMenu>();
        check("title menu controller wired",
              titleMenu != null && titleMenu.settingsPanel != null &&
              titleMenu.volumeSlider != null);
        var titleMusic = Object.FindObjectOfType<MenuMusic>();
        check("title menu music wired",
              titleMusic != null && titleMusic.source != null);

        // leave the editor on a clean empty scene
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Log(string.Format("VALIDATION: {0} passed, {1} failed.", pass, fail));
        if (fail > 0) Fail();
    }

    [MenuItem("Tools/Night Shift/Build All Scenes")]
    public static void BuildAll()
    {
        if (!TmpReady())
        {
            Log("FATAL: TMP essentials missing. Run Tools > Night Shift > Import TMP Essentials first.");
            Fail();
            return;
        }

        EnsureLayers();
        EnsureFolders();
        var assets = BuildArtAssets();

        BuildSchoolScene(assets, SchoolScene, false);
        BuildSchoolScene(assets, HardScene, true);
        BuildTitleScene();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(TitleScene, true),
            new EditorBuildSettingsScene(SchoolScene, true),
            new EditorBuildSettingsScene(HardScene, true),
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Log("BUILD OK: Title.unity, School.unity, School2.unity, build settings written.");
        Validate();
        if (!batch) EditorUtility.DisplayDialog("Night Shift", "Build complete.", "OK");
    }

    // Called by -executeMethod so a failed run exits non-zero.
    static void Fail()
    {
        if (batch) EditorApplication.Exit(1);
    }

    // ================= Tmp / layers / folders =================

    static bool TmpReady() =>
        File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset");

    static void EnsureLayers()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) { Log("FATAL: no TagManager.asset"); Fail(); return; }
        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("layers");
        string[] names = { "Blocking", "Interactable", "Player", "Monster" };
        for (int i = 0; i < names.Length; i++)
        {
            var element = layers.GetArrayElementAtIndex(8 + i);
            if (element.stringValue != names[i]) element.stringValue = names[i];
        }
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Log("Layers ensured: 8=Blocking 9=Interactable 10=Player 11=Monster");
    }

    static void EnsureFolders()
    {
        foreach (var f in new[] { GenDir, TileDir, "Assets/Prefabs", "Assets/Scenes" })
        {
            if (!AssetDatabase.IsValidFolder(f))
            {
                int slash = f.LastIndexOf('/');
                AssetDatabase.CreateFolder(f.Substring(0, slash), f.Substring(slash + 1));
            }
        }
    }

    // ================= Art assets =================

    class Art
    {
        public Dictionary<string, Sprite> props;
        public Dictionary<string, Sprite> itemIcons;
        public Sprite furniture, locker, player, monster,
                     sparkle, cone, glow, eye, vignette, vent, white,
                     janitorBody, curseBody,
                     lobbyFloor, roomFloor, crFloor, field,
                     doorEntrance, doorRoomA, doorRoomB, doorCR;
        // Janitor + curse-student animation frames, by name from JanitorSheet.
        public Sprite[] janitorIdle, janitorClean, janitorDissolve, janitorMop;
        public Sprite[] curseFrames;
        public Sprite jscareFace;
        public Sprite[] wallSprites;
        public Tile furnitureTile,
                    lobbyFloorTile, roomFloorTile, crFloorTile, fieldTile;
        public Tile[] wallTiles;
        public RuntimeAnimatorController playerAnim, ghostAnim;
    }

    static readonly string[] NoteIconRows =
    {
        "................",
        "..KKKKKKKKKKKK..",
        "..KWWWWWWWWWWK..",
        "..KWGGGGGGGGWK..",
        "..KWWWWWWWWWWK..",
        "..KWGGGGGGGWWK..",
        "..KWWWWWWWWWWK..",
        "..KWGGGGGGGGWK..",
        "..KWWWWWWWWWWK..",
        "..KWGGGGGGGWWK..",
        "..KWWWWWWWWWWK..",
        "..KWGGGGGGGGWK..",
        "..KWWWWWWWWWWK..",
        "..KKKKKKKKKKKK..",
        "................",
        "................"
    };

    static readonly string[] LogIconRows =
    {
        "................",
        "..KKKKKKKKKKKK..",
        "..KNNNNNNNNNNK..",
        "..KNKKNNNNNNNK..",
        "..KNKKNNNNNNNK..",
        "..KNKKNNNNNNNK..",
        "..KNKKWWWWWWWK..",
        "..KNKKNNNNNNNK..",
        "..KNKKWWWWWWWK..",
        "..KNKKNNNNNNNK..",
        "..KNKKWWWWWWWK..",
        "..KNKKNNNNNNNK..",
        "..KNNNNNNNNNNK..",
        "..KKKKKKKKKKKK..",
        "................",
        "................"
    };

    static readonly string[] KeyIconRows =
    {
        "................",
        ".....KKKKKK.....",
        "....KYYYYYYK....",
        "....KY....YK....",
        "....KY....YK....",
        ".....KYYYYK.....",
        ".......YY.......",
        ".......YY.......",
        ".......YY.......",
        ".......YY.......",
        ".......YYKK.....",
        ".......YYYYK....",
        ".......YYKK.....",
        ".......YY.......",
        "................",
        "................"
    };

    static readonly string[] FuseIconRows =
    {
        "................",
        "....KKKKKKKK....",
        "...KSSSSSSSSK...",
        "..KSSSSSSSSSSK..",
        "..KSSRRRRRRSSK..",
        "..KSSRRRRRRSSK..",
        "..KSSSSSSSSSSK..",
        "..KSSSSSSSSSSK..",
        "..KSSRRRRRRSSK..",
        "..KSSRRRRRRSSK..",
        "..KSSSSSSSSSSK..",
        "...KSSSSSSSSK...",
        "....KKKKKKKK....",
        "................",
        "................",
        "................"
    };

    static readonly string[] DriveIconRows =
    {
        "................",
        ".....KKKKKK.....",
        "....KSSSSSSK....",
        "....KSSSSSSK....",
        ".....KKKKKK.....",
        "....KKKKKKKK....",
        "....KNNNNNNK....",
        "....KNNBNNNK....",
        "....KNNNNNNK....",
        "....KNNNNNNK....",
        "....KNNNNNNK....",
        "....KNNNNNNK....",
        "....KKKKKKKK....",
        "................",
        "................",
        "................"
    };

    static Art BuildArtAssets()
    {
        var a = new Art();
        var sh = Shader.Find(AdditiveShader);
        if (sh == null) { Log("FATAL: additive shader not found: " + AdditiveShader); Fail(); }

        a.white = Solid("White", 16, 16, Color.white, 16f);
        a.furniture = Solid("Furniture", 16, 16, FurnitureColor, 16f);
        a.locker = Solid("Locker", 16, 16, LockerColor, 16f);
        a.sparkle = Solid("Sparkle", 8, 8, SparkleColor, 8f);
        a.itemIcons = new Dictionary<string, Sprite>
        {
            { "note", Icon("ItemNote", NoteIconRows, 32f) },
            { "log", Icon("ItemLog", LogIconRows, 32f) },
            { "key", Icon("ItemKey", KeyIconRows, 32f) },
            { "fuse", Icon("ItemFuse", FuseIconRows, 32f) },
            { "drive", Icon("ItemDrive", DriveIconRows, 32f) }
        };
        foreach (var pair in a.itemIcons)
            if (pair.Value == null) { Log("FATAL: item icon missing: " + pair.Key); Fail(); }
        a.cone = Cone("FlashlightCone", 128, 128);
        a.glow = Radial("Glow", 128);
        a.eye = Radial("MonsterEye", 64);
        a.vignette = Vignette("RedVignette", 128);
        a.vent = Solid("LockerVent", 8, 8, new Color(0.10f, 0.10f, 0.12f, 0.95f), 8f);

        // Floor art stays on the old screenshot sheet and the generated field.
        // Walls, interior doors and props come from the newly imported sheet.
        a.lobbyFloor   = BakeSlice("floors_1", "LobbyFloor", TilePx, TilePx);
        a.roomFloor    = BakeSlice("floors_2", "RoomFloor", TilePx, TilePx);
        a.crFloor      = BakeSlice("floors_3", "CrFloor", TilePx, TilePx);
        a.field        = Field("Field", TilePx);
        a.wallSprites  = new Sprite[16];
        var wallSprite = BakeSlice(PropSheet, "wall", "WallReal", TilePx, TilePx);
        for (int m = 0; m < 16; m++) a.wallSprites[m] = wallSprite;
        a.doorEntrance = BakeSlice("doors_5",  "DoorEntrance", TilePx, TilePx);
        var steelDoor = BakeSlice(PropSheet, "Steeldoor", "SteelDoor", TilePx, TilePx);
        a.doorRoomA    = steelDoor;
        a.doorRoomB    = steelDoor;
        a.doorCR       = steelDoor;
        a.props = SliceMap(PropSheet);
        foreach (var name in PropNames)
            if (!a.props.ContainsKey(name) || a.props[name] == null)
            {
                Log("FATAL: missing prop slice " + name + " in " + PropSheet);
                Fail();
            }

        // Characters come from the hand-authored sheets, not from Solid().
        // The janitor sheet is left at its own 100 PPU (no Reimport).
        Reimport(PlayerSheet, PlayerPpu);
        Reimport(NpcSheet, CharacterPpu);
        var playerSlices = Slices(PlayerSheet);
        var npcSlices = Slices(NpcSheet);
        var janitorSlices = SliceMap(JanitorSheet);
        if (playerSlices.Length == 0 || npcSlices.Length < 16 || janitorSlices.Count == 0)
        {
            Log("FATAL: character sheets are not sliced: " + PlayerSheet + " / " +
                NpcSheet + " / " + JanitorSheet);
            Fail();
        }

        // Frame 0 of each sheet is the standing pose, which is what the
        // SpriteRenderer shows before the Animator takes over.
        a.player = playerSlices[0];
        a.monster = npcSlices[0];   // still NPCS_0-4, driven by GhostAnimator
        a.janitorIdle = Frames(janitorSlices, "jidle_", 4);
        a.janitorClean = Frames(janitorSlices, "jcleaning_", 8);
        a.janitorDissolve = Frames(janitorSlices, "jdiss_", 4);
        a.janitorMop = Frames(janitorSlices, "mopf_", 10);
        a.janitorBody = a.janitorIdle[0];
        a.curseFrames = Frames(janitorSlices, "cstudent_", 9);
        a.curseBody = a.curseFrames[0];
        Sprite scareFace;
        if (!janitorSlices.TryGetValue("jscare_0", out scareFace) || scareFace == null)
        {
            Log("FATAL: missing slice jscare_0 in " + JanitorSheet);
            Fail();
        }
        a.jscareFace = scareFace;
        a.playerAnim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimPath);
        a.ghostAnim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GhostAnimPath);
        if (a.playerAnim == null || a.ghostAnim == null)
        {
            Log("FATAL: animator controller missing: " + PlayerAnimPath + " / " + GhostAnimPath);
            Fail();
        }
        Log(string.Format("Loaded {0} player, {1} npc and {2} janitor slices.",
            playerSlices.Length, npcSlices.Length, janitorSlices.Count));

        // Tiles wrap the same sprites.
        a.lobbyFloorTile = MakeTile("LobbyFloorTile", a.lobbyFloor);
        a.roomFloorTile = MakeTile("RoomFloorTile", a.roomFloor);
        a.crFloorTile = MakeTile("CrFloorTile", a.crFloor);
        a.fieldTile = MakeTile("FieldTile", a.field);
        a.wallTiles = new Tile[16];
        for (int m = 0; m < 16; m++) a.wallTiles[m] = MakeTile("WallTile_" + m, a.wallSprites[m]);
        a.furnitureTile = MakeTile("FurnitureTile", a.furniture);
        return a;
    }

    // Sub-sprites of a sliced texture, in slice order. Every Sprite asset
    // LoadAllAssetsAtPath returns belongs to this texture (the texture object
    // itself is not a Sprite), so there is no need to match the file name:
    // the player sheet's slices are named "NewCharacterNightShift_<n>" while
    // the file is "NewCharacterNightShift 1.png", so a name prefix would miss
    // them. Order by the "_<n>" suffix so frame 0 stays the standing pose.
    static Sprite[] Slices(string path)
    {
        var list = new List<Sprite>();
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = obj as Sprite;
            if (s != null) list.Add(s);
        }
        list.Sort((a, b) => SliceIndex(a.name).CompareTo(SliceIndex(b.name)));
        return list.ToArray();
    }

    static int SliceIndex(string name)
    {
        int i = name.LastIndexOf('_'), n;
        return i >= 0 && int.TryParse(name.Substring(i + 1), out n) ? n : 0;
    }

    // Named sub-sprites of a sliced texture, keyed by slice name. Unlike
    // Slices(), this keeps groups apart: jidle_0 and mopf_0 both end in "_0",
    // so an order-by-suffix list would interleave them.
    static Dictionary<string, Sprite> SliceMap(string path)
    {
        var map = new Dictionary<string, Sprite>();
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = obj as Sprite;
            if (s != null) map[s.name] = s;
        }
        return map;
    }

    // Pull "prefix0".."prefix(count-1)" out of a slice map, in order.
    static Sprite[] Frames(Dictionary<string, Sprite> map, string prefix, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            Sprite s;
            if (!map.TryGetValue(prefix + i, out s) || s == null)
            {
                Log("FATAL: missing slice " + prefix + i + " in " + JanitorSheet);
                Fail();
            }
            frames[i] = s;
        }
        return frames;
    }

    // Force a PPU onto a hand-authored sheet. A sub-sprite's internalID is
    // derived from its rect, not from the scale, so every existing .anim clip
    // that references these slices keeps working across a reimport.
    static void Reimport(string path, float ppu)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null || imp.spritePixelsPerUnit == ppu) return;
        imp.spritePixelsPerUnit = ppu;
        imp.SaveAndReimport();
    }

    static Sprite Write(string name, int w, int h, Texture2D tex, float ppu,
                        FilterMode filter = FilterMode.Bilinear)
    {
        string path = GenDir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.mipmapEnabled = false;
            imp.filterMode = filter;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite Solid(string name, int w, int h, Color c, float ppu)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        var c32 = (Color32)c;
        for (int i = 0; i < px.Length; i++) px[i] = c32;
        tex.SetPixels32(px);
        tex.Apply();
        return Write(name, w, h, tex, ppu);
    }

    static Color32 IconPixel(char c)
    {
        switch (c)
        {
            case 'K': return new Color32(18, 16, 14, 255);
            case 'W': return new Color32(236, 225, 196, 255);
            case 'G': return new Color32(110, 104, 92, 255);
            case 'N': return new Color32(86, 64, 48, 255);
            case 'Y': return new Color32(224, 178, 58, 255);
            case 'R': return new Color32(176, 42, 36, 255);
            case 'S': return new Color32(184, 188, 194, 255);
            case 'B': return new Color32(64, 150, 220, 255);
            default: return new Color32(0, 0, 0, 0);
        }
    }

    static Sprite Icon(string name, string[] rows, float ppu)
    {
        int size = rows.Length;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                px[(size - 1 - r) * size + c] = IconPixel(rows[r][c]);
        tex.SetPixels32(px);
        tex.Apply();
        return Write(name, size, size, tex, ppu, FilterMode.Point);
    }

    // A muted green field tile with a deterministic blocky speckle, so the
    // campus grounds read as grass rather than a flat slab. The noise is hashed
    // from (x, y) instead of Random so every rebuild bakes the same art.
    static Sprite Field(string name, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Hash01(x >> 2, y >> 2) * 2f - 1f;
                tex.SetPixel(x, y, new Color(
                    Mathf.Clamp01(FieldGreen.r + n * FieldJitter),
                    Mathf.Clamp01(FieldGreen.g + n * FieldJitter),
                    Mathf.Clamp01(FieldGreen.b + n * FieldJitter), 1f));
            }
        }
        tex.Apply();
        return Write(name, size, size, tex, size);
    }

    // One square wall cell: transparent except a beveled band (band px thick)
    // on every edge that faces floor. Bit 1 = south, 2 = north, 4 = west,
    // 8 = east. The room-facing lip is outlined, then a light highlight, then
    // the body, so the band reads as a thin top-down pixel wall.
    static Sprite WallBand(string name, int mask, int size, int band)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = clear;
                bool inBand = false;
                if ((mask & 1) != 0 && y < band) { inBand = true; c = BandColor(y, band); }
                if ((mask & 2) != 0 && y >= size - band) { inBand = true; c = BandColor(size - 1 - y, band); }
                if ((mask & 4) != 0 && x < band) { inBand = true; c = BandColor(x, band); }
                if ((mask & 8) != 0 && x >= size - band) { inBand = true; c = BandColor(size - 1 - x, band); }
                tex.SetPixel(x, y, inBand ? c : clear);
            }
        }
        tex.Apply();
        return Write(name, size, size, tex, size, FilterMode.Point);
    }

    // offset 0 is the room-facing lip of a wall band, increasing toward the
    // wall interior.
    static Color BandColor(int offset, int band)
    {
        if (offset == 0) return WallOutline;
        if (offset <= 2) return WallLip;
        return WallBody;
    }

    static float Hash01(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    // Bake one named sub-sprite of the hand-authored screenshot sheet into a
    // single w x h cell sprite. The sheet is opaque, so the slice is resampled
    // as-is (bilinear) and written through the same pipeline as Solid().
    static Sprite BakeSlice(string slice, string outName, int w, int h) =>
        BakeSlice(WallFloorSheet, slice, outName, w, h);

    static Sprite BakeSlice(string path, string slice, string outName, int w, int h)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Log("FATAL: tile sheet missing: " + path); Fail(); }
        if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }

        Sprite src = null;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = obj as Sprite;
            if (s != null && s.name == slice) { src = s; break; }
        }
        if (src == null) { Log("FATAL: slice not found: " + slice); Fail(); }

        var rect = src.textureRect;
        int sw = (int)rect.width, sh = (int)rect.height;
        var pixels = src.texture.GetPixels((int)rect.x, (int)rect.y, sw, sh);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * sw - 0.5f;
                float v = (y + 0.5f) / h * sh - 0.5f;
                tex.SetPixel(x, y, SampleBilinear(pixels, sw, sh, u, v));
            }
        }
        tex.Apply();
        return Write(outName, w, h, tex, w);
    }

    static Color SampleBilinear(Color[] p, int w, int h, float x, float y)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, w - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, h - 1);
        int x1 = Mathf.Min(x0 + 1, w - 1);
        int y1 = Mathf.Min(y0 + 1, h - 1);
        float fx = Mathf.Clamp01(x - Mathf.Floor(x));
        float fy = Mathf.Clamp01(y - Mathf.Floor(y));
        var top = Color.Lerp(p[y0 * w + x0], p[y0 * w + x1], fx);
        var bot = Color.Lerp(p[y1 * w + x0], p[y1 * w + x1], fx);
        return Color.Lerp(top, bot, fy);
    }

    // A cone opening upward (light travels up), soft-edged. Alpha fades with
    // radius and toward the cone's sides.
    static Sprite Cone(string name, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float halfAngle = 45f * Mathf.Deg2Rad;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;      // -1..1 across
                float v = (y + 0.5f) / h;                 // 0 at base, 1 at tip
                float angle = Mathf.Atan2(u, Mathf.Max(v, 0.0001f));
                float dist = new Vector2(u, v).magnitude;
                float radial = 1f - Mathf.SmoothStep(0.35f, 1f, v);       // dim at the tip
                float edge = 1f - Mathf.SmoothStep(halfAngle * 0.45f, halfAngle, Mathf.Abs(angle));
                float a = Mathf.Clamp01(radial * edge * (1f - dist * 0.25f));
                tex.SetPixel(x, y, new Color(1f, 0.97f, 0.88f, a));
            }
        }
        tex.Apply();
        return Write(name, w, h, tex, 128f / 6f);
    }

    // Soft circle, used for the player glow and the monster's eye.
    static Sprite Radial(string name, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        return Write(name, size, size, tex, (float)size);
    }

    // Red edge, transparent centre: the danger vignette.
    static Sprite Vignette(string name, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.SmoothStep(0.35f, 1f, d);
                tex.SetPixel(x, y, new Color(1f, 0.1f, 0.1f, a));
            }
        }
        tex.Apply();
        return Write(name, size, size, tex, 1f);
    }

    static Tile MakeTile(string name, Sprite sprite)
    {
        string path = TileDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (existing != null) { existing.sprite = sprite; return existing; }
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }

    // ================= Grid derivation =================

    const int GridX0 = 0, GridX1 = 50, GridY0 = -41, GridY1 = 0;

    static bool IsFloor(int x, int y)
    {
        foreach (var r in Rooms) if (r.Contains(x, y)) return true;
        foreach (var d in OpenDoorways) if (d.x == x && d.y == y) return true;
        foreach (var d in LockedDoorwayCells) if (d.x == x && d.y == y) return true;
        if (LobbyOpening.Contains(x, y)) return true;
        if (y == MainDoorY && x >= MainDoorX0 && x <= MainDoorX1) return true;
        return false;
    }

    static bool IsWall(int x, int y) =>
        x >= GridX0 && x <= GridX1 && y >= GridY0 && y <= GridY1 && !IsFloor(x, y);

    // Rooms with their own floor art. Everything else uses the room floor.
    static bool IsMainLobby(int x, int y) => InRange(x, 14, 29) && InRange(y, -31, -17);
    static bool IsComfortRoom(int x, int y) => InRange(x, 2, 12) && InRange(y, -31, -26);
    static bool IsCampusGrounds(int x, int y) => InRange(x, 14, 29) && InRange(y, -40, -33);

    // Furniture cells: room desks/shelves plus the hallway, lobby and grounds
    // pinches. Solid prop sprites are pinned to these same cells.
    static bool IsFurniture(int x, int y)
    {
        // Room 4-A desks (note at (10,-9) stays clear)
        if (InRange(x, 3, 12) && (y == -5 || y == -8) && (x % 3 == 0)) return true;
        // Faculty desks (note at (19,-4), lab key at (25,-7) stay clear)
        if (InRange(x, 17, 26) && (y == -4 || y == -8) && (x % 3 == 2)) return true;
        // Library bookshelves, guide's exact columns
        if (InRange(y, -8, -3) && (x == 32 || x == 35 || x == 38 ||
                                   x == 41 || x == 44 || x == 47)) return true;
        // Hallway pinches around the lobby opening.
        if (y == -14 && (x == 10 || InRange(x, 11, 13) || InRange(x, 26, 28))) return true;
        // Reception island: forces a west or east lane through the lobby.
        if (InRange(x, 20, 23) && InRange(y, -25, -23)) return true;
        // Guard office desk cluster, clear of the log and the key cabinet.
        if (y == -21 && (x == 5 || x == 7 || x == 9)) return true;
        // Comfort room fixtures, clear of the janitor at (7,-28).
        if (y == -29 && (x == 4 || x == 10)) return true;
        // Lab computer desks; PC-07 at (47,-25) stays clear.
        if ((x == 42 || x == 45 || x == 48) &&
            (y == -19 || y == -22 || y == -25 || y == -28)) return true;
        if (x == 42 && y == -27) return true;
        // Grounds hedges funnel the approach to the main doors.
        if ((InRange(x, 14, 18) || InRange(x, 26, 29)) &&
            (y == -35 || y == -36)) return true;
        return false;
    }

    static bool InRange(int v, int lo, int hi) => v >= lo && v <= hi;

    // ================= School scene =================

    static void BuildSchoolScene(Art a, string scenePath, bool hard)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsureEventSystem();
        var systems = new GameObject("Systems");
        var camera = BuildCamera();
        var lockerPrefab = BuildLockerPrefab(a);
        var monsterPrefab = BuildMonsterPrefab(a);
        BuildGrid(a);
        BuildProps(a);
        var player = BuildPlayer(a, camera);
        BuildInteractables(a, lockerPrefab, hard);
        BuildJanitor(a);
        BuildCurseStudent(a, hard);
        BuildCanvas(a, systems);
        BuildSystems(systems, a, player, monsterPrefab, camera, hard);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Log("Saved " + scenePath);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static Camera BuildCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        go.AddComponent<AudioListener>();
        var follow = go.AddComponent<CameraFollow>();
        follow.smooth = 10f;
        return cam;
    }

    static void BuildGrid(Art a)
    {
        var gridGo = new GameObject("Grid");
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        var floorGo = new GameObject("Floor");
        floorGo.transform.SetParent(gridGo.transform, false);
        floorGo.layer = 0;
        var floorMap = floorGo.AddComponent<Tilemap>();
        floorMap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var floorRenderer = floorGo.AddComponent<TilemapRenderer>();
        floorRenderer.sortingOrder = WorldRootOrder;

        var wallsGo = new GameObject("Walls");
        wallsGo.transform.SetParent(gridGo.transform, false);
        wallsGo.layer = Blocking;
        var wallsMap = wallsGo.AddComponent<Tilemap>();
        wallsMap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var wallRenderer = wallsGo.AddComponent<TilemapRenderer>();
        wallRenderer.sortingOrder = WallOrder;
        var wallCol = wallsGo.AddComponent<TilemapCollider2D>();
        wallCol.usedByComposite = true;
        var wallsRb = wallsGo.AddComponent<Rigidbody2D>();
        wallsRb.bodyType = RigidbodyType2D.Static;
        wallsGo.AddComponent<CompositeCollider2D>();

        var furnGo = new GameObject("Furniture");
        furnGo.transform.SetParent(gridGo.transform, false);
        furnGo.layer = Blocking;
        var furnMap = furnGo.AddComponent<Tilemap>();
        furnMap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var furnRenderer = furnGo.AddComponent<TilemapRenderer>();
        furnRenderer.sortingOrder = WallOrder;
        var furnCol = furnGo.AddComponent<TilemapCollider2D>();
        furnCol.usedByComposite = true;
        var furnRb = furnGo.AddComponent<Rigidbody2D>();
        furnRb.bodyType = RigidbodyType2D.Static;
        furnGo.AddComponent<CompositeCollider2D>();

        int floors = 0, walls = 0, furniture = 0;
        for (int x = GridX0; x <= GridX1; x++)
        {
            for (int y = GridY0; y <= GridY1; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (IsWall(x, y))
                {
                    int wmask = 0;
                    if (IsFloor(x, y - 1)) wmask |= 1;
                    if (IsFloor(x, y + 1)) wmask |= 2;
                    if (IsFloor(x - 1, y)) wmask |= 4;
                    if (IsFloor(x + 1, y)) wmask |= 8;
                    wallsMap.SetTile(cell, a.wallTiles[wmask]);
                    walls++;
                }
                else
                {
                    var floorTile = IsCampusGrounds(x, y) ? a.roomFloorTile
                                  : IsComfortRoom(x, y)   ? a.crFloorTile
                                  : IsMainLobby(x, y)     ? a.lobbyFloorTile
                                                          : a.roomFloorTile;
                    floorMap.SetTile(cell, floorTile);
                    floors++;
                    if (IsFurniture(x, y)) { furnMap.SetTile(cell, a.furnitureTile); furniture++; }
                }
            }
        }
        Log(string.Format("Painted {0} floor, {1} wall, {2} furniture tiles.", floors, walls, furniture));
    }

    static void BuildProps(Art a)
    {
        int placed = 0;
        foreach (var p in Props)
        {
            Sprite s;
            if (!a.props.TryGetValue(p.sprite, out s) || s == null)
            {
                Log("FATAL: missing prop slice " + p.sprite);
                Fail();
                continue;
            }
            Bounds b = s.bounds;
            if (b.size.x <= 0f || b.size.y <= 0f) continue;
            float scale = Mathf.Min(p.w / b.size.x, p.h / b.size.y);
            // New-sheet slices have a (0,0) pivot, unlike the centred old
            // sprites. Subtract the sprite's local bounds centre so the
            // visible art, not the pivot, lands on the cell centre.
            Vector3 pivotOffset = b.center * scale;
            for (int x = p.x0; x <= p.x1; x += p.step)
                for (int y = p.y0; y <= p.y1; y++)
                {
                    var go = new GameObject("Prop_" + p.sprite + "_" + x + "_" + y);
                    go.layer = p.solid ? Blocking : 0;
                    go.transform.position = GridMap.CellToWorld(new Vector2Int(x, y)) +
                        new Vector3((p.w - 1) * 0.5f, (p.h - 1) * 0.5f, 0f) - pivotOffset;
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = s;
                    sr.sortingOrder = ObjectOrder;
                    placed++;
                }
        }
        Log("Placed " + placed + " prop sprites from " + PropSheet);
    }

    static void AddDeskBase(string label, Vector2Int cell, string sprite, Art a)
    {
        Sprite s;
        if (!a.props.TryGetValue(sprite, out s) || s == null)
        {
            Log("FATAL: missing desk sprite " + sprite);
            Fail();
            return;
        }
        var go = new GameObject("Desk_" + label + "_" + cell.x + "_" + cell.y);
        go.transform.position = GridMap.CellToWorld(cell);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = ObjectOrder - 1;
        Bounds b = s.bounds;
        if (b.size.x <= 0f || b.size.y <= 0f) return;
        float scale = Mathf.Min(1f / b.size.x, 1f / b.size.y);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.position -= (Vector3)(b.center * scale);
    }

    static PlayerController BuildPlayer(Art a, Camera cam)
    {
        var go = new GameObject("Player");
        go.layer = PlayerLayer;
        go.transform.position = GridMap.CellToWorld(new Vector2Int(21, -38));

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = a.player;
        sr.sortingOrder = ObjectOrder;

        // The walk clips drive m_Sprite on this same renderer.
        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = a.playerAnim;
        anim.applyRootMotion = false;

        // Flashlight cone, additive, points up by default (player starts facing up).
        var coneGo = new GameObject("Flashlight");
        coneGo.transform.SetParent(go.transform, false);
        var coneSr = coneGo.AddComponent<SpriteRenderer>();
        coneSr.sprite = a.cone;
        coneSr.sortingOrder = LightOrder;
        coneSr.material = new Material(Shader.Find(AdditiveShader));
        coneSr.color = new Color(1f, 0.97f, 0.88f, 1.2f);
        // Sprite pivot is centred, so shift it forward by half its length.
        coneGo.transform.localPosition = new Vector3(0f, 3f, 0f);

        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(go.transform, false);
        var glowSr = glowGo.AddComponent<SpriteRenderer>();
        glowSr.sprite = a.glow;
        glowSr.sortingOrder = LightOrder;
        glowSr.material = new Material(Shader.Find(AdditiveShader));
        glowSr.color = new Color(1f, 1f, 1f, 0.4f);
        glowGo.transform.localScale = new Vector3(3.6f, 3.6f, 1f);

        var pc = go.AddComponent<PlayerController>();
        pc.body = sr;
        pc.animator = anim;
        pc.flashlight = coneGo.transform;
        pc.stepTime = 0.18f;

        var follow = cam.GetComponent<CameraFollow>();
        follow.target = go.transform;
        return pc;
    }

    // ---------------- Interactables ----------------

    // Interactables live on the Interactable layer so they never block walking.
    // Doors are the exception: a closed door must stop the player, so they sit
    // on the Blocking layer and GridMap.Rebuild() re-bakes the cell when the
    // collider is switched off on open.
    static GameObject CellObject(string name, Vector2Int cell, Sprite sprite, int order,
                                 int layer = InteractableLayer)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.position = GridMap.CellToWorld(cell);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        return go;
    }

    static void BuildInteractables(Art a, GameObject lockerPrefab, bool hard)
    {
        // Desk note, Room 4-A
        var note = CellObject("Note_DeskNote", new Vector2Int(10, -9), a.itemIcons["note"], ObjectOrder)
                       .AddComponent<NoteInteractable>();
        AddDeskBase("Table", new Vector2Int(10, -9), "Table", a);
        note.lines = new[]
        {
            "Your groupmate's note, on loose paper:",
            "the faculty room code is your defense date, MMDD.",
            "Don't ask why. She already did."
        };
        note.setsFlag = "note";

        // Whiteboard: one object spanning (5,-1)..(10,-1)
        var boardGo = new GameObject("Note_Whiteboard");
        boardGo.layer = InteractableLayer;
        var boardSr = boardGo.AddComponent<SpriteRenderer>();
        boardSr.sprite = a.props["WhiteBoard"];
        boardSr.sortingOrder = ObjectOrder;
        boardSr.color = Color.white;
        Bounds wb = boardSr.sprite.bounds;
        float wbScale = Mathf.Min(4f / wb.size.x, 2f / wb.size.y);
        boardGo.transform.localScale = new Vector3(wbScale, wbScale, 1f);
        boardGo.transform.position = new Vector3(8f, 0f, 0f) - wb.center * wbScale;
        var boardCol = boardGo.AddComponent<BoxCollider2D>();
        boardCol.size = Vector2.one;
        var board = boardGo.AddComponent<NoteInteractable>();
        board.lines = new[]
        {
            "CAPSTONE FINAL DEFENSE",
            "Group 4  -  April 17",
            "Room 203. Be early. Bring the printed deck."
        };
        board.setsFlag = "board";

        // Adviser's note, faculty room
        var adviser = CellObject("Note_Adviser", new Vector2Int(19, -4), a.itemIcons["note"], ObjectOrder)
                           .AddComponent<NoteInteractable>();
        AddDeskBase("Table", new Vector2Int(19, -4), "Table", a);
        adviser.lines = new[]
        {
            "A note taped inside a faculty room drawer, in a different hand:",
            "\"If a Batch 2019 student comes back for their defense, do not let them into the lab alone.\"",
            "\"They were never finished. Something else is.\"",
            "The rest of the page is torn off."
        };
        adviser.setsFlag = "";

        // Lab key, faculty room
        var key = CellObject("Item_LabKey", new Vector2Int(25, -7), a.itemIcons["key"], ObjectOrder)
                      .AddComponent<ItemPickup>();
        AddDeskBase("Table", new Vector2Int(25, -7), "Table", a);
        key.flag = "labKey";
        key.pickupLines = new[]
        {
            "You find the key tagged CAPSTONE LAB, cold under a stack of papers.",
            "Footsteps in the hall. Not yours."
        };
        key.storyEvent = "labKey";
        AddSparkle(key.gameObject, a);

        // Guard logbook
        var log = CellObject("Note_GuardLogbook", new Vector2Int(5, -19), a.itemIcons["log"], ObjectOrder)
                      .AddComponent<NoteInteractable>();
        AddDeskBase("Table", new Vector2Int(5, -19), "Table", a);
        log.lines = new[]
        {
            "Guard logbook, last entry, handwriting gone sloppy:",
            "\"Doors stay sealed until the main fuse is replaced. Fuse in the library,\"",
            "\"key in the cabinet. If you're reading this, the power's out and I'm not.\""
        };
        log.setsFlag = "log";

        // Electrical key cabinet, guard office
        var cab = CellObject("Item_ElecKey", new Vector2Int(11, -17), a.itemIcons["key"], ObjectOrder)
                      .AddComponent<ItemPickup>();
        AddDeskBase("OfficeDrawer", new Vector2Int(11, -17), "OfficeDrawer", a);
        cab.flag = "elecKey";
        cab.pickupLines = new[] { "One hook says ELECTRICAL. You take it." };
        AddSparkle(cab.gameObject, a);

        // Library cabinet: the spare fuse
        var fuse = CellObject("Item_Fuse", new Vector2Int(49, -2), a.itemIcons["fuse"], ObjectOrder)
                       .AddComponent<ItemPickup>();
        AddDeskBase("OfficeDrawer", new Vector2Int(49, -2), "OfficeDrawer", a);
        fuse.flag = "fuse";
        fuse.pickupLines = new[]
        {
            "A spare fuse, still in its cardboard sleeve.",
            "Then the books start falling."
        };
        fuse.storyEvent = "fuse";
        AddSparkle(fuse.gameObject, a);

        // PC-07: the flash drive
        var pc = CellObject("Item_PC07", new Vector2Int(47, -25), a.itemIcons["drive"], ObjectOrder)
                     .AddComponent<ItemPickup>();
        AddDeskBase("Table", new Vector2Int(47, -25), "Table", a);
        pc.flag = "drive";
        pc.pickupLines = new[]
        {
            "PC-07 is still logged in. Your flash drive is in the port.",
            "The screen says, in your own account's text: ARE YOU FINISHED?"
        };
        pc.storyEvent = "drive";
        AddSparkle(pc.gameObject, a);

        // Faculty keypad door
        var faculty = CellObject("Door_Faculty", new Vector2Int(21, -12), a.doorRoomA, ObjectOrder,
                                 Blocking)
                           .AddComponent<KeypadDoor>();
        faculty.code = "0417";
        faculty.openFlag = "facultyOpen";
        faculty.lockedLines = new[] { "A keypad, four digits, no label." };
        faculty.unlockLines = null;

        // Lab door - needs the lab key found in the faculty room.
        var lab = CellObject("Door_Lab", new Vector2Int(45, -16), a.doorRoomB, ObjectOrder, Blocking)
                      .AddComponent<LockedDoor>();
        lab.requiredFlag = "labKey";
        lab.openFlag = "labOpen";
        lab.keyLabel = "Lab Key";
        lab.lockedLines = new[] { "Locked. The faculty room would keep the lab key." };
        lab.unlockLines = new[] { "The CAPSTONE LAB key turns. The lab opens." };

        // Electrical door
        var elec = CellObject("Door_Electrical", new Vector2Int(35, -16), a.doorRoomA, ObjectOrder,
                              Blocking)
                        .AddComponent<LockedDoor>();
        elec.requiredFlag = "elecKey";
        elec.openFlag = "elecOpen";
        elec.keyLabel = "Electrical Key";
        elec.lockedLines = new[] { "Locked. The guard would have the key." };
        elec.unlockLines = new[] { "The ELECTRICAL key fits. The breaker room opens." };

        // Comfort room door - sensor door off the lobby.
        var cr = CellObject("Door_ComfortRoom", new Vector2Int(13, -27), a.doorCR, ObjectOrder,
                            Blocking)
                     .AddComponent<AutoDoor>();
        cr.cell = new Vector2Int(13, -27);

        // Main fuse box, in the wall at (39,-19) facing the electrical room
        var fuseBox = CellObject("FuseBox", new Vector2Int(39, -19), a.white, ObjectOrder)
                          .AddComponent<FuseBox>();
        fuseBox.GetComponent<SpriteRenderer>().color = new Color(0.35f, 0.35f, 0.38f);

        // Main doors: one object covering (20,-32)..(23,-32)
        var doors = new GameObject("MainDoors");
        doors.layer = Blocking;
        doors.transform.position = new Vector3((MainDoorX0 + (MainDoorX1 + 1)) * 0.5f,
                                               MainDoorY + 0.5f, 0f);
        var dSr = doors.AddComponent<SpriteRenderer>();
        dSr.sprite = a.doorEntrance;
        dSr.sortingOrder = ObjectOrder;
        doors.transform.localScale = new Vector3(4f, 1f, 1f);
        var dCol = doors.AddComponent<BoxCollider2D>();
        dCol.size = Vector2.one;
        doors.AddComponent<ExitDoor>();

        // Lockers
        foreach (var cell in hard ? HardLockers : Lockers)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(lockerPrefab);
            go.name = "Locker_" + cell.x + "_" + cell.y;
            go.transform.position = GridMap.CellToWorld(cell);
        }
    }

    static void AddSparkle(GameObject parent, Art a)
    {
        var go = new GameObject("Sparkle");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0.25f, 0.25f, 0f);
        go.transform.localScale = Vector3.one * 0.35f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = a.sparkle;
        sr.sortingOrder = ObjectOrder + 1;
        parent.GetComponent<ItemPickup>().sparkle = go;
    }

    static void BuildJanitor(Art a)
    {
        var go = new GameObject("Janitor");
        go.layer = InteractableLayer;
        go.transform.position = GridMap.CellToWorld(new Vector2Int(7, -28));
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = a.janitorBody;
        sr.sortingOrder = ObjectOrder;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        var janitor = go.AddComponent<Janitor>();
        janitor.body = sr;
        janitor.idleFrames = a.janitorIdle;
        janitor.cleanFrames = a.janitorClean;
        janitor.dissolveFrames = a.janitorDissolve;
        janitor.mopFrames = a.janitorMop;
        janitor.story = new[]
        {
            "A janitor mopping a floor that is already spotless. He doesn't look up.",
            "\"You're from the lab. The one who never graduated.\"",
            "\"Batch 2019. Capstone. They defended, and then they came back unfinished.\"",
            "\"When the lights die, the one who never handed it in comes looking.\"",
            "\"It waits by the generator. In the electrical room.\"",
            "\"Don't let it catch you. It cannot finish. It makes you finish for it.\"",
            "The mop rings on the tile. He's gone."
        };
    }

    static void BuildCurseStudent(Art a, bool hard)
    {
        var go = new GameObject("CurseStudent");
        go.layer = InteractableLayer;
        go.transform.position = GridMap.CellToWorld(new Vector2Int(38, -19));
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = a.curseBody;
        sr.sortingOrder = ObjectOrder;
        var jc = go.AddComponent<Jumpscare>();
        jc.body = sr;
        jc.idleFrames = a.curseFrames;
        jc.scareFrames = new[] { a.jscareFace };
        jc.requiresFlag = "janitorStory";
        jc.alsoRequiresFlag = "elecOpen";
        jc.hoverA = GridMap.CellToWorld(new Vector2Int(32, -19));
        jc.hoverB = GridMap.CellToWorld(new Vector2Int(37, -19));
        jc.settledWorld = GridMap.CellToWorld(new Vector2Int(38, -19));
        jc.hoverCycles = hard ? 1 : 2;
        jc.hoverSeconds = hard ? 2.5f : 4f;
        jc.damage = hard ? 35f : 25f;
        jc.cooldownSeconds = hard ? 12f : 20f;
        jc.triggerDistance = hard ? 2f : 1.5f;
    }

    static GameObject BuildLockerPrefab(Art a)
    {
        var go = new GameObject("Locker");
        go.layer = Blocking;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = a.locker;
        sr.sortingOrder = ObjectOrder;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        // Vent slats, so the player can tell lockers are enterable.
        for (int i = 0; i < 3; i++)
        {
            var slat = new GameObject("Vent" + i);
            slat.transform.SetParent(go.transform, false);
            slat.transform.localPosition = new Vector3(0f, 0.22f - i * 0.22f, 0f);
            slat.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var slatSr = slat.AddComponent<SpriteRenderer>();
            slatSr.sprite = a.vent;
            slatSr.sortingOrder = ObjectOrder + 1;
        }

        go.AddComponent<Locker>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, LockerPrefabPath);
        // SaveAsPrefabAsset leaves the source object in the scene as an
        // instance; without this a 25th locker sits at the origin.
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject BuildMonsterPrefab(Art a)
    {
        var root = new GameObject("Monster");
        root.layer = MonsterLayer;

        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var bodySr = body.AddComponent<SpriteRenderer>();
        bodySr.sprite = a.monster;
        bodySr.sortingOrder = ObjectOrder;

        // GhostAnimator is a single looping state with no parameters, so the
        // five-frame ghost cycle plays without anything driving it.
        var bodyAnim = body.AddComponent<Animator>();
        bodyAnim.runtimeAnimatorController = a.ghostAnim;
        bodyAnim.applyRootMotion = false;

        var eye = new GameObject("Eye");
        eye.transform.SetParent(body.transform, false);
        eye.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        var eyeSr = eye.AddComponent<SpriteRenderer>();
        eyeSr.sprite = a.eye;
        eyeSr.sortingOrder = LightOrder;
        eyeSr.material = new Material(Shader.Find(AdditiveShader));
        eyeSr.color = new Color(1f, 0.15f, 0.15f, 1.5f);
        eye.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        var ai = root.AddComponent<MonsterAI>();
        ai.body = bodySr;
        ai.chaseStepTime = 0.215f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------- Systems ----------------

    static void BuildSystems(GameObject systems, Art a, PlayerController player,
                             GameObject monsterPrefab, Camera cam, bool hard)
    {
        var grid = systems.AddComponent<GridMap>();
        grid.blockingMask = 1 << Blocking;

        var lighting = systems.AddComponent<LightingController>();
        lighting.flashlight = player.transform.Find("Flashlight");
        lighting.glow = player.transform.Find("Glow");

        systems.AddComponent<WorldTint>();

        var audio = systems.AddComponent<AudioManager>();
        var ambience = systems.AddComponent<AudioSource>();
        var chase = systems.AddComponent<AudioSource>();
        var sfx = systems.AddComponent<AudioSource>();
        foreach (var s in new[] { ambience, chase, sfx }) s.playOnAwake = false;
        audio.ambienceSource = ambience;
        audio.chaseSource = chase;
        audio.sfxSource = sfx;

        var gm = systems.AddComponent<GameManager>();
        gm.player = player;
        gm.monsterPrefab = monsterPrefab.GetComponent<MonsterAI>();
        gm.timeLimit = hard ? 420f : 900f;
        gm.randomSpawnGap = hard ? new Vector2(12f, 20f) : new Vector2(40f, 70f);
        gm.nextScene = hard ? "" : HardScene;
        gm.monsterChaseStepTime = hard ? 0.175f : 0.215f;
        gm.monsterFinalStepTime = hard ? 0.16f : 0.195f;
        gm.monsterSearchStepTime = hard ? 0.25f : 0.3f;
        gm.monsterCatchDistance = hard ? 0.78f : 0.62f;
    }

    // ---------------- UI ----------------

    static void BuildCanvas(Art a, GameObject systems)
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Dialogue panel: bottom, stretched horizontally, 220 tall.
        var dialogue = NewImage(canvasGo.transform, "DialoguePanel",
                                new Color(0f, 0f, 0f, 0.90f), null);
        BottomBar(dialogue.GetComponent<RectTransform>(), 220f);
        var dText = NewText(dialogue.transform, "DialogueText", 36f, TextAlignmentOptions.TopLeft,
                            Vector2.zero);
        Stretch((RectTransform)dText.transform, 40f, 40f, 24f, 24f);
        dialogue.SetActive(false);

        // HUD: clock top-left, objective top-right, toast near the top-centre.
        var clock = NewText(canvasGo.transform, "ClockText", 40f, TextAlignmentOptions.TopLeft,
                            new Vector2(40f, 26f));
        var cRect = (RectTransform)clock.transform;
        cRect.anchorMin = new Vector2(0f, 1f); cRect.anchorMax = new Vector2(0f, 1f);
        cRect.pivot = new Vector2(0f, 1f); cRect.anchoredPosition = new Vector2(40f, -26f);
        cRect.sizeDelta = new Vector2(420f, 56f);

        var objective = NewText(canvasGo.transform, "ObjectiveText", 30f, TextAlignmentOptions.TopRight,
                                new Vector2(-40f, -26f));
        var oRect = (RectTransform)objective.transform;
        oRect.anchorMin = new Vector2(1f, 1f); oRect.anchorMax = new Vector2(1f, 1f);
        oRect.pivot = new Vector2(1f, 1f); oRect.anchoredPosition = new Vector2(-40f, -26f);
        oRect.sizeDelta = new Vector2(760f, 110f);
        objective.color = new Color(0.72f, 0.90f, 0.72f);

        var toast = NewText(canvasGo.transform, "ToastText", 32f, TextAlignmentOptions.Top, new Vector2(0f, -110f));
        var tRect = (RectTransform)toast.transform;
        tRect.anchorMin = new Vector2(0.5f, 1f); tRect.anchorMax = new Vector2(0.5f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f); tRect.anchoredPosition = new Vector2(0f, -110f);
        tRect.sizeDelta = new Vector2(1400f, 44f);

        // Interaction prompt: bottom-centre, sitting above the dialogue bar.
        var interactPrompt = NewText(canvasGo.transform, "PromptText", 32f,
                                     TextAlignmentOptions.Center, new Vector2(0f, 250f));
        var ipRect = (RectTransform)interactPrompt.transform;
        ipRect.anchorMin = new Vector2(0.5f, 0f); ipRect.anchorMax = new Vector2(0.5f, 0f);
        ipRect.pivot = new Vector2(0.5f, 0f); ipRect.anchoredPosition = new Vector2(0f, 250f);
        ipRect.sizeDelta = new Vector2(900f, 48f);
        interactPrompt.color = new Color(0.95f, 0.92f, 0.70f);
        interactPrompt.text = "";

        // Keys HUD: top-left, directly under the clock.
        var keysHud = NewText(canvasGo.transform, "KeysText", 28f,
                              TextAlignmentOptions.TopLeft, new Vector2(40f, -92f));
        var keysRect = (RectTransform)keysHud.transform;
        keysRect.anchorMin = new Vector2(0f, 1f); keysRect.anchorMax = new Vector2(0f, 1f);
        keysRect.pivot = new Vector2(0f, 1f); keysRect.anchoredPosition = new Vector2(40f, -92f);
        keysRect.sizeDelta = new Vector2(700f, 160f);
        keysHud.color = new Color(0.80f, 0.82f, 0.95f);
        keysHud.text = "";

        // Health HUD: top-left, under the keys readout.
        var healthText = NewText(canvasGo.transform, "HealthText", 28f,
                                 TextAlignmentOptions.TopLeft, new Vector2(40f, -272f));
        var healthRect = (RectTransform)healthText.transform;
        healthRect.anchorMin = new Vector2(0f, 1f); healthRect.anchorMax = new Vector2(0f, 1f);
        healthRect.pivot = new Vector2(0f, 1f); healthRect.anchoredPosition = new Vector2(40f, -272f);
        healthRect.sizeDelta = new Vector2(400f, 40f);
        healthText.color = Color.white;
        healthText.text = "HP 100 / 100";

        // Red vignette, full screen, no raycast.
        var vignette = NewImage(canvasGo.transform, "RedVignette", new Color(1f, 0.1f, 0.1f, 0f), null);
        var vRect = vignette.GetComponent<RectTransform>();
        Stretch(vRect, 0f, 0f, 0f, 0f);
        var vImg = vignette.GetComponent<Image>();
        vImg.sprite = a.vignette;
        vImg.raycastTarget = false;

        // Hiding overlay: black with vent slits + hint text.
        var hiding = new GameObject("HidingOverlay");
        hiding.transform.SetParent(canvasGo.transform, false);
        var hImg = hiding.AddComponent<Image>();
        hImg.color = new Color(0f, 0f, 0f, 0.94f);
        hImg.raycastTarget = false;
        Stretch(hiding.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        for (int i = 0; i < 5; i++)
        {
            var slit = NewImage(hiding.transform, "Slit" + i, new Color(0.10f, 0.10f, 0.12f, 0.9f), null);
            var sRect = slit.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.5f, 0.5f); sRect.anchorMax = new Vector2(0.5f, 0.5f);
            sRect.pivot = new Vector2(0.5f, 0.5f);
            sRect.anchoredPosition = new Vector2(0f, 150f - i * 75f);
            sRect.sizeDelta = new Vector2(760f, 18f);
            slit.GetComponent<Image>().raycastTarget = false;
        }
        var hint = NewText(hiding.transform, "Hint", 28f, TextAlignmentOptions.Bottom, new Vector2(0f, 70f));
        hint.text = "Hiding. Press E to step out.";
        var hintRect = (RectTransform)hint.transform;
        hintRect.anchorMin = new Vector2(0.5f, 0.5f); hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -220f);
        hintRect.sizeDelta = new Vector2(800f, 40f);

        // Keypad panel, centred, 400 x 300.
        var keypad = NewImage(canvasGo.transform, "KeypadPanel", new Color(0.06f, 0.06f, 0.08f, 0.97f), null);
        var kRect = keypad.GetComponent<RectTransform>();
        kRect.anchorMin = new Vector2(0.5f, 0.5f); kRect.anchorMax = new Vector2(0.5f, 0.5f);
        kRect.pivot = new Vector2(0.5f, 0.5f);
        kRect.anchoredPosition = Vector2.zero;
        kRect.sizeDelta = new Vector2(400f, 300f);

        var prompt = NewText(keypad.transform, "Prompt", 26f, TextAlignmentOptions.Top, Vector2.zero);
        prompt.text = "ENTER CODE";
        Centre(prompt.transform, new Vector2(0f, 112f), new Vector2(340f, 36f));
        var status = prompt;   // KeypadUI rewrites this text on Open/Wrong

        var inputGo = new GameObject("CodeInput");
        inputGo.transform.SetParent(keypad.transform, false);
        var inputBg = inputGo.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        Centre(inputGo.transform, new Vector2(0f, 24f), new Vector2(300f, 56f));
        var input = inputGo.AddComponent<TMPro.TMP_InputField>();
        var inputText = NewText(inputGo.transform, "Text", 32f, TextAlignmentOptions.Center, Vector2.zero);
        Stretch((RectTransform)inputText.transform, 10f, 10f, 6f, -6f);
        input.textComponent = inputText;
        input.contentType = TMPro.TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 4;
        input.text = "";

        var ok = NewButton(keypad.transform, "OkButton", "OK", new Vector2(-84f, -104f), 150f, 48f);
        var cancel = NewButton(keypad.transform, "CancelButton", "Cancel", new Vector2(84f, -104f), 150f, 48f);
        keypad.SetActive(false);

        // Jumpscare overlay: a full-screen scare face, off until triggered.
        var scare = NewImage(canvasGo.transform, "ScareOverlay", Color.white, a.curseBody);
        Stretch(scare.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var scareImg = scare.GetComponent<Image>();
        scareImg.preserveAspect = true;
        scareImg.raycastTarget = false;
        scare.SetActive(false);

        // Pause panel: Esc toggles it (PauseMenu). Built before the end panel so
        // the end screen still draws over it.
        var pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvasGo.transform, false);
        var pauseImg = pausePanel.AddComponent<Image>();
        pauseImg.color = new Color(0f, 0f, 0f, 0.82f);
        pauseImg.raycastTarget = true;
        Stretch(pausePanel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        var pauseTitle = NewText(pausePanel.transform, "PauseTitle", 80f,
                                 TextAlignmentOptions.Center, Vector2.zero);
        pauseTitle.text = "PAUSED";
        Centre(pauseTitle.transform, new Vector2(0f, 220f), new Vector2(1200f, 100f));

        var pauseResume = NewButton(pausePanel.transform, "ResumeButton", "Resume",
                                    new Vector2(0f, 60f), 360f, 64f);
        var pauseRestart = NewButton(pausePanel.transform, "RestartButton", "Start over",
                                     new Vector2(0f, -30f), 360f, 64f);
        var pauseQuit = NewButton(pausePanel.transform, "QuitButton", "Quit",
                                  new Vector2(0f, -120f), 360f, 64f);
        var pauseMenuButton = NewButton(pausePanel.transform, "MenuButton", "Main Menu",
                                        new Vector2(0f, -210f), 360f, 64f);
        pausePanel.SetActive(false);

        // End panel last so it draws over everything.
        var end = new GameObject("EndPanel");
        end.transform.SetParent(canvasGo.transform, false);
        var endImg = end.AddComponent<Image>();
        endImg.color = new Color(0f, 0f, 0f, 0.97f);
        endImg.raycastTarget = true;
        Stretch(end.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        var title = NewText(end.transform, "EndTitle", 90f, TextAlignmentOptions.Center, new Vector2(0f, 300f));
        title.text = "";
        Centre(title.transform, new Vector2(0f, 300f), new Vector2(1400f, 120f));
        var body = NewText(end.transform, "EndBody", 32f, TextAlignmentOptions.TopLeft, new Vector2(0f, 90f));
        Centre(body.transform, new Vector2(0f, 80f), new Vector2(1200f, 360f));
        body.enableWordWrapping = true;
        var retry = NewButton(end.transform, "RetryButton", "Checkpoint",
                              new Vector2(-360f, -260f), 300f, 60f);
        var cont = NewButton(end.transform, "ContinueButton", "Next level",
                             new Vector2(-120f, -260f), 300f, 60f);
        var restart = NewButton(end.transform, "RestartButton", "Start over",
                                new Vector2(120f, -260f), 300f, 60f);
        var endMenu = NewButton(end.transform, "MenuButton", "Main Menu",
                                new Vector2(360f, -260f), 300f, 60f);
        end.SetActive(false);

        // ---- wire the managers ----
        var dialogueManager = systems.AddComponent<DialogueManager>();
        dialogueManager.panel = dialogue;
        dialogueManager.text = dText;
        dialogueManager.charsPerSecond = 55f;

        var keypadUI = systems.AddComponent<KeypadUI>();
        keypadUI.panel = keypad;
        keypadUI.input = input;
        keypadUI.status = status;
        UnityEventTools.AddPersistentListener(ok.onClick, keypadUI.Submit);
        UnityEventTools.AddPersistentListener(cancel.onClick, keypadUI.Close);

        var ui = systems.AddComponent<UIManager>();
        ui.clockText = clock;
        ui.objectiveText = objective;
        ui.toastText = toast;
        ui.promptText = interactPrompt;
        ui.keysText = keysHud;
        ui.healthText = healthText;
        ui.redVignette = vImg;
        ui.hidingOverlay = hiding;
        ui.scareOverlay = scare;
        ui.scareImage = scareImg;
        ui.endPanel = end;
        ui.endTitle = title;
        ui.endBody = body;
        ui.retryButton = retry;
        ui.continueButton = cont;
        ui.menuButton = endMenu;
        UnityEventTools.AddPersistentListener(retry.onClick, ui.OnRetry);
        UnityEventTools.AddPersistentListener(cont.onClick, ui.OnContinue);
        UnityEventTools.AddPersistentListener(restart.onClick, ui.OnRestart);
        UnityEventTools.AddPersistentListener(endMenu.onClick, ui.OnMainMenu);

        var pauseMenu = systems.AddComponent<PauseMenu>();
        pauseMenu.panel = pausePanel;
        UnityEventTools.AddPersistentListener(pauseResume.onClick, pauseMenu.Resume);
        UnityEventTools.AddPersistentListener(pauseRestart.onClick, pauseMenu.RestartGame);
        UnityEventTools.AddPersistentListener(pauseQuit.onClick, pauseMenu.QuitGame);
        UnityEventTools.AddPersistentListener(pauseMenuButton.onClick, TitleScreen.MainMenu);
    }

    // ---------------- UI helpers ----------------

    static void Stretch(RectTransform r, float left, float right, float top, float bottom)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    // A full-width strip pinned to the bottom of the screen.
    static void BottomBar(RectTransform r, float height)
    {
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = new Vector2(0f, height);
    }

    static void Centre(Transform t, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)t;
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // Pin a widget to the middle-left edge. pos is the offset of its pivot
    // (left edge, vertical centre) from that anchor, so X is px in from the
    // left edge and Y is px from the vertical middle.
    static void MiddleLeft(Transform t, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)t;
        r.anchorMin = new Vector2(0f, 0.5f);
        r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static GameObject NewImage(Transform parent, string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null) img.sprite = sprite;
        return go;
    }

    static TMPro.TMP_Text NewText(Transform parent, string name, float size,
                                  TMPro.TextAlignmentOptions align, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TMPro.TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.rectTransform.anchoredPosition = pos;
        return text;
    }

    static Button NewButton(Transform parent, string name, string label, Vector2 pos,
                            float width, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.16f, 0.20f, 1f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        Centre(go.transform, pos, new Vector2(width, height));
        var text = NewText(go.transform, "Label", 26f, TMPro.TextAlignmentOptions.Center, Vector2.zero);
        Stretch((RectTransform)text.transform, 8f, 8f, 4f, -4f);
        text.text = label;
        return button;
    }

    // Standard UGUI slider: Background / Fill Area+FILL / Handle Slide Area+HANDLE.
    static Slider NewSlider(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Centre(go.transform, pos, size);
        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        var background = NewImage(go.transform, "Background", new Color(0.16f, 0.16f, 0.20f), null);
        Stretch((RectTransform)background.transform, 0f, 0f, 0f, 0f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)fillArea.transform, 8f, 8f, 4f, -4f);
        var fill = NewImage(fillArea.transform, "Fill", new Color(0.55f, 0.55f, 0.62f), null);
        Stretch((RectTransform)fill.transform, 0f, 0f, 0f, 0f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)handleArea.transform, 12f, 12f, 0f, 0f);
        var handle = NewImage(handleArea.transform, "Handle", Color.white, null);
        var handleRect = (RectTransform)handle.transform;
        handleRect.sizeDelta = new Vector2(24f, 0f);
        Stretch(handleRect, -12f, -12f, 0f, 0f);

        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    // ================= Title scene =================

    static void BuildTitleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.orthographic = true;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;
        cam.AddComponent<AudioListener>();

        // Menu music. The clip is synthesized at runtime (see MenuMusic), so the
        // scene only stores the looping source.
        var musicSource = cam.AddComponent<AudioSource>();
        var menuMusic = cam.AddComponent<MenuMusic>();
        menuMusic.source = musicSource;

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Buttons need an EventSystem to route pointer/keyboard input. The
        // School scene has one; the title scene did not, so START was dead.
        EnsureEventSystem();

        // Full-screen background art. mainmenu.png is imported as a single
        // sprite, so it is loaded straight from its path (no slicing).
        const string MainMenuPath = "Assets/Scenes/mainmenu.png";
        Sprite bg = AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuPath);
        if (bg == null)
        {
            Log("FATAL: main menu background missing: " + MainMenuPath);
            Fail();
            return;
        }
        var backdropGo = NewImage(canvasGo.transform, "Backdrop", Color.white, bg);
        Stretch((RectTransform)backdropGo.transform, 0f, 0f, 0f, 0f);
        backdropGo.GetComponent<Image>().raycastTarget = false;
        backdropGo.transform.SetAsFirstSibling();

        // Menu buttons pinned to the middle-left edge (anchor + pivot 0,0.5),
        // so X is px in from the left edge and Y is px from the vertical middle.
        var start = NewButton(canvasGo.transform, "StartButton", "START", Vector2.zero, 360f, 64f);
        MiddleLeft(start.transform, new Vector2(125f, -80f), new Vector2(360f, 64f));
        var settings = NewButton(canvasGo.transform, "SettingsButton", "SETTINGS", Vector2.zero, 360f, 64f);
        MiddleLeft(settings.transform, new Vector2(125f, -190f), new Vector2(360f, 64f));
        var exit = NewButton(canvasGo.transform, "ExitButton", "EXIT", Vector2.zero, 360f, 64f);
        MiddleLeft(exit.transform, new Vector2(125f, -300f), new Vector2(360f, 64f));

        // Settings overlay: a volume slider and a back button, off by default.
        var settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(canvasGo.transform, false);
        var settingsImg = settingsPanel.AddComponent<Image>();
        settingsImg.color = new Color(0f, 0f, 0f, 0.82f);
        settingsImg.raycastTarget = true;
        Stretch(settingsPanel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        var settingsTitle = NewText(settingsPanel.transform, "SettingsTitle", 72f,
                                    TextAlignmentOptions.Center, Vector2.zero);
        settingsTitle.text = "SETTINGS";
        Centre(settingsTitle.transform, new Vector2(0f, 220f), new Vector2(1200f, 100f));

        var volumeLabel = NewText(settingsPanel.transform, "VolumeLabel", 30f,
                                  TextAlignmentOptions.Center, Vector2.zero);
        volumeLabel.text = "Volume";
        Centre(volumeLabel.transform, new Vector2(0f, 80f), new Vector2(600f, 40f));

        var volume = NewSlider(settingsPanel.transform, "VolumeSlider",
                               new Vector2(0f, 20f), new Vector2(600f, 32f));
        var back = NewButton(settingsPanel.transform, "BackButton", "BACK", Vector2.zero, 360f, 64f);
        Centre(back.transform, new Vector2(0f, -120f), new Vector2(360f, 64f));
        settingsPanel.SetActive(false);

        // Systems object owns the menu controller. The slider listener is added
        // at runtime (TitleMenu.Awake) so the value comes from the control.
        var systems = new GameObject("Systems");
        var menu = systems.AddComponent<TitleMenu>();
        menu.settingsPanel = settingsPanel;
        menu.volumeSlider = volume;

        UnityEventTools.AddPersistentListener(start.onClick, TitleScreen.StartGame);
        UnityEventTools.AddPersistentListener(settings.onClick, menu.OpenSettings);
        UnityEventTools.AddPersistentListener(exit.onClick, TitleScreen.QuitGame);
        UnityEventTools.AddPersistentListener(back.onClick, menu.CloseSettings);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TitleScene);
        Log("Saved " + TitleScene);
    }

    // ================= Logging =================

    static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }
        return count;
    }

    static void Log(string message)
    {
        Debug.Log("[NightShiftBuilder] " + message);
    }
}
