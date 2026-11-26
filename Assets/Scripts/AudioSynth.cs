using UnityEngine;

// Procedurally synthesized SFX and music loops, so the game has audio without
// shipping any binary assets (the same approach SchoolBuilder uses for its
// sprites). Everything is deterministic, so a rebuild sounds identical.
public static class AudioSynth
{
    public const int Rate = 44100;

    static float[] Clip(float seconds)
    {
        return new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // xorshift: stable white noise without touching UnityEngine.Random.
    static float Noise(ref uint s)
    {
        s ^= s << 13;
        s ^= s >> 17;
        s ^= s << 5;
        return (s / (float)uint.MaxValue) * 2f - 1f;
    }

    static float Thump(float t, float start)
    {
        float x = t - start;
        if (x < 0f) return 0f;
        return Mathf.Sin(2f * Mathf.PI * 52f * x) * Mathf.Exp(-x * 12f) * 0.9f;
    }

    public static AudioClip Step()
    {
        var d = Clip(0.07f);
        uint s = 0x9E3779B9u;
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            lp += (Noise(ref s) - lp) * 0.10f;
            d[i] = lp * Mathf.Exp(-t * 55f) * 0.9f;
        }
        return Make("sfx_step", d);
    }

    public static AudioClip Pickup()
    {
        var d = Clip(0.34f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = t < 0.11f ? 659.25f : 987.77f;
            float local = t < 0.11f ? t : t - 0.11f;
            d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-local * 13f) * 0.45f;
        }
        return Make("sfx_pickup", d);
    }

    public static AudioClip Unlock()
    {
        var d = Clip(0.30f);
        uint s = 0x01234567u;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float click = Noise(ref s) * Mathf.Exp(-t * 90f) * 0.5f;
            float ring = Mathf.Sin(2f * Mathf.PI * 2100f * t) * Mathf.Exp(-t * 22f) * 0.25f;
            d[i] = click + ring;
        }
        return Make("sfx_unlock", d);
    }

    public static AudioClip Slam()
    {
        var d = Clip(0.55f);
        uint s = 0x00BEEF01u;
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            lp += (Noise(ref s) - lp) * 0.05f;
            float thud = Mathf.Sin(2f * Mathf.PI * 58f * t) * Mathf.Exp(-t * 9f);
            d[i] = thud * 0.9f + lp * Mathf.Exp(-t * 14f) * 0.5f;
        }
        return Make("sfx_slam", d);
    }

    public static AudioClip PowerDown()
    {
        var d = Clip(0.8f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float e = t / 0.8f;
            float f = Mathf.Lerp(420f, 55f, e * e);
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = Mathf.Sin(phase) * (1f - e) * 0.5f;
        }
        return Make("sfx_powerdown", d);
    }

    public static AudioClip PowerUp()
    {
        var d = Clip(0.8f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float e = t / 0.8f;
            float f = Mathf.Lerp(55f, 320f, e);
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = (Mathf.Sin(phase) * 0.4f + Mathf.Sin(phase * 2f) * 0.15f) * Mathf.Min(1f, e * 4f);
        }
        return Make("sfx_powerup", d);
    }

    public static AudioClip Sting()
    {
        var d = Clip(0.9f);
        uint s = 0x0005AD1Fu;
        float p1 = 0f, p2 = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float e = t / 0.9f;
            float f = Mathf.Lerp(510f, 90f, e);
            p1 += 2f * Mathf.PI * f / Rate;
            p2 += 2f * Mathf.PI * (f * 1.06f) / Rate;   // detuned = dissonant
            float body = (Mathf.Sin(p1) + Mathf.Sin(p2)) * 0.35f;
            d[i] = (body + Noise(ref s) * 0.2f) * Mathf.Exp(-t * 3.2f);
        }
        return Make("sfx_sting", d);
    }

    public static AudioClip Scream()
    {
        var d = Clip(1.2f);
        uint s = 0x0005C2E4u;
        float p = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float e = t / 1.2f;
            float f = Mathf.Lerp(700f, 240f, e) + Mathf.Sin(t * 37f) * 60f;
            p += 2f * Mathf.PI * f / Rate;
            float tone = Mathf.Sin(p) + Mathf.Sin(p * 1.5f) * 0.5f;
            float dist = tone / (1f + Mathf.Abs(tone));   // soft clip, no Tanh in Mathf
            float env = Mathf.Min(1f, t * 12f) * Mathf.Exp(-t * 1.6f);
            d[i] = (dist * 0.6f + Noise(ref s) * 0.35f) * env * 0.8f;
        }
        return Make("sfx_scream", d);
    }

    public static AudioClip Locker()
    {
        var d = Clip(0.28f);
        uint s = 0x000077AAu;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = Noise(ref s) * Mathf.Exp(-t * 45f) * 0.45f +
                   Mathf.Sin(2f * Mathf.PI * 1400f * t) * Mathf.Exp(-t * 16f) * 0.3f;
        }
        return Make("sfx_locker", d);
    }

    public static AudioClip Heartbeat()
    {
        var d = Clip(0.55f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = Thump(t, 0f) + Thump(t, 0.20f) * 0.7f;
        }
        return Make("sfx_heartbeat", d);
    }

    // Seamless 4 s loop: every partial completes a whole number of cycles in
    // the buffer, and the slow AM is exactly one cycle, so the ends line up.
    public static AudioClip Ambience()
    {
        var d = Clip(4f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float am = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
            d[i] = (Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.35f +
                    Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.25f +
                    Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.12f) * am * 0.6f;
        }
        return Make("amb_drone", d);
    }

    // Seamless 2 s loop: 55/58 Hz give 110/116 cycles and the 2 Hz pulse gives
    // four, so the chase bed loops without a click.
    public static AudioClip Chase()
    {
        var d = Clip(2f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f * t);
            float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.4f +
                          Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.4f;
            d[i] = drone * (0.55f + 0.45f * pulse) * 0.6f;
        }
        return Make("mus_chase", d);
    }

    // Seamless 16 s title bed: a low drone over a slow minor arpeggio. Every
    // partial and the 0.25 Hz swell complete a whole number of cycles in the
    // buffer, and the per-note arpeggio envelope reaches zero at each note
    // boundary, so the loop point is silent and click-free.
    public static AudioClip MenuMusic()
    {
        const float dur = 16f;
        // A minor 7th (A3, C4, E4, G4) in whole Hz so nothing beats at the seam.
        int[] arp = { 220, 262, 330, 392 };
        var d = Clip(dur);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
            float drone = (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.22f +
                           Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.15f) * swell;

            int slot = i / Rate;                       // whole seconds
            float local = t - slot;                    // 0..1 within the note
            float env = Mathf.Sin(Mathf.PI * local);   // zero at both ends
            float tone = Mathf.Sin(2f * Mathf.PI * arp[slot & 3] * local) * env * 0.30f;

            d[i] = drone * 0.5f + tone;
        }
        return Make("mus_menu", d);
    }
}
