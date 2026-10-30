using UnityEngine;

public class BirdSpawner : MonoBehaviour
{
    public GameObject birdPrefab;
    [Min(.1f)] public float spawnInterval = 2f;
    float timer;

    public float CurrentInterval => Mathf.Max(.38f, spawnInterval *
        Mathf.Pow(.85f, GameManager.Instance == null ? 0 : GameManager.Instance.DifficultyLevel - 1));

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

    public void ResetSpawner(bool spawnImmediately)
    {
        timer = CurrentInterval;
        if (spawnImmediately) SpawnBird();
    }

    public GameObject SpawnBird()
    {
        var game = GameManager.Instance;
        var camera = Camera.main;
        if (birdPrefab == null || game == null || !game.IsPlaying || camera == null) return null;
        int level = game.DifficultyLevel;
        float direction = Random.value < .32f ? -1f : 1f;
        float depth = Mathf.Abs(camera.transform.position.z);
        Vector3 lower = camera.ViewportToWorldPoint(new Vector3(-.12f, .24f, depth));
        Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1.12f, .78f, depth));
        var position = new Vector3(direction > 0f ? lower.x : upper.x, Random.Range(lower.y, upper.y), 0f);
        var bird = Instantiate(birdPrefab, position, Quaternion.identity);
        var movement = bird.GetComponent<BirdMovement>();
        if (movement != null)
        {
            float speed = (3f + (level - 1) * .48f) * Random.Range(.82f, 1.22f);
            var pattern = (BirdFlightPattern)Random.Range(0, 3);
            movement.Configure(speed, direction, pattern, lower.y, upper.y, lower.x - 1f, upper.x + 1f);
        }
        var sprite = bird.GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.color = CosmeticCatalog.Birds[game.Progress.Selected(CosmeticCategory.Bird)].Color;
        if (game.useProceduralBirdArt)
        {
            var visual = bird.GetComponent<WingShotBirdVisual>();
            if (visual == null) visual = bird.AddComponent<WingShotBirdVisual>();
            bird.transform.localScale = Vector3.one * Random.Range(1.25f, 1.55f);
            var box = bird.GetComponent<BoxCollider2D>();
            if (box != null) { box.size = new Vector2(.92f, .67f); box.offset = new Vector2(0, .03f); }
        }
        return bird;
    }
}
