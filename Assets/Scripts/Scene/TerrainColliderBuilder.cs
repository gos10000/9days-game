using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 把二值地形遮罩转成闭合轮廓。纯 C#，可在 EditMode 直接测试。
    /// 边界按“实心格子的外露边”提取，再用 RDP 简化。
    /// </summary>
    public static class TerrainColliderBuilder
    {
        /// <summary>从遮罩像素生成世界坐标闭合轮廓。</summary>
        public static List<Vector2[]> BuildContours(Color32[] pixels, int width, int height,
                                                    Rect worldRect, float threshold, float simplifyTolerance)
        {
            var contours = new List<Vector2[]>();
            if (pixels == null || width <= 0 || height <= 0) return contours;
            if (pixels.Length < width * height) return contours;

            var solid = new bool[width * height];
            for (int i = 0; i < solid.Length; i++)
                solid[i] = pixels[i].r >= Mathf.Clamp01(threshold) * 255f;

            float pixelSize = worldRect.width / width;
            return BuildContours(solid, width, height, worldRect.min, pixelSize, simplifyTolerance);
        }

        /// <summary>从布尔格子生成世界坐标闭合轮廓。</summary>
        public static List<Vector2[]> BuildContours(bool[] solid, int width, int height,
                                                    Vector2 origin, float pixelSize, float simplifyTolerance)
        {
            var contours = new List<Vector2[]>();
            if (solid == null || width <= 0 || height <= 0 || solid.Length < width * height) return contours;

            Dictionary<Vector2Int, List<Vector2Int>> edges = CollectBoundaryEdges(solid, width, height);
            while (edges.Count > 0)
            {
                Vector2[] loop = TraceLoop(edges, width, height);
                if (loop == null || loop.Length < 4) continue;

                Vector2[] world = new Vector2[loop.Length];
                for (int i = 0; i < loop.Length; i++)
                    world[i] = origin + loop[i] * pixelSize;

                Vector2[] simplified = Simplify(world, simplifyTolerance);
                if (simplified.Length >= 4) contours.Add(simplified);
            }

            return contours;
        }

        /// <summary>每个实心格子的外露边按统一方向入表。</summary>
        private static Dictionary<Vector2Int, List<Vector2Int>> CollectBoundaryEdges(
            bool[] solid, int width, int height)
        {
            var edges = new Dictionary<Vector2Int, List<Vector2Int>>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!solid[x + y * width]) continue;

                    if (y == 0 || !solid[x + (y - 1) * width])
                        AddEdge(edges, new Vector2Int(x, y), new Vector2Int(x + 1, y));

                    if (x == width - 1 || !solid[(x + 1) + y * width])
                        AddEdge(edges, new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));

                    if (y == height - 1 || !solid[x + (y + 1) * width])
                        AddEdge(edges, new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));

                    if (x == 0 || !solid[(x - 1) + y * width])
                        AddEdge(edges, new Vector2Int(x, y + 1), new Vector2Int(x, y));
                }
            }

            return edges;
        }

        private static void AddEdge(Dictionary<Vector2Int, List<Vector2Int>> edges,
                                    Vector2Int start, Vector2Int end)
        {
            if (!edges.TryGetValue(start, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>(1);
                edges.Add(start, list);
            }
            list.Add(end);
        }

        /// <summary>沿边界边走出一个闭环；遇到分叉时取第一条，剩余边留给后续环。</summary>
        private static Vector2[] TraceLoop(Dictionary<Vector2Int, List<Vector2Int>> edges,
                                           int width, int height)
        {
            Vector2Int start = default;
            bool found = false;
            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in edges)
            {
                if (pair.Value.Count == 0) continue;
                start = pair.Key;
                found = true;
                break;
            }
            if (!found) return null;

            var points = new List<Vector2Int> { start };
            Vector2Int current = start;
            int guard = Mathf.Max(16, width * height * 4);

            for (int i = 0; i < guard; i++)
            {
                if (!edges.TryGetValue(current, out List<Vector2Int> nextList) || nextList.Count == 0)
                    break;

                Vector2Int next = nextList[0];
                nextList.RemoveAt(0);
                if (nextList.Count == 0) edges.Remove(current);

                current = next;
                points.Add(current);
                if (current == start) break;
            }

            if (points.Count < 4 || points[points.Count - 1] != start) return null;

            Vector2[] result = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
                result[i] = new Vector2(points[i].x, points[i].y);
            return result;
        }

        /// <summary>闭合轮廓简化；首尾保持同一点。</summary>
        public static Vector2[] Simplify(Vector2[] points, float tolerance)
        {
            if (points == null || points.Length <= 3) return points ?? new Vector2[0];

            int count = points.Length;
            bool closed = points[0] == points[count - 1];
            int last = count - 1;
            if (last < 2) return points;

            var keep = new bool[count];
            keep[0] = true;
            keep[last] = true;

            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(0, last));
            float sqrTolerance = Mathf.Max(0f, tolerance) * Mathf.Max(0f, tolerance);

            while (stack.Count > 0)
            {
                Vector2Int range = stack.Pop();
                int first = range.x;
                int end = range.y;
                if (end - first < 2) continue;

                float maxDistance = -1f;
                int maxIndex = -1;
                Vector2 a = points[first];
                Vector2 b = points[end];

                for (int i = first + 1; i < end; i++)
                {
                    float distance = SqrDistanceToSegment(points[i], a, b);
                    if (distance <= maxDistance) continue;
                    maxDistance = distance;
                    maxIndex = i;
                }

                if (maxIndex < 0 || maxDistance <= sqrTolerance) continue;

                keep[maxIndex] = true;
                stack.Push(new Vector2Int(first, maxIndex));
                stack.Push(new Vector2Int(maxIndex, end));
            }

            var result = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                if (!keep[i]) continue;
                if (result.Count > 0 && (result[result.Count - 1] - points[i]).sqrMagnitude < 0.0000001f)
                    continue;
                result.Add(points[i]);
            }

            if (closed && result.Count > 0 && result[0] != result[result.Count - 1])
                result.Add(result[0]);

            return result.ToArray();
        }

        private static float SqrDistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.sqrMagnitude;
            if (length < 0.0000001f) return (point - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / length);
            Vector2 closest = a + ab * t;
            return (point - closest).sqrMagnitude;
        }
    }
}

