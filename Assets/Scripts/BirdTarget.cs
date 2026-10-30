using UnityEngine;

public class BirdTarget : MonoBehaviour
{
    [Min(1)] public int points = 10;
    public bool IsHit { get; private set; }

    // ShootingSystem owns input and shot statistics; OnMouseDown would count hits twice.
    // Accept one hit per bird, hide it immediately, and then remove it.
    public bool TryHit()
    {
        if (IsHit || GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            return false;
        // Disable its collider now because Destroy finishes at the end of the frame.
        IsHit = true;
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        foreach (var sprite in GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
        Destroy(gameObject);
        return true;
    }
}
