using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>纯数学取景：目标点 → 相机中心 / orthographicSize，并限制在场地内。</summary>
    public static class CameraFramer
    {
        /// <summary>根据目标点计算姿态；目标为空时返回全景姿态。</summary>
        public static CameraPose Frame(IReadOnlyList<Vector2> points, float padding, float minSize,
                                       float maxSize, float aspect, Rect bounds, float overviewPadding = 0f)
        {
            aspect = Mathf.Max(0.01f, aspect);
            minSize = Mathf.Max(0.01f, minSize);
            maxSize = Mathf.Max(minSize, maxSize);
            padding = Mathf.Max(0f, padding);

            Vector2 center;
            float size;

            if (points == null || points.Count == 0)
            {
                center = bounds.center;
                size = GetOverviewSize(bounds, overviewPadding, aspect);
            }
            else
            {
                float minX = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                float minY = float.PositiveInfinity;
                float maxY = float.NegativeInfinity;

                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 point = points[i];
                    minX = Mathf.Min(minX, point.x);
                    maxX = Mathf.Max(maxX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }

                center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                float width = maxX - minX + padding * 2f;
                float height = maxY - minY + padding * 2f;
                size = Mathf.Max(height * 0.5f, width / (2f * aspect));
            }

            size = Mathf.Clamp(size, minSize, maxSize);
            if (IsValid(bounds)) center = ClampCenter(center, size, aspect, bounds);
            return new CameraPose(center, size);
        }

        /// <summary>计算完整场地取景。</summary>
        public static CameraPose FrameOverview(Rect bounds, float padding, float minSize, float maxSize, float aspect)
        {
            return Frame(null, 0f, minSize, maxSize, aspect, bounds, padding);
        }

        /// <summary>把相机中心夹到场地内；视野比场地大时居中。</summary>
        public static Vector2 ClampCenter(Vector2 center, float orthographicSize, float aspect, Rect bounds)
        {
            if (!IsValid(bounds)) return center;

            float halfWidth = orthographicSize * Mathf.Max(0.01f, aspect);
            float halfHeight = orthographicSize;

            float x = ClampAxis(center.x, bounds.xMin, bounds.xMax, halfWidth);
            float y = ClampAxis(center.y, bounds.yMin, bounds.yMax, halfHeight);
            return new Vector2(x, y);
        }

        /// <summary>允许在场地外围额外留出的镜头空间。</summary>
        public static Rect Expand(Rect bounds, float padding)
        {
            float p = Mathf.Max(0f, padding);
            return new Rect(bounds.xMin - p, bounds.yMin - p,
                            bounds.width + p * 2f, bounds.height + p * 2f);
        }

        private static float GetOverviewSize(Rect bounds, float padding, float aspect)
        {
            return Mathf.Max(bounds.height * 0.5f, bounds.width / (2f * aspect)) + Mathf.Max(0f, padding);
        }

        private static float ClampAxis(float value, float min, float max, float halfExtent)
        {
            float low = min + halfExtent;
            float high = max - halfExtent;
            if (low > high) return (min + max) * 0.5f;
            return Mathf.Clamp(value, low, high);
        }

        private static bool IsValid(Rect bounds) => bounds.width > 0.001f && bounds.height > 0.001f;
    }
}
