using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 场景模块运行时入口：按 ArenaConfig 重建分层地形、统一处理抹除与追加。
    /// 不包含爆发/子弹逻辑，只暴露 ApplyTerrainBreak 给外部调用。
    /// </summary>
    public class ArenaController : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private ArenaConfig config;
        [SerializeField] private ArenaBounds bounds;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private string obstacleLayerName = "Obstacle";

        [Header("物理")]
        [Tooltip("轮廓点简化容差（世界单位）")]
        [SerializeField] private float simplifyTolerance = 0.02f;
        [Tooltip("EdgeCollider2D 的碰撞厚度")]
        [SerializeField] private float edgeRadius = 0.01f;
        [Tooltip("单次破坏最大范围，防止一次抹掉整个场地")]
        [SerializeField] private float maxBreakRadius = 6f;

        [Header("调试")]
        [SerializeField] private bool logBuild;

        private readonly List<TerrainLayer> layers = new List<TerrainLayer>();
        private Transform terrainRoot;
        private TerrainPhysics physics;

        public static ArenaController Instance { get; private set; }
        public ArenaConfig Config => config;
        public ArenaBounds Bounds => bounds;
        public Material TerrainMaterial => terrainMaterial;
        public IReadOnlyList<TerrainLayer> Layers => layers;
        public TerrainPhysics Physics => physics;

        private void Awake()
        {
            Instance = this;
            if (bounds == null) bounds = FindAnyObjectByType<ArenaBounds>();
        }

        /// <summary>运行时注入配置（测试与生成工具使用）。</summary>
        public void Configure(ArenaConfig arenaConfig, ArenaBounds arenaBounds, Material material)
        {
            config = arenaConfig;
            bounds = arenaBounds;
            terrainMaterial = material;
        }

        /// <summary>战斗开始重建全部地形层。</summary>
        public void BuildForBattle()
        {
            ClearRuntime();

            if (config == null)
            {
                Debug.LogError("[Arena] ArenaConfig 未配置");
                return;
            }

            config.Validate();
            var root = new GameObject("TerrainRoot");
            root.transform.SetParent(transform, false);
            terrainRoot = root.transform;

            for (int i = 0; i < config.layers.Count; i++)
            {
                TerrainLayerData data = config.layers[i];
                if (data == null) continue;

                var go = new GameObject($"Terrain_{data.layerId}");
                go.transform.SetParent(terrainRoot, false);
                var layer = go.AddComponent<TerrainLayer>();
                layer.Initialize(data, config.worldRect, config.MaskWidth, config.MaskHeight, terrainMaterial);
                layers.Add(layer);
            }

            var physicsGo = new GameObject("TerrainPhysics");
            physicsGo.transform.SetParent(terrainRoot, false);
            int obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
            if (obstacleLayer < 0) obstacleLayer = 0;

            physics = physicsGo.AddComponent<TerrainPhysics>();
            physics.Initialize(obstacleLayer);
            RefreshPhysics();

            if (logBuild) Debug.Log($"[Arena] 已重建 {layers.Count} 层地形");
            GameEvents.RaiseTerrainBuilt(this);
        }


        /// <summary>抹除指定范围内的可破坏地形。</summary>
        public TerrainBreakResult ApplyTerrainBreak(in TerrainBreakRequest request)
        {
            if (config == null || layers.Count == 0) return default;

            float radius = Mathf.Clamp(request.Radius, 0f, maxBreakRadius);
            float minRadius = Mathf.Max(0.0001f, config.PixelSize * 0.5f);
            radius = Mathf.Max(radius, minRadius);

            Vector2 size = request.Size;
            if (request.Shape == TerrainBreakShape.Box && (size.x <= 0f || size.y <= 0f))
                size = new Vector2(radius * 2f, radius * 2f);

            int affectedLayers = 0;
            int affectedPixels = 0;

            for (int i = 0; i < layers.Count; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null) continue;

                int changed = request.Shape == TerrainBreakShape.Circle
                    ? layer.EraseCircle(request.Point, radius, request.BreakPower)
                    : layer.EraseBox(request.Point, size, request.BreakPower);

                if (changed <= 0) continue;
                affectedLayers++;
                affectedPixels += changed;
            }

            var result = new TerrainBreakResult(affectedLayers, affectedPixels);
            if (result.AnythingChanged)
            {
                RefreshPhysics();
                GameEvents.RaiseTerrainBroken(request, result);
            }

            return result;
        }

        /// <summary>在指定层补回圆形地形。</summary>
        public int AppendTerrainCircle(string layerId, Vector2 center, float radius)
        {
            TerrainLayer layer = GetLayer(layerId);
            if (layer == null) return 0;

            int changed = layer.AppendCircle(center, Mathf.Max(0f, radius));
            if (changed <= 0) return 0;

            RefreshPhysics();
            GameEvents.RaiseTerrainAppended(layerId, changed);
            return changed;
        }

        /// <summary>在指定层补回矩形地形。</summary>
        public int AppendTerrainBox(string layerId, Vector2 center, Vector2 size)
        {
            TerrainLayer layer = GetLayer(layerId);
            if (layer == null) return 0;

            int changed = layer.AppendBox(center, size);
            if (changed <= 0) return 0;

            RefreshPhysics();
            GameEvents.RaiseTerrainAppended(layerId, changed);
            return changed;
        }

        public TerrainLayer GetLayer(string layerId)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null && layers[i].LayerId == layerId) return layers[i];
            }
            return null;
        }

        /// <summary>碰撞体是否属于地形物理根；供子弹/爆发判断。</summary>
        public bool IsTerrainCollider(Collider2D collider)
        {
            return collider != null && collider.GetComponentInParent<TerrainPhysics>() != null;
        }

        /// <summary>清空运行时地形，可重复调用。</summary>
        public void ClearRuntime()
        {
            if (terrainRoot != null)
            {
                terrainRoot.gameObject.SetActive(false);
                Destroy(terrainRoot.gameObject);
            }

            terrainRoot = null;
            physics = null;
            layers.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>取所有碰撞层遮罩的并集，重新生成 CompositeCollider 边界。</summary>
        private void RefreshPhysics()
        {
            if (physics == null || config == null) return;

            int width = config.MaskWidth;
            int height = config.MaskHeight;
            var solid = new bool[width * height];

            for (int i = 0; i < layers.Count; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null || !layer.CollisionEnabled) continue;

                Color32[] source = layer.MaskPixels;
                if (source == null) continue;

                int count = Mathf.Min(source.Length, solid.Length);
                for (int p = 0; p < count; p++)
                {
                    if (source[p].r >= 128) solid[p] = true;
                }
            }

            List<Vector2[]> contours = TerrainColliderBuilder.BuildContours(
                solid, width, height, config.worldRect.min, config.PixelSize, simplifyTolerance);

            int obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
            if (obstacleLayer < 0) obstacleLayer = 0;
            physics.Rebuild(contours, obstacleLayer, edgeRadius);
        }
    }
}

