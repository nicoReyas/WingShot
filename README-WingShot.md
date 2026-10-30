# Wing Shot

Open Assets/Scenes/SampleScene.unity, wait for compilation, and press Play. No new Inspector wiring is required. The existing GameManager, bird prefab, Canvas, TMP labels, results panel, and restart callback are preserved. New menus and HUD elements are generated at runtime.

## Editor version

The supplied project records **6000.6.0f1**, and that is the locally installed editor. ProjectVersion.txt and package versions have been preserved. Runtime scripts use Unity 6 APIs, with a compatibility branch for object discovery in 6000.0. Opening this existing package set in 6000.0.60f1 has not been verified; use the project's recorded editor for the deadline.

## Controls and rules

- Left mouse button: fire once per click. Defeat a bird for 10, 30, or 60 points.
- Every miss subtracts **10 round-score points and one second** from the timer. Round scores can go below zero. A miss that uses the last second ends the round immediately; menu and pause clicks never incur penalties.
- Regular birds take one hit (+10), larger sturdy birds take two (+30), and elite birds take three (+60). Health pips show hits remaining, and damage briefly flashes the bird. Every damaging shot counts as a hit for accuracy; only defeating the bird pays its reward.
- ESC: pause or resume. Losing application focus also pauses; resume explicitly.
- Menus use the system cursor; gameplay uses the selected custom crosshair, with a temporary scope while the sniper is active.
- Each round starts with 60 seconds. Hits, misses, accuracy, score, remaining time, and flight level appear on the HUD.
- Accuracy is hits / (hits + misses); zero shots shows 0%.
- Each round opens with three targets already visible, including a two-hit bird. The existing scene now spawns another bird every 0.9 seconds initially, down to 0.28 seconds at higher levels. Normal birds start around 4.8 world units/second; tougher birds fly a little slower to allow follow-up shots.
- Flight level increases every 50 reward points earned during the round, up to level 10. Misses do not lower difficulty. Three-hit birds join the flock at level 3. Flights use random heights, both directions, and straight, wave, or swooping paths.
- Every bird reward immediately enters the cosmetic wallet and lifetime total. Miss penalties affect round score and time; saved currency stays earned. Results report earned currency separately from the penalized score. Leaving a round retains earned points. Restarting never awards the same round twice.
- Best score is recorded when the round ends, you leave it, or you quit, using the score after miss penalties.
- Main Menu > Customize lets you buy and select styles. Purchases deduct the listed price once. Owned styles remain free to select.

## Power-up crates

Reinforced supply crates start dropping after three seconds, then at nine-second intervals when the previous crate has been opened or expired. Each descends from above the screen over 1.35 seconds, settles with a small bounce, and then starts its 18-second lifetime. Pausing freezes the drop. The rewards rotate through sniper, double points, extra time, and slow birds. Each crate takes **8 damage** to open and shows a durability bar and reward badge. Only one supply crate is active at a time.

| Reward | Effect |
| --- | --- |
| Sniper | 3 damage per shot for 12 seconds, with a scope reticle and a distinct low rifle report. Defeats even three-hit birds in one shot and opens another crate in three shots. Hold right-click for 2x optical zoom. |
| Double points | Doubles bird rewards, round score, and earned cosmetic currency for 12 seconds. It does not increase miss penalties. |
| Extra time | Immediately adds 10 seconds, including above the round's starting time. |
| Slow birds | Halves bird movement and flight-pattern speed for 8 seconds. The round timer continues at its normal rate. |

Crate damage counts as a hit for accuracy, without awarding bird points or causing a miss penalty. A shot damages only the frontmost bird or crate. The opening hit automatically claims the reward once. Different timed effects work together; collecting the same effect refreshes its duration. The HUD shows active effects and their remaining seconds. Pausing freezes effect durations and crates; starting a round, returning to the menu, and game over clear them.

The sniper also has a dedicated countdown showing tenths of a second and a draining bar. While it is active, **hold right mouse to aim through the scope**, move the mouse to pan, and left-click to fire at the center reticle. A circular aperture, scope markings, and smooth camera zoom provide magnification of both birds and scenery. Release right mouse to zoom out. Pausing, losing focus, leaving the round, or weapon expiry restores the camera and frees the cursor. Scope zoom does not slow the clock or change power-up durations.

## Customization and scenery

The shop has 21 styles in three columns. Scroll the catalog or drag its scrollbar to reach premium styles. Existing purchases and selections keep their stable save IDs.

| Category | Free | Paid styles (points) |
| --- | --- | --- |
| Crosshair | Classic | Bullseye 75; Diamond 150; Pinpoint 500; Tactical 1,000; Orbital 2,500; Supernova 5,000 |
| Bird color | Golden Finch | Coral Swift 100; Mint Glider 200; Azure Streak 500; Royal Plumage 1,000; Arctic Pearl 2,200; Phoenix Gold 4,500 |
| Background | Blue Skies | Golden Hour 125; Moonlit Meadow 250; Autumn Grove 600; Alpine Frost 1,200; Aurora Wilds 2,400; Ember Valley 5,000 |

All seven environments have layered woodland scenery, distant mountains and pine forests, foreground trees, meadow flowers, fences, a winding stream, drifting clouds, and windblown particles. Autumn changes the foliage, winter adds snow, and night scenes add stars and fireflies; Aurora Wilds also adds northern lights. Scenery stays behind birds and never intercepts shots. Clouds and particles freeze during pause and results.

Wallet, lifetime points, best score, unlocks, selected cosmetics, and sound preference persist using PlayerPrefs keys beginning with WingShot.v1.

