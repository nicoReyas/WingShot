# Wing Shot verification

Validated with the supplied project's recorded editor, Unity 6000.6.0f1, on October 29, 2026.

- Runtime and Editor C# sources compile with zero errors and zero warnings against the installed Unity assemblies.
- **53 automated Unity Play Mode checks passed**, with no gameplay errors or Editor exceptions in the final run. See Verification/WingShotValidation.json.
- Checks include actual collider hit/miss accounting, destroyed-target protection, HUD/UI raycasts, timer advancement, pause freeze, queued ESC keyboard input, Resume button, difficulty, round expiry, Play Again, shop button purchases, cosmetic appearance application, save reload, three menu/restart cycles, and a scene reload while paused.
- Final Windows player build: **Succeeded, 0 errors**. The build reports 488 shader warnings from existing packages; no C# warnings. Details: Verification/WingShotBuild.txt and Verification/WingShot-WindowsBuild.log.
- SHA-256 comparisons confirm the scene, bird prefab, package manifest/lock, project/version/editor/build settings, and four original script GUIDs match the pre-change backup. Details: Verification/WingShotAssetPreservation.json.
- Rendered previews of main menu, customization, gameplay, pause, and game over were inspected. A 4:3 main menu preview was also checked. Files: Previews/WingShot-*.png.

No human gameplay session or audio listening test was performed. The exact requested editor version 6000.0.60f1 is not installed and was not tested; project version and package versions were preserved.

No additional Inspector setup is required. Open Assets/Scenes/SampleScene.unity and press Play. Follow README-WingShot.md for the manual acceptance test.

The preview capture tour sets up sample scores and cosmetics in the isolated test project. Tests restore the original PlayerPrefs afterward; preview values are not baked into the game or its default save.

Deliverables:
- Builds/Windows/Wing Shot.exe, together with the adjacent Data/Mono/DLL folders.
- Builds/WingShot-Windows.zip for sharing the player.
- Builds/WingShot-UnityProject.zip for the completed Unity source project.
- Backups/Before-WingShot-20261029-175755.zip is the original pre-change backup.

