using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WingShotBirdVisual : MonoBehaviour
{
    SpriteRenderer sprite;
    float phase;
    // Find the bird's renderer and give its wing animation a random starting point.
    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        phase = Random.value * .3f;
        sprite.sprite = WingShotArt.Bird(0);
    }
    // Switch between two wing pictures; paused birds keep their current picture.
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        phase += Time.deltaTime;
        sprite.sprite = WingShotArt.Bird(Mathf.FloorToInt(phase / .14f));
    }
}
