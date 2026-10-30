using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
public class ShootingSystem : MonoBehaviour
{
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    GameManager game;
    public WingShotSound LastShotSound { get; private set; }

    // Find the GameManager on the same object.
    void Awake() { game = GetComponent<GameManager>(); }

    // Listen for one mouse click while the game is running and focused.
    void Update()
    {
        if (game == null || !game.IsPlaying || !Application.isFocused ||
            Time.frameCount <= game.StateChangedFrame) return;
        if (WingShotInput.ShootPressed) TryShootAt(game.Scope != null && game.Scope.IsSniperAiming
            ? game.Scope.AimScreenPosition : WingShotInput.PointerPosition);
    }

    // Public for other input adapters and the editor smoke test. Exactly one result per shot.
    // Check the shot against birds, then count it as one hit or one miss.
    public bool TryShootAt(Vector2 screenPosition)
    {
        if (game == null || !game.IsPlaying || Time.frameCount <= game.StateChangedFrame ||
            screenPosition.x < 0 || screenPosition.y < 0 ||
            screenPosition.x >= Screen.width || screenPosition.y >= Screen.height) return false;
        var camera = Camera.main;
        if (camera == null || !camera.pixelRect.Contains(screenPosition) || IsPointerOverUI(screenPosition))
            return false;

        LastShotSound = game.Powerups.ShotSound;
        bool quickscope = game.Scope != null && game.Scope.ConsumeQuickscopeShot();
        game.Sounds.Play(LastShotSound);
        // Update collider positions before checking this frame's shot.
        Physics2D.SyncTransforms();
        var hits = Physics2D.GetRayIntersectionAll(camera.ScreenPointToRay(screenPosition), camera.farClipPlane);
        // When birds overlap, prefer the one drawn in front.
        BirdTarget target = null;
        WingShotCrate crate = null;
        WingShotBoss boss = null;
        WingShotDropping poop = null;
        int topOrder = int.MinValue;
        foreach (var hit in hits)
        {
            var candidate = hit.collider.GetComponentInParent<BirdTarget>();
            var candidateCrate = hit.collider.GetComponentInParent<WingShotCrate>();
            var candidateBoss = hit.collider.GetComponentInParent<WingShotBoss>();
            var candidatePoop = hit.collider.GetComponentInParent<WingShotDropping>();
            if ((candidate == null || candidate.IsHit) && (candidateCrate == null || candidateCrate.IsOpened) &&
                (candidateBoss == null || candidateBoss.IsDefeated) && (candidatePoop == null || candidatePoop.IsResolved)) continue;
            var sprite = hit.collider.GetComponent<SpriteRenderer>();
            int order = sprite == null ? 0 : sprite.sortingOrder;
            if (order > topOrder)
            {
                target = candidate; crate = candidateCrate; boss = candidateBoss; poop = candidatePoop; topOrder = order;
            }
        }
        if (poop != null && poop.TryShoot())
        {
            game.RegisterShot(true, 0);
            game.UI.BlockFeedback();
            game.Sounds.Play(WingShotSound.Hit);
            return true;
        }
        if (boss != null && boss.TryHit(game.Powerups.ShotDamage))
        {
            int points = boss.IsDefeated ? WingShotBoss.Reward : 0;
            game.RegisterShot(true, points, boss.RemainingHealth, quickscope && boss.IsDefeated);
            game.UI.BossShotFeedback(boss.RemainingHealth, boss.MaxHealth,
                points * game.Powerups.PointMultiplier * (quickscope ? 2 : 1), quickscope && boss.IsDefeated);
            game.Sounds.Play(WingShotSound.Hit);
            return true;
        }
        if (crate != null && crate.TryHit(game.Powerups.ShotDamage))
        {
            game.RegisterShot(true, 0, crate.RemainingHealth);
            if (crate.IsOpened) game.UI.PowerupFeedback(crate.Kind);
            else
            {
                game.UI.CrateFeedback(crate.RemainingHealth, crate.MaxHealth);
                game.Sounds.Play(WingShotSound.CrateHit);
            }
            return true;
        }
        bool didHit = target != null && target.TryHit(game.Powerups.ShotDamage);
        game.RegisterShot(didHit, didHit && target.IsHit ? target.points : 0,
            didHit ? target.RemainingHealth : 0, quickscope && didHit && target.IsHit);
        game.Sounds.Play(didHit ? WingShotSound.Hit : WingShotSound.Miss);
        return true;
    }

    // Check whether a menu or button should receive the click instead of a bird.
    public bool IsPointerOverUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = position };
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);
        foreach (var hit in uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }
}
