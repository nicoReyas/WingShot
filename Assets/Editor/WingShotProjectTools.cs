using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// No test-package dependency. Run in the editor or through -executeMethod in a disposable copy.
[InitializeOnLoad]
public static class WingShotProjectTools
{
    const string Running = "WingShot.Checks.Running";
    const string Ending = "WingShot.Checks.Ending";
    const string Batch = "WingShot.Checks.Batch";
    const string ReportKey = "WingShot.Checks.Report";
    const string PrefsKey = "WingShot.Checks.Prefs";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static double readyAt, startedAt;
    static int stage;
    static float pausedTime, runningTime;
    static float originalInterval;
    static Vector3 pausedBirdPosition;
    static BirdTarget pausedBird;
    static Report report;

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string completedUtc;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> editorIssues = new List<string>();
    }
    [Serializable] sealed class PrefEntry
    {
        public string key;
        public bool existed;
        public bool text;
        public int number;
        public string value;
    }
    [Serializable] sealed class PrefSnapshot { public List<PrefEntry> entries = new List<PrefEntry>(); }

    static WingShotProjectTools()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += OnLog;
        if (SessionState.GetBool(Running, false))
        {
            report = JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, "{}"));
            startedAt = EditorApplication.timeSinceStartup;
        }
    }

    [MenuItem("Tools/Wing Shot/Validate Scene")]
    public static void ValidateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before validating the authored scene.");
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your open scene before running validation.");
            EditorSceneManager.OpenScene(ScenePath);
        }
        var manager = Object.FindAnyObjectByType<GameManager>();
        Require(manager != null, "GameManager is present");
        Require(manager.scoreText != null && manager.timerText != null, "Original score and timer references are intact");
        Require(manager.gameOverPanel != null && manager.finalScoreText != null, "Original results references are intact");
        Require(manager.gameOverPanel.GetComponentInChildren<Button>(true) != null, "Original restart button is present");
        Require(manager.scoreText.font != null && TMP_Settings.defaultFontAsset != null, "TextMeshPro font resources are available");
        Require(Camera.main != null && Camera.main.orthographic, "An orthographic Main Camera is present");
        Require(Object.FindAnyObjectByType<EventSystem>() != null, "UI EventSystem is present");
        var spawner = Object.FindAnyObjectByType<BirdSpawner>();
        Require(spawner != null && spawner.birdPrefab != null, "BirdSpawner prefab reference is intact");
        Require(spawner.birdPrefab.GetComponent<BirdMovement>() != null &&
            spawner.birdPrefab.GetComponent<BirdTarget>() != null &&
            spawner.birdPrefab.GetComponent<Collider2D>() != null, "Bird prefab has movement, target and collider");
        Require(spawner.birdPrefab.GetComponent<SpriteRenderer>().sprite != null, "Bird sprite reference resolves");
        bool inBuild = false;
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.enabled && scene.path == ScenePath) inBuild = true;
        Require(inBuild, "Gameplay scene is enabled in Build Settings");
        foreach (var go in WingShotObjects.All<GameObject>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0,
                "No missing scripts on " + go.name, false);
        Debug.Log("Wing Shot scene reference validation passed.");
    }

    [MenuItem("Tools/Wing Shot/Run Automated Play Mode Checks")]
    public static void RunPlayModeChecks() { Begin(false); }

    public static void RunBatchChecks() { Begin(true); }

    [MenuItem("Tools/Wing Shot/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        BuildWindows(false);
    }

    public static void BuildWindowsBatch()
    {
        BuildWindows(true);
    }

    static void BuildWindows(bool batch)
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            string output = Path.GetFullPath("Builds/Windows/Wing Shot.exe");
            string[] arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(arguments, "-wingShotBuildOutput");
            if (outputIndex >= 0 && outputIndex + 1 < arguments.Length)
                output = Path.GetFullPath(arguments[outputIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var buildReport = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            bool passed = buildReport.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/WingShotBuild.txt",
                "Result: " + buildReport.summary.result + Environment.NewLine +
                "Unity: " + Application.unityVersion + Environment.NewLine +
                "Player: " + output + Environment.NewLine +
                "Errors: " + buildReport.summary.totalErrors + Environment.NewLine +
                "Warnings: " + buildReport.summary.totalWarnings + Environment.NewLine +
                "Bytes: " + buildReport.summary.totalSize + Environment.NewLine +
                "UTC: " + DateTime.UtcNow.ToString("O"));
            if (!passed) throw new InvalidOperationException("Player build failed; see Logs/WingShotBuild.txt and the Unity log.");
            Debug.Log("Wing Shot Windows build completed: " + output);
            if (batch) EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (batch) EditorApplication.Exit(1);
            else throw;
        }
    }
    static void Begin(bool batch)
    {
        if (SessionState.GetBool(Running, false))
            throw new InvalidOperationException("Wing Shot checks are already running.");
        report = new Report { unityVersion = Application.unityVersion };
        try
        {
            ValidateScene();
            CheckEconomy();
            CapturePrefs();
            SessionState.SetBool(Batch, batch);
            SessionState.SetBool("WingShot.Checks.Capture", Array.IndexOf(Environment.GetCommandLineArgs(), "-wingShotCapture") >= 0);
            SessionState.SetBool(Ending, false);
            SessionState.SetBool(Running, true);
            SaveReport();
            startedAt = EditorApplication.timeSinceStartup;
            stage = 0;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e)
        {
            report.errors.Add(e.ToString());
            WriteReport();
            if (batch) EditorApplication.Exit(1);
            else Debug.LogError(e);
        }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || SessionState.GetBool(Ending, false)) return;
        if (EditorApplication.timeSinceStartup - startedAt > 120)
        {
            Fail(new TimeoutException("Play Mode checks exceeded 120 seconds. Playing=" + EditorApplication.isPlaying + ", Manager=" + (GameManager.Instance != null) + ", UI=" + (GameManager.Instance != null && GameManager.Instance.UI != null)));
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.timeSinceStartup < readyAt) return;
        var game = GameManager.Instance;
        if (game == null || game.UI == null) return;
        try
        {
            switch (stage)
            {
                case 0:
                    Require(game.State == WingShotState.MainMenu && Time.timeScale == 1f, "Boot opens main menu with normal time scale");
                    Require(ActiveBirds().Length == 0, "No birds spawn behind main menu");
                    FindButton("CUSTOMIZE").onClick.Invoke();
                    Require(game.State == WingShotState.Customization, "Customization button opens the shop");
                    FindButton("BACK TO MENU").onClick.Invoke();
                    runningTime = game.roundDuration;
                    FindButton("START GAME").onClick.Invoke();
                    Require(game.IsPlaying && game.Shots == 0 && game.score == 0, "Start Game resets round statistics");
                    Next(.25);
                    break;
                case 1:
                    Require(game.timeRemaining < runningTime && game.timeRemaining > 0, "Round timer advances during gameplay");
                    Require(ActiveBirds().Length > 0, "Start Game immediately spawns a bird");
                    var spawner = Object.FindAnyObjectByType<BirdSpawner>();
                    originalInterval = spawner.CurrentInterval;
                    spawner.enabled = false;
                    foreach (var bird in ActiveBirds()) { bird.gameObject.SetActive(false); Object.Destroy(bird.gameObject); }
                    var target = MakeTarget(spawner);
                    var screen = Camera.main.WorldToScreenPoint(target.transform.position);
                    var shooting = game.GetComponent<ShootingSystem>();
                    Require(shooting.TryShootAt(screen) && game.Hits == 1 && game.Misses == 0 && game.score == 10,
                        "A collider hit records one hit and awards exactly ten points");
                    Require(shooting.TryShootAt(screen) && game.Hits == 1 && game.Misses == 1 && game.score == 10,
                        "A second shot cannot score the destroyed target again");
                    Require(game.Shots == 2 && Mathf.Approximately(game.Accuracy, 50f), "Accuracy is hits divided by shots");
                    var pauseButton = FindButton("PAUSE");
                    Vector2 pausePoint = RectTransformUtility.WorldToScreenPoint(null, pauseButton.transform.position);
                    Require(!shooting.TryShootAt(pausePoint) && game.Shots == 2, "UI button clicks do not shoot or count as misses");
                    Require(!shooting.IsPointerOverUI(new Vector2(Screen.width * .5f, Screen.height * .45f)),
                        "Gameplay HUD and crosshair do not block the shooting area");
                    pausedBird = MakeTarget(spawner);
                    pausedBird.GetComponent<BirdMovement>().enabled = true;
                    pausedBirdPosition = pausedBird.transform.position;
                    pauseButton.onClick.Invoke();
                    pausedTime = game.timeRemaining;
                    Require(game.State == WingShotState.Paused && Time.timeScale == 0f, "Pause stops gameplay time");
                    Require(!shooting.TryShootAt(screen) && game.Shots == 2 && !pausedBird.TryHit(),
                        "Shooting and direct target hits are rejected while paused");
                    Next(.25);
                    break;
                case 2:
                    Require(Mathf.Approximately(pausedTime, game.timeRemaining), "Timer stays frozen during pause");
                    Require(pausedBird != null && pausedBird.transform.position == pausedBirdPosition, "Bird movement stays frozen during pause");
#if ENABLE_INPUT_SYSTEM
                    PrepareKeyboardTest();
                    var keyboard = Keyboard.current;
                    if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.EnableDevice(keyboard);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
#else
                    FindButton("RESUME").onClick.Invoke();
#endif
                    Next(.2);
                    break;
                case 3:
                    Require(game.IsPlaying && Time.timeScale == 1f, "ESC input resumes gameplay through the input adapter");
#if ENABLE_INPUT_SYSTEM
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
#endif
                    Require(game.timeRemaining < pausedTime, "Timer continues after resume");
                    game.AddScore(490);
                    Require(game.DifficultyLevel == 10 &&
                        Object.FindAnyObjectByType<BirdSpawner>().CurrentInterval < originalInterval,
                        "Score increases difficulty and shortens spawn interval with a level cap");
                    game.timeRemaining = .001f;
                    Next(.2);
                    break;
                case 4:
                    Require(game.State == WingShotState.GameOver && game.gameOver && Time.timeScale == 0f,
                        "Timer expiry opens game over and freezes gameplay");
                    Require(game.gameOverPanel.activeInHierarchy && game.finalScoreText.text.Contains("500"),
                        "Results show the final score");
                    Require(!game.GetComponent<ShootingSystem>().TryShootAt(new Vector2(Screen.width / 2f, Screen.height / 2f)),
                        "Game over rejects shooting");
                    FindButton("PLAY AGAIN").onClick.Invoke();
                    Next(.2);
                    break;
                case 5:
                    Require(game.IsPlaying && !game.gameOver && game.score == 0 && game.Shots == 0 &&
                        game.Accuracy == 0 && Time.timeScale == 1f && game.timeRemaining > 0,
                        "Play Again starts a clean playable round with zero-shot accuracy");
                    game.ReturnToMainMenu();
                    game.OpenCustomization();
                    int wallet = game.Progress.Points;
                    FindButtonStarting("Bullseye").onClick.Invoke();
                    Require(game.Progress.Selected(CosmeticCategory.Crosshair) == 1 &&
                        game.Progress.Points == wallet - 75, "Shop button purchases and selects the crosshair");
                    FindButtonStarting("Bullseye").onClick.Invoke();
                    Require(game.Progress.Points == wallet - 75, "Shop does not charge again for an owned style");
                    FindButtonStarting("Mint Glider").onClick.Invoke();
                    FindButtonStarting("Golden Hour").onClick.Invoke();
                    Require(game.Progress.Selected(CosmeticCategory.Bird) == 2 &&
                        game.Progress.Selected(CosmeticCategory.Background) == 1 &&
                        game.Progress.Points == wallet - 400, "Shop buttons purchase bird and background styles");
                    game.ReturnToMainMenu();
                    game.StartGame();
                    Require(Camera.main.backgroundColor == CosmeticCatalog.Backgrounds[1].Color,
                        "Selected background is applied to the game camera");
                    Require(ActiveBirds()[0].GetComponent<SpriteRenderer>().color == CosmeticCatalog.Birds[2].Color,
                        "Spawned birds use the selected cosmetic color");
                    bool correctCrosshair = false;
                    foreach (var graphic in WingShotObjects.All<WingShotCrosshair>(true))
                        if (graphic.name == "Gameplay Crosshair" && graphic.style == 1) correctCrosshair = true;
                    Require(correctCrosshair, "Gameplay crosshair uses the selected style");
                    game.PauseGame();
                    FindButton("RESUME").onClick.Invoke();
                    Require(game.IsPlaying && Time.timeScale == 1f, "Resume button restores gameplay time");
                    for (int i = 0; i < 3; i++)
                    {
                        game.PauseGame();
                        game.ReturnToMainMenu();
                        Require(game.State == WingShotState.MainMenu && Time.timeScale == 1f &&
                            ActiveBirds().Length == 0, "Return to menu clears birds and restores time (cycle " + (i + 1) + ")");
                        game.StartGame();
                    }
                    game.PauseGame();
                    SceneManager.LoadScene(ScenePath);
                    Next(.3);
                    break;
                case 6:
                    Require(game.State == WingShotState.MainMenu && Time.timeScale == 1f && !game.gameOver,
                        "Scene reload while paused restores main menu and time scale");
                    Require(WingShotObjects.All<GameManager>().Length == 1,
                        "Scene reload leaves exactly one GameManager");
                    Require(ActiveBirds().Length == 0, "Reload does not leave targets from an earlier round");
                    if (SessionState.GetBool("WingShot.Checks.Capture", false))
                    {
                        game.Progress.UnlockAndSelect(CosmeticCategory.Background, 0);
                        game.ApplyCosmetics();
                        CaptureFrame("Logs/WingShot-MainMenu.png");
                        CaptureFrame("Logs/WingShot-MainMenu-4x3.png", 1024, 768);
                        Next(.5);
                    }
                    else { report.passed = report.errors.Count == 0; Finish(); }
                    break;
                case 7:
                    game.OpenCustomization();
                    Next(.3);
                    break;
                case 8:
                    CaptureFrame("Logs/WingShot-Customization.png");
                    Next(.5);
                    break;
                case 9:
                    game.ReturnToMainMenu();
                    game.StartGame();
                    var photoSpawner = Object.FindAnyObjectByType<BirdSpawner>();
                    for (int i = 0; i < 4; i++)
                    {
                        var photoBird = photoSpawner.SpawnBird();
                        photoBird.transform.position = new Vector3(-4f + i * 2.5f, -.5f + (i % 2) * 2f, 0);
                    }
                    Next(.3);
                    break;
                case 10:
                    CaptureFrame("Logs/WingShot-Gameplay.png");
                    Next(.5);
                    break;
                case 11:
                    game.PauseGame();
                    Next(.3);
                    break;
                case 12:
                    CaptureFrame("Logs/WingShot-Pause.png");
                    Next(.5);
                    break;
                case 13:
                    game.ResumeGame();
                    for (int i = 0; i < 20; i++) game.RegisterShot(true);
                    for (int i = 0; i < 5; i++) game.RegisterShot(false);
                    game.EndGame();
                    Next(.3);
                    break;
                case 14:
                    CaptureFrame("Logs/WingShot-GameOver.png");
                    Next(.5);
                    break;
                case 15:
                    report.passed = report.errors.Count == 0;
                    Finish();
                    break;
            }
        }
        catch (Exception e) { Fail(e); }
    }

    static void PrepareKeyboardTest()
    {
#if ENABLE_INPUT_SYSTEM
        SessionState.SetInt("WingShot.Checks.InputFocus", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
        SessionState.SetInt("WingShot.Checks.InputBackground", (int)InputSystem.settings.backgroundBehavior);
        SessionState.SetBool("WingShot.Checks.InputOverride", true);
        // A hidden batch editor has no focused Game view. Route only this test's queued event to the game.
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
    }

    static void RestoreInputSettings()
    {
#if ENABLE_INPUT_SYSTEM
        if (!SessionState.GetBool("WingShot.Checks.InputOverride", false)) return;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt("WingShot.Checks.InputFocus", 0);
        InputSystem.settings.backgroundBehavior =
            (InputSettings.BackgroundBehavior)SessionState.GetInt("WingShot.Checks.InputBackground", 0);
        SessionState.SetBool("WingShot.Checks.InputOverride", false);
#endif
    }
    static void CaptureFrame(string path, int width = 1280, int height = 720)
    {
        // Batch-mode Game views do not service ScreenCapture requests. Render the same camera
        // and Canvas explicitly through URP, temporarily using camera-space UI for the capture.
        var camera = Camera.main;
        var canvas = GameManager.Instance.scoreText.GetComponentInParent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        var originalMode = canvas.renderMode;
        var originalUICamera = canvas.worldCamera;
        var originalTarget = camera.targetTexture;
        var originalActive = RenderTexture.active;
        float originalAspect = camera.aspect, originalPlane = canvas.planeDistance;
        float originalScale = canvas.scaleFactor;
        bool scalerEnabled = scaler != null && scaler.enabled;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = null;
        WingShotCrosshair cursor = null;
        bool cursorWasActive = false;
        Vector2 oldCursorPosition = Vector2.zero;
        if (GameManager.Instance.IsPlaying)
        {
            foreach (var graphic in WingShotObjects.All<WingShotCrosshair>(true))
                if (graphic.name == "Gameplay Crosshair") { cursor = graphic; break; }
            if (cursor != null)
            {
                cursorWasActive = cursor.gameObject.activeSelf;
                oldCursorPosition = cursor.rectTransform.anchoredPosition;
                cursor.gameObject.SetActive(true);
                cursor.rectTransform.anchoredPosition = new Vector2(60, -20);
            }
        }
        try
        {
            target.Create();
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.scaleFactor = Mathf.Sqrt(width / 1280f * (height / 720f));
            GameManager.Instance.Landscape.SendMessage("FitToCamera");
            Canvas.ForceUpdateCanvases();
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest
                { destination = target };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = originalActive;
            camera.targetTexture = originalTarget;
            camera.aspect = originalAspect;
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalUICamera;
            canvas.planeDistance = originalPlane;
            canvas.scaleFactor = originalScale;
            if (scaler != null) scaler.enabled = scalerEnabled;
            GameManager.Instance.Landscape.SendMessage("FitToCamera");
            Canvas.ForceUpdateCanvases();
            if (cursor != null)
            {
                cursor.gameObject.SetActive(cursorWasActive);
                cursor.rectTransform.anchoredPosition = oldCursorPosition;
            }
            target.Release();
            Object.DestroyImmediate(target);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }
    static BirdTarget MakeTarget(BirdSpawner spawner)
    {
        var go = Object.Instantiate(spawner.birdPrefab, Vector3.zero, Quaternion.identity);
        go.name = "Validation Target";
        var movement = go.GetComponent<BirdMovement>();
        movement.Configure(3f, 1f, BirdFlightPattern.Wave, -3f, 3f, -12f, 12f);
        movement.enabled = false;
        return go.GetComponent<BirdTarget>();
    }

    static BirdTarget[] ActiveBirds() { return WingShotObjects.All<BirdTarget>(); }

    static Button FindButtonStarting(string title)
    {
        foreach (var button in WingShotObjects.All<Button>())
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && label.text.StartsWith(title, StringComparison.Ordinal)) return button;
        }
        throw new InvalidOperationException("Active button not found: " + title);
    }
    static Button FindButton(string text)
    {
        foreach (var button in WingShotObjects.All<Button>())
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && label.text == text) return button;
        }
        throw new InvalidOperationException("Active button not found: " + text);
    }

    static void Next(double delay)
    {
        stage++;
        readyAt = EditorApplication.timeSinceStartup + delay;
    }

    static void Require(bool condition, string message, bool record = true)
    {
        if (!condition) throw new InvalidOperationException(message);
        if (record && report != null) { report.checks.Add(message); SaveReport(); }
    }

    static void CheckEconomy()
    {
        string prefix = "WingShot.Validation." + Guid.NewGuid().ToString("N") + ".";
        try
        {
            var progress = new CosmeticProgress(prefix);
            Require(progress.Points == 0 && progress.Selected(CosmeticCategory.Crosshair) == 0,
                "Fresh progress has free default cosmetics and zero points");
            Require(!progress.UnlockAndSelect(CosmeticCategory.Crosshair, 1), "Insufficient funds cannot unlock a cosmetic");
            progress.Earn(500, 500);
            Require(progress.UnlockAndSelect(CosmeticCategory.Crosshair, 1) && progress.Points == 425,
                "Unlock deducts the exact cost");
            Require(progress.UnlockAndSelect(CosmeticCategory.Crosshair, 1) && progress.Points == 425,
                "Selecting an owned cosmetic does not charge again");
            Require(progress.UnlockAndSelect(CosmeticCategory.Bird, 1) &&
                progress.UnlockAndSelect(CosmeticCategory.Background, 1), "Bird and background cosmetics can be unlocked");
            progress.SetMuted(true);
            var reloaded = new CosmeticProgress(prefix);
            Require(reloaded.Points == 200 && reloaded.LifetimePoints == 500 && reloaded.BestScore == 500 &&
                reloaded.Muted && reloaded.Selected(CosmeticCategory.Crosshair) == 1 &&
                reloaded.Selected(CosmeticCategory.Bird) == 1 && reloaded.Selected(CosmeticCategory.Background) == 1,
                "PlayerPrefs reload preserves balance, purchases, selections, best score and sound preference");
            Require(!reloaded.UnlockAndSelect(CosmeticCategory.Bird, -1) &&
                !reloaded.UnlockAndSelect(CosmeticCategory.Bird, 100), "Invalid cosmetic indices are rejected");
        }
        finally { foreach (var key in PreferenceKeys(prefix)) PlayerPrefs.DeleteKey(key.key); PlayerPrefs.Save(); }
    }

    static List<PrefEntry> PreferenceKeys(string prefix)
    {
        var keys = new List<PrefEntry>();
        foreach (var key in new[] { "points", "lifetime", "best", "muted" })
            keys.Add(new PrefEntry { key = prefix + key });
        foreach (CosmeticCategory category in Enum.GetValues(typeof(CosmeticCategory)))
        {
            keys.Add(new PrefEntry { key = prefix + "selected." + category, text = true });
            foreach (var item in CosmeticCatalog.Items(category))
                keys.Add(new PrefEntry { key = prefix + "unlocked." + category + "." + item.Id });
        }
        return keys;
    }

    static void CapturePrefs()
    {
        var snapshot = new PrefSnapshot { entries = PreferenceKeys(CosmeticProgress.DefaultPrefix) };
        foreach (var entry in snapshot.entries)
        {
            entry.existed = PlayerPrefs.HasKey(entry.key);
            if (entry.text) entry.value = PlayerPrefs.GetString(entry.key);
            else entry.number = PlayerPrefs.GetInt(entry.key);
        }
        SessionState.SetString(PrefsKey, JsonUtility.ToJson(snapshot));
    }

    static void RestorePrefs()
    {
        string json = SessionState.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;
        var snapshot = JsonUtility.FromJson<PrefSnapshot>(json);
        foreach (var entry in snapshot.entries)
        {
            if (!entry.existed) PlayerPrefs.DeleteKey(entry.key);
            else if (entry.text) PlayerPrefs.SetString(entry.key, entry.value);
            else PlayerPrefs.SetInt(entry.key, entry.number);
        }
        PlayerPrefs.Save();
        SessionState.EraseString(PrefsKey);
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (report == null || !SessionState.GetBool(Running, false) ||
            (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        // Keep unrelated Editor indexing failures visible without classifying them as gameplay errors.
        if (stack.Contains("UnityEditor.Search."))
        {
            report.editorIssues.Add(message + "\n" + stack);
            SaveReport();
            return;
        }
        report.errors.Add(message + "\n" + stack);
        SaveReport();
    }

    static void Fail(Exception e)
    {
        report.errors.Add(e.ToString());
        report.passed = false;
        Finish();
    }

    static void Finish()
    {
        SaveReport();
        SessionState.SetBool(Ending, true);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else Complete();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            readyAt = EditorApplication.timeSinceStartup + .2;
            startedAt = EditorApplication.timeSinceStartup;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (!SessionState.GetBool(Ending, false))
            {
                report.errors.Add("Play Mode checks were interrupted.");
                report.passed = false;
            }
            Complete();
        }
    }

    static void Complete()
    {
        RestoreInputSettings();
        RestorePrefs();
        report.passed &= report.errors.Count == 0;
        WriteReport();
        bool batch = SessionState.GetBool(Batch, false);
        SessionState.SetBool(Running, false);
        SessionState.SetBool(Ending, false);
        Debug.Log("Wing Shot automated checks: " + (report.passed ? "PASS" : "FAIL") +
            " (" + report.checks.Count + " checks). Report: Logs/WingShotValidation.json");
        if (batch) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    static void SaveReport()
    {
        if (report != null) SessionState.SetString(ReportKey, JsonUtility.ToJson(report));
    }

    static void WriteReport()
    {
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WingShotValidation.json", JsonUtility.ToJson(report, true));
    }
}








