using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>纯数学取景：目标点 → 相机中心 / orthographicSize，并限制在场地内。</summary>
    public static class CameraFramer
    {
        /// <summary>根据目标点计算姿态；目标为空时返回全景姿态。</summary>
        public static CameraPose Frame(IReadOnlyList<Vector2> points, float padding, float minSize,
                                       float maxSize, float aspect, Rect bounds, float overviewPadding = 0f,
                                       float edgeMargin = 0f, IReadOnlyList<Vector2> marginPoints = null)
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
            bool clampToBounds = IsValid(bounds);
            if (clampToBounds) center = ClampCenter(center, size, aspect, bounds);

            // 中心被夹回场地后，目标可能贴到屏幕边缘（贴墙、站在地面时最常见）。
            // 这里按「目标距边缘至少 edgeMargin」反解缩放，用拉远把空间补回来。
            // 场地比视野矮时相机 y 被锁死，只能靠缩放；横向同样用缩放，避免两套策略混用。
            IReadOnlyList<Vector2> margin = marginPoints ?? points;
            if (edgeMargin > 0f && margin != null && margin.Count > 0)
            {
                // 拉远会收窄可平移范围，中心可能再次移动，所以迭代求解；
                // 目标数很少，2~3 轮即收敛，这里给 6 轮上限兜底。
                for (int pass = 0; pass < 6; pass++)
                {
                    float required = MinSizeForMargin(margin, center, edgeMargin, aspect);
                    if (required <= size + 0.0001f) break;

                    size = Mathf.Clamp(required, minSize, maxSize);
                    if (clampToBounds) center = ClampCenter(center, size, aspect, bounds);

                    // 已经顶到上限，再迭代也补不出更多空间
                    if (size >= maxSize - 0.0001f) break;
                }
            }

            return new CameraPose(center, size);
        }

        /// <summary>
        /// 让目标点距屏幕四边都至少有 margin 所需的最小 orthographicSize。
        /// 横向 halfWidth = size × aspect，纵向 halfHeight = size，故两者换算不同。
        /// </summary>
        private static float MinSizeForMargin(IReadOnlyList<Vector2> points, Vector2 center,
                                             float margin, float aspect)
        {
            float required = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 delta = points[i] - center;
                required = Mathf.Max(required, (Mathf.Abs(delta.x) + margin) / aspect);
                required = Mathf.Max(required, Mathf.Abs(delta.y) + margin);
            }

            return required;
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
