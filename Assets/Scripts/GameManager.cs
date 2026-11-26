using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    // AudioManager (Step 11) listens to this; nothing breaks without it.
    public static event Action<string> Sound;
    public static void PlaySound(string id) => Sound?.Invoke(id);

    [Header("References")]
    public PlayerController player;
    public MonsterAI monsterPrefab;

    [Header("Timing")]
    public float timeLimit = 900f;                     // 15 minutes
    public Vector2 randomSpawnGap = new Vector2(40f, 70f);

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Level")]
    public string nextScene = "";
    public float monsterChaseStepTime = 0.215f;
    public float monsterFinalStepTime = 0.195f;
    public float monsterSearchStepTime = 0.3f;
    public float monsterCatchDistance = 0.62f;

    public float Health { get; private set; }
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, timeLimit - Elapsed);
    public int Deaths { get; private set; }
    public bool HasEnded => ended;

    bool ended, clockOn, scare;
    float nextSpawn = 60f;

    class Checkpoint { public List<string> flags; public Vector2Int cell; }
    Checkpoint checkpoint;

    public bool IsPaused =>
        ended ||
        scare ||
        (PauseMenu.I != null && PauseMenu.I.IsOpen) ||
        (DialogueManager.I != null && DialogueManager.I.IsBusy) ||
        (KeypadUI.I != null && KeypadUI.I.IsBusy);

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;                 // never start (or restart) frozen
        Flags.Clear();
        Health = maxHealth;
    }

    void Start()
    {
        LightingController.I.Set(LightingController.Mode.Normal);
        DialogueManager.I.Show(new[] {
            "11:44 PM. The campus is empty. The guard post is empty too.",
            "The lights in the main building are still on. The lab is on the ground floor, east wing.",
            "Five minutes. In and out."
        });
    }

    void Update()
    {
        if (IsPaused) return;

        if (clockOn)
        {
            Elapsed += Time.deltaTime;
            if (Elapsed >= timeLimit) { TimeUp(); return; }
        }

        if (Flags.Has("entered") && MonsterAI.Active == null && !player.IsHidden)
        {
            nextSpawn -= Time.deltaTime;
            if (nextSpawn <= 0f)
            {
                bool finalChase = Flags.Has("power") && Flags.Has("drive");
                SpawnMonster(null, finalChase ? 9999f : 20f, finalChase);
            }
        }
    }

    // ---------------- Story ----------------

    public void OnPlayerArrived(Vector2Int cell)
    {
        PlaySound("step");
        if (!Flags.Has("blackout") && cell.y >= -36) Blackout();
        if (!Flags.Has("entered") && cell.y >= -31) EnterLobby();
    }

    void Blackout()
    {
        Flags.Set("blackout");
        LightingController.I.Set(LightingController.Mode.Blackout);
        PlaySound("powerdown");
        DialogueManager.I.Show(new[] {
            "Click.",
            "Every light on campus dies at once. The streetlamps. The guard post. The whole building.",
            "Just a blackout... right? Get the drive and go home."
        });
    }

    void EnterLobby()
    {
        Flags.Set("entered");
        RefreshWorld();                                // closes the main doors
        clockOn = true;
        PlaySound("slam");
        DialogueManager.I.Show(new[] {
            "SLAM.",
            "The front doors swing shut behind you. You push. They don't move.",
            "...",
            "Something is standing at the far end of the lobby. It is much too tall."
        }, () =>
        {
            SaveCheckpoint();
            SpawnMonster(new Vector2Int(21, -17), 15f, false);
            UIManager.I.Toast("Face a locker and press E to hide", 5f);
        });
    }

    // Called by ItemPickup.storyEvent and FuseBox.
    public void TriggerEvent(string id)
    {
        bool power = Flags.Has("power"), drive = Flags.Has("drive");
        SaveCheckpoint();
        switch (id)
        {
            case "labKey": SpawnMonster(new Vector2Int(3, -14), 24f, false); break;
            case "fuse":   SpawnMonster(new Vector2Int(14, -14), 24f, false); break;
            case "drive":  SpawnMonster(new Vector2Int(35, -14), power ? 9999f : 26f, power); break;
            case "power":  SpawnMonster(new Vector2Int(4, -14), drive ? 9999f : 24f, drive); break;
        }
    }

    // ---------------- Monster ----------------

    public void SpawnMonster(Vector2Int? at, float chaseSeconds, bool final)
    {
        if (MonsterAI.Active != null) Destroy(MonsterAI.Active.gameObject);
        Vector2Int start = at ?? FarCell();
        var monster = Instantiate(monsterPrefab);
        monster.chaseStepTime = monsterChaseStepTime;
        monster.finalStepTime = monsterFinalStepTime;
        monster.searchStepTime = monsterSearchStepTime;
        monster.catchDistance = monsterCatchDistance;
        monster.Begin(player, start, chaseSeconds, final);
        PlaySound("sting");
    }

    // A random reachable cell 14 to 26 steps away, never outside.
    Vector2Int FarCell()
    {
        var field = GridMap.I.DistanceField(player.Cell);
        var good = new List<Vector2Int>();
        Vector2Int farthest = player.Cell;
        int best = -1;
        foreach (var kv in field)
        {
            if (kv.Key.y <= -32) continue;
            if (kv.Value > best) { best = kv.Value; farthest = kv.Key; }
            if (kv.Value >= 14 && kv.Value <= 26) good.Add(kv.Key);
        }
        return good.Count > 0 ? good[Random.Range(0, good.Count)] : farthest;
    }

    public void OnMonsterGone() => nextSpawn = Random.Range(randomSpawnGap.x, randomSpawnGap.y);

    public void BeginScare() => scare = true;
    public void EndScare() => scare = false;

    // A jumpscare lands on the player. Four hits empty the bar.
    public void TakeDamage(float amount)
    {
        if (ended) return;
        Health = Mathf.Max(0f, Health - amount);
        if (UIManager.I != null) UIManager.I.FlashDamage();
        if (Health <= 0f) PlayerCaught();
    }

    // ---------------- Checkpoints and endings ----------------

    void SaveCheckpoint() =>
        checkpoint = new Checkpoint { flags = Flags.Save(), cell = player.Cell };

    public void PlayerCaught()
    {
        if (ended) return;
        ended = true;
        Deaths++;
        PlaySound("scream");
        UIManager.I.ShowEnd("Game Over - Caught.",
            "It stood over you for a long time, the paper where its face should be crinkling.\n\n" +
            "Then it asked, very quietly: \"Are you finished?\"", checkpoint != null);
    }

    // The clock is NOT rewound, so the 15-minute limit still holds.
    public void RetryFromCheckpoint()
    {
        if (PauseMenu.I != null) PauseMenu.I.Resume();   // never retry while frozen
        if (checkpoint == null) { Restart(); return; }
        if (MonsterAI.Active != null) Destroy(MonsterAI.Active.gameObject);
        Flags.Load(checkpoint.flags);
        player.SnapTo(checkpoint.cell);
        RefreshWorld();
        nextSpawn = (Flags.Has("power") && Flags.Has("drive")) ? 6f : 25f;
        Health = maxHealth;
        ended = false;
        UIManager.I.HideEnd();
        UIManager.I.Toast("Checkpoint", 2f);
    }

    void RefreshWorld()
    {
        foreach (var it in UnityEngine.Object.FindObjectsOfType<Interactable>())
            it.RefreshFromFlags();
        foreach (var jc in UnityEngine.Object.FindObjectsOfType<Jumpscare>())
            jc.ResetForRetry();
        var mode = Flags.Has("power") ? LightingController.Mode.Emergency :
                   Flags.Has("blackout") ? LightingController.Mode.Blackout :
                   LightingController.Mode.Normal;
        LightingController.I.Set(mode);
        GridMap.I.Rebuild();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        if (string.IsNullOrEmpty(nextScene)) { Restart(); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextScene);
    }

    public void Win()
    {
        ended = true;
        int m = (int)(Elapsed / 60f), s = (int)(Elapsed % 60f);
        bool more = !string.IsNullOrEmpty(nextScene);
        string title = more ? "Level Complete" : "You got out.";
        string body = more
            ? "The drive is yours. Outside, the streetlamps blink back on one by one.\n\n" +
              "The main doors open onto the same campus. Same night. The footsteps are faster now.\n\n" +
              $"Time inside: {m}m {s:00}s. Times caught: {Deaths}."
            : "At home you plug in the drive. CAPSTONE_FINAL_v7: every file is there.\n\n" +
              "And one more you don't remember: DEFENSE_2019_UNFINISHED.mp4. Created tonight, 12:00 AM.\n\n" +
              $"Time inside: {m}m {s:00}s. Times caught: {Deaths}.";
        UIManager.I.ShowEnd(title, body, false, more);
    }

    void TimeUp()
    {
        ended = true;
        PlaySound("scream");
        UIManager.I.ShowEnd("Game Over - 12:00 AM.",
            "Midnight. Every light in the building goes out at once.\n\n" +
            "In the morning the guard finds a flash drive on the lobby floor. Nobody finds the student.", false);
    }

    // ---------------- HUD text ----------------

    public string ClockText()
    {
        int minute = 45 + (int)(Elapsed / 60f);
        return minute >= 60 ? "12:00 AM" : $"11:{minute:00} PM";
    }

    public string Objective()
    {
        if (!Flags.Has("entered")) return "Get inside. Your flash drive is in the capstone lab.";
        if (!Flags.Has("drive"))
        {
            if (Flags.Has("labKey")) return "Get your flash drive from PC-07 in the capstone lab.";
            if (Flags.Has("facultyOpen")) return "Search the faculty room for the lab key.";
            if (Flags.Has("note") && Flags.Has("board")) return "Faculty room code: your defense date, MMDD.";
            if (Flags.Has("note")) return "The code is your defense date. Check Room 4-A's whiteboard.";
            return "The lab key is in the faculty room. Find its door code; try Room 4-A.";
        }
        if (!Flags.Has("power"))
        {
            if (!Flags.Has("log")) return "The front doors are sealed. Check the guard office.";
            if (!Flags.Has("fuse") && !Flags.Has("elecKey"))
                return "Get the spare fuse (library) and the electrical room key (guard office).";
            if (!Flags.Has("fuse")) return "Find the spare fuse in the library's back cabinet.";
            if (!Flags.Has("elecKey")) return "Get the electrical room key from the guard office.";
            return "Replace the main fuse in the electrical room.";
        }
        return "Get out. Main lobby doors. Now.";
    }
}
