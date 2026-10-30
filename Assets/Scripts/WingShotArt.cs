using UnityEngine;

// Small generated sprites keep the placeholder prefab and its GUID untouched.
public static class WingShotArt
{
    // Share these two frames between all birds instead of creating textures per bird.
    static Sprite[] birds;
    // Return a cached wing picture, creating the two pictures only when needed.
    public static Sprite Bird(int frame)
    {
        if (birds == null || birds[0] == null) CreateBirds();
        return birds[Mathf.Abs(frame) % birds.Length];
    }

    // Paint a small bird from simple shapes and create its two animation frames.
    static void CreateBirds()
    {
        birds = new Sprite[2];
        for (int frame = 0; frame < 2; frame++)
        {
            const int width = 96, height = 72;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Wing Shot bird " + frame;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x, y);
                bool tail = Triangle(p, new Vector2(30, 33), new Vector2(2, 47), new Vector2(14, 22));
                bool body = Ellipse(x, y, 46, 31, 25, 14);
                bool head = Ellipse(x, y, 68, 41, 12, 12);
                bool beak = Triangle(p, new Vector2(77, 43), new Vector2(93, 37), new Vector2(77, 34));
                bool wing = frame == 0
                    ? Triangle(p, new Vector2(37, 32), new Vector2(30, 67), new Vector2(62, 37))
                    : Triangle(p, new Vector2(37, 36), new Vector2(39, 3), new Vector2(64, 32));
                Color color = Color.clear;
                if (tail || body || head) color = Color.white;
                if (beak) color = new Color(.72f, .67f, .52f);
                if (wing) color = new Color(.77f, .77f, .77f);
                if (Ellipse(x, y, 71, 44, 4, 4)) color = Color.white;
                if (Ellipse(x, y, 72, 44, 2, 2)) color = new Color(.08f, .12f, .18f);
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels);
            // Upload the finished pixels and release the CPU-side pixel copy.
            texture.Apply(false, true);
            birds[frame] = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 96f);
            birds[frame].name = texture.name;
        }
    }

    // Check whether a pixel sits inside an oval.
    public static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    // Check whether a pixel sits inside a triangle.
    static bool Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p - b, a - b), d2 = Cross(p - c, b - c), d3 = Cross(p - a, c - a);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }
    // Compare which side of a triangle edge a point lies on.
    static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
}
