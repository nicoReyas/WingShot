using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Builds around the original Canvas, score/timer labels, results panel and restart button.
// Everything is runtime-only: the authored scene and persistent references stay intact.
public class WingShotUI : MonoBehaviour
{
    // Shared palette keeps all generated screens consistent with the original game UI.
    static readonly Color Navy = new Color(.125f, .247f, .345f);
    static readonly Color Cream = new Color(.97f, .98f, .91f);
    static readonly Color Yellow = new Color(1f, .84f, .27f);
    static readonly Color Muted = new Color(.70f, .82f, .85f);
    GameManager game;
    Canvas canvas;
    TMP_FontAsset font;
    GameObject hud, mainMenu, pauseMenu, customization, resultsOverlay;
    TMP_Text stats, level, feedback, mainProgress, customizationPoints, customizationMessage;
    TMP_Text resultStats, resultProgress;
    TMP_Text mainSoundLabel, pauseSoundLabel;
    WingShotCrosshair crosshair;
    float feedbackUntil;
    // Store each shop button with the category and style it controls.
    [SerializeField] List<CosmeticButton> cosmeticButtons = new List<CosmeticButton>();
    readonly Dictionary<WingShotState, Button> firstButtons = new Dictionary<WingShotState, Button>();
    bool built;

    [System.Serializable]
    sealed class CosmeticButton
    {
        public CosmeticCategory Category;
        public int Index;
        public Button Button;
        public TMP_Text Label;
    }

