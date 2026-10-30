using UnityEngine;
using UnityEngine.UI;

// A circular scope aperture and graduated reticle, built from UI geometry.
[RequireComponent(typeof(CanvasRenderer))]
public class WingShotScopeOverlay : MaskableGraphic
{
    [SerializeField] float amount;
    public void SetZoom(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(amount, value)) return;
        amount = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (amount <= 0f) return;
        var rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .38f;
        Color shade = new Color(.015f, .025f, .035f, .94f * amount);
        Color ink = new Color(.04f, .11f, .15f, .9f * amount);
        Color rim = new Color(.38f, .84f, .94f, .8f * amount);
        const int segments = 128;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            float outerA = Mathf.Min(rect.width * .5f / Mathf.Max(.0001f, Mathf.Abs(da.x)),
                rect.height * .5f / Mathf.Max(.0001f, Mathf.Abs(da.y)));
            float outerB = Mathf.Min(rect.width * .5f / Mathf.Max(.0001f, Mathf.Abs(db.x)),
                rect.height * .5f / Mathf.Max(.0001f, Mathf.Abs(db.y)));
            Quad(vh, center + da * radius, center + db * radius,
                center + db * outerB, center + da * outerA, shade);
            Line(vh, center + da * radius, center + db * radius, 7f, ink);
            Line(vh, center + da * (radius - 4f), center + db * (radius - 4f), 1.5f, rim);
        }
        for (int axis = 0; axis < 4; axis++)
        {
            var d = new Vector2(Mathf.Cos(axis * Mathf.PI / 2f), Mathf.Sin(axis * Mathf.PI / 2f));
            var normal = new Vector2(-d.y, d.x);
            Line(vh, center + d * 22f, center + d * (radius - 10f), 1.5f, ink);
            for (int tick = 1; tick < 6; tick++)
            {
                Vector2 p = center + d * (radius * tick / 7f);
                float length = tick % 2 == 0 ? 7f : 4f;
                Line(vh, p - normal * length, p + normal * length, 1.5f, ink);
            }
        }
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        var n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        Quad(vh, a - n, a + n, b + n, b - n, color);
    }
    static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
