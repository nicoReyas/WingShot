using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
public class ShootingSystem : MonoBehaviour
{
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    GameManager game;

    void Awake() { game = GetComponent<GameManager>(); }

    void Update()
    {
        if (game == null || !game.IsPlaying || !Application.isFocused ||
            Time.frameCount <= game.StateChangedFrame) return;
        if (WingShotInput.ShootPressed) TryShootAt(WingShotInput.PointerPosition);
    }

    // Public for other input adapters and the editor smoke test. Exactly one result per shot.
    public bool TryShootAt(Vector2 screenPosition)
    {
        if (game == null || !game.IsPlaying || Time.frameCount <= game.StateChangedFrame ||
            screenPosition.x < 0 || screenPosition.y < 0 ||
            screenPosition.x >= Screen.width || screenPosition.y >= Screen.height) return false;
        var camera = Camera.main;
        if (camera == null || !camera.pixelRect.Contains(screenPosition) || IsPointerOverUI(screenPosition))
            return false;

        game.Sounds.Play(WingShotSound.Shoot);
        Physics2D.SyncTransforms();
        var hits = Physics2D.GetRayIntersectionAll(camera.ScreenPointToRay(screenPosition), camera.farClipPlane);
        BirdTarget target = null;
        int topOrder = int.MinValue;
        foreach (var hit in hits)
        {
            var candidate = hit.collider.GetComponentInParent<BirdTarget>();
            if (candidate == null || candidate.IsHit) continue;
            var sprite = candidate.GetComponent<SpriteRenderer>();
            int order = sprite == null ? 0 : sprite.sortingOrder;
            if (target == null || order > topOrder) { target = candidate; topOrder = order; }
        }
        bool didHit = target != null && target.TryHit();
        game.RegisterShot(didHit, didHit ? target.points : 0);
        game.Sounds.Play(didHit ? WingShotSound.Hit : WingShotSound.Miss);
        return true;
    }

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
