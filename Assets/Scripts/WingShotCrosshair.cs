using UnityEngine;
using UnityEngine.UI;

public class WingShotCrosshair : MaskableGraphic
{
    // 0 is the classic cross, 1 is a ring, and 2 is a diamond.
    public int style;
    float pulseUntil;
    bool lastHit;
    // Choose the crosshair shape and ask Unity to redraw it.
    public void SetStyle(int value)
    {
        if (style == value) return;
        style = value;
        SetVerticesDirty();
    }
    // Briefly flash green for a hit or red for a miss.
    public void Pulse(bool hit)
    {
        lastHit = hit;
        pulseUntil = Time.unscaledTime + .16f;
        SetVerticesDirty();
    }
    // Refresh the flash until its short real-time timer ends.
    void Update()
    {
        if (pulseUntil > 0f)
        {
            SetVerticesDirty();
            if (Time.unscaledTime > pulseUntil) pulseUntil = 0f;
        }
    }

    // Draw the selected crosshair with a dark outline and a colored center.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color ink = new Color(.08f, .15f, .22f, .95f);
        Color fill = pulseUntil > Time.unscaledTime
            ? (lastHit ? new Color(.4f, 1f, .6f) : new Color(1f, .5f, .46f)) : color;
        // Use the same drawing code for small cursors and larger shop previews.
        float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 36f;
        if (style == 1)
        {
            Ring(vh, 12f * scale, 5f * scale, ink, 40);
            Ring(vh, 12f * scale, 2.2f * scale, fill, 40);
        }
        else if (style == 2)
        {
            Ring(vh, 15f * scale, 5f * scale, ink, 4);
            Ring(vh, 15f * scale, 2.2f * scale, fill, 4);
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = new Vector2(Mathf.Cos(i * Mathf.PI / 2), Mathf.Sin(i * Mathf.PI / 2));
                Line(vh, d * 7f * scale, d * 16f * scale, 5f * scale, ink);
                Line(vh, d * 7f * scale, d * 16f * scale, 2.2f * scale, fill);
            }
        }
        Line(vh, Vector2.left * 1.4f * scale, Vector2.right * 1.4f * scale, 4f * scale, ink);
        Line(vh, Vector2.left * .9f * scale, Vector2.right * .9f * scale, 1.8f * scale, fill);
    }

    // Build a circle, or a diamond, by joining small line segments.
    static void Ring(VertexHelper vh, float radius, float width, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            Line(vh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
        }
    }

    // Build one thick line from two triangles.
    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        int index = vh.currentVertCount;
        vh.AddVert(a - normal, color, Vector2.zero);
        vh.AddVert(a + normal, color, Vector2.zero);
        vh.AddVert(b + normal, color, Vector2.zero);
        vh.AddVert(b - normal, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
