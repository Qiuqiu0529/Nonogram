using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIShatterFragments : Image
{
    sealed class Piece
    {
        public List<Vector2> points;
        public Vector2 center;
        public Vector2 velocity;
        public float spin;
    }

    readonly List<Piece> pieces = new List<Piece>();
    float progress, gravity;

    public void Initialize(Image source, float scatter, float fall, float rotation)
    {
        sprite = source.overrideSprite;
        color = source.color;
        raycastTarget = false;
        type = Type.Simple;
        preserveAspect = source.preserveAspect;
        gravity = fall;

        var seeds = new Vector2[6];
        for (int i = 0; i < seeds.Length; i++)
            seeds[i] = new Vector2((i % 3 + Random.Range(.28f, .72f)) / 3f,
                (i / 3 + Random.Range(.28f, .72f)) / 2f);

        for (int i = 0; i < seeds.Length; i++)
        {
            Vector2 seed = seeds[i];
            var points = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            for (int j = 0; j < seeds.Length; j++)
                if (i != j) points = Clip(points, (seed + seeds[j]) * .5f, seeds[j] - seed);
            if (points.Count < 3) continue;

            pieces.Add(new Piece
            {
                points = points,
                center = seed,
                velocity = new Vector2((seed.x - .5f) * 2f * scatter,
                    -Random.Range(.04f, .30f)),
                spin = (Random.value < .5f ? -1f : 1f) * Random.Range(.45f, 1f) * rotation
            });
        }
        SetAllDirty();
    }

    public void SetProgress(float value)
    {
        progress = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        // Start with the same quad and atlas UVs Unity generates for a regular Image.
        base.OnPopulateMesh(helper);
        if (helper.currentVertCount != 4 || pieces.Count == 0) return;
        UIVertex lower = default, upper = default;
        helper.PopulateUIVertex(ref lower, 0);
        helper.PopulateUIVertex(ref upper, 2);
        Vector2 size = (Vector2)(upper.position - lower.position);
        Vector2 origin = lower.position;
        helper.Clear();
        Color tint = lower.color;
        tint.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, progress));
        foreach (var piece in pieces)
        {
            int start = helper.currentVertCount;
            float angle = piece.spin * progress * Mathf.Deg2Rad;
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            Vector2 center = origin + Vector2.Scale(piece.center, size);
            float travel = .045f + progress;
            Vector2 offset = Vector2.Scale(piece.velocity * travel, size)
                + Vector2.down * (size.y * gravity * progress * progress);
            foreach (var point in piece.points)
            {
                Vector2 relative = Vector2.Scale(point - piece.center, size) * .94f;
                Vector2 rotated = new Vector2(cos * relative.x - sin * relative.y, sin * relative.x + cos * relative.y);
                Vector2 position = center + rotated + offset;
                Vector2 texturePoint = new Vector2(Mathf.Lerp(lower.uv0.x, upper.uv0.x, point.x),
                    Mathf.Lerp(lower.uv0.y, upper.uv0.y, point.y));
                helper.AddVert(position, tint, texturePoint);
            }
            for (int i = 1; i < piece.points.Count - 1; i++)
                helper.AddTriangle(start, start + i + 1, start + i);
        }
    }

    static List<Vector2> Clip(List<Vector2> polygon, Vector2 midpoint, Vector2 normal)
    {
        var result = new List<Vector2>(polygon.Count + 1);
        if (polygon.Count == 0) return result;
        Vector2 previous = polygon[polygon.Count - 1];
        float previousDistance = Vector2.Dot(previous - midpoint, normal);
        foreach (var current in polygon)
        {
            float distance = Vector2.Dot(current - midpoint, normal);
            if ((previousDistance <= 0f) != (distance <= 0f))
                result.Add(Vector2.LerpUnclamped(previous, current, previousDistance / (previousDistance - distance)));
            if (distance <= 0f) result.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return result;
    }
}
