using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WingShotBirdVisual : MonoBehaviour
{
    SpriteRenderer sprite;
    float phase;
    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        phase = Random.value * .3f;
        sprite.sprite = WingShotArt.Bird(0);
    }
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        phase += Time.deltaTime;
        sprite.sprite = WingShotArt.Bird(Mathf.FloorToInt(phase / .14f));
    }
}
