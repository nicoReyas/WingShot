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
    static int shotsBeforePause;
    static Transform pausedCloud;
    static Vector3 pausedCloudPosition;
    static WingShotCrate pausedCrate;
    static float pausedCrateLife, pausedSniper, pausedDouble, pausedSlow;
    static Vector3 pausedCratePosition, slowProbePosition;
    static BirdTarget slowProbe;
    static WingShotBoss testBoss;
    static WingShotDropping testDropping;
    static Vector3 bossPausePosition, droppingPausePosition;
    static int healthBeforeFall;
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

    // Register Editor callbacks that run and finish the automated checks.
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
    // Check scene references without saving changes to the authored scene.
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
        // Confirm the original loop imports as stereo PCM, with its license beside it.
        var music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Skybound.wav");
        Require(music != null && music.channels == 2 && music.length > 30f,
            "Original music asset imports as a full-length stereo clip");
        var musicImporter = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/Skybound.wav");
        Require(musicImporter.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM &&
            musicImporter.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad,
            "Music uses preloaded PCM for smooth looping");
        Require(File.Exists("Assets/Resources/Audio/Skybound-LICENSE.txt"),
            "Music CC0 dedication is included in the project");
        Debug.Log("Wing Shot scene reference validation passed.");
    }

    [MenuItem("Tools/Wing Shot/Run Automated Play Mode Checks")]
    // Start the automated checks from Unity's Tools menu.
    public static void RunPlayModeChecks() { Begin(false); }

    // Start the same checks when Unity is launched from a command line.
    public static void RunBatchChecks() { Begin(true); }

    [MenuItem("Tools/Wing Shot/Build Windows Player")]
    // Build the Windows player from the Tools menu.
    public static void BuildWindowsPlayer()
    {
        BuildWindows(false);
    }

    // Build the Windows player from a command line.
    public static void BuildWindowsBatch()
    {
        BuildWindows(true);
    }

    // Build the scene into a Windows game and write the build result to a file.
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
    // Check the scene, back up saved values, and enter Play Mode for testing.
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
            foreach (var key in PreferenceKeys(CosmeticProgress.DefaultPrefix)) PlayerPrefs.DeleteKey(key.key);
            PlayerPrefs.Save();
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

    // Run one stage of the gameplay checks after the previous stage has had time to finish.
    static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || SessionState.GetBool(Ending, false)) return;
        if (EditorApplication.timeSinceStartup - startedAt > 180)
        {
            Fail(new TimeoutException("Play Mode checks exceeded 180 seconds. Playing=" + EditorApplication.isPlaying + ", Manager=" + (GameManager.Instance != null) + ", UI=" + (GameManager.Instance != null && GameManager.Instance.UI != null)));
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.timeSinceStartup < readyAt) return;
        var game = GameManager.Instance;
        if (game == null || game.UI == null) return;
        try
        {
            // Each numbered stage checks a different part of a complete play/replay cycle.
            switch (stage)
            {
                case 0:
                    Require(game.State == WingShotState.MainMenu && Time.timeScale == 1f, "Boot opens main menu with normal time scale");
                    Require(ActiveBirds().Length == 0, "No birds spawn behind main menu");
                    // Music should share the saved sound setting but use its own repeating source.
                    var musicChannel = game.Sounds.MusicSource;
                    Require(musicChannel != null && musicChannel.clip != null &&
                        musicChannel.clip.name == "Skybound" && musicChannel.loop &&
                        musicChannel.spatialBlend == 0f &&
                        musicChannel != game.Sounds.GetComponent<AudioSource>(),
                        "Music loads automatically into a separate looping 2D audio source");
                    Require(musicChannel.clip.loadState == AudioDataLoadState.Loaded,
                        "Music samples are loaded and ready to play");
                    bool originalMute = game.Progress.Muted;
                    FindButton(originalMute ? "SOUND: OFF" : "SOUND: ON").onClick.Invoke();
                    Require(musicChannel.mute == !originalMute &&
                        game.Sounds.GetComponent<AudioSource>().mute == !originalMute,
                        "Sound button mutes both music and effects");
                    FindButton(originalMute ? "SOUND: ON" : "SOUND: OFF").onClick.Invoke();
                    Require(musicChannel.mute == originalMute && game.Progress.Muted == originalMute,
                        "Sound button restores both channels and the saved preference");
                    FindButton("CUSTOMIZE").onClick.Invoke();
                    Require(game.State == WingShotState.Customization, "Customization button opens the shop");
                    FindButton("BACK TO MENU").onClick.Invoke();
                    runningTime = game.roundDuration;
                    FindButton("START GAME").onClick.Invoke();
                    Require(game.IsPlaying && game.Shots == 0 && game.score == 0, "Start Game resets round statistics");
                    Require(ActiveBirds().Length == 3 && Array.Exists(ActiveBirds(), bird => bird.MaxHealth == 2),
                        "Opening flock contains three birds including a two-hit target");
                    foreach (var openingBird in ActiveBirds())
                    {
                        var viewportPosition = Camera.main.WorldToViewportPoint(openingBird.transform.position);
                        Require(viewportPosition.x > 0f && viewportPosition.x < 1f &&
                            viewportPosition.y > .2f && viewportPosition.y < .8f,
                            "Opening target is immediately visible inside the flight area", false);
                    }
                    Next(.25);
                    break;
                case 1:
                    Require(game.timeRemaining < runningTime && game.timeRemaining > 0, "Round timer advances during gameplay");
                    Require(ActiveBirds().Length > 0, "Start Game immediately spawns a bird");
                    var spawner = Object.FindAnyObjectByType<BirdSpawner>();
                    originalInterval = spawner.CurrentInterval;
                    Require(originalInterval <= .91f, "Opening spawn interval is under one second with the existing scene settings");
                    spawner.enabled = false;
                    foreach (var bird in ActiveBirds()) { bird.gameObject.SetActive(false); Object.Destroy(bird.gameObject); }
                    var target = MakeTarget(spawner);
                    var screen = Camera.main.WorldToScreenPoint(target.transform.position);
                    var shooting = game.GetComponent<ShootingSystem>();
                    Require(shooting.TryShootAt(screen) && game.Hits == 1 && game.Misses == 0 && game.score == 10,
                        "A collider hit records one hit and awards exactly ten points");
                    float beforeMissTime = game.timeRemaining;
                    int beforeMissWallet = game.Progress.Points;
                    Require(shooting.TryShootAt(screen) && game.Hits == 1 && game.Misses == 1 && game.score == 0 &&
                        Mathf.Approximately(game.timeRemaining, beforeMissTime - 1f),
                        "A second shot cannot score a defeated bird and deducts exactly ten points and one second");
                    Require(game.Progress.Points == beforeMissWallet && game.EarnedPoints == 10,
                        "Miss penalties leave earned currency intact");
                    Require(game.Shots == 2 && Mathf.Approximately(game.Accuracy, 50f), "Accuracy is hits divided by shots");
                    CheckCombatAndPenalties(game, spawner, shooting, screen);
                    shotsBeforePause = game.Shots;
                    var pauseButton = FindButton("PAUSE");
                    Vector2 pausePoint = RectTransformUtility.WorldToScreenPoint(null, pauseButton.transform.position);
                    Require(!shooting.TryShootAt(pausePoint) && game.Shots == shotsBeforePause, "UI button clicks do not shoot or count as misses");
                    Require(!shooting.IsPointerOverUI(new Vector2(Screen.width * .5f, Screen.height * .45f)),
                        "Gameplay HUD and crosshair do not block the shooting area");
                    pausedBird = MakeTarget(spawner);
                    pausedBird.GetComponent<BirdMovement>().enabled = true;
                    pausedBirdPosition = pausedBird.transform.position;
                    pausedCloud = game.Landscape.transform.Find("Drifting cloud 1");
                    pausedCloudPosition = pausedCloud.position;
                    pauseButton.onClick.Invoke();
                    pausedTime = game.timeRemaining;
                    Require(game.State == WingShotState.Paused && Time.timeScale == 0f, "Pause stops gameplay time");
                    Require(!shooting.TryShootAt(screen) && game.Shots == shotsBeforePause && !pausedBird.TryHit(),
                        "Shooting and direct target hits are rejected while paused");
                    Next(.25);
                    break;
                case 2:
                    Require(Mathf.Approximately(pausedTime, game.timeRemaining), "Timer stays frozen during pause");
                    Require(pausedBird != null && pausedBird.transform.position == pausedBirdPosition, "Bird movement stays frozen during pause");
                    Require(pausedCloud.position == pausedCloudPosition, "Cloud movement stays frozen during pause");
                    Require(game.Sounds.MusicSource.volume < game.Sounds.musicVolume, "Music fades quieter while gameplay is paused");
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
                    Require(pausedCloud.position != pausedCloudPosition, "Cloud movement resumes with gameplay");
                    game.AddScore(500 - game.score);
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
                    Require(game.Progress.BestScore == 500, "Best score records the settled round score");
                    Require(!game.GetComponent<ShootingSystem>().TryShootAt(new Vector2(Screen.width / 2f, Screen.height / 2f)),
                        "Game over rejects shooting");
                    FindButton("PLAY AGAIN").onClick.Invoke();
                    Next(.2);
                    break;
                case 5:
                    Require(game.IsPlaying && !game.gameOver && game.score == 0 && game.Shots == 0 &&
                        game.Accuracy == 0 && Time.timeScale == 1f && game.timeRemaining > 0,
                        "Play Again starts a clean playable round with zero-shot accuracy");
                    game.timeRemaining = .5f;
                    int walletBeforeExpiry = game.Progress.Points;
                    game.RegisterShot(false);
                    Require(game.State == WingShotState.GameOver && game.timeRemaining == 0f && game.score == -10 &&
                        game.Misses == 1 && game.Progress.Points == walletBeforeExpiry,
                        "A miss in the last second immediately ends the round with its score penalty");
                    game.RegisterShot(false);
                    Require(game.Misses == 1 && game.score == -10, "No further penalties are applied after game over");
                    game.ReturnToMainMenu();
                    game.OpenCustomization();
                    int wallet = game.Progress.Points;
                    FindButtonStarting("Bullseye").onClick.Invoke();
                    Require(game.Progress.Selected(CosmeticCategory.Crosshair) == 1 &&
                        game.Progress.Points == wallet - 75, "Shop button purchases and selects the crosshair");
                    FindButtonStarting("Bullseye").onClick.Invoke();
                    Require(game.Progress.Points == wallet - 75, "Shop does not charge again for an owned style");
                    // The shop is on the scene Canvas rather than parented under GameManager.
                    var catalogScroll = Object.FindAnyObjectByType<ScrollRect>();
                    Require(catalogScroll != null && catalogScroll.content.rect.height > catalogScroll.viewport.rect.height &&
                        catalogScroll.viewport.GetComponent<RectMask2D>() != null,
                        "Expanded catalog has a clipped scrolling viewport");
                    catalogScroll.verticalNormalizedPosition = 0f;
                    Canvas.ForceUpdateCanvases();
                    var premiumButton = FindButtonStarting("Supernova");
                    Require(RectTransformUtility.RectangleContainsScreenPoint(catalogScroll.viewport,
                        RectTransformUtility.WorldToScreenPoint(null, premiumButton.transform.position)),
                        "Scrolling to the bottom makes the most expensive reticle visible");
                    catalogScroll.verticalNormalizedPosition = 1f;
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
                    CheckLandscapeThemes(game);
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
                    Require(game.Sounds.GetComponentsInChildren<AudioSource>(true).Length == 2,
                        "Repeated rounds keep exactly one music source and one effects source");
                    SceneManager.LoadScene(ScenePath);
                    Next(.3);
                    break;
                case 6:
                    Require(game.State == WingShotState.MainMenu && Time.timeScale == 1f && !game.gameOver,
                        "Scene reload while paused restores main menu and time scale");
                    Require(WingShotObjects.All<GameManager>().Length == 1,
                        "Scene reload leaves exactly one GameManager");
                    Require(ActiveBirds().Length == 0, "Reload does not leave targets from an earlier round");
                    stage = 15;
                    Next(.1);
                    break;
                case 7:
                    game.OpenCustomization();
                    Next(.3);
                    break;
                case 8:
                    CaptureFrame("Logs/WingShot-Customization.png");
                    CaptureFrame("Logs/WingShot-Customization-4x3.png", 1024, 768);
                    var previewScroll = Object.FindAnyObjectByType<ScrollRect>();
                    previewScroll.verticalNormalizedPosition = 0f;
                    Canvas.ForceUpdateCanvases();
                    CaptureFrame("Logs/WingShot-Customization-Premium.png");
                    previewScroll.verticalNormalizedPosition = 1f;
                    Next(.5);
                    break;
                case 9:
                    game.ReturnToMainMenu();
                    game.StartGame();
                    var photoSpawner = Object.FindAnyObjectByType<BirdSpawner>();
                    for (int i = 0; i < 4; i++)
                    {
                        var photoBird = photoSpawner.SpawnBird();
                        if (i == 3 && photoBird.GetComponent<BirdTarget>().MaxHealth == 1)
                            photoBird.GetComponent<BirdTarget>().Configure(3, 60);
                        photoBird.transform.position = new Vector3(-4f + i * 2.5f, -.5f + (i % 2) * 2f, 0);
                    }
                    Next(.3);
                    break;
                case 10:
                    CaptureFrame("Logs/WingShot-Gameplay.png");
                    CaptureFrame("Logs/WingShot-Gameplay-4x3.png", 1024, 768);
                    for (int theme = 1; theme < CosmeticCatalog.Backgrounds.Length; theme++)
                    {
                        game.Landscape.Apply(theme);
                        CaptureFrame("Logs/WingShot-Scenery-" + CosmeticCatalog.Backgrounds[theme].Id + ".png");
                    }
                    game.Landscape.Apply(game.Progress.Selected(CosmeticCategory.Background));
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
                case 16:
                    Require(game.Powerups.CurrentCrate == null && game.Powerups.ShotDamage == 1,
                        "Main menu has no crates or active weapon boosts");
                    game.StartGame();
                    game.Encounter.enabled = false;
                    Object.FindAnyObjectByType<BirdSpawner>().enabled = false;
                    foreach (var bird in ActiveBirds()) { bird.gameObject.SetActive(false); Object.Destroy(bird.gameObject); }
                    Next(3.75);
                    break;
                case 17:
                    Require(game.Powerups.CurrentCrate != null && game.Powerups.CurrentCrate.Kind == WingShotPowerup.Sniper,
                        "The first sniper supply crate spawns automatically after three seconds");
                    pausedCrate = game.Powerups.CurrentCrate;
                    Require(pausedCrate.IsDropping && pausedCrate.transform.position.y > pausedCrate.LandingPosition.y &&
                        pausedCrate.TimeToLive == 18f, "A supply crate falls from above and keeps its full lifetime until landing");
                    if (SessionState.GetBool("WingShot.Checks.Capture", false)) CaptureFrame("Logs/WingShot-Crate-Dropping.png");
                    game.PauseGame();
                    pausedCratePosition = pausedCrate.transform.position;
                    pausedCrateLife = pausedCrate.TimeToLive;
                    stage = 20;
                    Next(.2);
                    break;
                case 22:
                    Require(!game.Powerups.CurrentCrate.IsDropping &&
                        Mathf.Abs(game.Powerups.CurrentCrate.transform.position.y - game.Powerups.CurrentCrate.LandingPosition.y) < .3f &&
                        game.Powerups.CurrentCrate.TimeToLive > 16.5f,
                        "A resumed drop lands smoothly and starts the crate lifetime after landing");
                    CheckPowerupCombat(game);
                    pausedCrate = game.Powerups.CurrentCrate;
                    game.PauseGame();
                    pausedCratePosition = pausedCrate.transform.position;
                    pausedCrateLife = pausedCrate.TimeToLive;
                    pausedSniper = game.Powerups.SniperRemaining;
                    pausedDouble = game.Powerups.DoublePointsRemaining;
                    pausedSlow = game.Powerups.SlowRemaining;
                    stage = 17;
                    Next(.3);
                    break;
                case 21:
                    Require(pausedCrate.transform.position == pausedCratePosition && pausedCrate.TimeToLive == pausedCrateLife &&
                        pausedCrate.IsDropping, "Pausing freezes a supply crate in the middle of its drop");
                    game.ResumeGame();
                    Next(1.5);
                    break;
                case 18:
                    Require(game.Powerups.SniperRemaining == pausedSniper && game.Powerups.DoublePointsRemaining == pausedDouble &&
                        game.Powerups.SlowRemaining == pausedSlow && pausedCrate.TimeToLive == pausedCrateLife &&
                        pausedCrate.transform.position == pausedCratePosition,
                        "Pause freezes every power-up duration, crate lifetime and crate movement");
                    float timeBeforePausedReward = game.timeRemaining;
                    game.Powerups.Activate(WingShotPowerup.ExtraTime);
                    Require(!pausedCrate.TryHit(3) && game.timeRemaining == timeBeforePausedReward,
                        "Paused games reject crate damage and power-up activation");
                    game.ResumeGame();
                    runningTime = game.timeRemaining;
                    slowProbePosition = slowProbe.transform.position;
                    Next(.3);
                    break;
                case 19:
                    Require(game.Powerups.SniperRemaining < pausedSniper && game.Powerups.DoublePointsRemaining < pausedDouble &&
                        game.Powerups.SlowRemaining < pausedSlow && pausedCrate.TimeToLive < pausedCrateLife &&
                        game.timeRemaining < runningTime && Time.timeScale == 1f,
                        "Resume advances power-ups, crate lifetime and the normal round clock");
                    float elapsed = runningTime - game.timeRemaining;
                    Require(Mathf.Abs((slowProbe.transform.position.x - slowProbePosition.x) - elapsed * 2f) < .12f,
                        "Slow-birds reward actually halves world movement without slowing the round timer");
                    slowProbe.GetComponent<BirdMovement>().enabled = false;
                    if (SessionState.GetBool("WingShot.Checks.Capture", false))
                    {
                        CaptureFrame("Logs/WingShot-Powerups.png");
                        CaptureFrame("Logs/WingShot-Powerups-4x3.png", 1024, 768);
                        CaptureCrateTypes(game);
                    }
                    Next(18.3);
                    break;
                case 20:
                    Require(game.Powerups.ShotDamage == 1 && game.Powerups.PointMultiplier == 1 &&
                        game.Powerups.BirdSpeedMultiplier == 1f && game.Powerups.SniperRemaining == 0f &&
                        game.Powerups.DoublePointsRemaining == 0f && game.Powerups.SlowRemaining == 0f,
                        "All timed power-ups expire automatically and restore ordinary damage, rewards and movement");
                    Require(pausedCrate == null, "An unopened crate expires after eighteen seconds of gameplay");
                    game.Powerups.ResetRound();
                    foreach (var bird in ActiveBirds()) { bird.gameObject.SetActive(false); Object.Destroy(bird.gameObject); }
                    var expiryTarget = MakeTarget(Object.FindAnyObjectByType<BirdSpawner>());
                    expiryTarget.Configure(3, 60);
                    var expiryShooting = game.GetComponent<ShootingSystem>();
                    Require(expiryShooting.TryShootAt(Camera.main.WorldToScreenPoint(expiryTarget.transform.position)) &&
                        expiryTarget.RemainingHealth == 2 && expiryShooting.LastShotSound == WingShotSound.Shoot,
                        "After sniper expiry the real firing path uses the ordinary sound and one damage again");
                    bool restoredReticle = false;
                    foreach (var graphic in WingShotObjects.All<WingShotCrosshair>(true))
                        if (graphic.name == "Gameplay Crosshair" && graphic.style == game.Progress.Selected(CosmeticCategory.Crosshair))
                            restoredReticle = true;
                    Require(restoredReticle, "Sniper expiry restores the selected cosmetic reticle");
                    game.Powerups.Activate(WingShotPowerup.Sniper);
                    game.Powerups.Activate(WingShotPowerup.DoublePoints);
                    game.Powerups.SpawnCrate(WingShotPowerup.ExtraTime);
                    game.EndGame();
                    Require(game.Powerups.ShotDamage == 1 && game.Powerups.PointMultiplier == 1 &&
                        WingShotObjects.All<WingShotCrate>().Length == 0, "Game over clears power-ups and all crates");
                    game.StartGame();
                    game.Powerups.Activate(WingShotPowerup.SlowBirds);
                    game.Powerups.SpawnCrate(WingShotPowerup.Sniper);
                    game.ReturnToMainMenu();
                    Require(game.Powerups.BirdSpeedMultiplier == 1f && game.Powerups.CurrentCrate == null &&
                        WingShotObjects.All<WingShotCrate>().Length == 0, "Returning to menu clears crates and timed effects");
                    stage = 22;
                    Next(.1);
                    break;
                case 23:
                    Require(game.Encounter.firstBossDelay == 18f, "The first boss is scheduled eighteen seconds into a normal round");
                    game.Encounter.firstBossDelay = .6f;
                    game.Encounter.enabled = true;
                    game.StartGame();
                    game.Encounter.firstBossDelay = 18f;
                    game.Powerups.enabled = false;
                    game.Scope.enabled = false;
                    foreach (var bird in ActiveBirds()) { bird.gameObject.SetActive(false); Object.Destroy(bird.gameObject); }
                    Require(game.PlayerHealth == 100 && !game.PlayerDefeated && game.QuickscopeKills == 0,
                        "A fresh round starts at full health with no quickscope kills");
                    game.Scope.SetAiming(true, game.Scope.AimScreenPosition);
                    for (int i = 0; i < 10; i++) game.Scope.AdvanceView(.1f, Vector2.zero);
                    slowProbe = CombatBird(game, 1, 10);
                    slowProbe.transform.position = new Vector3(-4f, 0f, 0f);
                    slowProbe.GetComponent<BirdMovement>().Configure(4f, 1f, BirdFlightPattern.Straight, -3f, 3f, -12f, 12f);
                    slowProbe.GetComponent<BirdMovement>().enabled = true;
                    slowProbePosition = slowProbe.transform.position;
                    runningTime = game.timeRemaining;
                    Next(.85);
                    break;
                case 24:
                    testBoss = game.Encounter.CurrentBoss;
                    Require(Mathf.Abs((slowProbe.transform.position.x - slowProbePosition.x) -
                        (runningTime - game.timeRemaining) * 4f * .97f) < .15f,
                        "Regular right-click focus slows actual bird movement by only three percent with a normal round clock");
                    slowProbe.gameObject.SetActive(false);
                    Object.Destroy(slowProbe.gameObject);
                    Require(testBoss != null && testBoss.MaxHealth == 24 && testBoss.RemainingHealth == 24,
                        "The encounter automatically spawns a twenty-four-health boss after its configured delay");
                    Require(game.Encounter.SpawnBoss() == null, "The encounter prevents duplicate bosses");
                    testBoss.enabled = false;
                    testBoss.transform.position = new Vector3(4f, 2f, 0f);
                    CheckQuickscopesAndFocus(game);
                    CheckBossDefense(game);
                    game.PauseGame();
                    bossPausePosition = testBoss.transform.position;
                    droppingPausePosition = testDropping.transform.position;
                    healthBeforeFall = game.PlayerHealth;
                    Next(.3);
                    break;
                case 25:
                    Require(testBoss.transform.position == bossPausePosition && testDropping.transform.position == droppingPausePosition &&
                        game.PlayerHealth == healthBeforeFall, "Pause freezes the boss, falling poop and player health");
                    Require(!testBoss.TryHit(3) && !testDropping.TryShoot() && !testDropping.Impact() && !game.DamagePlayer(12),
                        "Paused combat rejects boss damage, defensive shots, impacts and player damage");
                    game.ResumeGame();
                    testBoss.enabled = true;
                    Next(2.15);
                    break;
                case 26:
                    Require(testDropping == null && game.PlayerHealth == healthBeforeFall - 12,
                        "An unblocked dropping naturally falls and removes exactly twelve player health");
                    Require(game.IsPlaying && game.PlayerHealth > 0, "A single boss attack leaves the player alive with most health intact");
                    Next(1.25);
                    break;
                case 27:
                    var attacks = WingShotObjects.All<WingShotDropping>();
                    Require(attacks.Length == 1, "The boss attack clock automatically releases a visible falling dropping");
                    testDropping = attacks[0];
                    testBoss.enabled = false;
                    CheckBossDamageAndDefeat(game);
                    Require(game.DamagePlayer(10000) && game.PlayerHealth == 0 && game.PlayerDefeated && game.gameOver &&
                        game.State == WingShotState.GameOver && Time.timeScale == 0f,
                        "Lethal damage clamps health at zero and ends the round with a health-defeat result");
                    Require(game.Progress.BestScore >= game.score,
                        "A health defeat records the settled best score before displaying results");
                    Require(!game.DamagePlayer(12) && WingShotObjects.All<WingShotBoss>().Length == 0 &&
                        WingShotObjects.All<WingShotDropping>().Length == 0,
                        "Game over rejects further damage and clears the boss and its projectiles");
                    if (SessionState.GetBool("WingShot.Checks.Capture", false)) CaptureFrame("Logs/WingShot-Health-Defeat.png");
                    game.StartGame();
                    Require(game.PlayerHealth == 100 && !game.PlayerDefeated && game.QuickscopeKills == 0 &&
                        game.Encounter.BossesDefeated == 0 && game.Encounter.CurrentBoss == null,
                        "Restart restores health and resets boss and quickscope round progress");
                    game.ReturnToMainMenu();
                    game.Powerups.enabled = game.Scope.enabled = game.Encounter.enabled = true;
                    Require(!game.DamagePlayer(12) && game.Encounter.SpawnBoss() == null,
                        "Menus cannot spawn a boss or damage the player");
                    if (SessionState.GetBool("WingShot.Checks.Capture", false))
                    {
                        game.Progress.UnlockAndSelect(CosmeticCategory.Background, 0);
                        game.ApplyCosmetics();
                        CaptureFrame("Logs/WingShot-MainMenu.png");
                        CaptureFrame("Logs/WingShot-MainMenu-4x3.png", 1024, 768);
                        stage = 6;
                        Next(.5);
                    }
                    else { report.passed = report.errors.Count == 0; Finish(); }
                    break;
            }
        }
        catch (Exception e) { Fail(e); }
    }

    // Let the hidden test window receive the queued ESC key event.
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

    // Restore the original input focus settings after testing.
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
    // Render a preview image, then restore the normal camera and UI settings.
    static void CaptureFrame(string path, int width = 1280, int height = 720)
    {
        // Batch-mode Game views do not service ScreenCapture requests. Render the same camera
        // and Canvas explicitly through URP, temporarily using camera-space UI for the capture.
        var camera = Camera.main;
        var canvas = GameManager.Instance.scoreText.GetComponentInParent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        var originalMode = canvas.renderMode;
        var originalUICamera = canvas.worldCamera;
        int originalSortingOrder = canvas.sortingOrder;
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
                cursor.rectTransform.anchoredPosition = GameManager.Instance.Scope != null && GameManager.Instance.Scope.IsAiming
                    ? Vector2.zero : new Vector2(60, -20);
            }
        }
        try
        {
            // Draw into a temporary image without changing the player's normal screen.
            target.Create();
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            // Match the live overlay canvas: world targets must remain behind the HUD.
            canvas.sortingOrder = 100;
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
            canvas.sortingOrder = originalSortingOrder;
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
    // Create a stationary target that lets the shooting checks aim reliably.
    static BirdTarget MakeTarget(BirdSpawner spawner)
    {
        var go = Object.Instantiate(spawner.birdPrefab, Vector3.zero, Quaternion.identity);
        go.name = "Validation Target";
        var movement = go.GetComponent<BirdMovement>();
        movement.Configure(3f, 1f, BirdFlightPattern.Wave, -3f, 3f, -12f, 12f);
        movement.enabled = false;
        return go.GetComponent<BirdTarget>();
    }

    // Return the birds currently active in the scene.
    static BirdTarget[] ActiveBirds() { return WingShotObjects.All<BirdTarget>(); }

    // Find a shop button by the beginning of its label.
    static Button FindButtonStarting(string title)
    {
        foreach (var button in WingShotObjects.All<Button>())
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && label.text.StartsWith(title, StringComparison.Ordinal)) return button;
        }
        throw new InvalidOperationException("Active button not found: " + title);
    }
    // Find an active button by its exact label.
    static Button FindButton(string text)
    {
        foreach (var button in WingShotObjects.All<Button>())
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && label.text == text) return button;
        }
        throw new InvalidOperationException("Active button not found: " + text);
    }

    // Move to the next test stage after a short real-time delay.
    static void Next(double delay)
    {
        stage++;
        readyAt = EditorApplication.timeSinceStartup + delay;
    }

    // Record a successful check, or fail the test if its condition is false.
    static void Require(bool condition, string message, bool record = true)
    {
        if (!condition) throw new InvalidOperationException(message);
        if (record && report != null) { report.checks.Add(message); SaveReport(); }
    }

    static void CheckPowerupCombat(GameManager game)
    {
        var effects = game.Powerups;
        var shooting = game.GetComponent<ShootingSystem>();
        var spawner = Object.FindAnyObjectByType<BirdSpawner>();
        effects.enabled = false;
        var crate = effects.CurrentCrate;
        Require(crate.MaxHealth == 8 && crate.RemainingHealth == 8,
            "Supply crates require eight normal damage to open");
        Require(effects.SpawnCrate(WingShotPowerup.ExtraTime, false) == null, "Only one natural supply crate can be active at a time");
        int initialHits = game.Hits, initialMisses = game.Misses, initialScore = game.score, wallet = game.Progress.Points;
        float initialTime = game.timeRemaining;
        var screen = Camera.main.WorldToScreenPoint(crate.transform.position);
        Require(!crate.TryHit(0) && !crate.TryHit(-3) && crate.RemainingHealth == 8,
            "Zero and negative crate damage are rejected");
        for (int i = 0; i < 7; i++) Require(shooting.TryShootAt(screen), "Crate hit is accepted through its collider", false);
        Require(crate.RemainingHealth == 1 && !crate.IsOpened && effects.ShotDamage == 1 &&
            game.Hits == initialHits + 7 && game.Misses == initialMisses && game.score == initialScore &&
            game.Progress.Points == wallet && game.timeRemaining == initialTime,
            "Seven crate hits count for accuracy without premature rewards, miss penalties or lost time");
        Require(shooting.TryShootAt(screen) && crate.IsOpened && effects.ShotDamage == 3 &&
            effects.SniperRemaining == WingShotPowerups.WeaponDuration,
            "The eighth hit opens the crate and activates a twelve-second sniper");
        Require(!crate.TryHit() && effects.SniperRemaining == WingShotPowerups.WeaponDuration,
            "An opened crate cannot grant its power-up twice");
        var ordinarySound = game.Sounds.GetClip(WingShotSound.Shoot);
        var sniperSound = game.Sounds.GetClip(WingShotSound.Sniper);
        Require(ordinarySound != null && sniperSound != null && ordinarySound != sniperSound &&
            sniperSound.length > ordinarySound.length * 2f,
            "The sniper has a separate longer synthesized firing sound");
        var doubleCrate = effects.SpawnCrate(WingShotPowerup.DoublePoints, false);
        screen = Camera.main.WorldToScreenPoint(doubleCrate.transform.position);
        Require(shooting.TryShootAt(screen) && doubleCrate.RemainingHealth == 5 &&
            shooting.LastShotSound == WingShotSound.Sniper, "Sniper firing uses its own sound and deals three damage to crates");
        Require(shooting.TryShootAt(screen) && doubleCrate.RemainingHealth == 2 && !doubleCrate.IsOpened,
            "A reinforced crate survives two sniper rounds");
        Require(shooting.TryShootAt(screen) && doubleCrate.IsOpened && effects.PointMultiplier == 2,
            "The third sniper round opens a double-points crate");
        var elite = MakeTarget(spawner);
        elite.Configure(3, 60);
        Require(shooting.TryShootAt(Camera.main.WorldToScreenPoint(elite.transform.position)) && elite.IsHit &&
            game.score == initialScore + 120 && game.Progress.Points == wallet + 120,
            "A sniper kills a three-hit bird in one shot and double points awards 120 score and currency");
        effects.Activate(WingShotPowerup.DoublePoints);
        Require(effects.DoublePointsRemaining == WingShotPowerups.DoublePointsDuration && effects.PointMultiplier == 2,
            "Repeated double points refreshes its duration without multiplying to four times");
        int beforeMiss = game.score;
        float beforeMissTime = game.timeRemaining;
        game.RegisterShot(false);
        Require(game.score == beforeMiss - 10 && game.timeRemaining == beforeMissTime - 1f &&
            game.Progress.Points == wallet + 120,
            "Double points does not double miss penalties or remove saved currency");
        game.timeRemaining = game.roundDuration - 2f;
        var timeCrate = effects.SpawnCrate(WingShotPowerup.ExtraTime, false);
        screen = Camera.main.WorldToScreenPoint(timeCrate.transform.position);
        for (int i = 0; i < 2; i++) shooting.TryShootAt(screen);
        float beforeTimeBoost = game.timeRemaining;
        Require(shooting.TryShootAt(screen) && timeCrate.IsOpened &&
            game.timeRemaining == beforeTimeBoost + 10f && game.timeRemaining > game.roundDuration,
            "A time crate adds exactly ten seconds and can extend the initial round duration");
        Require(!timeCrate.TryHit(3) && game.timeRemaining == beforeTimeBoost + 10f,
            "The same time crate cannot extend the timer twice");
        var slowCrate = effects.SpawnCrate(WingShotPowerup.SlowBirds, false);
        screen = Camera.main.WorldToScreenPoint(slowCrate.transform.position);
        for (int i = 0; i < 3; i++) shooting.TryShootAt(screen);
        Require(slowCrate.IsOpened && effects.BirdSpeedMultiplier == .5f && effects.SlowRemaining == 8f &&
            effects.ShotDamage == 3 && effects.PointMultiplier == 2 && Time.timeScale == 1f,
            "Slow birds runs alongside the sniper and double points without changing global time scale");
        var overlapBird = MakeTarget(spawner);
        overlapBird.Configure(3, 60);
        var frontCrate = effects.SpawnCrate(WingShotPowerup.Sniper, false);
        frontCrate.transform.position = overlapBird.transform.position;
        Require(shooting.TryShootAt(Camera.main.WorldToScreenPoint(overlapBird.transform.position)) &&
            frontCrate.RemainingHealth == 5 && overlapBird.RemainingHealth == 3,
            "When crate and bird overlap, a shot damages only the visible front crate");
        overlapBird.gameObject.SetActive(false);
        Object.Destroy(overlapBird.gameObject);
        slowProbe = MakeTarget(spawner);
        if (game.useProceduralBirdArt)
        {
            slowProbe.gameObject.AddComponent<WingShotBirdVisual>();
            slowProbe.transform.localScale = Vector3.one * 1.4f;
            slowProbe.GetComponent<SpriteRenderer>().color = CosmeticCatalog.Birds[game.Progress.Selected(CosmeticCategory.Bird)].Color;
        }
        slowProbe.transform.position = new Vector3(-4f, 0f, 0f);
        slowProbe.GetComponent<BirdMovement>().Configure(4f, 1f, BirdFlightPattern.Straight, -3f, 3f, -12f, 12f);
        slowProbe.GetComponent<BirdMovement>().enabled = true;
        game.UI.Refresh();
        bool scopeFound = false;
        foreach (var graphic in WingShotObjects.All<WingShotCrosshair>(true))
            if (graphic.name == "Gameplay Crosshair" && graphic.style == 7) scopeFound = true;
        Require(scopeFound, "An active sniper replaces the cosmetic cursor with its scope reticle");
        CheckScopedView(game);
        effects.enabled = true;
    }

    static BirdTarget CombatBird(GameManager game, int health, int reward)
    {
        var bird = MakeTarget(Object.FindAnyObjectByType<BirdSpawner>());
        bird.Configure(health, reward);
        bird.gameObject.AddComponent<WingShotBirdVisual>();
        bird.transform.localScale = Vector3.one * 1.4f;
        bird.GetComponent<SpriteRenderer>().color = CosmeticCatalog.Birds[game.Progress.Selected(CosmeticCategory.Bird)].Color;
        return bird;
    }

    static void CheckQuickscopesAndFocus(GameManager game)
    {
        var scope = game.Scope;
        var shooting = game.GetComponent<ShootingSystem>();
        game.Powerups.ResetRound();
        Require(scope.SetAiming(true, scope.AimScreenPosition), "Right-click aiming works without owning an active sniper");
        for (int i = 0; i < 20; i++) scope.AdvanceView(.05f, Vector2.zero);
        Require(Mathf.Abs(Camera.main.orthographicSize - scope.NormalSize * .9f) < .01f &&
            Mathf.Approximately(game.Powerups.BirdSpeedMultiplier, .97f) && scope.LensAmount == 0f &&
            !scope.IsSniperAiming && Cursor.lockState == CursorLockMode.None,
            "Regular focus has a small zoom, a tiny slowdown, a free pointer and no sniper aperture");
        var ordinary = CombatBird(game, 3, 60);
        if (SessionState.GetBool("WingShot.Checks.Capture", false))
        {
            game.UI.Refresh(); game.UI.SendMessage("LateUpdate");
            CaptureFrame("Logs/WingShot-Regular-Focus.png");
        }
        Require(shooting.TryShootAt(Camera.main.WorldToScreenPoint(ordinary.transform.position)) && ordinary.RemainingHealth == 2 &&
            game.QuickscopeKills == 0, "Ordinary focused shots keep one damage and cannot claim quickscope bonuses");
        scope.ResetRound();
        Require(game.Powerups.BirdSpeedMultiplier == 1f, "Releasing focus immediately restores ordinary bird speed");
        game.Powerups.Activate(WingShotPowerup.Sniper);
        scope.SetAiming(true, scope.AimScreenPosition);
        scope.AdvanceView(.2f, Vector2.zero);
        int score = game.score, wallet = game.Progress.Points;
        Require(shooting.TryShootAt(Camera.main.WorldToScreenPoint(ordinary.transform.position)) && ordinary.IsHit &&
            game.score == score + 120 && game.Progress.Points == wallet + 120 && game.QuickscopeKills == 1,
            "A sniper kill within half a second of aiming awards double score and currency");
        var next = CombatBird(game, 1, 10);
        score = game.score;
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(next.transform.position));
        Require(game.score == score + 10 && game.QuickscopeKills == 1,
            "Only the first shot in a scope attempt can earn a quickscope bonus");
        scope.SetAiming(false);
        scope.SetAiming(true, scope.AimScreenPosition);
        next = CombatBird(game, 1, 10);
        score = game.score;
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(next.transform.position));
        Require(game.score == score + 10 && game.QuickscopeKills == 1,
            "Instant repeated scope toggles cannot bypass the quickscope rearm delay");
        scope.ResetRound();
        scope.SetAiming(true, scope.AimScreenPosition);
        scope.AdvanceView(.6f, Vector2.zero);
        next = CombatBird(game, 1, 10);
        score = game.score;
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(next.transform.position));
        Require(game.score == score + 10 && game.QuickscopeKills == 1,
            "A kill after the quickscope window awards only its normal reward");
        scope.ResetRound(); scope.SetAiming(true, scope.AimScreenPosition);
        score = game.score;
        shooting.TryShootAt(scope.AimScreenPosition);
        next = CombatBird(game, 1, 10);
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(next.transform.position));
        Require(game.score == score && game.QuickscopeKills == 1,
            "Missing the opening scope shot consumes the bonus chance and retains normal miss penalties");
        scope.ResetRound();
        game.Powerups.Activate(WingShotPowerup.DoublePoints);
        scope.SetAiming(true, scope.AimScreenPosition); scope.AdvanceView(.1f, Vector2.zero);
        next = CombatBird(game, 3, 60);
        score = game.score; wallet = game.Progress.Points;
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(next.transform.position));
        Require(game.score == score + 240 && game.Progress.Points == wallet + 240 && game.QuickscopeKills == 2,
            "Quickscope and double-points bonuses stack to four times the bird's base reward");
        if (SessionState.GetBool("WingShot.Checks.Capture", false))
        {
            game.UI.SendMessage("LateUpdate"); Canvas.ForceUpdateCanvases();
            CaptureFrame("Logs/WingShot-Quickscope.png");
        }
        scope.ResetRound(); scope.SetAiming(true, scope.AimScreenPosition);
        var crate = game.Powerups.SpawnCrate(WingShotPowerup.Sniper, false);
        crate.transform.position = Vector3.zero;
        score = game.score;
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(crate.transform.position));
        Require(crate.RemainingHealth == 5 && game.score == score && game.QuickscopeKills == 2,
            "Crate damage cannot create quickscope rewards");
        crate.gameObject.SetActive(false); Object.Destroy(crate.gameObject);
        game.Powerups.ResetRound();
    }

    static void CheckBossDefense(GameManager game)
    {
        var shooting = game.GetComponent<ShootingSystem>();
        Require(!testBoss.TryHit(0) && !testBoss.TryHit(-1) && testBoss.RemainingHealth == 24,
            "Boss health rejects zero and negative damage");
        var blocked = testBoss.DropPoop();
        int hits = game.Hits, misses = game.Misses, score = game.score;
        Require(shooting.TryShootAt(Camera.main.WorldToScreenPoint(blocked.transform.position)) && blocked.IsResolved &&
            game.PlayerHealth == 100 && game.Hits == hits + 1 && game.Misses == misses && game.score == score,
            "Shooting a dropping blocks its damage, counts as a hit, and awards no farmable points");
        Require(!blocked.Impact(), "An intercepted dropping cannot damage the player afterward");
        var impact = testBoss.DropPoop();
        Require(impact.Impact() && game.PlayerHealth == 88 && !impact.Impact(),
            "An unblocked dropping removes twelve health exactly once");
        Require(!game.DamagePlayer(12) && game.PlayerHealth == 88,
            "Brief invulnerability prevents stacked hits in the same damage window");
        Require(!game.DamagePlayer(0) && !game.DamagePlayer(-10), "Invalid player damage is rejected");
        testDropping = testBoss.DropPoop();
    }

    static void CheckBossDamageAndDefeat(GameManager game)
    {
        var shooting = game.GetComponent<ShootingSystem>();
        int score = game.score, wallet = game.Progress.Points;
        for (int i = 0; i < 3; i++) shooting.TryShootAt(Camera.main.WorldToScreenPoint(testBoss.transform.position));
        Require(testBoss.RemainingHealth == 21 && game.score == score && game.Progress.Points == wallet,
            "Three ordinary shots remove three boss health without premature reward");
        game.Powerups.Activate(WingShotPowerup.Sniper);
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(testBoss.transform.position));
        Require(testBoss.RemainingHealth == 18 && !testBoss.IsDefeated,
            "The sniper deals three boss damage but cannot one-shot the boss");
        game.UI.Refresh(); game.UI.SendMessage("LateUpdate");
        if (SessionState.GetBool("WingShot.Checks.Capture", false))
        {
            CaptureFrame("Logs/WingShot-Boss-Battle.png");
            CaptureFrame("Logs/WingShot-Boss-Battle-4x3.png", 1024, 768);
        }
        for (int i = 0; i < 5; i++) shooting.TryShootAt(Camera.main.WorldToScreenPoint(testBoss.transform.position));
        Require(testBoss.RemainingHealth == 3 && game.score == score, "The boss pays no reward until its final health is depleted");
        shooting.TryShootAt(Camera.main.WorldToScreenPoint(testBoss.transform.position));
        Require(testBoss.IsDefeated && game.Encounter.CurrentBoss == null && game.Encounter.BossesDefeated == 1 &&
            game.score == score + 300 && game.Progress.Points == wallet + 300,
            "Defeating the boss awards exactly three hundred score and cosmetic points once");
        Require(!testBoss.TryHit(3) && testDropping.IsResolved && !testDropping.Impact() &&
            WingShotObjects.All<WingShotDropping>().Length == 0,
            "Boss defeat cancels every falling dropping and prevents duplicate rewards or late damage");
        var rematch = game.Encounter.SpawnBoss();
        Require(rematch != null && rematch.MaxHealth == 30 && WingShotBoss.DroppingDamage == 12,
            "A later boss gains modest health while keeping the same balanced player damage");
    }

    static void CheckScopedView(GameManager game)
    {
        var scope = game.Scope;
        var camera = Camera.main;
        float originalSize = camera.orthographicSize;
        Vector3 originalPosition = camera.transform.position;
        var crate = game.Powerups.CurrentCrate;
        crate.gameObject.SetActive(false);
        scope.enabled = false;
        try
        {
            Require(scope.SetAiming(true, scope.AimScreenPosition), "An active sniper accepts scope aiming");
            for (int i = 0; i < 20; i++) scope.AdvanceView(.05f, Vector2.zero);
            Require(scope.IsAiming && Mathf.Abs(camera.orthographicSize - originalSize * .5f) < .01f &&
                scope.ZoomAmount > .99f && (!Application.isFocused || Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked),
                "Scope smoothly reaches two-times optical zoom and centers mouse aiming");
            var target = MakeTarget(Object.FindAnyObjectByType<BirdSpawner>());
            target.Configure(3, 60);
            target.gameObject.AddComponent<WingShotBirdVisual>();
            target.GetComponent<SpriteRenderer>().color = CosmeticCatalog.Birds[game.Progress.Selected(CosmeticCategory.Bird)].Color;
            game.UI.Refresh();
            game.UI.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases();
            var lens = game.UI.GetComponentInChildren<WingShotScopeOverlay>();
            if (lens == null) lens = Object.FindAnyObjectByType<WingShotScopeOverlay>();
            Require(lens != null && lens.gameObject.activeInHierarchy && lens.rectTransform.rect.width > 100f &&
                lens.canvasRenderer.GetMesh() != null && lens.canvasRenderer.GetMesh().vertexCount > 100,
                "The scoped view renders a full-size circular aperture and graduated reticle");
            if (SessionState.GetBool("WingShot.Checks.Capture", false))
            {
                CaptureFrame("Logs/WingShot-Sniper-Scope.png");
                CaptureFrame("Logs/WingShot-Sniper-Scope-4x3.png", 1024, 768);
            }
            Require(game.GetComponent<ShootingSystem>().TryShootAt(scope.AimScreenPosition) && target.IsHit,
                "A centered scope shot hits the magnified bird through the real collider path");
            scope.AdvanceView(1f, new Vector2(10000f, 10000f));
            Require(camera.transform.position.x > originalPosition.x && camera.transform.position.y > originalPosition.y &&
                camera.transform.position.x <= originalPosition.x + originalSize * .5f * camera.aspect + .01f &&
                camera.transform.position.y <= originalPosition.y + originalSize * .5f + .01f,
                "Mouse movement pans scoped aim and stays inside the original meadow");
            scope.SetAiming(false);
            for (int i = 0; i < 20; i++) scope.AdvanceView(.05f, Vector2.zero);
            Require(Mathf.Abs(camera.orthographicSize - originalSize) < .01f &&
                Vector3.Distance(camera.transform.position, originalPosition) < .01f && Cursor.lockState == CursorLockMode.None,
                "Releasing aim restores normal camera size, position and cursor behavior");
            scope.SetAiming(true, scope.AimScreenPosition);
            scope.AdvanceView(.25f, Vector2.zero);
            game.PauseGame();
            Require(!scope.IsAiming && scope.ZoomAmount == 0f && Cursor.lockState == CursorLockMode.None,
                "Pause immediately exits the scope and releases the cursor");
            game.ResumeGame();
        }
        finally
        {
            scope.ResetView();
            scope.enabled = true;
            crate.gameObject.SetActive(true);
            game.UI.SendMessage("LateUpdate");
        }
    }

    static void CaptureCrateTypes(GameManager game)
    {
        var original = game.Powerups.CurrentCrate;
        var previews = new List<GameObject>();
        try
        {
            if (original != null) original.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                var preview = new GameObject("Crate preview", typeof(SpriteRenderer), typeof(BoxCollider2D));
                previews.Add(preview);
                preview.GetComponent<SpriteRenderer>().sharedMaterial = original.GetComponent<SpriteRenderer>().sharedMaterial;
                preview.transform.position = Camera.main.ViewportToWorldPoint(new Vector3(.15f + i * .23f, .43f,
                    Mathf.Abs(Camera.main.transform.position.z)));
                preview.transform.localScale = Vector3.one * 1.25f;
                var target = preview.AddComponent<WingShotCrate>();
                target.Initialize(game, (WingShotPowerup)i, 8, false);
                target.enabled = false;
            }
            CaptureFrame("Logs/WingShot-Crate-Rewards.png");
            CaptureFrame("Logs/WingShot-Crate-Rewards-4x3.png", 1024, 768);
        }
        finally
        {
            foreach (var preview in previews) { preview.SetActive(false); Object.Destroy(preview); }
            if (original != null) original.gameObject.SetActive(true);
        }
    }

    static void CheckCombatAndPenalties(GameManager game, BirdSpawner spawner, ShootingSystem shooting, Vector2 screen)
    {
        float beforeTime = game.timeRemaining;
        int wallet = game.Progress.Points;
        Require(shooting.TryShootAt(screen) && game.score == -10 &&
            Mathf.Approximately(game.timeRemaining, beforeTime - 1f),
            "Repeated misses still subtract ten points below zero and one second each");
        var sturdy = MakeTarget(spawner);
        sturdy.Configure(2, 30);
        int hits = game.Hits;
        Require(shooting.TryShootAt(screen) && game.Hits == hits + 1 && game.score == -10 &&
            sturdy.RemainingHealth == 1 && !sturdy.IsHit && sturdy.GetComponent<Collider2D>().enabled &&
            game.Progress.Points == wallet,
            "First hit damages a two-hit bird, counts for accuracy, and awards no premature reward");
        Require(shooting.TryShootAt(screen) && sturdy.IsHit && sturdy.RemainingHealth == 0 &&
            game.score == 20 && game.Progress.Points == wallet + 30 && game.EarnedPoints == 40,
            "Second hit defeats a two-hit bird and pays exactly thirty points once");
        Require(!sturdy.TryHit(), "A defeated multi-hit bird rejects additional damage");
        var elite = MakeTarget(spawner);
        elite.Configure(3, 60);
        Require(shooting.TryShootAt(screen) && elite.RemainingHealth == 2 && !elite.IsHit && game.score == 20,
            "An elite bird survives its first shot");
        Require(shooting.TryShootAt(screen) && elite.RemainingHealth == 1 && !elite.IsHit && game.score == 20,
            "An elite bird survives its second shot");
        Require(shooting.TryShootAt(screen) && elite.IsHit && game.score == 80 &&
            game.Progress.Points == wallet + 90 && game.EarnedPoints == 100,
            "Third hit defeats an elite bird and pays exactly sixty points");
        int level = game.DifficultyLevel;
        game.RegisterShot(false);
        Require(game.DifficultyLevel == level && game.EarnedPoints == 100,
            "Misses do not reduce flight difficulty or erase already earned rewards");
        Require(game.Progress.BestScore == 0, "Provisional scores are not saved as best before miss penalties settle");
        Require(game.Shots == game.Hits + game.Misses &&
            Mathf.Approximately(game.Accuracy, 100f * game.Hits / game.Shots),
            "Multi-hit damage and misses both contribute correctly to accuracy");
    }

    static void CheckLandscapeThemes(GameManager game)
    {
        int original = game.Progress.Selected(CosmeticCategory.Background);
        int layers = game.Landscape.GetComponentsInChildren<SpriteRenderer>().Length;
        for (int i = 0; i < CosmeticCatalog.Backgrounds.Length; i++)
        {
            game.Landscape.Apply(i);
            Require(Camera.main.backgroundColor == CosmeticCatalog.Backgrounds[i].Color,
                "Scenery theme applies: " + CosmeticCatalog.Backgrounds[i].Name);
        }
        Require(layers == 25 && game.Landscape.GetComponentsInChildren<SpriteRenderer>().Length == layers &&
            game.Landscape.GetComponentsInChildren<Collider2D>().Length == 0,
            "Switching scenery reuses cloud and wind layers without adding colliders or duplicate renderers");
        var ground = GameObject.Find("Grass").GetComponent<SpriteRenderer>();
        var woodland = game.Landscape.transform.Find("Wing Shot Woodland").GetComponent<SpriteRenderer>();
        Require(ground.sortingOrder < woodland.sortingOrder,
            "The original grass placeholder does not cover the new stream, fence and flowers");
        game.Landscape.Apply(original);
    }

    // Check purchases and saved selections using temporary PlayerPrefs keys.
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
            progress.Earn(100000, 500);
            foreach (CosmeticCategory category in Enum.GetValues(typeof(CosmeticCategory)))
            {
                var items = CosmeticCatalog.Items(category);
                Require(items.Length == 7 && items[items.Length - 1].Cost >= 4500,
                    "Seven styles and a high-price goal exist in " + category);
                var ids = new HashSet<string>();
                for (int i = 0; i < items.Length; i++)
                {
                    Require(ids.Add(items[i].Id), "Cosmetic IDs are unique in " + category, false);
                    int before = progress.Points;
                    bool owned = progress.IsUnlocked(category, i);
                    Require(progress.UnlockAndSelect(category, i) &&
                        progress.Points == before - (owned ? 0 : items[i].Cost),
                        "Every catalog purchase deducts its exact price", false);
                    Require(new CosmeticProgress(prefix).Selected(category) == i,
                        "Every catalog selection persists", false);
                }
            }
            Require(progress.Selected(CosmeticCategory.Crosshair) == 6 &&
                progress.Selected(CosmeticCategory.Bird) == 6 && progress.Selected(CosmeticCategory.Background) == 6,
                "Premium cosmetics purchase and persist in every category");
        }
        finally { foreach (var key in PreferenceKeys(prefix)) PlayerPrefs.DeleteKey(key.key); PlayerPrefs.Save(); }
    }

    // List the save keys that testing must preserve or clean up.
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

    // Back up the player's saved values before gameplay tests change them.
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

    // Put the player's original saved values back after the tests.
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

    // Record gameplay errors and keep unrelated Editor search errors in a separate list.
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

    // Record the failed check and stop the test safely.
    static void Fail(Exception e)
    {
        report.errors.Add(e.ToString());
        report.passed = false;
        Finish();
    }

    // Leave Play Mode before restoring saves and writing the report.
    static void Finish()
    {
        SaveReport();
        SessionState.SetBool(Ending, true);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else Complete();
    }

    // Continue or finish tests when Unity enters or leaves Play Mode.
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

    // Restore settings, write the final report, and close a batch test run.
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

    // Keep the current test report safe across Editor script reloads.
    static void SaveReport()
    {
        if (report != null) SessionState.SetString(ReportKey, JsonUtility.ToJson(report));
    }

    // Write the completed check results to a readable JSON file.
    static void WriteReport()
    {
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WingShotValidation.json", JsonUtility.ToJson(report, true));
    }
}

