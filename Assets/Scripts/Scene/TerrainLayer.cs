using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 运行时地形层：负责该层视觉、遮罩像素和局部挖除/追加。
    /// 物理边界由 ArenaController 统一从所有层的并集重建。
    /// </summary>
    public class TerrainLayer : MonoBehaviour
    {
        private TerrainLayerData data;
        private Rect worldRect;
        private int width;
        private int height;
        private Texture2D mask;
        private Color32[] pixels;
        private Material material;
        private readonly List<GameObject> artObjects = new List<GameObject>();

        public TerrainLayerData Data => data;
        public string LayerId => data != null ? data.layerId : string.Empty;
        public bool Destructible => data != null && data.destructible;
        public bool CollisionEnabled => data != null && data.collisionEnabled;
        public float RequiredBreakPower => data != null ? data.requiredBreakPower : 0f;
        public Texture2D MaskTexture => mask;
        public Color32[] MaskPixels => pixels;
        public int MaskWidth => width;
        public int MaskHeight => height;

        /// <summary>创建遮罩、视觉和遮罩材质；由 ArenaController 调用。</summary>
        public void Initialize(TerrainLayerData layerData, Rect bounds, int maskWidth, int maskHeight,
                               Material baseMaterial)
        {
            data = layerData;
            worldRect = bounds;
            width = Mathf.Max(1, maskWidth);
            height = Mathf.Max(1, maskHeight);

            CreateMask();
            CreateMaterial(baseMaterial);
            BuildArt();
        }

        /// <summary>复制初始遮罩；尺寸不匹配时按最近邻采样。</summary>
        private void CreateMask()
        {
            pixels = new Color32[width * height];
            Color32[] source = data != null && data.initialMask != null
                ? data.initialMask.GetPixels32()
                : null;

            if (source == null || source.Length == 0)
            {
                FillMask(new Color32(255, 255, 255, 255));
            }
            else if (source.Length == pixels.Length)
            {
                System.Array.Copy(source, pixels, pixels.Length);
            }
            else
            {
                int sourceWidth = data.initialMask.width;
                int sourceHeight = data.initialMask.height;
                for (int y = 0; y < height; y++)
                {
                    float sy = (y + 0.5f) / height * sourceHeight;
                    int srcY = Mathf.Clamp(Mathf.FloorToInt(sy), 0, sourceHeight - 1);
                    for (int x = 0; x < width; x++)
                    {
                        float sx = (x + 0.5f) / width * sourceWidth;
                        int srcX = Mathf.Clamp(Mathf.FloorToInt(sx), 0, sourceWidth - 1);
                        pixels[x + y * width] = source[srcX + srcY * sourceWidth];
                    }
                }
                Debug.LogWarning($"[Terrain] 遮罩尺寸不匹配，已采样: {data.layerId}");
            }

            mask = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"TerrainMask_{LayerId}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            ApplyMask();
        }

        /// <summary>创建该层专用的遮罩材质。</summary>
        private void CreateMaterial(Material baseMaterial)
        {
            Shader shader = baseMaterial != null ? baseMaterial.shader : Shader.Find("NineDays/TerrainMask");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            material = new Material(shader);
            if (material.HasProperty("_MaskTex")) material.SetTexture("_MaskTex", mask);
            if (material.HasProperty("_WorldRect"))
                material.SetVector("_WorldRect", new Vector4(worldRect.x, worldRect.y, worldRect.width, worldRect.height));
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
        }

        /// <summary>按 Bake 数据重建该层的 SpriteRenderer。</summary>
        private void BuildArt()
        {
            if (data == null || data.art == null) return;

            for (int i = 0; i < data.art.Count; i++)
            {
                TerrainArtPiece piece = data.art[i];
                if (piece.sprite == null) continue;

                var go = new GameObject($"Art_{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = piece.position;
                go.transform.eulerAngles = piece.eulerAngles;
                go.transform.localScale = piece.scale == Vector3.zero ? Vector3.one : piece.scale;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = piece.sprite;
                renderer.color = piece.color;
                renderer.flipX = piece.flipX;
                renderer.flipY = piece.flipY;
                renderer.drawMode = piece.drawMode;
                if (piece.drawMode != SpriteDrawMode.Simple) renderer.size = piece.size;
                renderer.tileMode = piece.tileMode;
                renderer.sortingLayerName = string.IsNullOrEmpty(piece.sortingLayerName)
                    ? "Default"
                    : piece.sortingLayerName;
                renderer.sortingOrder = piece.sortingOrder;
                if (material != null) renderer.sharedMaterial = material;

                artObjects.Add(go);
            }
        }

        /// <summary>在圆形范围抹除；返回实际清除的像素数。</summary>
        public int EraseCircle(Vector2 center, float radius, float breakPower)
        {
            if (!CanErase(breakPower)) return 0;

            float r2 = radius * radius;
            int minX = WorldToPixelX(center.x - radius);
            int maxX = WorldToPixelX(center.x + radius);
            int minY = WorldToPixelY(center.y - radius);
            int maxY = WorldToPixelY(center.y + radius);
            int changed = 0;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 pixelCenter = PixelToWorld(x, y);
                    if ((pixelCenter - center).sqrMagnitude > r2) continue;
                    if (!IsSolid(x, y)) continue;

                    pixels[x + y * width] = new Color32(0, 0, 0, 0);
                    changed++;
                }
            }

            if (changed > 0) ApplyMask();
            return changed;
        }

        /// <summary>在矩形范围抹除；返回实际清除的像素数。</summary>
        public int EraseBox(Vector2 center, Vector2 size, float breakPower)
        {
            if (!CanErase(breakPower)) return 0;

            Vector2 half = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
            int minX = WorldToPixelX(center.x - half.x);
            int maxX = WorldToPixelX(center.x + half.x);
            int minY = WorldToPixelY(center.y - half.y);
            int maxY = WorldToPixelY(center.y + half.y);
            int changed = 0;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!IsSolid(x, y)) continue;
                    pixels[x + y * width] = new Color32(0, 0, 0, 0);
                    changed++;
                }
            }

            if (changed > 0) ApplyMask();
            return changed;
        }

        /// <summary>在圆形范围补回地形；返回实际写入的像素数。</summary>
        public int AppendCircle(Vector2 center, float radius)
        {
            float r2 = radius * radius;
            int minX = WorldToPixelX(center.x - radius);
            int maxX = WorldToPixelX(center.x + radius);
            int minY = WorldToPixelY(center.y - radius);
            int maxY = WorldToPixelY(center.y + radius);
            int changed = 0;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 pixelCenter = PixelToWorld(x, y);
                    if ((pixelCenter - center).sqrMagnitude > r2) continue;
                    if (IsSolid(x, y)) continue;

                    pixels[x + y * width] = new Color32(255, 255, 255, 255);
                    changed++;
                }
            }

            if (changed > 0) ApplyMask();
            return changed;
        }

        /// <summary>在矩形范围补回地形；返回实际写入的像素数。</summary>
        public int AppendBox(Vector2 center, Vector2 size)
        {
            Vector2 half = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
            int minX = WorldToPixelX(center.x - half.x);
            int maxX = WorldToPixelX(center.x + half.x);
            int minY = WorldToPixelY(center.y - half.y);
            int maxY = WorldToPixelY(center.y + half.y);
            int changed = 0;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsSolid(x, y)) continue;
                    pixels[x + y * width] = new Color32(255, 255, 255, 255);
                    changed++;
                }
            }

            if (changed > 0) ApplyMask();
            return changed;
        }

        /// <summary>破坏力是否达到该层门槛。</summary>
        private bool CanErase(float breakPower)
        {
            return data != null && data.destructible && breakPower >= data.requiredBreakPower;
        }


        private bool IsSolid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            return pixels[x + y * width].r >= 128;
        }

        private int WorldToPixelX(float worldX)
        {
            float t = (worldX - worldRect.xMin) / Mathf.Max(0.0001f, worldRect.width);
            return Mathf.Clamp(Mathf.FloorToInt(t * width), 0, width - 1);
        }

        private int WorldToPixelY(float worldY)
        {
            float t = (worldY - worldRect.yMin) / Mathf.Max(0.0001f, worldRect.height);
            return Mathf.Clamp(Mathf.FloorToInt(t * height), 0, height - 1);
        }

        private Vector2 PixelToWorld(int x, int y)
        {
            float px = worldRect.xMin + (x + 0.5f) / width * worldRect.width;
            float py = worldRect.yMin + (y + 0.5f) / height * worldRect.height;
            return new Vector2(px, py);
        }

        private void FillMask(Color32 color)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        }

        private void ApplyMask()
        {
            if (mask == null) return;
            mask.SetPixels32(pixels);
            mask.Apply(false, false);
        }

        /// <summary>清空该层运行时对象。</summary>
        public void Clear()
        {
            for (int i = 0; i < artObjects.Count; i++)
                if (artObjects[i] != null) DestroyObject(artObjects[i]);
            artObjects.Clear();

            if (material != null) DestroyObject(material);
            if (mask != null) DestroyObject(mask);
            material = null;
            mask = null;
            pixels = null;
        }

        private void OnDestroy()
        {
            if (material != null) DestroyObject(material);
            if (mask != null) DestroyObject(mask);
        }

        private static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}