The original CC0 track "Skybound" provides cheerful looping background music. It uses synthesized instruments and no third-party samples; its dedication is in Assets/Resources/Audio/Skybound-LICENSE.txt. Eight synthesized arcade effects provide shooting, hit, miss, unlock, game-over, sniper, crate-hit, and crate-opening sounds without missing references. The SOUND button toggles music and effects together. Music fades quieter during pauses and results, and continues smoothly across rounds. Optional AudioClip overrides are supported by WingShotAudio; add that component to GameManager to assign replacements before Play.

## Quick acceptance test

1. Open SampleScene and press Play. Check the WING SHOT title, Start Game, Customize, best score, and points balance.
2. Start a game and confirm three visible targets immediately. Shoot a regular bird, then empty sky: the score should return to zero, the miss should cost an extra second, and accuracy should be 50%. Miss again to confirm the score can go negative. Click Pause; it must not count a shot.
3. Shoot a bird with two health pips. Its first hit removes one pip without awarding points; its second hit defeats it for +30. At flight level 3, look for three-pip birds worth +60. Confirm the displayed reward and remaining health match each shot.
4. Press ESC to resume, then ESC to pause. Timer, birds, clouds, and particles must stop. Resume and verify all continue. Switching to another application should pause the game.
5. From pause, choose Main Menu. Start again. Score, hits, misses, earned-round points, and timer must reset, while earned cosmetic points remain.
6. Earn at least 75 points. Open Customize, buy Bullseye, then select it again. Confirm only one deduction. Scroll to the premium styles. Try bird and background styles when affordable; confirm them during play.
7. Stop and restart Play Mode (or relaunch a built player). Confirm the wallet, owned cosmetics, selected styles, best score, and sound setting persist.
8. Let the timer reach zero, or miss with less than a second remaining. Confirm final score, accuracy, hits, misses, earned currency, and Play Again. Repeat Play Again and Main Menu several times and check the Console.
9. Test the Game view and scrolling shop at 16:9 and 4:3.
10. Wait three seconds for a sniper crate. Shoot it eight times. Its durability should decrease on every hit, without losing time or score; the final hit grants the sniper. Listen for the new rifle report, check the scope and countdown, and defeat a three-pip bird with one shot.
11. Open the next crates: double points should double bird rewards and wallet income, extra time should add 10 seconds, and slow birds should halve flight speed while the round clock continues normally. Pause with several effects active and confirm all their countdowns freeze. After they expire, the normal sound, selected crosshair, damage, points, and speed should return.
12. With the sniper active, watch its separate countdown and time bar. Hold right-click, aim with the mouse, and left-click a bird through the center scope reticle. Release right-click to restore the normal view; also check ESC and losing focus while scoped. Watch a fresh crate fall, pause mid-drop, and resume until it lands.

## Validation and building

- Tools > Wing Shot > Validate Scene checks existing references without saving the scene.
- Tools > Wing Shot > Run Automated Play Mode Checks exercises economy persistence across all 21 cosmetics, actual 2D collider shooting, multi-hit rewards, miss deductions, last-second miss expiry, UI blocking, shop scrolling, scenery themes, timing, pause/resume, difficulty, repeated restarts, and a scene reload while paused. It restores the original PlayerPrefs after finishing. Save open work before running editor tests.
- The report is written to Logs/WingShotValidation.json.
- Tools > Wing Shot > Build Windows Player builds Builds/Windows/Wing Shot.exe.
- Tools/Compile-WingShot.ps1 compiles runtime and Editor sources against installed Unity assemblies. It expects Unity-generated C# project files; regenerate them through Unity if absent.
- Tools/Validate-WingShot.ps1 runs the automated checks in a disposable copy under Temp/WingShotValidationProject, with a separate PlayerPrefs identity. Add -CaptureScreenshots for a graphics-enabled run. It launches asynchronously and prints the log/report paths.

Automated checks exercise the game inside Unity Play Mode. They do not replace a human check of mouse feel, audio levels, or visual quality on the final target display.

## Inspector adjustments (optional)

- GameManager.roundDuration: 60 seconds by default.
- GameManager.useProceduralBirdArt: enabled by default; disable to render the original prefab sprite.
- BirdSpawner.spawnInterval: the authored base interval, multiplied by paceMultiplier (0.45 by default) and flight-level scaling. Existing scene values need no edits.
- WingShotAudio: optional assigned clips and synthesized-fallback toggle.
- WingShotPowerups: firstDropDelay (3 seconds) and dropInterval (9 seconds). The component is created automatically on GameManager.

Original asset backup: Backups/Before-WingShot-20261029-175755.zip. The authored scene, bird prefab, package manifest, and project settings were not rewritten for the new features.

Input implementation follows Unity's Input System device polling guidance:
https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/corresponding-old-new-api.md

Delivered test and build reports are in Verification/. Rerunning the tools writes fresh reports into Logs/. The Windows player closes with Alt+F4.

Gameplay expansion verification: Verification/WingShotGameplayExpansion.json. The October 29, 2026 run passed 95 automated checks with no gameplay errors. Previews/ includes current gameplay, the scrolling shop, 4:3 layouts, and each environment. Backup before the expansion: Backups/Before-Gameplay-Expansion-20261029-230250.zip.

Power-up and scope verification: Verification/WingShotPowerups.json. The October 30, 2026 run passed 135 automated checks with no gameplay errors. Crate-drop, reward, countdown, and zoomed-scope previews are in Previews/. Backup before this update: Backups/Before-Powerup-Crates-20261030-002422.zip.

