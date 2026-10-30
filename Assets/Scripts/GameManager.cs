using UnityEngine;
using TMPro;

public enum WingShotState { MainMenu, Playing, Paused, Customization, GameOver }

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    // Keep the original serialized field names and public button callback intact.
    static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
                instance = FindAnyObjectByType<GameManager>();
            return instance;
        }
        private set { instance = value; }
    }
    public int score;
    public float timeRemaining = 60f;
    public bool gameOver;
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public GameObject gameOverPanel;
    public TMP_Text finalScoreText;

    [Min(1f)] public float roundDuration = 60f;
    public bool useProceduralBirdArt = true;
    [field: SerializeField] public WingShotState State { get; private set; } = WingShotState.MainMenu;
    public bool IsPlaying => State == WingShotState.Playing;
    [field: SerializeField] public int Hits { get; private set; }
    [field: SerializeField] public int Misses { get; private set; }
    public int Shots => Hits + Misses;
    public float Accuracy => Shots == 0 ? 0f : 100f * Hits / Shots;
    public int DifficultyLevel => Mathf.Clamp(1 + score / 50, 1, 10);
    [field: SerializeField] public int StateChangedFrame { get; private set; }
    public CosmeticProgress Progress { get; private set; }
    [field: SerializeField] public WingShotUI UI { get; private set; }
    [field: SerializeField] public WingShotAudio Sounds { get; private set; }
    [field: SerializeField] public WingShotLandscape Landscape { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 1f;
        Progress = new CosmeticProgress();
        Sounds = GetComponent<WingShotAudio>();
        if (Sounds == null) Sounds = gameObject.AddComponent<WingShotAudio>();
        if (GetComponent<ShootingSystem>() == null) gameObject.AddComponent<ShootingSystem>();
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (Instance != this) return;
        // Unity can reload assemblies during Play Mode even with domain reload disabled.
        if (Progress == null) Progress = new CosmeticProgress();
        if (Sounds == null) Sounds = GetComponent<WingShotAudio>();
        if (UI == null) UI = GetComponent<WingShotUI>();
        if (Landscape == null) Landscape = GetComponent<WingShotLandscape>();
    }

    void Start()
    {
        if (Instance != this) return;
        UI = GetComponent<WingShotUI>();
        if (UI == null) UI = gameObject.AddComponent<WingShotUI>();
        UI.Build(this);
        Landscape = GetComponent<WingShotLandscape>();
        if (Landscape == null) Landscape = gameObject.AddComponent<WingShotLandscape>();
        Landscape.Initialize(this);
        ApplyCosmetics();
        ReturnToMainMenu();
    }

    void Update()
    {
        if (Instance != this || UI == null) return;
        if (WingShotInput.PausePressed)
        {
            if (IsPlaying) PauseGame();
            else if (State == WingShotState.Paused) ResumeGame();
            else if (State == WingShotState.Customization) ReturnToMainMenu();
        }
        if (!IsPlaying) return;
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        if (timeRemaining <= 0f) EndGame();
        UI.Refresh();
    }

    public void StartGame()
    {
        ClearBirds();
        score = 0;
        Hits = Misses = 0;
        timeRemaining = Mathf.Max(1f, roundDuration);
        ChangeState(WingShotState.Playing);
        foreach (var spawner in WingShotObjects.All<BirdSpawner>())
            spawner.ResetSpawner(true);
    }

    public void RestartGame() { StartGame(); }

    public void PauseGame()
    {
        if (IsPlaying) ChangeState(WingShotState.Paused);
    }

    public void ResumeGame()
    {
        if (State == WingShotState.Paused) ChangeState(WingShotState.Playing);
    }

    public void ReturnToMainMenu()
    {
        ClearBirds();
        ChangeState(WingShotState.MainMenu);
    }

    public void OpenCustomization()
    {
        if (State == WingShotState.MainMenu) ChangeState(WingShotState.Customization);
    }

    public void EndGame()
    {
        if (!IsPlaying) return;
        timeRemaining = 0f;
        ChangeState(WingShotState.GameOver);
        Sounds.Play(WingShotSound.GameOver);
        Progress.Save();
    }

    public void RegisterShot(bool hit, int points = 10)
    {
        if (!IsPlaying) return;
        if (hit) { Hits++; AddScore(points); }
        else Misses++;
        if (UI != null) UI.ShotFeedback(hit);
    }

    public void AddScore(int points)
    {
        if (!IsPlaying || points <= 0) return;
        score = (int)System.Math.Min(int.MaxValue, (long)score + points);
        Progress.Earn(points, score);
        if (UI != null) UI.Refresh();
    }

    public void SelectCosmetic(CosmeticCategory category, int index)
    {
        if (State != WingShotState.Customization) return;
        bool wasUnlocked = Progress.IsUnlocked(category, index);
        if (!Progress.UnlockAndSelect(category, index)) return;
        if (!wasUnlocked) Sounds.Play(WingShotSound.Unlock);
        ApplyCosmetics();
        UI.ShowCustomizationMessage(wasUnlocked ? "Selected. Ready for your next flight!" : "Unlocked! Yours to keep.");
    }

    public void ToggleSound()
    {
        Progress.SetMuted(!Progress.Muted);
        Sounds.SetMuted(Progress.Muted);
        if (UI != null) UI.Refresh();
    }

    public void ApplyCosmetics()
    {
        Sounds.SetMuted(Progress.Muted);
        if (Landscape != null) Landscape.Apply(Progress.Selected(CosmeticCategory.Background));
        if (UI != null) UI.Refresh();
    }

    void ChangeState(WingShotState state)
    {
        State = state;
        gameOver = state == WingShotState.GameOver;
        StateChangedFrame = Time.frameCount;
        Time.timeScale = state == WingShotState.Paused || state == WingShotState.GameOver ? 0f : 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (UI != null) UI.ShowState();
    }

    void ClearBirds()
    {
        foreach (var bird in WingShotObjects.All<BirdTarget>())
        {
            bird.gameObject.SetActive(false);
            Destroy(bird.gameObject);
        }
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused && IsPlaying) PauseGame();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && IsPlaying) PauseGame();
    }

    void OnApplicationQuit() { Progress?.Save(); }

    void OnDestroy()
    {
        if (Instance != this) return;
        Progress?.Save();
        Instance = null;
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}


