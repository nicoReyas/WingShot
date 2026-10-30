using UnityEngine;

// World-space scenery renders behind the targets, so even clouds cannot obscure bird clicks.
public class WingShotLandscape : MonoBehaviour
{
    SpriteRenderer scenery;
    Texture2D texture;
    Sprite landscapeSprite;
    Camera gameCamera;

    // Create a scenery layer behind the birds and reuse the scene's sprite material.
    public void Initialize(GameManager game)
    {
        gameCamera = Camera.main;
        var layer = new GameObject("Wing Shot Meadow");
        layer.transform.SetParent(transform, false);
        scenery = layer.AddComponent<SpriteRenderer>();
        // Draw scenery behind every target, with no collider or UI click blocker.
        scenery.sortingOrder = -100;
        var spawner = FindAnyObjectByType<BirdSpawner>();
        var original = spawner == null || spawner.birdPrefab == null ? null :
            spawner.birdPrefab.GetComponent<SpriteRenderer>();
        if (original != null) scenery.sharedMaterial = original.sharedMaterial;
    }

    // Paint the selected sky, clouds, sun or moon, and green meadow.
    public void Apply(int theme)
    {
        if (gameCamera == null || scenery == null) return;
        gameCamera.backgroundColor = CosmeticCatalog.Backgrounds[theme].Color;
        if (landscapeSprite != null) Destroy(landscapeSprite);
        if (texture != null) Destroy(texture);
        const int width = 640, height = 360;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Wing Shot Meadow Theme " + theme;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * height];
        Color grass = theme == 2 ? new Color(.12f, .31f, .29f) : new Color(.29f, .64f, .37f);
        Color hill = theme == 2 ? new Color(.14f, .36f, .35f) : new Color(.43f, .74f, .45f);
        Color cloud = theme == 2 ? new Color(.60f, .70f, .83f, .24f) : new Color(1f, 1f, .96f, .7f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (float)x / width, v = (float)y / height;
            Color color = Color.clear;
            bool sun = WingShotArt.Ellipse(u, v, .84f, .79f, .046f, .081f);
            if (sun) color = theme == 2 ? new Color(.95f, .95f, .78f) : new Color(1f, .91f, .56f);
            bool clouds = WingShotArt.Ellipse(u, v, .18f, .81f, .10f, .027f) ||
                WingShotArt.Ellipse(u, v, .16f, .84f, .045f, .047f) ||
                WingShotArt.Ellipse(u, v, .21f, .83f, .04f, .035f) ||
                WingShotArt.Ellipse(u, v, .62f, .91f, .09f, .026f) ||
                WingShotArt.Ellipse(u, v, .60f, .93f, .04f, .038f);
            if (clouds) color = cloud;
            if (theme == 2 && y > 120 && ((x * 73 + y * 137) % 2317 == 0))
                color = new Color(1f, 1f, .88f, .8f);
            // Layer two green hills to give the meadow a little depth.
            float horizon = .12f + .035f * Mathf.Sin(u * 9f) + .025f * Mathf.Sin(u * 17f + 1f);
            if (v < horizon) color = hill;
            if (v < .065f + .012f * Mathf.Sin(u * 23f)) color = grass;
            if (v < .02f) color = Color.Lerp(grass, Color.black, .08f);
            pixels[y * width + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        landscapeSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
        scenery.sprite = landscapeSprite;
        FitToCamera();
    }

    // Keep the scenery fitted to the camera if the game window changes size.
    void LateUpdate() { FitToCamera(); }

    // Match the background's position and size to the visible camera area.
    void FitToCamera()
    {
        if (gameCamera == null || scenery == null || scenery.sprite == null) return;
        float height = 2f * gameCamera.orthographicSize;
        scenery.transform.position = new Vector3(gameCamera.transform.position.x, gameCamera.transform.position.y, 0f);
        scenery.transform.localScale = new Vector3(height * gameCamera.aspect / 6.4f, height / 3.6f, 1f);
    }

    // Free the background texture and sprite created by this script.
    void OnDestroy()
    {
        if (landscapeSprite != null) Destroy(landscapeSprite);
        if (texture != null) Destroy(texture);
    }
}
