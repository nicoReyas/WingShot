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
    // These values describe only the current round; saved points are stored separately.
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
    // Accuracy is zero until the player has taken at least one shot.
    public int Shots => Hits + Misses;
    public float Accuracy => Shots == 0 ? 0f : 100f * Hits / Shots;
    public int DifficultyLevel => Mathf.Clamp(1 + score / 50, 1, 10);
    [field: SerializeField] public int StateChangedFrame { get; private set; }
    // Shared helpers for saved progress, menus, sound, and the background.
    public CosmeticProgress Progress { get; private set; }
    [field: SerializeField] public WingShotUI UI { get; private set; }
    [field: SerializeField] public WingShotAudio Sounds { get; private set; }
    [field: SerializeField] public WingShotLandscape Landscape { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // Clear the shared GameManager reference before a new Play Mode session.
    static void ResetStatics() { Instance = null; }

    // Set up the shared manager, saved progress, shooting, and audio.
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

    // Recover needed references if Unity reloads scripts during Play Mode.
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

    // Build the menus and scenery, apply saved styles, and show the main menu.
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

    // Handle ESC and count down the timer while the round is running.
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

    // Clear old birds and start a fresh round with zero score and full time.
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

    // Let the Play Again button start another clean round.
    public void RestartGame() { StartGame(); }

    // Freeze an active round and open the pause menu.
    public void PauseGame()
    {
        if (IsPlaying) ChangeState(WingShotState.Paused);
    }

    // Continue the paused round.
    public void ResumeGame()
    {
        if (State == WingShotState.Paused) ChangeState(WingShotState.Playing);
    }

    // Remove any remaining birds and return to the title screen.
    public void ReturnToMainMenu()
    {
        ClearBirds();
        ChangeState(WingShotState.MainMenu);
    }

    // Open the shop from the main menu.
    public void OpenCustomization()
    {
        if (State == WingShotState.MainMenu) ChangeState(WingShotState.Customization);
    }

    // Finish the round, show results, play the ending sound, and save progress.
    public void EndGame()
    {
        if (!IsPlaying) return;
        timeRemaining = 0f;
        ChangeState(WingShotState.GameOver);
        Sounds.Play(WingShotSound.GameOver);
        Progress.Save();
    }

    // Count the shot, award points for a hit, and update the shot feedback.
    public void RegisterShot(bool hit, int points = 10)
    {
        if (!IsPlaying) return;
        if (hit) { Hits++; AddScore(points); }
        else Misses++;
        if (UI != null) UI.ShotFeedback(hit);
    }

    // Increase the round score and immediately save the earned cosmetic points.
    public void AddScore(int points)
    {
        if (!IsPlaying || points <= 0) return;
        score = (int)System.Math.Min(int.MaxValue, (long)score + points);
        Progress.Earn(points, score);
        if (UI != null) UI.Refresh();
    }

    // Buy or select a style, then apply it to the game.
    public void SelectCosmetic(CosmeticCategory category, int index)
    {
        if (State != WingShotState.Customization) return;
        bool wasUnlocked = Progress.IsUnlocked(category, index);
        if (!Progress.UnlockAndSelect(category, index)) return;
        if (!wasUnlocked) Sounds.Play(WingShotSound.Unlock);
        ApplyCosmetics();
        UI.ShowCustomizationMessage(wasUnlocked ? "Selected. Ready for your next flight!" : "Unlocked! Yours to keep.");
    }

    // Save the sound preference and apply it to music and effects.
    public void ToggleSound()
    {
        Progress.SetMuted(!Progress.Muted);
        Sounds.SetMuted(Progress.Muted);
        if (UI != null) UI.Refresh();
    }

    // Apply saved sound and background choices and refresh the menu previews.
    public void ApplyCosmetics()
    {
        Sounds.SetMuted(Progress.Muted);
        if (Landscape != null) Landscape.Apply(Progress.Selected(CosmeticCategory.Background));
        if (UI != null) UI.Refresh();
    }

    // Switch screens and update time, cursor visibility, and music volume together.
    void ChangeState(WingShotState state)
    {
        State = state;
        gameOver = state == WingShotState.GameOver;
        StateChangedFrame = Time.frameCount;
        // A zero time scale freezes movement and the round timer, but UI still works.
        Time.timeScale = state == WingShotState.Paused || state == WingShotState.GameOver ? 0f : 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Sounds != null) Sounds.SetGameState(state);
        if (UI != null) UI.ShowState();
    }

    // Hide and remove all birds so previous rounds cannot leave targets behind.
    void ClearBirds()
    {
        foreach (var bird in WingShotObjects.All<BirdTarget>())
        {
            bird.gameObject.SetActive(false);
            Destroy(bird.gameObject);
        }
    }

    // Pause the round when the player switches to another application.
    void OnApplicationFocus(bool focused)
    {
        if (!focused && IsPlaying) PauseGame();
    }

    // Pause when the operating system suspends the game.
    void OnApplicationPause(bool paused)
    {
        if (paused && IsPlaying) PauseGame();
    }

    // Save progress before the game closes.
    void OnApplicationQuit() { Progress?.Save(); }

    // Save progress and restore normal time and the cursor when leaving this scene.
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
