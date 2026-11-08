using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 把当前的单层地面拆成「可破坏的表层土壤 + 不可破坏的底部岩层」。
    ///
    /// 菜单：9Days/Arena/Split Ground Into Layers
    ///
    /// 为什么要拆：现在只有一层 test_battle，门槛 0，任何子弹都能挖，而地面只有 0.875 世界单位厚，
    /// 几发就能打穿、角色掉出去没有任何救援。拆出岩层后，挖到 y = 0.25 就停手。
    ///
    /// 做法：直接对已烘焙好的遮罩按行切分，不依赖美术重新 Bake，所以不会动任何贴图。
    /// 幂等：源遮罩取「被替换的几层」的并集，重复执行结果一致。
    /// 注意：如果之后重新跑 9Days/Arena/Bake Terrain（按美术节点重烘），本次拆分会被覆盖，重跑本菜单即可。
    /// </summary>
    internal static class GroundLayerBuilder
    {
        private const string MenuPath = "9Days/Arena/Split Ground Into Layers";
        private const string DefaultConfigPath = "Assets/Data/Arena/ArenaConfig_Test_Battle.asset";
        private const string MaskRoot = "Assets/Data/Arena/Masks";

        private const string SourceLayerId = "test_battle";
        private const string SoilLayerId = "Ground_Soil";
        private const string RockLayerId = "Ground_Rock";

        /// <summary>底部岩层厚度（世界单位）。地面总厚 0.875，留 0.25 给岩层，上面 0.625 可挖。</summary>
        private const float RockThickness = 0.25f;

        /// <summary>岩层着色，靠它和土壤区分（两层共用同一张地面贴图）。</summary>
        private static readonly Color RockTint = new Color(0.55f, 0.58f, 0.64f, 1f);

        private static readonly string[] ReplacedIds = { SourceLayerId, SoilLayerId, RockLayerId };

        [MenuItem(MenuPath)]
        internal static void BuildMenu()
        {
            if (Build()) Debug.Log("[GroundLayer] 地面分层完成");
        }

        internal static bool Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[GroundLayer] 请退出播放模式再执行");
                return false;
            }

            ArenaConfig config = ResolveConfig();
            if (config == null)
            {
                Debug.LogError("[GroundLayer] 找不到 ArenaConfig（当前场景没有 ArenaController，或默认配置资产不存在）");
                return false;
            }

            config.Validate();
            int width = config.MaskWidth;
            int height = config.MaskHeight;
            float pixelSize = config.PixelSize;

            // ---------- 1. 收集源遮罩：被替换层的并集 ----------
            var solid = new bool[width * height];
            int sourceCount = 0;
            int sourceArtIndex = -1;

            for (int i = 0; i < config.layers.Count; i++)
            {
                TerrainLayerData layer = config.layers[i];
                if (layer == null || layer.initialMask == null) continue;
                if (!IsReplaced(layer.layerId)) continue;

                if (sourceArtIndex < 0 && layer.HasArt) sourceArtIndex = i;

                Texture2D mask = layer.initialMask;
                if (mask.width != width || mask.height != height)
                {
                    Debug.LogWarning($"[GroundLayer] 跳过 {layer.layerId}：遮罩 {mask.width}×{mask.height} 与配置 {width}×{height} 不符");
                    continue;
                }

                Color32[] pixels = mask.GetPixels32();
                if (pixels == null || pixels.Length != solid.Length) continue;

                for (int p = 0; p < solid.Length; p++)
                    if (pixels[p].r >= 128) solid[p] = true;

                sourceCount++;
            }

            if (sourceCount == 0)
            {
                Debug.LogError($"[GroundLayer] 没有找到可拆分的源层（期望 {SourceLayerId} / {SoilLayerId} / {RockLayerId}），未做修改");
                return false;
            }

            int solidTotal = 0;
            for (int p = 0; p < solid.Length; p++) if (solid[p]) solidTotal++;
            if (solidTotal == 0)
            {
                Debug.LogError("[GroundLayer] 源遮罩全为空，未做修改");
                return false;
            }

            // ---------- 2. 按行切成岩层 / 土壤 ----------
            int rockRows = Mathf.Clamp(Mathf.RoundToInt(RockThickness / Mathf.Max(0.0001f, pixelSize)), 1, height - 1);

            var soilPixels = new Color32[width * height];
            var rockPixels = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);

            int soilCount = 0;
            int rockCount = 0;

            for (int y = 0; y < height; y++)
            {
                bool rockRow = y < rockRows;
                for (int x = 0; x < width; x++)
                {
                    int p = x + y * width;
                    bool isSolid = solid[p];
                    soilPixels[p] = isSolid && !rockRow ? white : clear;
                    rockPixels[p] = isSolid && rockRow ? white : clear;
                    if (isSolid && !rockRow) soilCount++;
                    if (isSolid && rockRow) rockCount++;
                }
            }

            if (soilCount == 0 || rockCount == 0)
            {
                Debug.LogError($"[GroundLayer] 拆分结果为空（土壤 {soilCount} 像素 / 岩层 {rockCount} 像素），未做修改。" +
                               "调小 RockThickness 或检查地面厚度");
                return false;
            }

            // ---------- 3. 生成遮罩资产 ----------
            Texture2D soilMask = CreateOrUpdateMask($"{SoilLayerId}_Mask", soilPixels, width, height);
            Texture2D rockMask = CreateOrUpdateMask($"{RockLayerId}_Mask", rockPixels, width, height);
            if (soilMask == null || rockMask == null)
            {
                Debug.LogError("[GroundLayer] 遮罩资产创建失败，未做修改");
                return false;
            }

            // ---------- 4. 取一份美术快照 ----------
            List<TerrainArtPiece> sourceArt = null;
            TerrainLayerData sourceLayer = null;
            if (sourceArtIndex >= 0)
            {
                sourceLayer = config.layers[sourceArtIndex];
                sourceArt = sourceLayer.art;
            }
            else
            {
                for (int i = 0; i < config.layers.Count && sourceArt == null; i++)
                {
                    if (config.layers[i] != null && config.layers[i].HasArt) sourceArt = config.layers[i].art;
                }
            }

            if (sourceArt == null || sourceArt.Count == 0)
                Debug.LogWarning("[GroundLayer] 源层没有美术快照，拆分后地面将没有视觉，只剩碰撞与遮罩");

            // ---------- 5. 组装新层 ----------
            var soil = new TerrainLayerData
            {
                layerId = SoilLayerId,
                destructible = true,
                requiredBreakPower = 0f,
                collisionEnabled = true,
                alphaThreshold = sourceLayer != null ? sourceLayer.alphaThreshold : 0.5f,
                initialMask = soilMask,
                art = CloneArt(sourceArt, Color.white)
            };

            var rock = new TerrainLayerData
            {
                layerId = RockLayerId,
                destructible = false,
                requiredBreakPower = 0f,
                collisionEnabled = true,
                alphaThreshold = sourceLayer != null ? sourceLayer.alphaThreshold : 0.5f,
                initialMask = rockMask,
                art = CloneArt(sourceArt, RockTint)
            };

            // ---------- 6. 替换进配置：保持原位置，其它层不受影响 ----------
            var rebuilt = new List<TerrainLayerData>(config.layers.Count + 1);
            bool inserted = false;

            for (int i = 0; i < config.layers.Count; i++)
            {
                TerrainLayerData layer = config.layers[i];
                if (layer != null && IsReplaced(layer.layerId))
                {
                    if (!inserted)
                    {
                        rebuilt.Add(soil);
                        rebuilt.Add(rock);
                        inserted = true;
                    }
                    continue;
                }

                rebuilt.Add(layer);
            }

            if (!inserted)
            {
                rebuilt.Add(soil);
                rebuilt.Add(rock);
            }

            config.layers = rebuilt;
            config.Validate();

            EditorUtility.SetDirty(soilMask);
            EditorUtility.SetDirty(rockMask);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GroundLayer] {config.name}：土壤 {soilCount} 像素（可破坏，门槛 0）／" +
                      $"岩层 {rockCount} 像素（不可破坏，厚 {rockRows} 像素 = {rockRows * pixelSize:F3} 世界单位）");
            return true;
        }

        private static bool IsReplaced(string layerId)
        {
            for (int i = 0; i < ReplacedIds.Length; i++)
                if (ReplacedIds[i] == layerId) return true;

            return false;
        }

        /// <summary>深拷贝美术快照，并叠一个整体着色（岩层用来区分材质）。</summary>
        private static List<TerrainArtPiece> CloneArt(List<TerrainArtPiece> source, Color tint)
        {
            var result = new List<TerrainArtPiece>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
            {
                TerrainArtPiece piece = source[i];
                piece.color = new Color(piece.color.r * tint.r, piece.color.g * tint.g,
                                        piece.color.b * tint.b, piece.color.a * tint.a);
                result.Add(piece);
            }

            return result;
        }

        /// <summary>创建或覆盖一张遮罩贴图资产；尺寸不符时重建。</summary>
        private static Texture2D CreateOrUpdateMask(string assetName, Color32[] pixels, int width, int height)
        {
            string path = $"{MaskRoot}/{assetName}.asset";
            EditorAssets.EnsureFolderOfFile(path);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null && (texture.width != width || texture.height != height))
            {
                AssetDatabase.DeleteAsset(path);
                texture = null;
            }

            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = assetName;
                AssetDatabase.CreateAsset(texture, path);
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>优先用当前打开场景里的 ArenaController，其次退回指定路径的配置资产。</summary>
        private static ArenaConfig ResolveConfig()
        {
            ArenaController[] controllers = Object.FindObjectsByType<ArenaController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null) continue;
                if (controllers[i].gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (controllers[i].Config != null) return controllers[i].Config;
            }

            var config = AssetDatabase.LoadAssetAtPath<ArenaConfig>(DefaultConfigPath);
            if (config != null) return config;

            return AssetDatabase.LoadAssetAtPath<ArenaConfig>(EditorAssets.ArenaConfigPath);
        }
    }

    /// <summary>
    /// 一次性自动执行：存在 Temp/ground_layer_builder.flag 时，编辑器空闲后自动跑一遍，成功才删标记。
    /// 与 AudioSetupAutoRun / ChoosePanelAutoRun 同一套机制。
    /// </summary>
    [InitializeOnLoad]
    internal static class GroundLayerAutoRun
    {
        private const string FlagPath = "Temp/ground_layer_builder.flag";
        private static bool hooked;

        static GroundLayerAutoRun()
        {
            if (!System.IO.File.Exists(FlagPath)) return;
            Hook();
            EditorApplication.delayCall += Run;
        }

        private static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused) EditorApplication.delayCall += Run;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += Run;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (!System.IO.File.Exists(FlagPath))
            {
                Unhook();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            try
            {
                if (GroundLayerBuilder.Build())
                {
                    System.IO.File.Delete(FlagPath);
                    Unhook();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[GroundLayer] 自动执行失败：" + exception);
                Unhook();
            }
        }
    }
}
