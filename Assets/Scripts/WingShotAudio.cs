using UnityEngine;

public enum WingShotSound { Shoot, Hit, Miss, Unlock, GameOver }

[RequireComponent(typeof(AudioSource))]
public class WingShotAudio : MonoBehaviour
{
    [Tooltip("Optional replacements. Empty slots use small built-in synthesized arcade sounds.")]
    public AudioClip shooting, hitting, missing, unlocking, gameOver;
    public bool useSynthesizedFallback = true;
    readonly AudioClip[] generated = new AudioClip[5];
    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    public void SetMuted(bool muted) { if (source != null) source.mute = muted; }

    public void Play(WingShotSound sound)
    {
        if (source == null || source.mute) return;
        AudioClip clip = null;
        switch (sound)
        {
            case WingShotSound.Shoot: clip = shooting; break;
            case WingShotSound.Hit: clip = hitting; break;
            case WingShotSound.Miss: clip = missing; break;
            case WingShotSound.Unlock: clip = unlocking; break;
            case WingShotSound.GameOver: clip = gameOver; break;
        }
        int index = (int)sound;
        if (clip == null && useSynthesizedFallback)
        {
            if (generated[index] == null) generated[index] = Synthesize(sound);
            clip = generated[index];
        }
        if (clip != null) source.PlayOneShot(clip, sound == WingShotSound.Shoot ? .16f : .3f);
    }

    static AudioClip Synthesize(WingShotSound sound)
    {
        const int sampleRate = 22050;
        float duration = sound == WingShotSound.Unlock ? .4f : sound == WingShotSound.GameOver ? .6f : .1f;
        var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
        double phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / sampleRate;
            float f = 440f;
            switch (sound)
            {
                case WingShotSound.Shoot: f = Mathf.Lerp(650f, 180f, t / duration); break;
                case WingShotSound.Hit: f = Mathf.Lerp(950f, 1400f, t / duration); break;
                case WingShotSound.Miss: f = Mathf.Lerp(230f, 140f, t / duration); break;
                case WingShotSound.Unlock:
                    f = t < .13f ? 523.25f : t < .26f ? 659.25f : 783.99f; break;
                case WingShotSound.GameOver:
                    f = t < .2f ? 523.25f : t < .4f ? 392f : 261.63f; break;
            }
            phase += 2d * System.Math.PI * f / sampleRate;
            float envelope = Mathf.Min(1f, t * 150f) * Mathf.Min(1f, (duration - t) * 35f);
            samples[i] = (float)System.Math.Sin(phase) * envelope * .6f;
        }
        var clip = AudioClip.Create("Wing Shot " + sound, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void OnDestroy()
    {
        foreach (var clip in generated) if (clip != null) Destroy(clip);
    }
}
