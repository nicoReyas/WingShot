using UnityEngine;

public enum WingShotPowerup { Sniper, DoublePoints, ExtraTime, SlowBirds }

// Round-only rewards. Durations use gameplay time, so pausing never consumes a power-up.
public class WingShotPowerups : MonoBehaviour
{
    public const int CrateHealth = 8;
    public const float WeaponDuration = 12f, DoublePointsDuration = 12f, SlowDuration = 8f, ExtraSeconds = 10f;
    [Min(1f)] public float firstDropDelay = 3f;
    [Min(3f)] public float dropInterval = 9f;
    [SerializeField] GameManager game;
    [SerializeField] WingShotCrate activeCrate;
    [field: SerializeField] public float SniperRemaining { get; private set; }
    [field: SerializeField] public float DoublePointsRemaining { get; private set; }
    [field: SerializeField] public float SlowRemaining { get; private set; }
    [SerializeField] float dropCountdown;
    [SerializeField] int dropNumber;

    public int ShotDamage => SniperRemaining > 0f ? 3 : 1;
    public int PointMultiplier => DoublePointsRemaining > 0f ? 2 : 1;
    public float BirdSpeedMultiplier => (SlowRemaining > 0f ? .5f : 1f) *
        (game != null && game.Scope != null ? game.Scope.AimBirdSpeedMultiplier : 1f);
    public WingShotSound ShotSound => SniperRemaining > 0f ? WingShotSound.Sniper : WingShotSound.Shoot;
    public WingShotCrate CurrentCrate => activeCrate != null && activeCrate.gameObject.activeSelf ? activeCrate : null;

    void Awake() { game = GetComponent<GameManager>(); }
    void OnEnable() { if (game == null) game = GetComponent<GameManager>(); }

    void Update()
    {
        if (game == null || !game.IsPlaying) return;
        SniperRemaining = Mathf.Max(0f, SniperRemaining - Time.deltaTime);
        DoublePointsRemaining = Mathf.Max(0f, DoublePointsRemaining - Time.deltaTime);
        SlowRemaining = Mathf.Max(0f, SlowRemaining - Time.deltaTime);
        dropCountdown -= Time.deltaTime;
        if (dropCountdown <= 0f && CurrentCrate == null)
        {
            // Predictable rotation guarantees all four rewards rather than starving one by chance.
            if (SpawnCrate((WingShotPowerup)(dropNumber % 4)) != null)
            {
                dropNumber++;
                dropCountdown = dropInterval;
            }
        }
    }

    public void ResetRound()
    {
        if (game != null && game.Scope != null) game.Scope.ResetRound();
        SniperRemaining = DoublePointsRemaining = SlowRemaining = 0f;
        dropCountdown = firstDropDelay;
        dropNumber = 0;
        foreach (var crate in WingShotObjects.All<WingShotCrate>())
        {
            crate.gameObject.SetActive(false);
            Destroy(crate.gameObject);
        }
        activeCrate = null;
    }

    public WingShotCrate SpawnCrate(WingShotPowerup kind, bool animateDrop = true)
    {
        if (game == null || !game.IsPlaying || Camera.main == null || CurrentCrate != null || !Valid(kind)) return null;
        var camera = Camera.main;
        var viewport = new Vector2(Random.Range(.28f, .72f), .34f);
        var position = game.Scope == null ? camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y,
            Mathf.Abs(camera.transform.position.z))) : game.Scope.WorldFromViewport(viewport);
        position.z = 0f;
        var root = new GameObject("Supply Crate - " + Name(kind), typeof(SpriteRenderer), typeof(BoxCollider2D));
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 1.25f;
        var sprite = root.GetComponent<SpriteRenderer>();
        var spawner = FindAnyObjectByType<BirdSpawner>();
        if (spawner != null && spawner.birdPrefab != null)
            sprite.sharedMaterial = spawner.birdPrefab.GetComponent<SpriteRenderer>().sharedMaterial;
        activeCrate = root.AddComponent<WingShotCrate>();
        activeCrate.Initialize(game, kind, CrateHealth, animateDrop);
        return activeCrate;
    }

    public void Activate(WingShotPowerup kind)
    {
        if (game == null || !game.IsPlaying || !Valid(kind)) return;
        // Repeated drops refresh the effect; different effects can run together.
        switch (kind)
        {
            case WingShotPowerup.Sniper: SniperRemaining = WeaponDuration; break;
            case WingShotPowerup.DoublePoints: DoublePointsRemaining = DoublePointsDuration; break;
            case WingShotPowerup.ExtraTime: game.timeRemaining += ExtraSeconds; break;
            case WingShotPowerup.SlowBirds: SlowRemaining = SlowDuration; break;
        }
        if (game.UI != null) game.UI.PowerupFeedback(kind);
    }

    static bool Valid(WingShotPowerup kind) { return kind >= WingShotPowerup.Sniper && kind <= WingShotPowerup.SlowBirds; }
    public static string Name(WingShotPowerup kind)
    {
        switch (kind)
        {
            case WingShotPowerup.Sniper: return "SNIPER";
            case WingShotPowerup.DoublePoints: return "DOUBLE POINTS";
            case WingShotPowerup.ExtraTime: return "EXTRA TIME";
            default: return "SLOW BIRDS";
        }
    }
    public static Color Tint(WingShotPowerup kind)
    {
        switch (kind)
        {
            case WingShotPowerup.Sniper: return new Color(.36f, .82f, 1f);
            case WingShotPowerup.DoublePoints: return new Color(1f, .83f, .24f);
            case WingShotPowerup.ExtraTime: return new Color(.48f, 1f, .67f);
            default: return new Color(.78f, .61f, 1f);
        }
    }
}
