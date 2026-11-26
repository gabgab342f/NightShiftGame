using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource ambienceSource, chaseSource, sfxSource;

    public AudioClip heartbeat, step, pickup, unlock, slam, powerdown,
                     powerup, sting, scream, locker;

    float heartbeatTimer;

    void OnEnable() { GameManager.Sound += Play; }
    void OnDisable() { GameManager.Sound -= Play; }

    void Start()
    {
        // No clip assets ship with the project, so build every sound in code.
        // Any clip the builder did assign wins, keeping this a pure fallback.
        Sync();
        ambienceSource.loop = true;
        chaseSource.loop = true;
        ambienceSource.volume = 0f;
        chaseSource.volume = 0f;
        ambienceSource.Play();
        chaseSource.Play();
    }

    // Fill in every missing clip from the procedural synth. Safe to call twice.
    public void Sync()
    {
        if (step == null) step = AudioSynth.Step();
        if (pickup == null) pickup = AudioSynth.Pickup();
        if (unlock == null) unlock = AudioSynth.Unlock();
        if (slam == null) slam = AudioSynth.Slam();
        if (powerdown == null) powerdown = AudioSynth.PowerDown();
        if (powerup == null) powerup = AudioSynth.PowerUp();
        if (sting == null) sting = AudioSynth.Sting();
        if (scream == null) scream = AudioSynth.Scream();
        if (locker == null) locker = AudioSynth.Locker();
        if (heartbeat == null) heartbeat = AudioSynth.Heartbeat();
        if (ambienceSource != null && ambienceSource.clip == null)
            ambienceSource.clip = AudioSynth.Ambience();
        if (chaseSource != null && chaseSource.clip == null)
            chaseSource.clip = AudioSynth.Chase();
    }

    public void Play(string id)
    {
        AudioClip clip;
        switch (id)
        {
            case "step": clip = step; break;
            case "pickup": clip = pickup; break;
            case "unlock": clip = unlock; break;
            case "slam": clip = slam; break;
            case "powerdown": clip = powerdown; break;
            case "powerup": clip = powerup; break;
            case "sting": clip = sting; break;
            case "scream": clip = scream; break;
            case "locker": clip = locker; break;
            default: clip = null; break;
        }
        if (clip == null) return;
        float volume = id == "step" ? 0.25f : 1f;
        sfxSource.PlayOneShot(clip, volume);
    }

    void Update()
    {
        float ambienceGoal = Flags.Has("blackout") ? 0.6f : 0f;
        ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceGoal, Time.deltaTime * 0.3f);

        var m = MonsterAI.Active;
        bool hunting = m != null && m.IsHunting;
        chaseSource.volume = Mathf.MoveTowards(chaseSource.volume, hunting ? 0.7f : 0f, Time.deltaTime * 0.8f);

        if (!hunting || heartbeat == null) return;
        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer > 0f) return;
        float d = m.DistanceToPlayer;
        sfxSource.PlayOneShot(heartbeat, Mathf.Clamp(0.9f - d * 0.05f, 0.1f, 0.9f));
        heartbeatTimer = Mathf.Clamp(0.3f + d * 0.07f, 0.38f, 1.3f);   // faster when close
    }
}
