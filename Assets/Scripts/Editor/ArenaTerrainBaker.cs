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
        internal static List<TerrainArtPiece> CollectArt(Transform root)
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            return CollectArt(renderers);
        }

        /// <summary>把一组 SpriteRenderer 收集成运行时可用数据（供对齐工具复用）。</summary>
        internal static List<TerrainArtPiece> CollectArt(IReadOnlyList<SpriteRenderer> renderers)
        {
            var pieces = new List<TerrainArtPiece>();

            for (int i = 0; i < renderers.Count; i++)
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
        internal static Texture2D BakeMask(ArenaConfig config, TerrainLayerData data)
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

        /// <summary>
        /// 把一个 Sprite 的 alpha 光栅化到世界遮罩。
        ///
        /// 用反向映射：遍历该 Sprite 覆盖到的每个遮罩格子，把格子中心（2×2 超采样）反算回贴图坐标再查 alpha。
        /// 不能按贴图像素正向填格子——遮罩比贴图密时（地面贴图竖直只有 22 像素、遮罩却有 28 行）
        /// 每个贴图像素只能填一格，会漏掉整行，地面就会渲染成一条一条的。
        /// </summary>
        internal static void RasterizePiece(Color32[] mask, int width, int height, Rect worldRect,
                                           TerrainArtPiece piece, float threshold)
        {
            Sprite sprite = piece.sprite;
            Texture2D texture = sprite.texture;

            // 内置贴图 / Atlas 贴图拿不到导入设置，读不出像素。这种情况按「整块填满」处理并明确告知，
            // 否则该部件会静默不进遮罩，运行时被 Shader 的 _WorldRect/mask 裁掉，看起来就是「消失了」。
            if (!TryEnsureReadable(texture, out string reason))
            {
                FillPieceQuad(mask, width, height, worldRect, piece);
                Debug.LogWarning($"[Arena] 贴图不可读，已按整块填充遮罩：{sprite.name}（{reason}）");
                return;
            }

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
            Quaternion inverse = Quaternion.Inverse(rotation);
            var white = new Color32(255, 255, 255, 255);
            float minAlpha = Mathf.Clamp01(threshold) * 255f;

            // 该 piece 在世界的包围盒（用于裁剪要遍历的格子范围）
            float minWx = float.MaxValue, maxWx = float.MinValue;
            float minWy = float.MaxValue, maxWy = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float lx = (i & 1) == 0 ? bounds.min.x : bounds.max.x;
                float ly = (i & 2) == 0 ? bounds.min.y : bounds.max.y;
                Vector3 world = piece.position + rotation * new Vector3(lx * scale.x, ly * scale.y, 0f);

                minWx = Mathf.Min(minWx, world.x);
                maxWx = Mathf.Max(maxWx, world.x);
                minWy = Mathf.Min(minWy, world.y);
                maxWy = Mathf.Max(maxWy, world.y);
            }

            int minX = Mathf.Clamp(Mathf.FloorToInt((minWx - worldRect.xMin) / worldRect.width * width), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((maxWx - worldRect.xMin) / worldRect.width * width), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((minWy - worldRect.yMin) / worldRect.height * height), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((maxWy - worldRect.yMin) / worldRect.height * height), 0, height - 1);

            float cellW = worldRect.width / width;
            float cellH = worldRect.height / height;

            for (int my = minY; my <= maxY; my++)
            {
                for (int mx = minX; mx <= maxX; mx++)
                {
                    if (mask[mx + my * width].r >= 128) continue;

                    bool hit = false;

                    for (int sy = 0; sy < 2 && !hit; sy++)
                    {
                        for (int sx = 0; sx < 2 && !hit; sx++)
                        {
                            float px = worldRect.xMin + (mx + 0.25f + 0.5f * sx) * cellW;
                            float py = worldRect.yMin + (my + 0.25f + 0.5f * sy) * cellH;

                            Vector3 local = inverse * (new Vector3(px, py, 0f) - piece.position);
                            float lx = Mathf.Abs(scale.x) > 0.0001f ? local.x / scale.x : 0f;
                            float ly = Mathf.Abs(scale.y) > 0.0001f ? local.y / scale.y : 0f;
                            if (piece.flipX) lx = -lx;
                            if (piece.flipY) ly = -ly;

                            float u = (lx - bounds.min.x) / Mathf.Max(0.0001f, bounds.size.x);
                            float v = (ly - bounds.min.y) / Mathf.Max(0.0001f, bounds.size.y);
                            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

                            int tx = sourceX + Mathf.Clamp(Mathf.FloorToInt(u * sourceWidth), 0, sourceWidth - 1);
                            int ty = sourceY + Mathf.Clamp(Mathf.FloorToInt(v * sourceHeight), 0, sourceHeight - 1);
                            if (tx < 0 || ty < 0 || tx >= texture.width || ty >= texture.height) continue;

                            if (source[tx + ty * texture.width].a >= minAlpha) hit = true;
                        }
                    }

                    if (hit) mask[mx + my * width] = white;
                }
            }
        }

        /// <summary>Bake 需要读取像素；不可读时自动改导入设置。</summary>
        /// <summary>尝试让贴图可读；内置资源 / Atlas 贴图会失败并给出原因，而不是抛异常中断整次烘焙。</summary>
        private static bool TryEnsureReadable(Texture2D texture, out string reason)
        {
            reason = null;

            if (texture == null)
            {
                reason = "贴图为空";
                return false;
            }

            if (texture.isReadable) return true;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                reason = "不是工程内资产（内置资源）";
                return false;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                reason = $"无法读取导入设置: {path}";
                return false;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"[ArenaTerrainBaker] 已开启 Read/Write: {path}");

            if (texture.isReadable) return true;

            reason = $"开启 Read/Write 后仍不可读: {path}";
            return false;
        }

        /// <summary>读不出像素时的兜底：把这个 Sprite 占的整块矩形填进遮罩（不做 alpha 裁剪）。</summary>
        private static void FillPieceQuad(Color32[] mask, int width, int height, Rect worldRect, TerrainArtPiece piece)
        {
            if (piece.sprite == null) return;

            Bounds bounds = piece.sprite.bounds;
            Vector3 scale = piece.scale == Vector3.zero ? Vector3.one : piece.scale;
            Quaternion rotation = Quaternion.Euler(piece.eulerAngles);
            Quaternion inverse = Quaternion.Inverse(rotation);
            var white = new Color32(255, 255, 255, 255);

            float minWx = float.MaxValue, maxWx = float.MinValue;
            float minWy = float.MaxValue, maxWy = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float lx = (i & 1) == 0 ? bounds.min.x : bounds.max.x;
                float ly = (i & 2) == 0 ? bounds.min.y : bounds.max.y;
                Vector3 world = piece.position + rotation * new Vector3(lx * scale.x, ly * scale.y, 0f);

                minWx = Mathf.Min(minWx, world.x);
                maxWx = Mathf.Max(maxWx, world.x);
                minWy = Mathf.Min(minWy, world.y);
                maxWy = Mathf.Max(maxWy, world.y);
            }

            int minX = Mathf.Clamp(Mathf.FloorToInt((minWx - worldRect.xMin) / worldRect.width * width), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((maxWx - worldRect.xMin) / worldRect.width * width), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((minWy - worldRect.yMin) / worldRect.height * height), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((maxWy - worldRect.yMin) / worldRect.height * height), 0, height - 1);

            float cellW = worldRect.width / width;
            float cellH = worldRect.height / height;

            for (int my = minY; my <= maxY; my++)
            {
                for (int mx = minX; mx <= maxX; mx++)
                {
                    float px = worldRect.xMin + (mx + 0.5f) * cellW;
                    float py = worldRect.yMin + (my + 0.5f) * cellH;

                    Vector3 local = inverse * (new Vector3(px, py, 0f) - piece.position);
                    float lx = Mathf.Abs(scale.x) > 0.0001f ? local.x / scale.x : 0f;
                    float ly = Mathf.Abs(scale.y) > 0.0001f ? local.y / scale.y : 0f;

                    if (lx < bounds.min.x || lx > bounds.max.x || ly < bounds.min.y || ly > bounds.max.y) continue;

                    mask[mx + my * width] = white;
                }
            }
        }
    }
}

