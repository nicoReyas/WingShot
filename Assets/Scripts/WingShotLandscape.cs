using UnityEngine;

// Layered scenery stays behind targets and never adds colliders or UI raycast blockers.
public class WingShotLandscape : MonoBehaviour
{
    const int Width = 960, Height = 540;
    SpriteRenderer scenery;
    Texture2D texture, cloudTexture;
    Sprite landscapeSprite, cloudSprite;
    Camera gameCamera;
    GameManager game;
    // Keep runtime layer references across Unity's Play Mode assembly reloads.
    [SerializeField] SpriteRenderer[] clouds = new SpriteRenderer[4];
    [SerializeField] SpriteRenderer[] motes = new SpriteRenderer[20];
    Color[] pixels;
    Material sceneryMaterial;
    float breeze;
    int currentTheme;
    SpriteRenderer originalGround;
    int originalGroundOrder;

    public void Initialize(GameManager manager)
    {
        game = manager;
        gameCamera = Camera.main;
        var spawner = FindAnyObjectByType<BirdSpawner>();
        var original = spawner == null || spawner.birdPrefab == null ? null :
            spawner.birdPrefab.GetComponent<SpriteRenderer>();
        if (original != null) sceneryMaterial = original.sharedMaterial;
        var ground = GameObject.Find("Grass");
        originalGround = ground == null ? null : ground.GetComponent<SpriteRenderer>();
        if (originalGround != null)
        {
            // Place the authored placeholder behind the new meadow, stream and fence.
            originalGroundOrder = originalGround.sortingOrder;
            originalGround.sortingOrder = -110;
        }
        scenery = Layer("Wing Shot Woodland", -100);
        CreateCloudArt();
        for (int i = 0; i < clouds.Length; i++)
        {
            clouds[i] = Layer("Drifting cloud " + i, -90);
            clouds[i].sprite = cloudSprite;
        }
        for (int i = 0; i < motes.Length; i++)
        {
            motes[i] = Layer("Windblown mote " + i, -80);
            motes[i].sprite = WingShotArt.Square;
        }
    }

    SpriteRenderer Layer(string title, int order)
    {
        var layer = new GameObject(title).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(transform, false);
        layer.sortingOrder = order;
        if (sceneryMaterial != null) layer.sharedMaterial = sceneryMaterial;
        return layer;
    }

    public void Apply(int theme)
    {
        if (gameCamera == null || scenery == null) return;
        if (scenery.sprite != null && currentTheme == theme) return;
        currentTheme = Mathf.Clamp(theme, 0, CosmeticCatalog.Backgrounds.Length - 1);
        theme = currentTheme;
        Color sky = CosmeticCatalog.Backgrounds[theme].Color;
        gameCamera.backgroundColor = sky;
        if (landscapeSprite != null) Destroy(landscapeSprite);
        if (texture != null) Destroy(texture);
        bool night = theme == 2 || theme == 5 || theme == 6;
        bool snow = theme == 4;
        Color haze = night ? new Color(.25f, .36f, .45f) : new Color(.91f, .93f, .78f);
        Color grass = snow ? new Color(.84f, .91f, .94f) : theme == 3
            ? new Color(.49f, .43f, .22f) : new Color(.25f, .48f, .31f);
        Color foliage = theme == 3 ? new Color(.78f, .35f, .15f) : snow
            ? new Color(.25f, .43f, .45f) : new Color(.18f, .42f, .30f);
        Color trunk = new Color(.30f, .24f, .18f);
        if (night)
        {
            grass = Color.Lerp(grass, sky, .64f);
            foliage = Color.Lerp(foliage, sky, .58f);
            trunk = Color.Lerp(trunk, sky, .58f);
        }
        pixels = new Color[Width * Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            float u = (float)x / Width, v = (float)y / Height;
            Color color = Color.Lerp(haze, sky * new Color(.72f, .80f, .91f, 1f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, 1f, v)));
            if (theme == 5 && v > .48f)
            {
                float ribbon = .71f + .09f * Mathf.Sin(u * 7f) + .025f * Mathf.Sin(u * 19f);
                float glow = Mathf.Exp(-Mathf.Pow((v - ribbon) / .055f, 2f));
                color = Color.Lerp(color, Color.Lerp(new Color(.19f, .88f, .66f),
                    new Color(.64f, .38f, .85f), u), glow * .66f);
            }
            float peaks = .31f + .10f * Mathf.Abs(Mathf.Sin(u * 10f + .4f)) + .05f * Mathf.Sin(u * 21f);
            if (v < peaks) color = Color.Lerp(haze, sky, .48f);
            if (snow && v < peaks && v > peaks - .018f) color = new Color(.93f, .96f, .97f);
            float ridge = .24f + .045f * Mathf.Sin(u * 12f + 1f) + .025f * Mathf.Sin(u * 27f);
            if (v < ridge) color = Color.Lerp(foliage, haze, .48f);
            float meadow = .145f + .025f * Mathf.Sin(u * 11f + .5f);
            if (v < meadow) color = Color.Lerp(grass, haze, v * 1.4f);
            float streamX = .54f + .13f * Mathf.Sin(v * 15f + .3f);
            float streamWidth = Mathf.Lerp(.085f, .006f, v / .19f);
            if (v < .19f && Mathf.Abs(u - streamX) < streamWidth)
                color = Color.Lerp(new Color(.31f, .62f, .66f), sky, night ? .68f : .20f);
            pixels[y * Width + x] = color;
        }

