using TMPro;
using UnityEngine;

// A durable, gently bobbing world target with a reward badge and visible damage.
public class WingShotCrate : MonoBehaviour
{
    [field: SerializeField] public WingShotPowerup Kind { get; private set; }
    [field: SerializeField] public int RemainingHealth { get; private set; }
    [field: SerializeField] public int MaxHealth { get; private set; }
    [field: SerializeField] public bool IsOpened { get; private set; }
    [field: SerializeField] public float TimeToLive { get; private set; } = 18f;
    [SerializeField] GameManager game;
    [SerializeField] SpriteRenderer body, healthFill;
    [SerializeField] TMP_Text label;
    [SerializeField] Vector3 origin;
    [SerializeField] float age, flashRemaining;
    public const float DropDuration = 1.35f;
    [SerializeField] Vector3 dropStart;
    [SerializeField] float dropElapsed = DropDuration;
    public bool IsDropping => dropElapsed < DropDuration;
    public Vector3 LandingPosition => origin;

    public void Initialize(GameManager manager, WingShotPowerup kind, int health, bool animateDrop = true)
    {
        game = manager;
        Kind = kind;
        MaxHealth = RemainingHealth = Mathf.Max(1, health);
        origin = transform.position;
        dropElapsed = animateDrop ? 0f : DropDuration;
        dropStart = game.Scope == null ? origin + Vector3.up * 10f : game.Scope.WorldFromViewport(new Vector2(.5f, 1.18f));
        dropStart.x = origin.x;
        if (animateDrop) transform.position = dropStart;
        body = GetComponent<SpriteRenderer>();
        body.sprite = WingShotArt.Crate;
        body.sortingOrder = 10;
        var collider = GetComponent<BoxCollider2D>();
        collider.size = new Vector2(1.12f, .94f);
        collider.offset = new Vector2(0f, -.04f);
        var border = Panel("Health border", transform, new Vector2(1.18f, .13f), new Color(.07f, .14f, .20f), 11);
        border.transform.localPosition = new Vector3(0f, .64f, 0f);
        healthFill = Panel("Remaining durability", transform, new Vector2(1.10f, .075f), WingShotPowerups.Tint(kind), 12);
        healthFill.transform.localPosition = new Vector3(0f, .64f, 0f);
        var caption = Panel("Reward caption backing", transform, new Vector2(2.45f, .58f),
            new Color(.07f, .14f, .20f, .92f), 12);
        caption.transform.localPosition = new Vector3(0f, .99f, 0f);
        label = Text("Crate reward and durability", new Vector3(0f, .99f, 0f), new Vector2(3f, .55f), 2.2f);
        label.color = WingShotPowerups.Tint(kind);
        var badge = Text("Reward badge", new Vector3(0f, -.01f, 0f), new Vector2(.9f, .45f), 3.6f);
        badge.text = kind == WingShotPowerup.Sniper ? "3x" : kind == WingShotPowerup.DoublePoints ? "2x" :
            kind == WingShotPowerup.ExtraTime ? "+10" : "1/2";
        badge.color = WingShotPowerups.Tint(kind);
        RefreshHealth();
    }

    SpriteRenderer Panel(string title, Transform parent, Vector2 size, Color color, int order)
    {
        var sprite = new GameObject(title).AddComponent<SpriteRenderer>();
        sprite.transform.SetParent(parent, false);
        sprite.transform.localScale = new Vector3(size.x, size.y, 1f);
        sprite.sprite = WingShotArt.Square;
        sprite.color = color;
        sprite.sortingOrder = order;
        sprite.sharedMaterial = body.sharedMaterial;
        return sprite;
    }

    TMP_Text Text(string title, Vector3 position, Vector2 size, float fontSize)
    {
        var text = new GameObject(title).AddComponent<TextMeshPro>();
        text.transform.SetParent(transform, false);
        text.transform.localPosition = position;
        text.rectTransform.sizeDelta = size;
        if (game.scoreText != null) text.font = game.scoreText.font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.GetComponent<Renderer>().sortingOrder = 14;
        return text;
    }

    public bool TryHit(int damage = 1)
    {
        if (IsOpened || RemainingHealth <= 0 || damage <= 0 || game == null || !game.IsPlaying) return false;
        RemainingHealth = Mathf.Max(0, RemainingHealth - damage);
        flashRemaining = .15f;
        RefreshHealth();
        if (RemainingHealth == 0)
        {
            IsOpened = true;
            // Disable the collider immediately; the opening shot must be the only reward payout.
            gameObject.SetActive(false);
            game.Powerups.Activate(Kind);
            game.Sounds.Play(WingShotSound.CrateOpen);
            Destroy(gameObject);
        }
        return true;
    }

    void RefreshHealth()
    {
        float fraction = (float)RemainingHealth / MaxHealth;
        healthFill.transform.localScale = new Vector3(1.1f * fraction, .075f, 1f);
        healthFill.transform.localPosition = new Vector3(-.55f * (1f - fraction), .64f, 0f);
        label.text = WingShotPowerups.Name(Kind) + "\n" + RemainingHealth + " / " + MaxHealth + " HP";
    }

    void Update()
    {
        if (game == null || !game.IsPlaying) return;
        if (IsDropping)
        {
            dropElapsed = Mathf.Min(DropDuration, dropElapsed + Time.deltaTime);
            float t = dropElapsed / DropDuration;
            transform.position = Vector3.Lerp(dropStart, origin, Mathf.SmoothStep(0f, 1f, t));
            age = 0f;
            // Give the player the full lifetime after the crate reaches its flight lane.
            return;
        }
        age += Time.deltaTime;
        TimeToLive = Mathf.Max(0f, TimeToLive - Time.deltaTime);
        flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
        body.color = Color.Lerp(Color.white, WingShotPowerups.Tint(Kind), flashRemaining / .15f);
        float landingBounce = Mathf.Abs(Mathf.Sin(age * 18f)) * .14f * Mathf.Exp(-age * 6f);
        transform.position = origin + new Vector3(Mathf.Sin(age * .8f) * .12f, Mathf.Sin(age * 1.6f) * .07f + landingBounce, 0f);
        if (TimeToLive <= 0f)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
