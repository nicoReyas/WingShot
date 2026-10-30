using UnityEngine;

public class BirdTarget : MonoBehaviour
{
    [Min(1)] public int points = 10;
    public bool IsHit { get; private set; }
    public int MaxHealth { get; private set; } = 1;
    public int RemainingHealth { get; private set; } = 1;
    SpriteRenderer[] healthPips;
    Transform healthDisplay;
    SpriteRenderer body;
    Color restingColor;
    float flashRemaining;

    // Bigger birds take two or three hits and show a small pip for each remaining hit.
    public void Configure(int health, int reward)
    {
        MaxHealth = RemainingHealth = Mathf.Clamp(health, 1, 3);
        points = Mathf.Max(1, reward);
        body = GetComponent<SpriteRenderer>();
        if (MaxHealth == 1) return;
        healthDisplay = new GameObject("Remaining hits").transform;
        healthDisplay.SetParent(transform, false);
        healthDisplay.localPosition = new Vector3(0f, .5f, 0f);
        healthPips = new SpriteRenderer[MaxHealth];
        for (int i = 0; i < MaxHealth; i++)
        {
            var backing = MakePip("Health outline", healthDisplay, new Color(.08f, .14f, .20f),
                new Vector2(.19f, .13f), body == null ? 1 : body.sortingOrder + 1);
            backing.transform.localPosition = new Vector3((i - (MaxHealth - 1) * .5f) * .23f, 0f, 0f);
            healthPips[i] = MakePip("Health", backing.transform,
                MaxHealth == 3 ? new Color(1f, .59f, .27f) : new Color(1f, .91f, .48f),
                new Vector2(.7f, .55f), backing.sortingOrder + 1);
        }
    }

    SpriteRenderer MakePip(string title, Transform parent, Color color, Vector2 size, int order)
    {
        var pip = new GameObject(title).AddComponent<SpriteRenderer>();
        pip.transform.SetParent(parent, false);
        pip.transform.localScale = new Vector3(size.x, size.y, 1f);
        pip.sprite = WingShotArt.Square;
        pip.color = color;
        pip.sortingOrder = order;
        if (body != null) pip.sharedMaterial = body.sharedMaterial;
        return pip;
    }

    // ShootingSystem owns input and shot statistics; OnMouseDown would count hits twice.
    // A successful damage hit counts for accuracy; only the final hit awards the reward.
    public bool TryHit(int damage = 1)
    {
        if (IsHit || damage <= 0 || GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            return false;
        RemainingHealth = Mathf.Max(0, RemainingHealth - damage);
        if (healthPips != null)
            for (int i = 0; i < healthPips.Length; i++)
                healthPips[i].enabled = i < RemainingHealth;
        if (RemainingHealth > 0)
        {
            if (body != null)
            {
                if (flashRemaining <= 0f) restingColor = body.color;
                flashRemaining = .16f;
            }
            return true;
        }
        // Disable its collider now because Destroy finishes at the end of the frame.
        IsHit = true;
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        foreach (var sprite in GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
        Destroy(gameObject);
        return true;
    }

    void LateUpdate()
    {
        if (healthDisplay != null) healthDisplay.rotation = Quaternion.identity;
        if (flashRemaining <= 0f || body == null) return;
        flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
        body.color = Color.Lerp(restingColor, Color.white, flashRemaining / .16f);
    }
}