        if (night)
            for (int i = 0; i < 85; i++)
            {
                float x = Hash(i * 7 + 3), y = .45f + Hash(i * 17 + 8) * .52f;
                Oval(x, y, .001f, .0018f, new Color(.92f, .96f, 1f, .35f + Hash(i) * .6f));
            }
        Color sun = night ? new Color(.91f, .96f, .85f) : new Color(1f, .90f, .59f);
        for (int i = 5; i > 0; i--)
            Oval(.84f, .78f, .035f + i * .009f, .062f + i * .016f, new Color(sun.r, sun.g, sun.b, .025f));
        Oval(.84f, .78f, .033f, .059f, sun);
        if (night) Oval(.85f, .795f, .028f, .05f, sky);

        // Several scales of trees create a distant forest and a closer grove.
        for (int i = 0; i < 40; i++)
        {
            float x = i / 39f;
            float ground = .20f + .017f * Mathf.Sin(x * 18f);
            Pine(x, ground, .055f + Hash(i + 20) * .07f,
                Color.Lerp(foliage, haze, .28f + Hash(i) * .17f), trunk, snow);
        }
        for (int i = 0; i < 11; i++)
        {
            float x = i < 6 ? -.035f + i * .043f : .84f + (i - 6) * .05f;
            Tree(x, .095f, .20f + Hash(i + 50) * .13f,
                Color.Lerp(foliage, haze, .12f), trunk, snow);
        }
        Tree(-.015f, .025f, .57f, foliage, trunk, snow);
        Tree(.075f, .035f, .43f, foliage, trunk, snow);
        Tree(.965f, .02f, .49f, foliage, trunk, snow);
        Pine(1.035f, .03f, .65f, Color.Lerp(foliage, Color.black, .14f), trunk, snow);

        Color fence = Color.Lerp(trunk, new Color(.74f, .61f, .39f), night ? .10f : .6f);
        Stroke(new Vector2(.18f, .073f), new Vector2(.43f, .093f), .004f, fence);
        Stroke(new Vector2(.18f, .05f), new Vector2(.43f, .07f), .004f, fence);
        for (int i = 0; i < 7; i++)
        {
            float x = .18f + i * .042f, y = .028f + i * .003f;
            Box(x, y, .007f, .062f, fence);
        }
        for (int i = 0; i < 150; i++)
        {
            float x = Hash(i * 13 + 11), y = Hash(i * 19 + 13) * .115f;
            float streamX = .54f + .13f * Mathf.Sin(y * 15f + .3f);
            if (Mathf.Abs(x - streamX) < .10f) continue;
            Color stem = Color.Lerp(foliage, Color.black, .12f);
            Stroke(new Vector2(x, y), new Vector2(x + .002f, y + .012f), .001f, stem);
            if (i % 3 == 0 && !snow)
            {
                Color flower = i % 2 == 0 ? new Color(1f, .85f, .36f) : new Color(.94f, .66f, .67f);
                if (night) flower = Color.Lerp(flower, sky, .5f);
                Oval(x + .002f, y + .014f, .0024f, .004f, flower);
            }
            if (i % 13 == 0)
                Oval(x, y, .008f, .007f, Color.Lerp(grass, haze, .45f));
        }
        for (int i = 0; i < 35; i++)
        {
            float y = Hash(i + 190) * .17f;
            float x = .54f + .13f * Mathf.Sin(y * 15f + .3f) + (Hash(i + 15) - .5f) * .04f;
            Box(x, y, .004f + Hash(i) * .014f, .0015f, new Color(.85f, .95f, 1f, night ? .18f : .45f));
        }

        texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.name = "Wing Shot Woodland Theme " + theme;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        pixels = null;
        landscapeSprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(.5f, .5f), 100f);
        scenery.sprite = landscapeSprite;
        for (int i = 0; i < clouds.Length; i++)
            clouds[i].color = night ? new Color(.63f, .73f, .87f, .13f) : new Color(1f, .99f, .94f, .65f);
        FitToCamera();
    }

    void Tree(float x, float y, float height, Color leaf, Color bark, bool snow)
    {
        Box(x, y, height * .065f, height * .70f, bark);
        Stroke(new Vector2(x, y + height * .35f), new Vector2(x - height * .12f, y + height * .68f), height * .022f, bark);
        Stroke(new Vector2(x, y + height * .43f), new Vector2(x + height * .13f, y + height * .77f), height * .021f, bark);
        for (int i = 0; i < 7; i++)
        {
            float cx = x + Mathf.Sin(i * 2.4f) * height * .14f;
            float cy = y + height * (.65f + Hash(i + 77) * .25f);
            Oval(cx, cy, height * .14f, height * .21f, Color.Lerp(leaf, Color.black, .04f + (i % 3) * .065f));
            Oval(cx - height * .018f, cy + height * .025f, height * .115f, height * .16f,
                Color.Lerp(leaf, snow ? Color.white : new Color(.69f, .77f, .42f), snow ? .8f : .15f));
        }
    }

    void Pine(float x, float y, float height, Color leaf, Color bark, bool snow)
    {
        Box(x, y, height * .045f, height * .8f, bark);
        for (int tier = 0; tier < 3; tier++)
        {
            float bottom = y + height * (.18f + tier * .20f);
            float halfWidth = height * (.26f - tier * .055f);
            Triangle(new Vector2(x - halfWidth, bottom), new Vector2(x + halfWidth, bottom),
                new Vector2(x, bottom + height * .45f), Color.Lerp(leaf, Color.black, (2 - tier) * .07f));
            if (snow)
                Triangle(new Vector2(x - halfWidth * .5f, bottom + height * .2f),
                    new Vector2(x + halfWidth * .5f, bottom + height * .2f),
                    new Vector2(x, bottom + height * .45f), new Color(.85f, .92f, .94f));
        }
    }

    void Oval(float cx, float cy, float rx, float ry, Color color)
    {
        int left = Mathf.Max(0, Mathf.FloorToInt((cx - rx) * Width));
        int right = Mathf.Min(Width - 1, Mathf.CeilToInt((cx + rx) * Width));
        int bottom = Mathf.Max(0, Mathf.FloorToInt((cy - ry) * Height));
        int top = Mathf.Min(Height - 1, Mathf.CeilToInt((cy + ry) * Height));
        for (int y = bottom; y <= top; y++)
        for (int x = left; x <= right; x++)
            if (WingShotArt.Ellipse((float)x / Width, (float)y / Height, cx, cy, rx, ry)) Paint(x, y, color);
    }

    void Box(float cx, float bottom, float width, float height, Color color)
    {
        for (int y = Mathf.Max(0, (int)(bottom * Height)); y < Mathf.Min(Height, (int)((bottom + height) * Height)); y++)
        for (int x = Mathf.Max(0, (int)((cx - width * .5f) * Width)); x < Mathf.Min(Width, (int)((cx + width * .5f) * Width)); x++)
            Paint(x, y, color);
    }

    void Stroke(Vector2 a, Vector2 b, float width, Color color)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * Width));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
            Oval(p.x, p.y, width * .5f, width * .89f, color);
        }
    }

    void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        for (int y = Mathf.Max(0, (int)(a.y * Height)); y < Mathf.Min(Height, (int)(c.y * Height)); y++)
        {
            float t = ((float)y / Height - a.y) / (c.y - a.y);
            float left = Mathf.Lerp(a.x, c.x, t), right = Mathf.Lerp(b.x, c.x, t);
            for (int x = Mathf.Max(0, (int)(left * Width)); x < Mathf.Min(Width, (int)(right * Width)); x++) Paint(x, y, color);
        }
    }

    void Paint(int x, int y, Color color)
    {
        int index = y * Width + x;
        pixels[index] = Color.Lerp(pixels[index], color, color.a);
    }

    static float Hash(int seed) { return Mathf.Repeat(Mathf.Sin(seed * 127.1f + 31.7f) * 43758.5453f, 1f); }

    void CreateCloudArt()
    {
        const int width = 192, height = 80;
        cloudTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cloudTexture.filterMode = FilterMode.Bilinear;
        cloudTexture.wrapMode = TextureWrapMode.Clamp;
        var colors = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool inside = WingShotArt.Ellipse(x, y, 95, 25, 88, 22) ||
                WingShotArt.Ellipse(x, y, 65, 43, 38, 31) || WingShotArt.Ellipse(x, y, 115, 38, 35, 28);
            colors[y * width + x] = inside ? Color.white : Color.clear;
        }
        cloudTexture.SetPixels(colors);
        cloudTexture.Apply(false, true);
        cloudSprite = Sprite.Create(cloudTexture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
    }

    void LateUpdate()
    {
        if (game == null || game.State == WingShotState.MainMenu || game.State == WingShotState.Customization || game.IsPlaying)
            breeze += Time.deltaTime;
        FitToCamera();
    }

    void FitToCamera()
    {
        if (gameCamera == null || scenery == null || scenery.sprite == null) return;
        float height = 2f * (game != null && game.Scope != null ? game.Scope.NormalSize : gameCamera.orthographicSize);
        float width = height * gameCamera.aspect;
        Vector3 center = game != null && game.Scope != null ? game.Scope.HomePosition : gameCamera.transform.position;
        center.z = 0f;
        scenery.transform.position = center;
        scenery.transform.localScale = new Vector3(width / (Width / 100f), height / (Height / 100f), 1f);
        for (int i = 0; i < clouds.Length; i++)
        {
            float x = Mathf.Repeat(i * .36f + breeze * (.003f + i * .0006f), 1.4f) - .2f;
            clouds[i].transform.position = center + new Vector3((x - .5f) * width, (.23f + i % 3 * .075f) * height, 0f);
            float scale = height * (.17f + i % 2 * .04f);
            clouds[i].transform.localScale = new Vector3(scale, scale, 1f);
        }
        bool night = currentTheme == 2 || currentTheme == 5 || currentTheme == 6;
        for (int i = 0; i < motes.Length; i++)
        {
            float x = Mathf.Repeat(Hash(i + 400) + breeze * (.009f + Hash(i) * .008f), 1.06f) - .03f;
            float y = .05f + Hash(i + 30) * .29f + Mathf.Sin(breeze * .7f + i) * .018f;
            motes[i].transform.position = center + new Vector3((x - .5f) * width, (y - .5f) * height, 0f);
            float size = height * (currentTheme == 4 ? .003f : .002f);
            motes[i].transform.localScale = new Vector3(size, size * (currentTheme == 3 ? 1.8f : 1f), 1f);
            motes[i].transform.rotation = Quaternion.Euler(0f, 0f, breeze * 30f + i * 47f);
            Color color = currentTheme == 3 ? new Color(1f, .65f, .25f) : night
                ? new Color(.72f, 1f, .52f) : new Color(1f, .98f, .82f);
            color.a = .3f + .4f * (.5f + .5f * Mathf.Sin(breeze * 2f + i));
            motes[i].color = color;
        }
    }

    void OnDestroy()
    {
        if (originalGround != null) originalGround.sortingOrder = originalGroundOrder;
        if (landscapeSprite != null) Destroy(landscapeSprite);
        if (texture != null) Destroy(texture);
        if (cloudSprite != null) Destroy(cloudSprite);
        if (cloudTexture != null) Destroy(cloudTexture);
    }
}