    // Create the menus around the existing Canvas and connect their buttons.
    public void Build(GameManager manager)
    {
        if (built) return;
        built = true;
        game = manager;
        font = game.scoreText == null ? TMP_Settings.defaultFontAsset : game.scoreText.font;
        canvas = game.scoreText == null ? FindAnyObjectByType<Canvas>() : game.scoreText.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            var created = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = created.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
        }
        EnsureEventSystem();
        BuildHud();
        BuildMainMenu();
        BuildPauseMenu();
        BuildCustomization();
        BuildResults();
        crosshair = Crosshair(canvas.transform, new Vector2(36, 36), Vector2.zero, 0);
        crosshair.name = "Gameplay Crosshair";
        // A decorative cursor must never intercept a shot or button click.
        crosshair.raycastTarget = false;
        BindActions();
        Refresh();
    }

    // Create a UI input handler only if the scene does not already have one.
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    // Lay out the score, timer, shot statistics, pause button, and instructions.
    void BuildHud()
    {
        hud = FullRect("Gameplay HUD", canvas.transform);
        var bar = Panel("Scoreboard", hud.transform, new Color(Cream.r, Cream.g, Cream.b, .94f), false);
        var r = bar.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one;
        r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(0, 86);
        r.anchoredPosition = Vector2.zero;

        if (game.scoreText == null) game.scoreText = Label(hud.transform, "", 30, Navy, Vector2.zero, new Vector2(220, 46));
        game.scoreText.transform.SetParent(hud.transform, false);
        Place(game.scoreText.rectTransform, new Vector2(220, 46), new Vector2(136, -37), new Vector2(0, 1));
        Style(game.scoreText, 30, Navy, TextAlignmentOptions.Left);

        if (game.timerText == null) game.timerText = Label(hud.transform, "", 30, Navy, Vector2.zero, new Vector2(210, 46));
        game.timerText.transform.SetParent(hud.transform, false);
        Place(game.timerText.rectTransform, new Vector2(200, 46), new Vector2(-245, -37), Vector2.one);
        Style(game.timerText, 30, Navy, TextAlignmentOptions.Right);
        stats = Label(hud.transform, "", 21, Navy, new Vector2(0, -31), new Vector2(540, 34), new Vector2(.5f, 1));
        level = Label(hud.transform, "", 15, Navy, new Vector2(0, -62), new Vector2(540, 24), new Vector2(.5f, 1));
        var pause = MakeButton(hud.transform, "PAUSE", new Vector2(-65, -40), new Vector2(108, 44), game.PauseGame);
        Place(pause.GetComponent<RectTransform>(), new Vector2(108, 44), new Vector2(-65, -40), Vector2.one);
        Label(hud.transform, "LEFT CLICK TO SHOOT    /    ESC TO PAUSE", 16, Navy,
            new Vector2(0, 27), new Vector2(650, 30), new Vector2(.5f, 0));
        feedback = Label(hud.transform, "", 26, Navy, new Vector2(0, -117), new Vector2(440, 45), new Vector2(.5f, 1));
    }

    // Create the title screen with Start Game, Customize, and sound controls.
    void BuildMainMenu()
    {
        mainMenu = Overlay("Main Menu");
        var card = Card(mainMenu.transform, "Main Menu Card", new Vector2(800, 560));
        Label(card, "A LITTLE AIM. A LOT OF SKY.", 17, Muted, new Vector2(0, 219), new Vector2(720, 30));
        Label(card, "WING SHOT", 78, Yellow, new Vector2(0, 142), new Vector2(750, 107));
        Label(card, "Catch the flight. Beat your best.", 24, Cream, new Vector2(0, 62), new Vector2(730, 44));
        mainProgress = Label(card, "", 21, Muted, new Vector2(0, 6), new Vector2(730, 35));
        var start = MakeButton(card, "START GAME", new Vector2(0, -70), new Vector2(430, 66), game.StartGame);
        firstButtons[WingShotState.MainMenu] = start;
        MakeButton(card, "CUSTOMIZE", new Vector2(0, -151), new Vector2(430, 60), game.OpenCustomization);
        Label(card, "60 seconds. +10 per hit. Spend earned points on your own style.", 16, Cream,
            new Vector2(0, -215), new Vector2(745, 30));
        var sound = MakeButton(card, "", new Vector2(0, -253), new Vector2(200, 32), game.ToggleSound);
        mainSoundLabel = sound.GetComponentInChildren<TMP_Text>();
        mainSoundLabel.fontSize = 14;
    }

    // Create the paused screen with Resume and Main Menu buttons.
    void BuildPauseMenu()
    {
        pauseMenu = Overlay("Pause Menu");
        var card = Card(pauseMenu.transform, "Pause Card", new Vector2(700, 460));
        Label(card, "TAKE A BREATHER", 18, Muted, new Vector2(0, 174), new Vector2(650, 35));
        Label(card, "PAUSED", 65, Yellow, new Vector2(0, 107), new Vector2(650, 85));
        var resume = MakeButton(card, "RESUME", new Vector2(0, 8), new Vector2(400, 65), game.ResumeGame);
        firstButtons[WingShotState.Paused] = resume;
        MakeButton(card, "MAIN MENU", new Vector2(0, -73), new Vector2(400, 58), game.ReturnToMainMenu);
        Label(card, "Your earned points are already saved. ESC to resume.", 17, Cream,
            new Vector2(0, -143), new Vector2(650, 34));
        var sound = MakeButton(card, "", new Vector2(0, -190), new Vector2(200, 32), game.ToggleSound);
        pauseSoundLabel = sound.GetComponentInChildren<TMP_Text>();
        pauseSoundLabel.fontSize = 14;
    }

    // Create three columns of purchasable and selectable cosmetic styles.
    void BuildCustomization()
    {
        customization = Overlay("Customization");
        var card = Card(customization.transform, "Customization Card", new Vector2(1080, 620));
        Label(card, "MAKE IT YOURS", 43, Yellow, new Vector2(-150, 254), new Vector2(710, 65));
        customizationPoints = Label(card, "", 24, Cream, new Vector2(370, 254), new Vector2(280, 60));
        Label(card, "Earn points with every hit. Unlock once, keep forever.", 19, Muted,
            new Vector2(0, 195), new Vector2(980, 35));
        string[] headings = { "CROSSHAIRS", "BIRD COLORS", "BACKGROUNDS" };
        for (int categoryIndex = 0; categoryIndex < 3; categoryIndex++)
        {
            var category = (CosmeticCategory)categoryIndex;
            float x = (categoryIndex - 1) * 350;
            Label(card, headings[categoryIndex], 18, Cream, new Vector2(x, 143), new Vector2(325, 35));
            var items = CosmeticCatalog.Items(category);
            for (int i = 0; i < items.Length; i++)
            {
                int capturedIndex = i;
                var capturedCategory = category;
                var button = MakeButton(card, "", new Vector2(x, 65 - i * 105), new Vector2(324, 90),
                    () => game.SelectCosmetic(capturedCategory, capturedIndex));
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 20; label.fontSizeMin = 14;
                Place(label.rectTransform, new Vector2(234, 80), new Vector2(35, 0));
                cosmeticButtons.Add(new CosmeticButton { Category = category, Index = i, Button = button, Label = label });
                if (category == CosmeticCategory.Crosshair)
                    Crosshair(button.transform, new Vector2(40, 40), new Vector2(-118, 0), i);
                else
                {
                    var swatch = Panel("Preview", button.transform, items[i].Color, false);
                    Place(swatch.rectTransform, new Vector2(52, 43), new Vector2(-118, 0));
                    if (category == CosmeticCategory.Bird)
                    {
                        swatch.sprite = WingShotArt.Bird(0);
                        swatch.preserveAspect = true;
                    }
                }
            }
        }
        customizationMessage = Label(card, "Choose an unlocked style, or spend points to unlock one.", 18, Cream,
            new Vector2(0, -224), new Vector2(1020, 35));
        var back = MakeButton(card, "BACK TO MENU", new Vector2(0, -274), new Vector2(280, 48), game.ReturnToMainMenu);
        firstButtons[WingShotState.Customization] = back;
    }

    // Reuse the existing game-over panel and add the final round statistics.
    void BuildResults()
    {
        resultsOverlay = Overlay("Results Overlay");
        if (game.gameOverPanel == null)
            game.gameOverPanel = Panel("GameOverPanel", resultsOverlay.transform, Navy, true).gameObject;
        game.gameOverPanel.transform.SetParent(resultsOverlay.transform, false);
        var card = game.gameOverPanel.GetComponent<RectTransform>();
        Place(card, new Vector2(780, 590), Vector2.zero);
        var background = card.GetComponent<Image>();
        if (background != null) { background.color = Navy; background.raycastTarget = true; }

        TMP_Text heading = null;
        foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
            if (text.gameObject.name == "GameOverText") { heading = text; break; }
        if (heading == null) heading = Label(card, "GAME OVER", 54, Yellow, Vector2.zero, new Vector2(720, 75));
        heading.text = "NICE SHOOTING!";
        Place(heading.rectTransform, new Vector2(720, 78), new Vector2(0, 212));
        Style(heading, 52, Yellow, TextAlignmentOptions.Center);
        Label(card, "TIME'S UP - HERE'S YOUR FLIGHT REPORT", 16, Muted, new Vector2(0, 158), new Vector2(730, 30));

        if (game.finalScoreText == null)
            game.finalScoreText = Label(card, "", 50, Cream, Vector2.zero, new Vector2(720, 70));
        Place(game.finalScoreText.rectTransform, new Vector2(720, 72), new Vector2(0, 93));
        Style(game.finalScoreText, 50, Cream, TextAlignmentOptions.Center);
        resultStats = Label(card, "", 28, Cream, new Vector2(0, 5), new Vector2(730, 88));
        resultProgress = Label(card, "", 22, Muted, new Vector2(0, -88), new Vector2(730, 70));

        var restart = card.GetComponentInChildren<Button>(true);
        if (restart == null)
            restart = MakeButton(card, "PLAY AGAIN", Vector2.zero, new Vector2(390, 62), game.RestartGame);
        else
        {
            restart.onClick = new Button.ButtonClickedEvent();
            restart.onClick.AddListener(game.RestartGame);
        }
        Place(restart.GetComponent<RectTransform>(), new Vector2(390, 62), new Vector2(0, -170));
        StyleButton(restart);
        var restartLabel = restart.GetComponentInChildren<TMP_Text>();
        if (restartLabel != null)
        {
            restartLabel.text = "PLAY AGAIN";
            Place(restartLabel.rectTransform, new Vector2(370, 56), Vector2.zero);
            Style(restartLabel, 24, Navy, TextAlignmentOptions.Center);
        }
        firstButtons[WingShotState.GameOver] = restart;
        MakeButton(card, "MAIN MENU", new Vector2(0, -245), new Vector2(390, 53), game.ReturnToMainMenu);
    }

    // Show the screen matching the current game state and hide the other screens.
    public void ShowState()
    {
        if (!built) return;
        hud.SetActive(game.IsPlaying);
        mainMenu.SetActive(game.State == WingShotState.MainMenu);
        pauseMenu.SetActive(game.State == WingShotState.Paused);
        customization.SetActive(game.State == WingShotState.Customization);
        resultsOverlay.SetActive(game.State == WingShotState.GameOver);
        game.gameOverPanel.SetActive(game.State == WingShotState.GameOver);
        crosshair.gameObject.SetActive(game.IsPlaying);
        feedback.text = "";
        feedbackUntil = 0;
        if (customizationMessage != null)
            customizationMessage.text = "Choose an unlocked style, or spend points to unlock one.";
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (firstButtons.TryGetValue(game.State, out var first))
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        Refresh();
    }

    // Update scores, prices, selections, and sound labels from the latest game data.
    public void Refresh()
    {
        if (!built || game.Progress == null) return;
        game.scoreText.text = "SCORE  " + game.score;
        game.timerText.text = "TIME  " + Mathf.CeilToInt(game.timeRemaining).ToString("00");
        game.timerText.color = game.timeRemaining <= 10f ? new Color(.75f, .22f, .22f) : Navy;
        stats.text = "HITS " + game.Hits + "    MISSES " + game.Misses + "    ACCURACY " + game.Accuracy.ToString("0") + "%";
        level.text = "FLIGHT LEVEL " + game.DifficultyLevel + (game.DifficultyLevel == 10 ? "  /  MAX" : "  /  NEXT AT " + game.DifficultyLevel * 50 + " PTS");
        mainProgress.text = "BEST  " + game.Progress.BestScore + "       /       POINTS  " + game.Progress.Points;
        customizationPoints.text = game.Progress.Points + " POINTS";
        game.finalScoreText.text = "SCORE  " + game.score;
        resultStats.text = game.Accuracy.ToString("0") + "% ACCURACY\n" + game.Hits + " HITS    /    " + game.Misses + " MISSES";
        resultProgress.text = "+" + game.score + " POINTS EARNED   /   BEST " + game.Progress.BestScore +
            "\n" + game.Progress.Points + " POINTS TO CUSTOMIZE";
        string sound = game.Progress.Muted ? "SOUND: OFF" : "SOUND: ON";
        mainSoundLabel.text = pauseSoundLabel.text = sound;
        if (crosshair != null)
        {
            int index = game.Progress.Selected(CosmeticCategory.Crosshair);
            crosshair.SetStyle(index);
            crosshair.color = CosmeticCatalog.Crosshairs[index].Color;
        }
        // Keep each shop entry matched to its saved ownership and selection.
        foreach (var entry in cosmeticButtons)
        {
            var item = CosmeticCatalog.Items(entry.Category)[entry.Index];
            bool unlocked = game.Progress.IsUnlocked(entry.Category, entry.Index);
            bool selected = game.Progress.Selected(entry.Category) == entry.Index;
            entry.Button.interactable = unlocked || game.Progress.Points >= item.Cost;
            entry.Button.GetComponent<Image>().color = selected ? new Color(.55f, .89f, .73f) : Yellow;
            entry.Label.text = item.Name + "\n<size=15>" +
                (selected ? "SELECTED" : unlocked ? "SELECT" : "UNLOCK - " + item.Cost + " PTS") + "</size>";
        }
    }

    // Show a short hit or miss message and flash the crosshair.
    public void ShotFeedback(bool hit)
    {
        feedback.text = hit ? "HIT  +10" : "MISS";
        feedback.color = hit ? new Color(.12f, .36f, .23f) : new Color(.68f, .17f, .18f);
        feedbackUntil = Time.unscaledTime + .5f;
        crosshair.Pulse(hit);
        Refresh();
    }

    // Show a short confirmation after selecting or unlocking a style.
    public void ShowCustomizationMessage(string message) { customizationMessage.text = message; }

    // Move the custom cursor to the mouse and clear expired shot feedback.
    void LateUpdate()
    {
        if (!built || crosshair == null) return;
        Vector2 pointer = WingShotInput.PointerPosition;
        bool visible = game.IsPlaying && Application.isFocused && WingShotInput.HasPointer &&
            pointer.x >= 0 && pointer.y >= 0 && pointer.x < Screen.width && pointer.y < Screen.height;
        crosshair.gameObject.SetActive(visible);
        // Menus use the system cursor; active gameplay shows our custom crosshair.
        Cursor.visible = !visible;
        if (visible)
        {
            var parent = (RectTransform)crosshair.transform.parent;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer, uiCamera, out var local))
                crosshair.rectTransform.anchoredPosition = local;
        }
        if (feedbackUntil > 0 && Time.unscaledTime > feedbackUntil)
        {
            feedback.text = "";
            feedbackUntil = 0;
        }
    }

    // Restore runtime button actions after Unity reloads scripts.
    void OnEnable()
    {
        if (!built) return;
        if (game == null) game = GetComponent<GameManager>();
        BindActions();
        RestoreDefaultButton(WingShotState.MainMenu, mainMenu, "START GAME");
        RestoreDefaultButton(WingShotState.Paused, pauseMenu, "RESUME");
        RestoreDefaultButton(WingShotState.Customization, customization, "BACK TO MENU");
        RestoreDefaultButton(WingShotState.GameOver, resultsOverlay, "PLAY AGAIN");
    }

    // Connect each visible button name and shop entry to its game action.
    void BindActions()
    {
        if (game == null || canvas == null) return;
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            UnityAction action = null;
            switch (label.text)
            {
                case "START GAME": action = game.StartGame; break;
                case "CUSTOMIZE": action = game.OpenCustomization; break;
                case "PAUSE": action = game.PauseGame; break;
                case "RESUME": action = game.ResumeGame; break;
                case "MAIN MENU":
                case "BACK TO MENU": action = game.ReturnToMainMenu; break;
                case "PLAY AGAIN": action = game.RestartGame; break;
                case "SOUND: ON":
                case "SOUND: OFF": action = game.ToggleSound; break;
            }
            if (action == null) continue;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }
        // Keep each shop entry matched to its saved ownership and selection.
        foreach (var entry in cosmeticButtons)
        {
            var category = entry.Category;
            int index = entry.Index;
            entry.Button.onClick = new Button.ButtonClickedEvent();
            entry.Button.onClick.AddListener(() => game.SelectCosmetic(category, index));
        }
    }
    // Remember which menu button keyboard navigation should select first.
    void RestoreDefaultButton(WingShotState state, GameObject root, string title)
    {
        if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text == title) { firstButtons[state] = button; return; }
        }
    }

    // Restore the system cursor if this UI component is disabled.
    void OnDisable() { Cursor.visible = true; }

    // Create a full-screen menu layer that blocks clicks from reaching birds.
    GameObject Overlay(string name)
    {
        var image = Panel(name, canvas.transform, new Color(.07f, .17f, .24f, .20f), true);
        Stretch(image.rectTransform);
        return image.gameObject;
    }

    // Create a dark menu panel with a small yellow accent line.
    Transform Card(Transform parent, string name, Vector2 size)
    {
        var panel = Panel(name, parent, Navy, true);
        Place(panel.rectTransform, size, Vector2.zero);
        // A thin yellow rule ties the new panels to the existing yellow buttons.
        var rule = Panel("Accent", panel.transform, Yellow, false);
        Place(rule.rectTransform, new Vector2(size.x - 64, 4), new Vector2(0, size.y * .5f - 24));
        return panel.transform;
    }

    // Create an empty UI container that fills its parent.
    GameObject FullRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go;
    }

    // Create a colored image and choose whether it should block mouse clicks.
    Image Panel(string name, Transform parent, Color color, bool blocksClicks)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksClicks;
        return image;
    }

    // Create and place a TextMeshPro text label.
    TMP_Text Label(Transform parent, string text, float size, Color color, Vector2 position,
        Vector2 dimensions, Vector2? anchor = null)
    {
        var go = new GameObject("Text - " + (text.Length > 24 ? text.Substring(0, 24) : text),
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        Place(label.rectTransform, dimensions, position, anchor);
        Style(label, size, color, TextAlignmentOptions.Center);
        return label;
    }

    // Apply the shared text size, color, alignment, and nonblocking behavior.
    static void Style(TMP_Text text, float size, Color color, TextAlignmentOptions alignment)
    {
        text.fontSize = size;
        text.fontSizeMax = size;
        text.fontSizeMin = size * .75f;
        text.enableAutoSizing = true;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.fontStyle = FontStyles.Bold;
        text.overflowMode = TextOverflowModes.Truncate;
    }

    // Create a yellow button, its text, and its click action.
    Button MakeButton(Transform parent, string text, Vector2 position, Vector2 dimensions, UnityAction action)
    {
        var panel = Panel("Button - " + text, parent, Yellow, true);
        Place(panel.rectTransform, dimensions, position);
        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;
        StyleButton(button);
        button.onClick.AddListener(action);
        Label(panel.transform, text, 24, Navy, Vector2.zero, dimensions - new Vector2(16, 6));
        return button;
    }

    // Apply colors for normal, highlighted, pressed, and unavailable buttons.
    static void StyleButton(Button button)
    {
        var image = button.GetComponent<Image>();
        if (image != null) { image.color = Yellow; image.raycastTarget = true; }
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .95f, .73f);
        colors.pressedColor = new Color(.82f, .79f, .58f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.78f, .83f, .9f, 1f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        // Mouse and explicit default selection work without arrow navigation jumping between columns.
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
    }

    // Create the same crosshair graphic for either gameplay or a shop preview.
    WingShotCrosshair Crosshair(Transform parent, Vector2 size, Vector2 position, int style)
    {
        var go = new GameObject("Crosshair Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(WingShotCrosshair));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var graphic = go.GetComponent<WingShotCrosshair>();
        Place(graphic.rectTransform, size, position);
        graphic.style = style;
        graphic.color = CosmeticCatalog.Crosshairs[style].Color;
        graphic.raycastTarget = false;
        return graphic;
    }

    // Make a UI rectangle fill its parent.
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    // Set a UI element's anchor, size, position, and normal scale.
    static void Place(RectTransform rect, Vector2 size, Vector2 position, Vector2? anchor = null)
    {
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }
}
