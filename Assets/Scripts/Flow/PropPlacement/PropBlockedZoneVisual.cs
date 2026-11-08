using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 放置阶段的可视化：把「已经被占住、放不进去」的范围画成红色虚线圈。
    /// 圆环贴图是运行时生成的，不依赖任何美术资源；每个区域一个 SpriteRenderer，可复用。
    /// </summary>
    public sealed class PropBlockedZoneVisual : MonoBehaviour
    {
        public readonly struct Zone
        {
            public readonly Vector2 Center;
            public readonly float Radius;

            public Zone(Vector2 center, float radius)
            {
                Center = center;
                Radius = Mathf.Max(0.1f, radius);
            }
        }

        private const int TextureSize = 256;
        private const float RingRadiusRatio = 0.47f;
        private const float ThicknessPixels = 4f;
        private const float DashOnDegrees = 7f;
        private const float DashPeriodDegrees = 12f;

        private static readonly Color32 RingColor = new Color32(255, 72, 72, 210);

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private Sprite ringSprite;
        private Texture2D ringTexture;
        private int sortingOrder = 200;

        /// <summary>在指定父节点下创建一个可视化对象，初始隐藏。</summary>
        public static PropBlockedZoneVisual Create(Transform parent)
        {
            var go = new GameObject("PropBlockedZones");
            go.transform.SetParent(parent, false);

            var visual = go.AddComponent<PropBlockedZoneVisual>();
            visual.EnsureSprite();
            go.SetActive(false);
            return visual;
        }

        public void Refresh(IReadOnlyList<Zone> zones)
        {
            EnsureSprite();

            int count = zones != null ? zones.Count : 0;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = GetRenderer(i);
                if (renderer == null) continue;

                Zone zone = zones[i];
                renderer.transform.position = new Vector3(zone.Center.x, zone.Center.y, 0f);
                float diameter = zone.Radius * 2f;
                renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
                renderer.enabled = true;
            }

            for (int i = count; i < renderers.Count; i++)
                renderers[i].enabled = false;

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            for (int i = 0; i < renderers.Count; i++)
                renderers[i].enabled = false;

            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private SpriteRenderer GetRenderer(int index)
        {
            while (renderers.Count <= index)
            {
                var go = new GameObject($"Zone_{renderers.Count}");
                go.transform.SetParent(transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = ringSprite;
                renderer.sortingOrder = sortingOrder;
                renderers.Add(renderer);
            }

            return renderers[index];
        }

        private void EnsureSprite()
        {
            if (ringSprite != null) return;

            ringTexture = BuildDashedRingTexture();
            // pixelsPerUnit = 贴图边长 → 精灵在世界里正好是 1×1，缩放值即直径
            ringSprite = Sprite.Create(
                ringTexture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            ringSprite.name = "PropBlockedRing";
        }

        private void OnDestroy()
        {
            // 运行时生成的资源不随场景卸载回收，手动清掉
            if (ringSprite != null) Destroy(ringSprite);
            if (ringTexture != null) Destroy(ringTexture);
        }

        private static Texture2D BuildDashedRingTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "PropBlockedRing",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[TextureSize * TextureSize];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            float center = TextureSize * 0.5f;
            float radius = TextureSize * RingRadiusRatio;
            int steps = Mathf.CeilToInt(2f * Mathf.PI * radius * 1.2f);

            for (int i = 0; i < steps; i++)
            {
                float angle = i * 2f * Mathf.PI / steps;
                float degrees = angle * Mathf.Rad2Deg;

                // 每 12° 一段：前 7° 实线、后 5° 留空，形成虚线
                if (Mathf.Repeat(degrees, DashPeriodDegrees) > DashOnDegrees) continue;

                int px = Mathf.RoundToInt(center + Mathf.Cos(angle) * radius);
                int py = Mathf.RoundToInt(center + Mathf.Sin(angle) * radius);
                StampDisc(pixels, px, py, ThicknessPixels, RingColor);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>画一个半径 thickness/2 的小圆点，避免细线在缩放后断裂。</summary>
        private static void StampDisc(Color32[] pixels, int cx, int cy, float thickness, Color32 color)
        {
            int r = Mathf.Max(1, Mathf.CeilToInt(thickness * 0.5f));
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > r * r) continue;

                    int px = cx + x;
                    int py = cy + y;
                    if (px < 0 || py < 0 || px >= TextureSize || py >= TextureSize) continue;

                    pixels[px + py * TextureSize] = color;
                }
            }
        }
    }
}
