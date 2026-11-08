using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 编辑期工具：从 ArenaAuthoring 场景读取分层美术，Bake 成 ArenaConfig + 遮罩资产。
    /// 支持重复堆叠多个普通 Sprite；Tiled/Sliced 需要拆成普通 Sprite。
    /// </summary>
    internal static class ArenaTerrainBaker
    {
        [MenuItem("9Days/Arena/Create Authoring Scene")]
        public static void CreateAuthoringScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("ArenaAuthoring");
            var authoring = root.AddComponent<ArenaAuthoring>();
            var layers = new GameObject("Layers");
            layers.transform.SetParent(root.transform, false);
            authoring.layersRoot = layers.transform;
            authoring.config = AssetDatabase.LoadAssetAtPath<ArenaConfig>(EditorAssets.ArenaConfigPath);

            EditorAssets.EnsureFolder(EditorAssets.SceneRoot);
            EditorSceneManager.SaveScene(scene, EditorAssets.ArenaAuthoringScenePath);
            EditorSceneManager.OpenScene(EditorAssets.ArenaAuthoringScenePath);
            Debug.Log($"[ArenaTerrainBaker] 已创建 {EditorAssets.ArenaAuthoringScenePath}");
        }

        [MenuItem("9Days/Arena/Bake Terrain")]
        public static void Bake()
        {
            ArenaAuthoring authoring = UnityEngine.Object.FindAnyObjectByType<ArenaAuthoring>();
            if (authoring == null)
            {
                throw new InvalidOperationException("当前场景没有 ArenaAuthoring，请先打开 ArenaAuthoring 场景");
            }

            ArenaConfig config = authoring.config;
            if (config == null)
                config = AssetDatabase.LoadAssetAtPath<ArenaConfig>(EditorAssets.ArenaConfigPath);
            if (config == null)
            {
                EditorAssets.EnsureFolderOfFile(EditorAssets.ArenaConfigPath);
                config = ScriptableObject.CreateInstance<ArenaConfig>();
                AssetDatabase.CreateAsset(config, EditorAssets.ArenaConfigPath);
            }

            config.worldRect = authoring.worldRect;
            config.pixelsPerUnit = Mathf.Max(1, authoring.pixelsPerUnit);
            config.layers = new List<TerrainLayerData>();

            TerrainLayerAuthoring[] layers = authoring.layersRoot != null
                ? authoring.layersRoot.GetComponentsInChildren<TerrainLayerAuthoring>(true)
                : UnityEngine.Object.FindObjectsByType<TerrainLayerAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < layers.Length; i++)
            {
                TerrainLayerAuthoring source = layers[i];
                if (source == null) continue;

                var data = new TerrainLayerData
                {
                    layerId = source.layerId,
                    destructible = source.destructible,
                    requiredBreakPower = source.requiredBreakPower,
                    collisionEnabled = source.collisionEnabled,
                    alphaThreshold = source.alphaThreshold,
                    art = CollectArt(source.transform)
                };

                data.initialMask = BakeMask(config, data);
                config.layers.Add(data);
            }

            authoring.config = config;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(authoring);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArenaTerrainBaker] Bake 完成: {config.layers.Count} 层");
        }

        /// <summary>
        /// 只用 ArenaConfig 里已有的 art 重烘遮罩，不需要当前场景里有 ArenaAuthoring。
        /// 场景里没摆 authoring 对象、或 worldRect 被改过之后，用这个重烘不会动到其它配置。
        /// </summary>
        [MenuItem("9Days/Arena/Rebake Mask From Config")]
        public static void RebakeFromConfig()
        {
            ArenaConfig config = Selection.activeObject as ArenaConfig;
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ArenaConfig");
                if (guids.Length == 1)
                    config = AssetDatabase.LoadAssetAtPath<ArenaConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (config == null)
            {
                Debug.LogError("[ArenaTerrainBaker] 请先在 Project 窗口选中一个 ArenaConfig 再执行");
                return;
            }

            int solidLayers = 0;
            int emptyLayers = 0;

            for (int i = 0; i < config.layers.Count; i++)
            {
                TerrainLayerData data = config.layers[i];
                if (data == null) continue;

                try
                {
                    Texture2D mask = BakeMask(config, data);
                    Color32[] pixels = mask != null ? mask.GetPixels32() : null;

                    int solid = 0;
                    if (pixels != null)
                        for (int p = 0; p < pixels.Length; p++)
                            if (pixels[p].r >= 128) solid++;

                    if (solid > 0) solidLayers++;
                    else emptyLayers++;

                    Debug.Log($"[ArenaTerrainBaker] {data.layerId}: {mask.width}x{mask.height}，" +
                              $"实心像素 {solid}，art {data.art?.Count ?? 0} 件");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArenaTerrainBaker] {data.layerId} 烘焙失败: {e.Message}");
                }
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ArenaTerrainBaker] 完成：{solidLayers} 层有内容、{emptyLayers} 层为空；" +
                      $"世界矩形 {config.worldRect}，遮罩 {config.MaskWidth}x{config.MaskHeight}");
        }

        /// <summary>把层根下所有 SpriteRenderer 收集成运行时可用数据。</summary>
        private static List<TerrainArtPiece> CollectArt(Transform root)
        {
            var pieces = new List<TerrainArtPiece>();
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.sprite == null) continue;

                pieces.Add(new TerrainArtPiece
                {
                    sprite = renderer.sprite,
                    position = renderer.transform.position,
                    eulerAngles = renderer.transform.eulerAngles,
                    scale = renderer.transform.lossyScale,
                    color = renderer.color,
                    sortingLayerName = renderer.sortingLayerName,
                    sortingOrder = renderer.sortingOrder,
                    flipX = renderer.flipX,
                    flipY = renderer.flipY,
                    drawMode = renderer.drawMode,
                    size = renderer.size,
                    tileMode = renderer.tileMode
                });
            }

            return pieces;
        }

        /// <summary>把该层所有 Sprite 的 alpha 光栅化到一张世界空间遮罩。</summary>
        private static Texture2D BakeMask(ArenaConfig config, TerrainLayerData data)
        {
            int width = config.MaskWidth;
            int height = config.MaskHeight;
            var pixels = new Color32[width * height];
            var black = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = black;

            if (data.art != null)
            {
                for (int i = 0; i < data.art.Count; i++)
                {
                    TerrainArtPiece piece = data.art[i];
                    if (piece.sprite == null) continue;
                    if (piece.drawMode != SpriteDrawMode.Simple)
                    {
                        throw new InvalidOperationException(
                            $"层 {data.layerId} 的 Sprite 使用了 {piece.drawMode}；请拆成多个普通 Sprite 再 Bake");
                    }

                    RasterizePiece(pixels, width, height, config.worldRect, piece, data.alphaThreshold);
                }
            }

            string maskPath = $"{EditorAssets.ArenaMaskRoot}/{data.layerId}_Mask.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) AssetDatabase.DeleteAsset(maskPath);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = $"{data.layerId}_Mask";
                EditorAssets.EnsureFolderOfFile(maskPath);
                AssetDatabase.CreateAsset(texture, maskPath);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        /// <summary>把一个 Sprite 的每个不透明像素映射到世界遮罩。</summary>
        private static void RasterizePiece(Color32[] mask, int width, int height, Rect worldRect,
                                           TerrainArtPiece piece, float threshold)
        {
            Sprite sprite = piece.sprite;
            Texture2D texture = sprite.texture;
            if (texture == null) return;
            EnsureReadable(texture);

            Rect rect = sprite.textureRect;
            int sourceX = Mathf.FloorToInt(rect.x);
            int sourceY = Mathf.FloorToInt(rect.y);
            int sourceWidth = Mathf.FloorToInt(rect.width);
            int sourceHeight = Mathf.FloorToInt(rect.height);
            if (sourceWidth <= 0 || sourceHeight <= 0) return;

            Color32[] source = texture.GetPixels32();
            Bounds bounds = sprite.bounds;
            Vector3 scale = piece.scale == Vector3.zero ? Vector3.one : piece.scale;
            Quaternion rotation = Quaternion.Euler(piece.eulerAngles);
            var white = new Color32(255, 255, 255, 255);
            float minAlpha = Mathf.Clamp01(threshold) * 255f;

            for (int y = 0; y < sourceHeight; y++)
            {
                int ty = sourceY + y;
                if (ty < 0 || ty >= texture.height) continue;

                for (int x = 0; x < sourceWidth; x++)
                {
                    int tx = sourceX + x;
                    if (tx < 0 || tx >= texture.width) continue;

                    Color32 color = source[tx + ty * texture.width];
                    if (color.a < minAlpha) continue;

                    float u = (x + 0.5f) / sourceWidth;
                    float v = (y + 0.5f) / sourceHeight;
                    float lx = Mathf.Lerp(bounds.min.x, bounds.max.x, u);
                    float ly = Mathf.Lerp(bounds.min.y, bounds.max.y, v);
                    if (piece.flipX) lx = -lx;
                    if (piece.flipY) ly = -ly;

                    Vector3 local = new Vector3(lx * scale.x, ly * scale.y, 0f);
                    Vector3 world = piece.position + rotation * local;
                    int mx = Mathf.FloorToInt((world.x - worldRect.xMin) / worldRect.width * width);
                    int my = Mathf.FloorToInt((world.y - worldRect.yMin) / worldRect.height * height);
                    if (mx < 0 || my < 0 || mx >= width || my >= height) continue;

                    mask[mx + my * width] = white;
                }
            }
        }

        /// <summary>Bake 需要读取像素；不可读时自动改导入设置。</summary>
        private static void EnsureReadable(Texture2D texture)
        {
            if (texture.isReadable) return;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException($"贴图不可读且不是资产: {texture.name}");
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"无法读取贴图导入设置: {path}");
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"[ArenaTerrainBaker] 已开启 Read/Write: {path}");
        }
    }
}

