using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 场景地形数据：人工在 ArenaAuthoring 里摆好后 Bake 到这份资产。
    /// 运行时只读它重建，不依赖编辑场景。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/场景/地形配置", fileName = "ArenaConfig")]
    public class ArenaConfig : ScriptableObject
    {
        [Header("场地")]
        [Tooltip("地形遮罩覆盖的世界范围")]
        public Rect worldRect = new Rect(-8f, -5f, 16f, 10f);

        [Tooltip("遮罩分辨率（像素 / 世界单位）")]
        [Min(1)] public int pixelsPerUnit = 16;

        [Header("地形层")]
        public List<TerrainLayerData> layers = new List<TerrainLayerData>();

        public int MaskWidth => Mathf.Max(1, Mathf.RoundToInt(worldRect.width * pixelsPerUnit));
        public int MaskHeight => Mathf.Max(1, Mathf.RoundToInt(worldRect.height * pixelsPerUnit));
        public float PixelSize => worldRect.width / MaskWidth;

        /// <summary>把配置夹到可用范围，并清理空层。</summary>
        public void Validate()
        {
            if (worldRect.width < 0.01f) worldRect.width = 0.01f;
            if (worldRect.height < 0.01f) worldRect.height = 0.01f;
            pixelsPerUnit = Mathf.Max(1, pixelsPerUnit);

            if (layers == null) layers = new List<TerrainLayerData>();
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i] == null)
                {
                    layers.RemoveAt(i);
                    continue;
                }

                layers[i].Validate();
            }
        }

        public TerrainLayerData GetLayer(string layerId)
        {
            if (layers == null) return null;

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null && layers[i].layerId == layerId) return layers[i];
            }
            return null;
        }

        private void OnValidate() => Validate();
    }
}
