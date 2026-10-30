# Wing Shot

Open Assets/Scenes/SampleScene.unity, wait for compilation, and press Play. No new Inspector wiring is required. The existing GameManager, bird prefab, Canvas, TMP labels, results panel, and restart callback are preserved. New menus and HUD elements are generated at runtime.

## Editor version

The supplied project records **6000.6.0f1**, and that is the locally installed editor. ProjectVersion.txt and package versions have been preserved. Runtime scripts use Unity 6 APIs, with a compatibility branch for object discovery in 6000.0. Opening this existing package set in 6000.0.60f1 has not been verified; use the project's recorded editor for the deadline.

## Controls and rules

- Left mouse button: fire once per click. A hit gives 10 points.
- ESC: pause or resume. Losing application focus also pauses; resume explicitly.
- Menus use the system cursor; gameplay uses the selected custom crosshair.
- Each round lasts 60 seconds. Hits, misses, accuracy, score, remaining time, and flight level appear on the HUD.
- Accuracy is hits / (hits + misses); zero shots shows 0%.
- Flight level increases every 50 score points, up to level 10. Birds spawn faster and fly faster, with random heights, both directions, and straight, wave, or swooping paths.
- Every scored point immediately enters the cosmetic wallet and lifetime total. Leaving a round retains earned points. Restarting never awards the same round twice.
- Main Menu > Customize lets you buy and select styles. Purchases deduct the listed price once. Owned styles remain free to select.

| Category | Free | Unlock 1 | Unlock 2 |
| --- | --- | --- | --- |
| Crosshair | Classic | Bullseye: 75 | Diamond: 150 |
| Bird color | Golden Finch | Coral Swift: 100 | Mint Glider: 200 |
| Background | Blue Skies | Golden Hour: 125 | Moonlit Meadow: 250 |

Wallet, lifetime points, best score, unlocks, selected cosmetics, and sound preference persist using PlayerPrefs keys beginning with WingShot.v1.

No audio files were supplied. Five short synthesized arcade effects provide shooting, hit, miss, unlock, and game-over sounds without missing references. Sound can be toggled in the main and pause menus. Optional AudioClip overrides are supported by WingShotAudio; add that component to GameManager to assign replacements before Play.

## Quick acceptance test

1. Open SampleScene and press Play. Check the WING SHOT title, Start Game, Customize, best score, and points balance.
2. Start a game. Click a bird, then empty sky. Confirm score +10, one hit, one miss, and 50% accuracy. Click the Pause button; it must not count a shot.
3. Press ESC to resume, then ESC to pause. Timer and birds must stop. Resume and verify both continue. Switching to another application should pause the game.
4. From pause, choose Main Menu. Start again. Score, hits, misses, and timer must reset, while earned cosmetic points remain.
5. Earn at least 75 points. Open Customize, buy Bullseye, then select it again. Confirm only one deduction. Try bird and background styles when affordable; confirm them during play.
6. Stop and restart Play Mode (or relaunch a built player). Confirm the wallet, owned cosmetics, selected styles, best score, and sound setting persist.
7. Let the timer reach zero. Confirm final score, accuracy, hits, misses, earned points, and Play Again. Repeat Play Again and Main Menu several times and check the Console.
8. Test the Game view at 16:9 and 4:3.

## Validation and building

- Tools > Wing Shot > Validate Scene checks existing references without saving the scene.
- Tools > Wing Shot > Run Automated Play Mode Checks exercises economy persistence, actual 2D collider shooting, UI blocking, timing, pause/resume, difficulty, round expiry, repeated restarts, and a scene reload while paused. It restores the original PlayerPrefs after finishing. Save open work before running editor tests.
- The report is written to Logs/WingShotValidation.json.
- Tools > Wing Shot > Build Windows Player builds Builds/Windows/Wing Shot.exe.
- Tools/Compile-WingShot.ps1 compiles runtime and Editor sources against installed Unity assemblies. It expects Unity-generated C# project files; regenerate them through Unity if absent.
- Tools/Validate-WingShot.ps1 runs the automated checks in a disposable copy under Temp/WingShotValidationProject, with a separate PlayerPrefs identity. Add -CaptureScreenshots for a graphics-enabled run. It launches asynchronously and prints the log/report paths.

Automated checks exercise the game inside Unity Play Mode. They do not replace a human check of mouse feel, audio levels, or visual quality on the final target display.

## Inspector adjustments (optional)

- GameManager.roundDuration: 60 seconds by default.
- GameManager.useProceduralBirdArt: enabled by default; disable to render the original prefab sprite.
- BirdSpawner.spawnInterval: the base interval before score difficulty scaling.
- WingShotAudio: optional assigned clips and synthesized-fallback toggle.

Original asset backup: Backups/Before-WingShot-20261029-175755.zip. The authored scene, bird prefab, package manifest, and project settings were not rewritten for the new features.

Input implementation follows Unity's Input System device polling guidance:
https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/corresponding-old-new-api.md

Delivered test and build reports are in Verification/. Rerunning the tools writes fresh reports into Logs/. The Windows player closes with Alt+F4.
