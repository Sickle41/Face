using System.Collections.Generic;
using Godot;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Rules;

public static class LineOfSight
{
    public static bool HasClearLineOfSight(Vector2 from, Vector2 to, IEnumerable<TerrainFeature> terrain)
    {
        foreach (var feature in terrain)
        {
            if (!feature.BlocksLineOfSight)
            {
                continue;
            }

            var blocked = feature.Shape == TerrainShape.Circle
                ? SegmentIntersectsCircle(from, to, feature)
                : SegmentIntersectsRect(from, to, feature);

            if (blocked)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentIntersectsCircle(Vector2 a, Vector2 b, TerrainFeature feature)
    {
        var center = new Vector2((float)feature.X, (float)feature.Y);
        var radius = (float)feature.Radius;

        var ab = b - a;
        var lengthSq = ab.LengthSquared();
        var t = lengthSq > 0f ? Mathf.Clamp(ab.Dot(center - a) / lengthSq, 0f, 1f) : 0f;
        var closest = a + ab * t;

        return (closest - center).LengthSquared() <= radius * radius;
    }

    private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, TerrainFeature feature)
    {
        var halfWidth = (float)feature.Width / 2f;
        var halfHeight = (float)feature.Height / 2f;
        var cx = (float)feature.X;
        var cy = (float)feature.Y;

        var topLeft = new Vector2(cx - halfWidth, cy - halfHeight);
        var topRight = new Vector2(cx + halfWidth, cy - halfHeight);
        var bottomRight = new Vector2(cx + halfWidth, cy + halfHeight);
        var bottomLeft = new Vector2(cx - halfWidth, cy + halfHeight);

        if (IsPointInRect(a, topLeft, bottomRight) || IsPointInRect(b, topLeft, bottomRight))
        {
            return true;
        }

        return SegmentsIntersect(a, b, topLeft, topRight) ||
               SegmentsIntersect(a, b, topRight, bottomRight) ||
               SegmentsIntersect(a, b, bottomRight, bottomLeft) ||
               SegmentsIntersect(a, b, bottomLeft, topLeft);
    }

    private static bool IsPointInRect(Vector2 p, Vector2 topLeft, Vector2 bottomRight) =>
        p.X >= topLeft.X && p.X <= bottomRight.X && p.Y >= topLeft.Y && p.Y <= bottomRight.Y;

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        var d1 = Cross(p4 - p3, p1 - p3);
        var d2 = Cross(p4 - p3, p2 - p3);
        var d3 = Cross(p2 - p1, p3 - p1);
        var d4 = Cross(p2 - p1, p4 - p1);

        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
