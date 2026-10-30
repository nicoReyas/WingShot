using UnityEngine;

public class WingShotBossEncounter : MonoBehaviour
{
    [Min(5f)] public float firstBossDelay = 18f;
    [Min(10f)] public float respawnDelay = 22f;
    [SerializeField] GameManager game;
    [SerializeField] WingShotBoss boss;
    [SerializeField] float spawnCountdown;
    [field: SerializeField] public int BossesDefeated { get; private set; }
    public WingShotBoss CurrentBoss => boss != null && boss.gameObject.activeSelf ? boss : null;

    void Awake() { game = GetComponent<GameManager>(); }
    void OnEnable() { if (game == null) game = GetComponent<GameManager>(); }
    void Update()
    {
        if (game == null || !game.IsPlaying || CurrentBoss != null) return;
        spawnCountdown -= Time.deltaTime;
        if (spawnCountdown <= 0f) SpawnBoss();
    }

    public WingShotBoss SpawnBoss()
    {
        if (game == null || !game.IsPlaying || CurrentBoss != null) return null;
        var root = new GameObject("The Royal Pooper", typeof(SpriteRenderer), typeof(BoxCollider2D));
        root.transform.localScale = Vector3.one * 2.5f;
        var spawner = FindAnyObjectByType<BirdSpawner>();
        if (spawner != null && spawner.birdPrefab != null)
            root.GetComponent<SpriteRenderer>().sharedMaterial = spawner.birdPrefab.GetComponent<SpriteRenderer>().sharedMaterial;
        boss = root.AddComponent<WingShotBoss>();
        boss.Initialize(game, 24 + Mathf.Min(12, BossesDefeated * 6));
        if (game.UI != null) game.UI.BossAnnouncement();
        return boss;
    }

    public void OnBossDefeated(WingShotBoss defeated)
    {
        if (boss != defeated) return;
        BossesDefeated++;
        boss = null;
        spawnCountdown = respawnDelay;
        ClearDroppings();
    }

    void ClearDroppings()
    {
        foreach (var poop in WingShotObjects.All<WingShotDropping>())
            poop.Cancel();
    }
    public void ClearTargets()
    {
        foreach (var target in WingShotObjects.All<WingShotBoss>())
        {
            target.gameObject.SetActive(false);
            Destroy(target.gameObject);
        }
        boss = null;
        ClearDroppings();
    }
    public void ResetRound()
    {
        ClearTargets();
        BossesDefeated = 0;
        spawnCountdown = firstBossDelay;
    }
}
