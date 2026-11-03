using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成 P0 默认地形配置与遮罩；已有 ArenaConfig 时不覆盖人工 Bake 的结果。
    /// </summary>
    internal static class ArenaTerrainFactory
    {
        private const string GroundId = "Ground";
        private const string WoodId = "Wood";
        private const string StoneId = "Stone";

        internal static ArenaConfig EnsureDefaultConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaConfig>(EditorAssets.ArenaConfigPath);
            if (existing != null)
            {
                existing.Validate();
                return existing;
            }

            EditorAssets.EnsureFolderOfFile(EditorAssets.ArenaConfigPath);
            var config = ScriptableObject.CreateInstance<ArenaConfig>();
            config.worldRect = new Rect(-8f, -5f, 16f, 10f);
            config.pixelsPerUnit = 16;
            AssetDatabase.CreateAsset(config, EditorAssets.ArenaConfigPath);

            Sprite square = EditorAssets.LoadSquareSprite();
            AddLayer(config, square, GroundId, false, 0f,
                     new Rect(-8f, -5f, 16f, 0.3f), new Color(0.25f, 0.28f, 0.34f), -10);
            AddLayer(config, square, WoodId, true, 1f,
                     new Rect(-0.7f, -4.7f, 1.4f, 1.2f), new Color(0.72f, 0.45f, 0.2f), -9);
            AddLayer(config, square, StoneId, true, 3f,
                     new Rect(2.2f, -4.7f, 1.6f, 1.0f), new Color(0.48f, 0.52f, 0.6f), -9);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArenaTerrainFactory] 已生成默认地形配置");
            return config;
        }

        internal static Material EnsureTerrainMaterial()
        {
            return EditorAssets.CreateOrLoadMaterial(
                EditorAssets.TerrainMaterialPath,
                "NineDays/TerrainMask",
                "Sprites/Default");
        }

        /// <summary>生成一层：遮罩按世界矩形填充，视觉用一个缩放到该矩形的 Sprite。</summary>
        private static void AddLayer(ArenaConfig config, Sprite sprite, string layerId, bool destructible,
                                     float requiredBreakPower, Rect area, Color color, int sortingOrder)
        {
            var layer = new TerrainLayerData
            {
                layerId = layerId,
                destructible = destructible,
                requiredBreakPower = requiredBreakPower,
                collisionEnabled = true,
                initialMask = CreateMask(config, layerId, area)
            };

            Vector3 bounds = sprite != null ? sprite.bounds.size : Vector3.one;
            Vector3 scale = new Vector3(
                bounds.x > 0.0001f ? area.width / bounds.x : area.width,
                bounds.y > 0.0001f ? area.height / bounds.y : area.height,
                1f);

            layer.art.Add(TerrainArtPiece.Create(sprite, area.center, scale, color, sortingOrder));
            config.layers.Add(layer);
        }

        /// <summary>创建或更新一张纯白矩形遮罩资产。</summary>
        private static Texture2D CreateMask(ArenaConfig config, string layerId, Rect area)
        {
            int width = config.MaskWidth;
            int height = config.MaskHeight;
            var pixels = new Color32[width * height];
            var black = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = black;

            for (int y = 0; y < height; y++)
            {
                float wy = config.worldRect.yMin + (y + 0.5f) / height * config.worldRect.height;
                if (wy < area.yMin || wy > area.yMax) continue;

                for (int x = 0; x < width; x++)
                {
                    float wx = config.worldRect.xMin + (x + 0.5f) / width * config.worldRect.width;
                    if (wx < area.xMin || wx > area.xMax) continue;
                    pixels[x + y * width] = white;
                }
            }

            string maskPath = $"{EditorAssets.ArenaMaskRoot}/{layerId}_Mask.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) AssetDatabase.DeleteAsset(maskPath);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = $"{layerId}_Mask";
                EditorAssets.EnsureFolderOfFile(maskPath);
                AssetDatabase.CreateAsset(texture, maskPath);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }
    }
}
