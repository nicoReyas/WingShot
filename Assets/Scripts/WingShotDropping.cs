using UnityEngine;

public class WingShotDropping : MonoBehaviour
{
    public const float FallDuration = 1.9f;
    [field: SerializeField] public bool IsResolved { get; private set; }
    [SerializeField] GameManager game;
    [SerializeField] Vector3 start, landing;
    [SerializeField] float age;

    public void Initialize(GameManager manager, Vector3 position)
    {
        game = manager;
        start = position;
        landing = game.Scope.WorldFromViewport(new Vector2(.5f, .14f));
        landing.x = Mathf.Clamp(position.x, game.Scope.WorldFromViewport(new Vector2(.15f, 0f)).x,
            game.Scope.WorldFromViewport(new Vector2(.85f, 0f)).x);
        transform.position = position;
        transform.localScale = Vector3.one * .65f;
        var body = GetComponent<SpriteRenderer>();
        body.sprite = WingShotArt.Poop;
        body.sortingOrder = 20;
        GetComponent<CircleCollider2D>().radius = .35f;
    }

    void Update()
    {
        if (game == null || !game.IsPlaying || IsResolved) return;
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / FallDuration);
        transform.position = Vector3.Lerp(start, landing, t * t);
        transform.localScale = Vector3.one * Mathf.Lerp(.65f, 1.3f, t);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 8f) * 14f);
        if (t >= 1f) Impact();
    }

    public bool TryShoot()
    {
        if (game == null || !game.IsPlaying || IsResolved) return false;
        Resolve();
        return true;
    }
    public bool Impact()
    {
        if (game == null || !game.IsPlaying || IsResolved) return false;
        Resolve();
        return game.DamagePlayer(WingShotBoss.DroppingDamage);
    }
    void Resolve()
    {
        IsResolved = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    public void Cancel() { if (!IsResolved) Resolve(); }
}
