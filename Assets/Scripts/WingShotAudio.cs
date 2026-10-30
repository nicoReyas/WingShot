using UnityEngine;

// Names for the five short sounds used by gameplay and the shop.
public enum WingShotSound { Shoot, Hit, Miss, Unlock, GameOver }

// Uses one audio source for effects and another for the looping music.
[RequireComponent(typeof(AudioSource))]
public class WingShotAudio : MonoBehaviour
{
    [Tooltip("Optional replacements. Empty slots use small built-in synthesized arcade sounds.")]
    public AudioClip shooting, hitting, missing, unlocking, gameOver;
    public bool useSynthesizedFallback = true;

    [Tooltip("Optional music override. The original Skybound loop loads automatically when empty.")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = .32f;

    // Save these references across Unity's Play Mode script reloads.
    [SerializeField, HideInInspector] AudioSource source;
    [SerializeField, HideInInspector] AudioSource musicSource;
    [SerializeField, HideInInspector] bool muted;
    [SerializeField, HideInInspector] WingShotState musicState = WingShotState.MainMenu;
    readonly AudioClip[] generated = new AudioClip[5];

    // Other scripts and the Editor checks can inspect the music without replacing its source.
    public AudioSource MusicSource => musicSource;

    // Prepare both sound channels as soon as this component is created.
    void Awake() { EnsureSources(); }

    // Recover the audio channels after enabling the component or reloading scripts.
    void OnEnable() { EnsureSources(); }

    // Connect the effects source, load the supplied music, and start just one repeating loop.
    void EnsureSources()
    {
        if (source == null) source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;

        if (musicSource == null)
        {
            var child = transform.Find("Wing Shot Music");
            if (child == null)
            {
                var musicObject = new GameObject("Wing Shot Music");
                musicObject.transform.SetParent(transform, false);
                child = musicObject.transform;
            }
            musicSource = child.GetComponent<AudioSource>();
            if (musicSource == null) musicSource = child.gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        musicSource.volume = MusicLevel();

        // A Resources asset is included in player builds, so no scene wiring is needed.
        AudioClip clip = backgroundMusic != null ? backgroundMusic : Resources.Load<AudioClip>("Audio/Skybound");
        if (musicSource.clip != clip) musicSource.clip = clip;
        var game = GetComponent<GameManager>();
        if (game != null && game.Progress != null) muted = game.Progress.Muted;
        SetMuted(muted);
        if (clip != null)
        {
            musicSource.UnPause();
            if (!musicSource.isPlaying) musicSource.Play();
        }
    }

    // Fade music using real time, so it can become quieter even while the game is paused.
    void Update()
    {
        if (musicSource != null)
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, MusicLevel(), Time.unscaledDeltaTime * .5f);
    }

    // The existing SOUND button controls both the music and the short effects.
    public void SetMuted(bool value)
    {
        muted = value;
        if (source != null) source.mute = value;
        if (musicSource != null) musicSource.mute = value;
    }

    // Change the mix for gameplay, menus, pauses, and the results screen.
    public void SetGameState(WingShotState state) { musicState = state; }

    // Keep effects easy to hear; pause and results screens use the quietest music.
    float MusicLevel()
    {
        float scale = musicState == WingShotState.Playing ? 1f :
            musicState == WingShotState.Paused ? .35f :
            musicState == WingShotState.GameOver ? .45f : .65f;
        return Mathf.Clamp01(musicVolume) * scale;
    }

    // Play an assigned sound clip, or make a small arcade tone if no clip was assigned.
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
            // Reuse generated effects instead of rebuilding their samples for every shot.
            if (generated[index] == null) generated[index] = Synthesize(sound);
            clip = generated[index];
        }
        if (clip != null) source.PlayOneShot(clip, sound == WingShotSound.Shoot ? .16f : .3f);
    }

    // Build each effect from a sine wave; changing its pitch gives each event its own sound.
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
            // Short fades keep the beginning and end of the effect from clicking.
            float envelope = Mathf.Min(1f, t * 150f) * Mathf.Min(1f, (duration - t) * 35f);
            samples[i] = (float)System.Math.Sin(phase) * envelope * .6f;
        }
        var clip = AudioClip.Create("Wing Shot " + sound, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Stop audible music when the component is disabled; enabling it resumes the same loop.
    void OnDisable() { if (musicSource != null) musicSource.Pause(); }

    // Free only the effects made at runtime; the music asset is shared and owned by Unity.
    void OnDestroy()
    {
        foreach (var clip in generated) if (clip != null) Destroy(clip);
    }
}
