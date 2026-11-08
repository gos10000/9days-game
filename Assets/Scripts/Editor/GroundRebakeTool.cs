using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 把场景里已经摆好的地面美术「接管」成可破坏地形。
    ///
    /// 菜单：9Days/Arena/Rebuild Ground From Selected Art
    /// 用法：在场景里多选地面/草地的 SpriteRenderer（比如 ground_0 + grass_0），执行菜单。
    ///
    /// 为什么需要它：运行时地形模块有两套坐标系
    /// - 遮罩与碰撞轮廓用 **ArenaController 的本地坐标**（TerrainLayer 的 worldRect 映射 + TerrainPhysics 子节点）
    /// - 美术用 **绝对世界坐标**（TerrainLayer.BuildArt 里 `go.transform.position = piece.position`）
    /// 所以控制器不在原点时，美术和碰撞会分家；而 worldRect / 遮罩又必须对上场景里真实地面的位置。
    /// 这个工具一次性把三者对齐：控制器归零、按选中美术重算 worldRect、重烘遮罩、重建成土壤 + 岩层两层。
    ///
    /// 两遍烘焙：先用选中美术的 bounds 粗框烘一张，量出实心范围，再把 worldRect 收紧到实心范围 + 边距，
    /// 这样遮罩不会因为 Sprite 的大片透明留白而浪费分辨率。
    ///
    /// 幂等：重复执行结果一致。执行后会把选中的 SpriteRenderer 与其 Collider2D 停用（美术由运行时重建，
    /// 视觉效果不变，但不再挡住挖出来的洞）。
    /// </summary>
    internal static class GroundRebakeTool
    {
        private const string MenuPath = "9Days/Arena/Rebuild Ground From Selected Art";
        private const string MaskRoot = "Assets/Data/Arena/Masks";
        private const string SoilId = "Ground_Soil";
        private const string RockId = "Ground_Rock";
        private const string BaseId = "Ground_Base";

        /// <summary>底部岩层厚度（世界单位）。</summary>
        private const float RockThickness = 0.25f;

        /// <summary>厚度超过这个值的部件当作「地基/边界块」，整块不可破坏；薄的才切土壤 + 岩层。</summary>
        private const float BasePieceThickness = 3f;

        /// <summary>worldRect 在实心范围外留的边距（世界单位）。</summary>
        private const float Margin = 0.1f;

        private const int PixelsPerUnit = 16;
        private const float AlphaThreshold = 0.5f;

        private static readonly Color RockTint = new Color(0.55f, 0.58f, 0.64f, 1f);

        [MenuItem(MenuPath)]
        private static void MenuEntry()
        {
            if (Build()) Debug.Log("[GroundRebake] 地面已接管为可破坏地形");
        }

        /// <summary>
        /// 取消接管：把选中的 SpriteRenderer / Collider2D 重新启用，恢复成普通静态美术。
        /// 之后若不希望它继续出现在地形配置里，重跑 Rebuild（不选它）即可——配置每次都是按选中项整体重建的。
        /// </summary>
        [MenuItem("9Days/Arena/Release Selected Art (恢复静态)")]
        private static void ReleaseMenu()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[GroundRebake] 请先选中要恢复的静态美术对象");
                return;
            }

            int renderers = 0;
            int colliders = 0;

            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null) continue;

                SpriteRenderer[] found = selected[i].GetComponentsInChildren<SpriteRenderer>(true);
                for (int j = 0; j < found.Length; j++)
                {
                    if (found[j] == null) continue;
                    if (!found[j].enabled) renderers++;
                    found[j].enabled = true;

                    Collider2D collider = found[j].GetComponent<Collider2D>();
                    if (collider != null)
                    {
                        if (!collider.enabled) colliders++;
                        collider.enabled = true;
                    }
                }

                EditorSceneManager.MarkSceneDirty(selected[i].scene);
            }

            Debug.Log($"[GroundRebake] 已恢复 {renderers} 个 SpriteRenderer、{colliders} 个 Collider2D 为启用状态。" +
                      "若还要把它从地形配置里移除，重跑 Rebuild Ground From Selected Art 时不要选中它");
        }

        internal static bool Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[GroundRebake] 请退出播放模式再执行");
                return false;
            }

            List<SpriteRenderer> renderers = CollectSelection();
            if (renderers.Count == 0)
            {
                Debug.LogError("[GroundRebake] 请先在场景里多选地面/草地的 SpriteRenderer（Hierarchy 里可按住 Ctrl 多选）");
                return false;
            }

            ArenaController arena = FindArena();
            if (arena == null)
            {
                Debug.LogError("[GroundRebake] 当前场景没有 ArenaController");
                return false;
            }

            ArenaConfig config = arena.Config;
            if (config == null)
            {
                Debug.LogError("[GroundRebake] ArenaController 上没有挂 ArenaConfig");
                return false;
            }

            List<TerrainArtPiece> art = ArenaTerrainBaker.CollectArt(renderers);
            if (art.Count == 0)
            {
                Debug.LogError("[GroundRebake] 选中的对象里没有可用的 Sprite");
                return false;
            }

            // 美术用绝对世界坐标，遮罩/碰撞体用控制器本地坐标 → 控制器必须归零
            Vector3 oldPosition = arena.transform.position;
            if (oldPosition != Vector3.zero)
            {
                arena.transform.position = Vector3.zero;
                Debug.Log($"[GroundRebake] ArenaController 位置 {oldPosition} → (0,0,0)：" +
                          "遮罩与碰撞体按本地坐标生成，美术按世界坐标生成，必须归零才能重合");
            }

            // ---------- 第一遍：粗框，量实心范围 ----------
            Rect rough = UnionBounds(renderers);
            if (rough.width <= 0.0001f || rough.height <= 0.0001f)
            {
                Debug.LogError("[GroundRebake] 选中美术的世界包围盒为空");
                return false;
            }

            rough = Expand(rough, Margin);

            int roughWidth = Mathf.Max(1, Mathf.RoundToInt(rough.width * PixelsPerUnit));
            int roughHeight = Mathf.Max(1, Mathf.RoundToInt(rough.height * PixelsPerUnit));
            Color32[] probe = Rasterize(rough, art, roughWidth, roughHeight);

            if (!TryGetSolidBounds(probe, roughWidth, roughHeight, rough, out Rect solidRect))
            {
                Debug.LogError("[GroundRebake] 选中美术全透明，烘不出任何地形");
                return false;
            }

            // ---------- 第二遍：收紧到实心范围 ----------
            Rect finalRect = Expand(solidRect, Margin);
            finalRect = SnapToPixelGrid(finalRect, PixelsPerUnit);

            int width = Mathf.Max(1, Mathf.RoundToInt(finalRect.width * PixelsPerUnit));
            int height = Mathf.Max(1, Mathf.RoundToInt(finalRect.height * PixelsPerUnit));
            float pixelSize = finalRect.width / width;

            // ---------- 按厚度分类：薄的当地面，厚的当不可破坏基底 ----------
            SplitRenderersByThickness(renderers, out List<SpriteRenderer> thinRenderers,
                                      out List<SpriteRenderer> thickRenderers);

            List<TerrainArtPiece> thinArt = ArenaTerrainBaker.CollectArt(thinRenderers);
            List<TerrainArtPiece> thickArt = ArenaTerrainBaker.CollectArt(thickRenderers);

            Color32[] thinSolid = Rasterize(finalRect, thinArt, width, height);
            Color32[] thickSolid = thickArt.Count > 0 ? Rasterize(finalRect, thickArt, width, height) : null;

            int thickCount = 0;
            if (thickSolid != null)
                for (int p = 0; p < thickSolid.Length; p++)
                    if (thickSolid[p].r >= 128) thickCount++;

            // ---------- 薄条按行切岩层 / 土壤 ----------
            var soilPixels = new Color32[width * height];
            var rockPixels = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            for (int p = 0; p < soilPixels.Length; p++)
            {
                soilPixels[p] = clear;
                rockPixels[p] = clear;
            }

            int soilCount = 0;
            int rockCount = 0;
            int rockRows = 0;

            if (TryGetSolidRows(thinSolid, width, height, out int rowMin, out int rowMax))
            {
                int solidRows = rowMax - rowMin + 1;
                rockRows = Mathf.Clamp(Mathf.RoundToInt(RockThickness / Mathf.Max(0.0001f, pixelSize)), 1,
                                       Mathf.Max(1, solidRows - 1));

                for (int y = 0; y < height; y++)
                {
                    bool rockRow = y >= rowMin && y < rowMin + rockRows;

                    for (int x = 0; x < width; x++)
                    {
                        int p = x + y * width;
                        if (thinSolid[p].r < 128) continue;

                        if (rockRow)
                        {
                            rockPixels[p] = thinSolid[p];
                            rockCount++;
                        }
                        else
                        {
                            soilPixels[p] = thinSolid[p];
                            soilCount++;
                        }
                    }
                }

                Debug.Log($"[GroundRebake] 薄地面 {thinArt.Count} 件 → 土壤 {soilCount} 像素 / 岩层 {rockCount} 像素" +
                          $"（岩层 {rockRows} 行 = {rockRows * pixelSize:F3} 单位）");
            }
            else
            {
                Debug.LogWarning("[GroundRebake] 没有可切的薄地面，只生成基底");
            }

            if (thickArt.Count > 0)
                Debug.Log($"[GroundRebake] 厚块 {thickArt.Count} 件 → 不可破坏基底 {thickCount} 像素");

            if (soilCount == 0 && thickCount == 0)
            {
                Debug.LogError("[GroundRebake] 既没有薄地面也没有厚块，未做修改");
                return false;
            }

            Texture2D soilMask = soilCount > 0 ? CreateOrUpdateMask($"{SoilId}_Mask", soilPixels, width, height) : null;
            Texture2D rockMask = rockCount > 0 ? CreateOrUpdateMask($"{RockId}_Mask", rockPixels, width, height) : null;
            Texture2D baseMask = thickCount > 0 ? CreateOrUpdateMask($"{BaseId}_Mask", thickSolid, width, height) : null;

            // 所有校验都通过后才改写配置，避免半途失败留下不一致的 worldRect
            config.worldRect = finalRect;
            config.pixelsPerUnit = PixelsPerUnit;

            // ---------- 重建层 ----------
            var layers = new List<TerrainLayerData>();

            if (soilMask != null)
            {
                layers.Add(new TerrainLayerData
                {
                    layerId = SoilId,
                    destructible = true,
                    requiredBreakPower = 0f,
                    collisionEnabled = true,
                    alphaThreshold = AlphaThreshold,
                    initialMask = soilMask,
                    art = CloneArt(thinArt, Color.white)
                });
            }

            if (rockMask != null)
            {
                layers.Add(new TerrainLayerData
                {
                    layerId = RockId,
                    destructible = false,
                    requiredBreakPower = 0f,
                    collisionEnabled = true,
                    alphaThreshold = AlphaThreshold,
                    initialMask = rockMask,
                    art = CloneArt(thinArt, RockTint)
                });
            }

            if (baseMask != null)
            {
                // 厚块（地底大块、边界墙）整块不可破坏，避免玩家把地基挖穿掉出世界
                layers.Add(new TerrainLayerData
                {
                    layerId = BaseId,
                    destructible = false,
                    requiredBreakPower = 0f,
                    collisionEnabled = true,
                    alphaThreshold = AlphaThreshold,
                    initialMask = baseMask,
                    art = CloneArt(thickArt, Color.white)
                });
            }

            config.layers = layers;
            config.Validate();
            // ---------- 停用被接管的美术 ----------
            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null) continue;

                renderer.enabled = false;
                Collider2D collider = renderer.GetComponent<Collider2D>();
                if (collider != null) collider.enabled = false;
            }

            EditorUtility.SetDirty(config);
            if (soilMask != null) EditorUtility.SetDirty(soilMask);
            if (rockMask != null) EditorUtility.SetDirty(rockMask);
            if (baseMask != null) EditorUtility.SetDirty(baseMask);
            EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GroundRebake] {config.name}：worldRect {finalRect}（{width}×{height}，{pixelSize:F4} 单位/像素）\n" +
                      $"  土壤 {soilCount} 像素（可破坏）/ 岩层 {rockCount} 像素（{rockRows} 行 = {rockRows * pixelSize:F3} 单位）\n" +
                      $"  不可破坏基底 {thickCount} 像素（厚块 {thickArt.Count} 件）\n" +
                      $"  共 {art.Count} 件美术已接管，原 SpriteRenderer 与 Collider2D 已停用");
            return true;
        }

        // ---------------- 选择与查找 ----------------

        private static List<SpriteRenderer> CollectSelection()
        {
            var result = new List<SpriteRenderer>();

            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null) continue;

                SpriteRenderer[] found = selected[i].GetComponentsInChildren<SpriteRenderer>(true);
                for (int j = 0; j < found.Length; j++)
                {
                    if (found[j] != null && found[j].sprite != null && !result.Contains(found[j]))
                        result.Add(found[j]);
                }
            }

            return result;
        }

        private static ArenaController FindArena()
        {
            Scene scene = SceneManager.GetActiveScene();
            ArenaController[] controllers = Object.FindObjectsByType<ArenaController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null) continue;
                if (controllers[i].gameObject.scene == scene) return controllers[i];
            }

            return null;
        }

        // ---------------- 几何与遮罩 ----------------

        private static Rect UnionBounds(List<SpriteRenderer> renderers)
        {
            bool has = false;
            float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;

            for (int i = 0; i < renderers.Count; i++)
            {
                Rect bounds = SpriteWorldBounds(renderers[i]);
                if (bounds.width <= 0f && bounds.height <= 0f) continue;

                if (!has)
                {
                    xMin = bounds.xMin;
                    xMax = bounds.xMax;
                    yMin = bounds.yMin;
                    yMax = bounds.yMax;
                    has = true;
                    continue;
                }

                xMin = Mathf.Min(xMin, bounds.xMin);
                xMax = Mathf.Max(xMax, bounds.xMax);
                yMin = Mathf.Min(yMin, bounds.yMin);
                yMax = Mathf.Max(yMax, bounds.yMax);
            }

            return has ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// Sprite 的世界包围盒。不用 renderer.bounds：SpriteRenderer 被停用后 bounds 会退化成零，
        /// 第二次执行（美术已被本工具停用）就拿不到范围。
        /// </summary>
        private static Rect SpriteWorldBounds(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return new Rect(0f, 0f, 0f, 0f);

            Bounds spriteBounds = renderer.sprite.bounds;
            Transform transform = renderer.transform;
            bool has = false;
            float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;

            for (int c = 0; c < 4; c++)
            {
                float lx = (c & 1) == 0 ? spriteBounds.min.x : spriteBounds.max.x;
                float ly = (c & 2) == 0 ? spriteBounds.min.y : spriteBounds.max.y;
                Vector3 world = transform.TransformPoint(new Vector3(lx, ly, 0f));

                if (!has)
                {
                    xMin = xMax = world.x;
                    yMin = yMax = world.y;
                    has = true;
                    continue;
                }

                xMin = Mathf.Min(xMin, world.x);
                xMax = Mathf.Max(xMax, world.x);
                yMin = Mathf.Min(yMin, world.y);
                yMax = Mathf.Max(yMax, world.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>按世界高度把部件分成「薄地面」和「厚地基」两类。</summary>
        private static void SplitRenderersByThickness(List<SpriteRenderer> renderers,
                                                     out List<SpriteRenderer> thin,
                                                     out List<SpriteRenderer> thick)
        {
            thin = new List<SpriteRenderer>();
            thick = new List<SpriteRenderer>();

            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null) continue;

                Rect bounds = SpriteWorldBounds(renderer);
                if (bounds.height > BasePieceThickness) thick.Add(renderer);
                else thin.Add(renderer);
            }
        }

        private static Rect Expand(Rect rect, float amount)
        {
            return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);
        }

        /// <summary>把矩形对齐到像素格，避免遮罩边缘采样抖动。</summary>
        private static Rect SnapToPixelGrid(Rect rect, int pixelsPerUnit)
        {
            float step = 1f / Mathf.Max(1, pixelsPerUnit);
            float xMin = Mathf.Floor(rect.xMin / step) * step;
            float yMin = Mathf.Floor(rect.yMin / step) * step;
            float xMax = Mathf.Ceil(rect.xMax / step) * step;
            float yMax = Mathf.Ceil(rect.yMax / step) * step;

            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin + step, xMax), Mathf.Max(yMin + step, yMax));
        }

        private static Color32[] Rasterize(Rect rect, List<TerrainArtPiece> art, int width, int height)
        {
            var pixels = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (int i = 0; i < art.Count; i++)
            {
                try
                {
                    ArenaTerrainBaker.RasterizePiece(pixels, width, height, rect, art[i], AlphaThreshold);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[GroundRebake] 光栅化失败：{exception.Message}");
                }
            }

            return pixels;
        }

        private static bool TryGetSolidBounds(Color32[] pixels, int width, int height, Rect rect, out Rect solid)
        {
            solid = new Rect(0f, 0f, 0f, 0f);

            int minX = width, maxX = -1, minY = height, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[x + y * width].r < 128) continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return false;

            float px = rect.width / width;
            float py = rect.height / height;
            solid = Rect.MinMaxRect(rect.xMin + minX * px, rect.yMin + minY * py,
                                    rect.xMin + (maxX + 1) * px, rect.yMin + (maxY + 1) * py);
            return true;
        }

        private static bool TryGetSolidRows(Color32[] pixels, int width, int height, out int rowMin, out int rowMax)
        {
            rowMin = height;
            rowMax = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[x + y * width].r < 128) continue;

                    if (y < rowMin) rowMin = y;
                    if (y > rowMax) rowMax = y;
                    break;
                }
            }

            return rowMax >= 0;
        }

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
    }
}
