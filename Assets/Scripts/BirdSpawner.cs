using UnityEngine;

public class BirdSpawner : MonoBehaviour
{
    public GameObject birdPrefab;
    [Min(.1f)] public float spawnInterval = 2f;
    [Range(.1f, 1f)] public float paceMultiplier = .45f;
    float timer;

    // Higher levels shorten the delay, but it never drops below the minimum.
    public float CurrentInterval => Mathf.Max(.28f, spawnInterval * paceMultiplier *
        Mathf.Pow(.85f, GameManager.Instance == null ? 0 : GameManager.Instance.DifficultyLevel - 1));

    // Count down to the next bird while a round is running.
    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnBird();
            timer = CurrentInterval;
        }
    }

    // Reset the spawn countdown when a new round starts.
    public void ResetSpawner(bool spawnImmediately)
    {
        timer = CurrentInterval;
        if (spawnImmediately)
        {
            // Give the player something to aim at as soon as the round opens.
            for (int i = 0; i < 3; i++)
                SpawnBird(new Vector2(.22f + i * .28f, .34f + i * .15f), i == 1 ? 2 : 1);
        }
    }

    // Create a bird and apply random movement and the player's chosen color.
    public GameObject SpawnBird() { return SpawnBird(null, 0); }

    GameObject SpawnBird(Vector2? openingPosition, int forcedHealth)
    {
        var game = GameManager.Instance;
        var camera = Camera.main;
        if (birdPrefab == null || game == null || !game.IsPlaying || camera == null) return null;
        int level = game.DifficultyLevel;
        float direction = Random.value < .32f ? -1f : 1f;
        float depth = Mathf.Abs(camera.transform.position.z);
        // Spawn just outside the camera so birds fly into view naturally.
        Vector3 lower = camera.ViewportToWorldPoint(new Vector3(-.055f, .27f, depth));
        Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1.055f, .77f, depth));
        if (game.Scope != null)
        {
            lower = game.Scope.WorldFromViewport(new Vector2(-.055f, .27f));
            upper = game.Scope.WorldFromViewport(new Vector2(1.055f, .77f));
        }
        var position = new Vector3(direction > 0f ? lower.x : upper.x, Random.Range(lower.y, upper.y), 0f);
        if (openingPosition.HasValue)
        {
            position = camera.ViewportToWorldPoint(new Vector3(openingPosition.Value.x, openingPosition.Value.y, depth));
            if (game.Scope != null) position = game.Scope.WorldFromViewport(openingPosition.Value);
            position.z = 0f;
        }
        var bird = Instantiate(birdPrefab, position, Quaternion.identity);
        float roll = Random.value;
        int health = forcedHealth > 0 ? forcedHealth :
            (level >= 3 && roll < .14f ? 3 : roll < .42f ? 2 : 1);
        var target = bird.GetComponent<BirdTarget>();
        if (target != null) target.Configure(health, health == 3 ? 60 : health == 2 ? 30 : 10);
        var movement = bird.GetComponent<BirdMovement>();
        if (movement != null)
        {
            float speed = (4.8f + (level - 1) * .45f) * Random.Range(.88f, 1.15f);
            speed *= health == 3 ? .72f : health == 2 ? .85f : 1f;
            var pattern = (BirdFlightPattern)Random.Range(0, 3);
            movement.Configure(speed, direction, pattern, lower.y, upper.y, lower.x - 1f, upper.x + 1f);
        }
        var sprite = bird.GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.color = CosmeticCatalog.Birds[game.Progress.Selected(CosmeticCategory.Bird)].Color;
        // Match the generated bird art with a comfortable clickable area.
        if (game.useProceduralBirdArt)
        {
            var visual = bird.GetComponent<WingShotBirdVisual>();
            if (visual == null) visual = bird.AddComponent<WingShotBirdVisual>();
            bird.transform.localScale = Vector3.one * Random.Range(1.25f, 1.5f) * (1f + (health - 1) * .13f);
            var box = bird.GetComponent<BoxCollider2D>();
            if (box != null) { box.size = new Vector2(.92f, .67f); box.offset = new Vector2(0, .03f); }
        }
        return bird;
    }
}
