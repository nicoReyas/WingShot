using UnityEngine;

// A readable attack cadence gives the player time to shoot each dropping out of the air.
public class WingShotBoss : MonoBehaviour
{
    public const int Reward = 300, DroppingDamage = 12;
    public const float AttackInterval = 4f, AttackWarning = .75f;
    [field: SerializeField] public int MaxHealth { get; private set; }
    [field: SerializeField] public int RemainingHealth { get; private set; }
    [field: SerializeField] public bool IsDefeated { get; private set; }
    [SerializeField] GameManager game;
    [SerializeField] SpriteRenderer body;
    [SerializeField] float flightAge, entryAge, attackCountdown = 3.2f, flashRemaining;
    public bool IsWindingUp => !IsDefeated && attackCountdown <= AttackWarning;

    public void Initialize(GameManager manager, int health)
    {
        game = manager;
        MaxHealth = RemainingHealth = Mathf.Max(1, health);
        body = GetComponent<SpriteRenderer>();
        body.sprite = WingShotArt.BossBird(0);
        body.sortingOrder = 5;
        GetComponent<BoxCollider2D>().size = new Vector2(1.35f, .8f);
        transform.position = game.Scope.WorldFromViewport(new Vector2(1.15f, .68f));
    }

    void Update()
    {
        if (game == null || !game.IsPlaying || IsDefeated) return;
        entryAge += Time.deltaTime;
        flightAge += Time.deltaTime * game.Powerups.BirdSpeedMultiplier;
        float x = .5f + Mathf.Sin(flightAge * .65f) * .25f;
        float y = .66f + Mathf.Sin(flightAge * 1.1f) * .035f;
        var destination = game.Scope.WorldFromViewport(new Vector2(x, y));
        var entrance = game.Scope.WorldFromViewport(new Vector2(1.15f, .68f));
        transform.position = Vector3.Lerp(entrance, destination, Mathf.SmoothStep(0f, 1f, entryAge / 1.3f));
        body.flipX = Mathf.Cos(flightAge * .65f) < 0f;
        body.sprite = WingShotArt.BossBird(Mathf.FloorToInt(flightAge / .18f));
        flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
        body.color = Color.Lerp(Color.white, new Color(1f, .5f, .46f), flashRemaining / .14f);
        attackCountdown -= Time.deltaTime;
        if (attackCountdown <= 0f)
        {
            DropPoop();
            attackCountdown = AttackInterval;
        }
    }

    public WingShotDropping DropPoop()
    {
        if (game == null || !game.IsPlaying || IsDefeated) return null;
        var root = new GameObject("Boss Dropping - Shoot to Block", typeof(SpriteRenderer), typeof(CircleCollider2D));
        root.GetComponent<SpriteRenderer>().sharedMaterial = body.sharedMaterial;
        var poop = root.AddComponent<WingShotDropping>();
        poop.Initialize(game, transform.position + Vector3.down * .65f);
        game.Sounds.Play(WingShotSound.BossDrop);
        return poop;
    }

    public bool TryHit(int damage)
    {
        if (game == null || !game.IsPlaying || IsDefeated || damage <= 0) return false;
        RemainingHealth = Mathf.Max(0, RemainingHealth - damage);
        flashRemaining = .14f;
        if (RemainingHealth == 0)
        {
            IsDefeated = true;
            gameObject.SetActive(false);
            game.Encounter.OnBossDefeated(this);
            game.Sounds.Play(WingShotSound.BossDefeat);
            Destroy(gameObject);
        }
        return true;
    }
}
