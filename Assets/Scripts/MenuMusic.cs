using UnityEngine;

// Title-screen menu music. The clip is synthesized at runtime (AudioSynth),
// so it is created here rather than assigned in the scene, mirroring
// AudioManager.Sync(). The Settings slider still controls it because
// AudioListener.volume is a global multiplier over every source.
public class MenuMusic : MonoBehaviour
{
    public AudioSource source;

    void Start()
    {
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.clip = AudioSynth.MenuMusic();
        source.loop = true;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.Play();
    }
}
